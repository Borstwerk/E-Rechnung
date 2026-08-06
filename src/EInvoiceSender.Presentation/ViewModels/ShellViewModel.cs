using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.UseCases;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Presentation.Editing;

namespace EInvoiceSender.Presentation.ViewModels;

/// <summary>Die fuenf Schritte des Ablaufs.</summary>
public enum WizardStep
{
    /// <summary>Schritt 1: PDF auswaehlen.</summary>
    SelectPdf = 1,

    /// <summary>Schritt 2: Rechnungsdaten erfassen.</summary>
    EnterData = 2,

    /// <summary>Schritt 3: Kontrollansicht und Bestaetigung.</summary>
    Review = 3,

    /// <summary>Schritt 4: Erzeugen und validieren.</summary>
    Generate = 4,

    /// <summary>Schritt 5: Speichern und versenden.</summary>
    Finish = 5,
}

/// <summary>
/// Steuert den gesamten Ablauf der Oberflaeche.
///
/// Das ViewModel enthaelt **keine Fachlogik**. Es haelt den Zustand der fuenf
/// Schritte, ruft den Anwendungsfall auf und bereitet dessen Ergebnisse fuer
/// die Anzeige auf. Gerechnet wird ausschliesslich im Domaenenkern, geprueft
/// ausschliesslich in der Validierungsschicht.
///
/// Bewusst plattformneutral: Es gibt keine Abhaengigkeit auf WPF, damit der
/// gesamte Ablauf auch auf einem Linux-Build-Agenten testbar bleibt.
///
/// **Regel fuer jedes await in dieser Klasse: <c>ConfigureAwait(true)</c>.**
/// Die Klasse kennt WPF zwar nicht, laeuft dort aber auf dem Oberflaechen-Thread,
/// und jede Zuweisung an eine gebundene Eigenschaft meldet an die Oberflaeche.
/// Mit <c>ConfigureAwait(false)</c> laeuft die Fortsetzung nach dem await auf
/// einem Threadpool-Thread; die Meldung kommt dann aus dem falschen Thread, und
/// WPF wirft "Der aufrufende Thread kann nicht auf dieses Objekt zugreifen".
/// Genau das ist beim ersten Start der Anwendung passiert. Bewacht wird die
/// Regel von <c>UiThreadAffinityTests</c>.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IPdfPreflightService _preflight;
    private readonly IBusinessRuleValidator _ruleValidator;
    private readonly ICreateEInvoiceUseCase _useCase;
    private readonly IEmailDraftService _emailDraftService;
    private readonly ISettingsStore _settingsStore;

    private CancellationTokenSource? _runningOperation;

    public ShellViewModel(
        IPdfPreflightService preflight,
        IBusinessRuleValidator ruleValidator,
        ICreateEInvoiceUseCase useCase,
        IEmailDraftService emailDraftService,
        ISettingsStore settingsStore)
    {
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _ruleValidator = ruleValidator ?? throw new ArgumentNullException(nameof(ruleValidator));
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
        _emailDraftService = emailDraftService ?? throw new ArgumentNullException(nameof(emailDraftService));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

        Draft.Lines.CollectionChanged += (_, _) => RecalculateTotals();
    }

    // --- Zustand -----------------------------------------------------------

    /// <summary>Der aktuell sichtbare Schritt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private WizardStep _currentStep = WizardStep.SelectPdf;

    /// <summary>Das Eingabeformular.</summary>
    public InvoiceDraft Draft { get; } = new();

    /// <summary>Ergebnis der Eingangspruefung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPdf))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private PdfPreflightReport? _preflightReport;

    /// <summary>Die berechneten Summen, laufend aktualisiert.</summary>
    [ObservableProperty]
    private InvoiceTotals? _totals;

    /// <summary>Befunde des aktuellen Schritts, aufbereitet fuer die Anzeige.</summary>
    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    /// <summary>Fortschrittsmeldungen der Erzeugung.</summary>
    public ObservableCollection<StepProgressViewModel> Progress { get; } = [];

    /// <summary>
    /// Bestaetigung, dass die erfassten Daten mit der sichtbaren PDF
    /// uebereinstimmen. Ohne sie ist die Erzeugung gesperrt.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private bool _contentMatchConfirmed;

    /// <summary>Bestaetigung, eine bereits eingebettete Rechnung zu ersetzen.</summary>
    [ObservableProperty]
    private bool _existingInvoiceReplacementConfirmed;

    /// <summary>Ausgabeverzeichnis.</summary>
    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    /// <summary>
    /// Laeuft gerade eine Erzeugung?
    ///
    /// Alle vier Befehle unten lesen <c>IsBusy</c> in ihrer Freigabepruefung,
    /// also muessen auch alle vier benachrichtigt werden. Fehlen "Zurueck" und
    /// "Weiter", bleiben deren Schaltflaechen dauerhaft gesperrt: Waehrend der
    /// Eingangspruefung ist <c>IsBusy</c> noch <c>true</c>, wenn
    /// <c>PreflightReport</c> gesetzt wird und "Weiter" das erste und einzige
    /// Mal neu bewertet wird.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private bool _isBusy;

    /// <summary>Ergebnis der Erzeugung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private CreateEInvoiceResult? _result;

    /// <summary>Statuszeile fuer den Anwender.</summary>
    [ObservableProperty]
    private string _statusMessage = "Waehlen Sie die PDF-Rechnung aus, die Sie versenden moechten.";

    // --- E-Mail-Felder -----------------------------------------------------

    [ObservableProperty]
    private string _emailRecipient = string.Empty;

    [ObservableProperty]
    private string _emailSubject = string.Empty;

    [ObservableProperty]
    private string _emailBody = string.Empty;

    [ObservableProperty]
    private string _emailDraftPath = string.Empty;

    // --- Abgeleitete Eigenschaften -----------------------------------------

    /// <summary>Ist eine verarbeitbare PDF ausgewaehlt?</summary>
    public bool HasPdf => PreflightReport?.CanProceed == true;

    /// <summary>Liegt ein Ergebnis vor?</summary>
    public bool HasResult => Result is not null;

    /// <summary>Ist die Anwendung gerade untaetig?</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Ueberschrift des aktuellen Schrittes.</summary>
    public string StepTitle => CurrentStep switch
    {
        WizardStep.SelectPdf => "Schritt 1 von 5: PDF-Rechnung auswaehlen",
        WizardStep.EnterData => "Schritt 2 von 5: Rechnungsdaten erfassen",
        WizardStep.Review => "Schritt 3 von 5: Angaben mit der PDF vergleichen",
        WizardStep.Generate => "Schritt 4 von 5: E-Rechnung erzeugen und pruefen",
        WizardStep.Finish => "Schritt 5 von 5: Speichern und versenden",
        _ => "E-Rechnung erstellen",
    };

    // --- Befehle -----------------------------------------------------------

    /// <summary>Liest die gespeicherte Firmenvorlage in das Formular.</summary>
    [RelayCommand]
    public async Task LoadTemplateAsync(CancellationToken cancellationToken)
    {
        CompanyTemplate template = await _settingsStore.LoadTemplateAsync(cancellationToken)
            .ConfigureAwait(true);

        ApplyTemplate(template);
    }

    /// <summary>Uebernimmt eine Vorlage in das Formular.</summary>
    public void ApplyTemplate(CompanyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        Draft.SellerName = template.SellerName ?? Draft.SellerName;
        Draft.SellerStreet = template.SellerStreet ?? Draft.SellerStreet;
        Draft.SellerPostalCode = template.SellerPostalCode ?? Draft.SellerPostalCode;
        Draft.SellerCity = template.SellerCity ?? Draft.SellerCity;
        Draft.SellerCountry = template.SellerCountry ?? Draft.SellerCountry;
        Draft.SellerEmail = template.SellerEmail ?? Draft.SellerEmail;
        Draft.SellerVatId = template.SellerVatId ?? Draft.SellerVatId;
        Draft.SellerTaxNumber = template.SellerTaxNumber ?? Draft.SellerTaxNumber;
        Draft.BankAccountHolder = template.BankAccountHolder ?? Draft.BankAccountHolder;
        Draft.BankIban = template.BankIban ?? Draft.BankIban;
        Draft.BankBic = template.BankBic ?? Draft.BankBic;
        Draft.PaymentTerms = template.DefaultPaymentTerms ?? Draft.PaymentTerms;

        if (template.DefaultPaymentTermDays > 0 && Draft.IssueDate is { } issue)
        {
            Draft.DueDate = issue.AddDays(template.DefaultPaymentTermDays);
        }

        EmailSubject = template.DefaultEmailSubject ?? EmailSubject;
        EmailBody = template.DefaultEmailBody ?? EmailBody;

        if (!string.IsNullOrWhiteSpace(template.LastOutputDirectory))
        {
            OutputDirectory = template.LastOutputDirectory;
        }
    }

    /// <summary>Prueft eine ausgewaehlte PDF-Datei und uebernimmt sie.</summary>
    [RelayCommand]
    public async Task SelectPdfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        IsBusy = true;

        try
        {
            PreflightReport = await _preflight.InspectAsync(filePath, cancellationToken)
                .ConfigureAwait(true);

            ShowFindings(PreflightReport.Findings);

            StatusMessage = PreflightReport.Verdict switch
            {
                PreflightVerdict.Suitable =>
                    $"'{PreflightReport.FileName}' kann verarbeitet werden "
                    + $"({PreflightReport.PageCount} Seite(n), "
                    + $"{PreflightReport.FileSizeInMegabytes.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"))} MB).",
                PreflightVerdict.SuitableWithWarnings =>
                    $"'{PreflightReport.FileName}' kann verarbeitet werden. "
                    + "Bitte beachten Sie die Hinweise.",
                _ => $"'{PreflightReport.FileName}' kann nicht verarbeitet werden. "
                     + "Die Liste nennt den Grund und was Sie tun koennen.",
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Berechnet die Summen aus dem aktuellen Formularstand neu.</summary>
    [RelayCommand]
    public void RecalculateTotals()
    {
        ValidationReport report = Draft.TryBuildInvoice(out Invoice? invoice);

        Totals = report.HasErrors || invoice is null
            ? null
            : InvoiceCalculator.Calculate(invoice);
    }

    /// <summary>
    /// Prueft die erfassten Daten und zeigt die Befunde an.
    /// Liefert true, wenn keine Fehler vorliegen.
    /// </summary>
    public bool ValidateData()
    {
        ValidationReport buildReport = Draft.TryBuildInvoice(out Invoice? invoice);

        if (buildReport.HasErrors || invoice is null)
        {
            ShowFindings(buildReport);
            StatusMessage = "Einige Eingaben konnten nicht gelesen werden.";

            return false;
        }

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);
        Totals = totals;

        ValidationReport ruleReport = _ruleValidator.Validate(invoice, totals);
        ShowFindings(buildReport.Concat(ruleReport));

        StatusMessage = ruleReport.HasErrors
            ? $"{ruleReport.ErrorCount} Angabe(n) muessen noch korrigiert werden."
            : "Die Angaben sind vollstaendig und in sich stimmig.";

        return !ruleReport.HasErrors;
    }

    /// <summary>Startet die Erzeugung der E-Rechnung.</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    public async Task GenerateAsync()
    {
        ValidationReport buildReport = Draft.TryBuildInvoice(out Invoice? invoice);

        if (buildReport.HasErrors || invoice is null || PreflightReport is null)
        {
            ShowFindings(buildReport);

            return;
        }

        _runningOperation?.Dispose();
        _runningOperation = new CancellationTokenSource();

        IsBusy = true;
        Progress.Clear();
        CurrentStep = WizardStep.Generate;

        try
        {
            var request = new CreateEInvoiceRequest(
                SourcePdfPath: PreflightReport.FilePath,
                Invoice: invoice,
                ContentMatchConfirmed: ContentMatchConfirmed,
                OutputDirectory: OutputDirectory,
                ExistingInvoiceReplacementConfirmed: ExistingInvoiceReplacementConfirmed);

            var progress = new Progress<PipelineProgress>(OnProgress);

            Result = await _useCase.ExecuteAsync(request, progress, _runningOperation.Token)
                .ConfigureAwait(true);

            ShowFindings(Result.Report);

            if (Result.Succeeded)
            {
                StatusMessage = "Die E-Rechnung wurde erzeugt und geprueft.";
                PrepareEmailFields(invoice);
                CurrentStep = WizardStep.Finish;
            }
            else if (Result.Canceled)
            {
                StatusMessage = "Der Vorgang wurde abgebrochen. Es wurde keine Datei erzeugt.";
            }
            else
            {
                StatusMessage = "Die E-Rechnung konnte nicht erzeugt werden. "
                                + "Die Liste nennt die Ursache.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanGenerate() => !IsBusy && ContentMatchConfirmed;

    /// <summary>Bricht eine laufende Erzeugung ab.</summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _runningOperation?.Cancel();
        StatusMessage = "Abbruch angefordert ...";
    }

    private bool CanCancel() => IsBusy;

    /// <summary>Erzeugt den E-Mail-Entwurf zur fertigen Rechnung.</summary>
    [RelayCommand]
    public async Task CreateEmailDraftAsync(CancellationToken cancellationToken = default)
    {
        if (Result?.OutputFile is not { } outputFile)
        {
            return;
        }

        byte[] content = await File.ReadAllBytesAsync(outputFile.FullPath, cancellationToken)
            .ConfigureAwait(true);

        var draft = new EmailDraft(
            From: string.IsNullOrWhiteSpace(Draft.SellerEmail) ? null : Draft.SellerEmail,
            FromDisplayName: Draft.SellerName,
            To: string.IsNullOrWhiteSpace(EmailRecipient) ? [] : [EmailRecipient],
            Subject: EmailSubject,
            Body: EmailBody,
            Attachments:
            [
                new EmailAttachment(
                    Path.GetFileName(outputFile.FullPath), "application/pdf", content),
            ]);

        EmailDraftResult draftResult = await _emailDraftService
            .CreateDraftAsync(draft, cancellationToken).ConfigureAwait(true);

        EmailDraftPath = draftResult.DraftFilePath ?? string.Empty;
        StatusMessage = draftResult.Message;
    }

    /// <summary>Geht einen Schritt zurueck.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void GoBack()
    {
        if (CurrentStep > WizardStep.SelectPdf)
        {
            CurrentStep--;
        }
    }

    private bool CanGoBack() => CurrentStep > WizardStep.SelectPdf && !IsBusy;

    /// <summary>Geht einen Schritt vor, sofern der aktuelle abgeschlossen ist.</summary>
    [RelayCommand(CanExecute = nameof(CanGoForward))]
    public void GoForward()
    {
        switch (CurrentStep)
        {
            case WizardStep.SelectPdf when HasPdf:
                CurrentStep = WizardStep.EnterData;
                break;

            case WizardStep.EnterData when ValidateData():
                CurrentStep = WizardStep.Review;
                break;

            case WizardStep.Review when ContentMatchConfirmed:
                CurrentStep = WizardStep.Generate;
                break;

            default:
                break;
        }
    }

    private bool CanGoForward() => CurrentStep switch
    {
        WizardStep.SelectPdf => HasPdf && !IsBusy,
        WizardStep.EnterData => !IsBusy,
        WizardStep.Review => ContentMatchConfirmed && !IsBusy,
        _ => false,
    };

    // --- Hilfen ------------------------------------------------------------

    private void OnProgress(PipelineProgress message)
    {
        StepProgressViewModel? existing = Progress
            .FirstOrDefault(p => p.Step == message.Step);

        if (existing is null)
        {
            Progress.Add(new StepProgressViewModel(message));
        }
        else
        {
            existing.Update(message);
        }

        StatusMessage = message.Description;
    }

    private void PrepareEmailFields(Invoice invoice)
    {
        if (string.IsNullOrWhiteSpace(EmailRecipient))
        {
            EmailRecipient = invoice.Buyer.Email ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(EmailSubject))
        {
            EmailSubject = $"Rechnung {invoice.InvoiceNumber}";
        }

        if (string.IsNullOrWhiteSpace(EmailBody))
        {
            EmailBody =
                "Sehr geehrte Damen und Herren,\n\n"
                + $"anbei erhalten Sie die Rechnung {invoice.InvoiceNumber} "
                + "als elektronische Rechnung.\n\n"
                + "Mit freundlichen Gruessen\n"
                + invoice.Seller.Name;
        }
    }

    /// <summary>Gibt die Abbruchquelle einer laufenden Erzeugung frei.</summary>
    public void Dispose()
    {
        _runningOperation?.Dispose();
        _runningOperation = null;
    }

    private void ShowFindings(ValidationReport report)
    {
        Findings.Clear();

        // Fehler zuerst, danach Warnungen, dann Hinweise – der Anwender soll
        // oben sehen, was ihn wirklich aufhaelt.
        foreach (ValidationFinding finding in report.Findings
                     .OrderByDescending(f => f.Severity))
        {
            Findings.Add(new FindingViewModel(finding));
        }
    }
}

/// <summary>Ein Befund, aufbereitet fuer die Anzeige.</summary>
public sealed class FindingViewModel(ValidationFinding finding)
{
    /// <summary>Der zugrunde liegende Befund.</summary>
    public ValidationFinding Finding { get; } = finding;

    /// <summary>Verstaendliche Meldung.</summary>
    public string Message => Finding.Message;

    /// <summary>Technische Angaben fuer den aufklappbaren Bereich.</summary>
    public string TechnicalDetail => Finding.BuildTechnicalSummary();

    /// <summary>Betroffenes Feld.</summary>
    public string FieldPath => Finding.FieldPath;

    /// <summary>
    /// Schweregrad als Wort. Fehler werden **nicht nur** durch Farbe
    /// gekennzeichnet – das waere fuer farbfehlsichtige Anwender unbrauchbar.
    /// </summary>
    public string SeverityLabel => Finding.Severity switch
    {
        FindingSeverity.Error => "Fehler",
        FindingSeverity.Warning => "Warnung",
        _ => "Hinweis",
    };

    /// <summary>Zeichen zur zusaetzlichen, farbunabhaengigen Kennzeichnung.</summary>
    public string SeverityGlyph => Finding.Severity switch
    {
        FindingSeverity.Error => "✕",
        FindingSeverity.Warning => "!",
        _ => "i",
    };

    /// <summary>Schweregrad fuer Vorlagenauswahl.</summary>
    public FindingSeverity Severity => Finding.Severity;
}

/// <summary>Der Zustand eines Ablaufschrittes in der Fortschrittsanzeige.</summary>
public sealed partial class StepProgressViewModel : ObservableObject
{
    public StepProgressViewModel(PipelineProgress progress)
    {
        Step = progress.Step;
        _description = progress.Description;
        _state = progress.State;
    }

    /// <summary>Der Schritt.</summary>
    public PipelineStep Step { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    private StepState _state;

    [ObservableProperty]
    private string _description;

    /// <summary>Zustand als Wort, nicht nur als Farbe.</summary>
    public string StateLabel => State switch
    {
        StepState.Running => "laeuft",
        StepState.Succeeded => "erledigt",
        StepState.SucceededWithWarnings => "erledigt, mit Hinweisen",
        StepState.Failed => "fehlgeschlagen",
        StepState.Skipped => "uebersprungen",
        _ => string.Empty,
    };

    /// <summary>Uebernimmt eine neue Meldung zu diesem Schritt.</summary>
    public void Update(PipelineProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        State = progress.State;
        Description = progress.Description;
    }
}

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Führt durch die fünf Schritte.
///
/// Dieses ViewModel bildet **keine** Fachlogik ab. Es kennt den aktuellen
/// Schritt, gibt den Weg vor und reicht das Ergebnis eines Schrittes an den
/// nächsten weiter. Gerechnet und geprüft wird ausschließlich in
/// <see cref="IEInvoiceService"/>.
///
/// **Regel für jedes await: <c>ConfigureAwait(true)</c>** – siehe
/// <see cref="StepViewModel"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private Invoice? _confirmedInvoice;
    private bool _hasPrefilled;

    public MainViewModel(
        PdfSelectionViewModel pdfSelection,
        InvoiceDataViewModel invoiceData,
        ReviewViewModel review,
        GenerationViewModel generation,
        ResultViewModel result,
        ISettingsStore settingsStore)
    {
        PdfSelection = pdfSelection;
        InvoiceData = invoiceData;
        Review = review;
        Generation = generation;
        Result = result;
        _settingsStore = settingsStore;

        PdfSelection.PropertyChanged += (_, _) => GoForwardCommand.NotifyCanExecuteChanged();
        Review.PropertyChanged += (_, _) => GoForwardCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Schritt 1.</summary>
    public PdfSelectionViewModel PdfSelection { get; }

    /// <summary>Schritt 2.</summary>
    public InvoiceDataViewModel InvoiceData { get; }

    /// <summary>Schritt 3.</summary>
    public ReviewViewModel Review { get; }

    /// <summary>Schritt 4.</summary>
    public GenerationViewModel Generation { get; }

    /// <summary>Schritt 5.</summary>
    public ResultViewModel Result { get; }

    /// <summary>
    /// Der aktuell sichtbare Schritt.
    ///
    /// Beide Navigationsbefehle lesen ihn, also müssen auch beide
    /// benachrichtigt werden.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private WizardStep _currentStep = WizardStep.SelectPdf;

    /// <summary>
    /// Läuft gerade eine längere Arbeit?
    ///
    /// Alle Befehle unten lesen diesen Wert in ihrer Freigabeprüfung, also
    /// müssen auch alle benachrichtigt werden. Fehlt hier ein Eintrag, bleibt
    /// die zugehörige Schaltfläche im zuletzt bewerteten Zustand hängen –
    /// bewacht von <c>CommandEnablementTests</c>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartOverCommand))]
    private bool _isBusy;

    /// <summary>Statuszeile für den Anwender.</summary>
    [ObservableProperty]
    private string _statusMessage = "Wählen Sie die PDF-Rechnung aus, die Sie versenden möchten.";

    /// <summary>
    /// Eine unerwartete Störung, die nicht zu einem einzelnen Feld gehört.
    /// Leer, solange alles in Ordnung ist.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    /// <summary>Liegt eine Störung an?</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Überschrift des aktuellen Schrittes.</summary>
    public string StepTitle => CurrentStep switch
    {
        WizardStep.SelectPdf => "Schritt 1 von 5: PDF-Rechnung auswählen",
        WizardStep.EnterData => "Schritt 2 von 5: Rechnungsdaten erfassen",
        WizardStep.Review => "Schritt 3 von 5: Angaben mit der PDF vergleichen",
        WizardStep.Generate => "Schritt 4 von 5: E-Rechnung erzeugen und prüfen",
        WizardStep.Finish => "Schritt 5 von 5: Speichern und versenden",
        _ => "E-Rechnung erstellen",
    };

    /// <summary>Lädt die gespeicherte Firmenvorlage beim Start.</summary>
    public async Task LoadTemplateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            CompanyTemplate template = await _settingsStore.LoadTemplateAsync(cancellationToken)
                .ConfigureAwait(true);

            InvoiceData.ApplyTemplate(template);
            Result.EmailSubject = template.DefaultEmailSubject ?? Result.EmailSubject;
            Result.EmailBody = template.DefaultEmailBody ?? Result.EmailBody;

            if (!string.IsNullOrWhiteSpace(template.LastOutputDirectory))
            {
                Review.OutputDirectory = template.LastOutputDirectory;
            }
        }
        catch (IOException exception)
        {
            // Eine beschädigte Vorlage darf den Start nicht verhindern.
            ErrorMessage = "Die gespeicherte Firmenvorlage konnte nicht gelesen werden. "
                           + "Die Felder bleiben leer; über Einstellungen können Sie sie neu "
                           + $"erfassen. Technisch: {exception.Message}";
        }
    }

    /// <summary>Geht einen Schritt zurück.</summary>
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
    public async Task GoForwardAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = string.Empty;

        switch (CurrentStep)
        {
            case WizardStep.SelectPdf when PdfSelection.IsSuitable:
                await PrefillFromDetectionAsync(cancellationToken).ConfigureAwait(true);
                CurrentStep = WizardStep.EnterData;
                break;

            case WizardStep.EnterData:
                EnterReviewIfDataIsValid();
                break;

            case WizardStep.Review when Review.ContentMatchConfirmed:
                await GenerateAsync(cancellationToken).ConfigureAwait(true);
                break;

            default:
                break;
        }
    }

    private bool CanGoForward() => CurrentStep switch
    {
        WizardStep.SelectPdf => PdfSelection.IsSuitable && !IsBusy,
        WizardStep.EnterData => !IsBusy,
        WizardStep.Review => Review.ContentMatchConfirmed && !IsBusy,
        _ => false,
    };

    /// <summary>
    /// Übergibt das Erkennungsergebnis aus Schritt 1 an das Formular.
    ///
    /// Passiert genau einmal beim Übergang, nicht bei jedem Blättern – sonst
    /// würden von Hand geänderte Werte wieder überschrieben.
    /// </summary>
    private async Task PrefillFromDetectionAsync(CancellationToken cancellationToken)
    {
        StatusMessage = "Erfassen Sie die Rechnungsdaten so, wie sie in der PDF stehen.";

        if (_hasPrefilled || PdfSelection.Detection is not { HasUsableText: true } detection)
        {
            return;
        }

        // Wie viele Felder vorausgefuellt wurden, steht im Hinweis oben im
        // Formular. Die Statuszeile meldet den Stand des Arbeitsablaufs; sie
        // wiederholt denselben Satz nicht ein zweites Mal.

        _hasPrefilled = true;

        CompanyTemplate? template = null;

        try
        {
            template = await _settingsStore.LoadTemplateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // Ohne Vorlage wird eben weniger vorausgefuellt.
        }

        InvoiceData.ApplyDetection(detection, template);
    }

    private void EnterReviewIfDataIsValid()
    {
        Invoice? invoice = InvoiceData.Validate(out string message);
        StatusMessage = message;

        if (invoice is null || InvoiceData.Totals is null)
        {
            return;
        }

        _confirmedInvoice = invoice;

        Review.Show(
            invoice,
            InvoiceData.Totals,
            PdfSelection.PreviewImage,
            PdfSelection.Report?.HasExistingInvoice == true);

        // Der aus der PDF gelesene Betrag ist keine rechtliche Wahrheit,
        // sondern ein zweites, unabhängiges Signal. Weicht er ab, sieht der
        // Anwender das genau dort, wo er ohnehin vergleichen soll.
        Review.ShowTotalsComparison(PdfSelection.Detection is { HasUsableText: true } detection
            ? TotalsCrossCheck.Compare(detection.Totals, InvoiceData.Totals)
            : TotalsComparison.NotPossible);

        CurrentStep = WizardStep.Review;
    }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        if (_confirmedInvoice is not { } invoice || PdfSelection.Report is not { } preflight)
        {
            return;
        }

        CurrentStep = WizardStep.Generate;
        IsBusy = true;

        try
        {
            var request = new CreateEInvoiceRequest(
                SourcePdfPath: preflight.FilePath,
                Invoice: invoice,
                ContentMatchConfirmed: Review.ContentMatchConfirmed,
                OutputDirectory: Review.OutputDirectory,
                ExistingInvoiceReplacementConfirmed: Review.ExistingInvoiceReplacementConfirmed);

            CreateEInvoiceResult result = await Generation.RunAsync(request).ConfigureAwait(true);

            if (result.Succeeded)
            {
                Result.Show(result, invoice);
                CurrentStep = WizardStep.Finish;
                StatusMessage = "Die E-Rechnung wurde erzeugt und geprüft.";

                await RememberOutputDirectoryAsync(cancellationToken).ConfigureAwait(true);
            }
            else
            {
                StatusMessage = result.Canceled
                    ? "Der Vorgang wurde abgebrochen. Es wurde keine Datei erzeugt."
                    : "Die E-Rechnung konnte nicht erzeugt werden. Die Liste nennt die Ursache.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RememberOutputDirectoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            CompanyTemplate template = await _settingsStore.LoadTemplateAsync(cancellationToken)
                .ConfigureAwait(true);

            await _settingsStore
                .SaveTemplateAsync(template with { LastOutputDirectory = Review.OutputDirectory }, cancellationToken)
                .ConfigureAwait(true);
        }
        catch (IOException)
        {
            // Das Merken des Ordners ist eine Annehmlichkeit. Schlägt es fehl,
            // ist die erzeugte Rechnung davon unberührt.
        }
    }

    /// <summary>Beginnt eine neue Rechnung.</summary>
    [RelayCommand(CanExecute = nameof(CanStartOver))]
    public void StartOver()
    {
        PdfSelection.Reset();
        Review.Reset();
        Generation.Reset();
        Result.Reset();
        _confirmedInvoice = null;
        _hasPrefilled = false;

        CurrentStep = WizardStep.SelectPdf;
        ErrorMessage = string.Empty;
        StatusMessage = "Wählen Sie die nächste PDF-Rechnung aus.";
    }

    private bool CanStartOver() => !IsBusy;

    /// <summary>Gibt die Abbruchquelle der Erzeugung frei.</summary>
    public void Dispose() => Generation.Dispose();
}

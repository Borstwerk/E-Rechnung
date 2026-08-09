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

    /// <summary>
    /// Hat der Anwender das Eingabeformular schon gesehen?
    ///
    /// Entscheidet, ob eine geänderte Firmenvorlage sofort greifen darf. Ab
    /// dem Augenblick, in dem Schritt 2 offen war, könnten dort eigene
    /// Eingaben stehen – die überschreibt nichts mehr ungefragt.
    /// </summary>
    private bool _formWasOpened;

    /// <summary>
    /// Die zuletzt eingelesene Firmenvorlage. Woran sonst sollte sich
    /// erkennen lassen, ob der Anwender in den Einstellungen wirklich etwas
    /// geändert hat?
    /// </summary>
    private CompanyTemplate? _appliedTemplate;

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

    /// <summary>
    /// Lädt die gespeicherte Firmenvorlage und trägt sie in das Formular ein.
    ///
    /// Läuft beim Start und beim Beginn jeder weiteren Rechnung – jedes Mal
    /// frisch von der Festplatte. Nur so wirkt eine Änderung in den
    /// Einstellungen auf den nächsten Vorgang, ohne dass die Anwendung neu
    /// gestartet werden muss.
    ///
    /// Bewusst **nicht** mitten in einem laufenden Vorgang: Dort würde sie
    /// eingetippte Rechnungsdaten überschreiben.
    /// </summary>
    public async Task LoadTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (await TryLoadTemplateAsync(cancellationToken).ConfigureAwait(true) is not { } template)
        {
            return;
        }

        InvoiceData.ApplyTemplate(template);
        Result.EmailSubject = template.DefaultEmailSubject ?? Result.EmailSubject;
        Result.EmailBody = template.DefaultEmailBody ?? Result.EmailBody;

        if (!string.IsNullOrWhiteSpace(template.LastOutputDirectory))
        {
            Review.OutputDirectory = template.LastOutputDirectory;
        }
    }

    /// <summary>
    /// Liest die gespeicherte Vorlage. Liefert <c>null</c>, wenn sie sich nicht
    /// lesen lässt – dann steht die Begründung in der Störungsanzeige.
    /// </summary>
    private async Task<CompanyTemplate?> TryLoadTemplateAsync(CancellationToken cancellationToken)
    {
        try
        {
            _appliedTemplate = await _settingsStore.LoadTemplateAsync(cancellationToken)
                .ConfigureAwait(true);

            return _appliedTemplate;
        }
        catch (IOException exception)
        {
            // Eine beschädigte Vorlage darf den Start nicht verhindern.
            ErrorMessage = "Die gespeicherte Firmenvorlage konnte nicht gelesen werden. "
                           + "Die Felder bleiben leer; über Einstellungen können Sie sie neu "
                           + $"erfassen. Technisch: {exception.Message}";

            return null;
        }
    }

    /// <summary>
    /// Übernimmt eine in den Einstellungen geänderte Firmenvorlage, sofern der
    /// Anwender noch keinen Vorgang begonnen hat.
    ///
    /// **Der Fall aus der Bedienung:** Anwendung läuft, Einstellungen öffnen,
    /// Firmenvorlage ändern, speichern, schließen. Ohne diesen Aufruf hätte
    /// die Anwendung neu gestartet werden müssen, damit die neuen Vorgaben
    /// ankommen.
    ///
    /// **Die Grenze:** War Schritt 2 schon offen, wird nichts ungefragt
    /// ausgetauscht – dort können längst eigene Rechnungsdaten stehen.
    /// Stattdessen legt das Formular die Frage vor, ob die neuen
    /// Verkäuferdaten schon für diese Rechnung gelten sollen. Wer verneint,
    /// bekommt sie ab der nächsten – siehe <see cref="StartOverAsync"/>.
    /// </summary>
    public async Task ApplyChangedTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (!_formWasOpened)
        {
            InvoiceData.Reset();

            await LoadTemplateAsync(cancellationToken).ConfigureAwait(true);

            return;
        }

        CompanyTemplate? template = await TryLoadTemplateAsync(cancellationToken).ConfigureAwait(true);

        if (template is not null && SellerDataChanged(template))
        {
            InvoiceData.AskAboutChangedTemplate(template);
        }
    }

    /// <summary>
    /// Unterscheidet sich die gespeicherte Vorlage in dem, was auf der Rechnung
    /// steht?
    ///
    /// Das zuletzt verwendete Ausgabeverzeichnis bleibt dabei außen vor: Es
    /// wird nach jeder erzeugten Rechnung mitgeschrieben und hat mit den
    /// Verkäuferdaten nichts zu tun. Ohne diese Ausnahme käme die Rückfrage,
    /// obwohl niemand etwas geändert hat.
    /// </summary>
    private bool SellerDataChanged(CompanyTemplate template)
        => _appliedTemplate is null
           || template with { LastOutputDirectory = null }
              != _appliedTemplate with { LastOutputDirectory = null };

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
                _formWasOpened = true;
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

        // Wie viele Felder vorausgefüllt wurden, steht im Hinweis oben im
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
            // Ohne Vorlage wird eben weniger vorausgefüllt.
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

    /// <summary>
    /// Beginnt eine neue Rechnung.
    ///
    /// **Alle fünf Schritte** werden zurückgesetzt – auch das Eingabeformular.
    /// Genau das fehlte: Rechnungsnummer, Käufer, Datumsangaben, Positionen
    /// und Summen der vorigen Rechnung standen danach noch im Formular, weil
    /// nur die übrigen vier Schritte zurückgesetzt wurden.
    ///
    /// Anschließend werden die dauerhaften Vorgaben des Anwenders wieder
    /// eingetragen: eigene Firma, Bankverbindung, Standardwährung,
    /// Zahlungsbedingungen, Ausgabeordner und E-Mail-Vorgaben. Sie kommen
    /// frisch aus der gespeicherten Vorlage, nicht aus dem alten Formular.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartOver))]
    public async Task StartOverAsync(CancellationToken cancellationToken = default)
    {
        PdfSelection.Reset();
        InvoiceData.Reset();
        Review.Reset();
        Generation.Reset();
        Result.Reset();
        _confirmedInvoice = null;
        _hasPrefilled = false;
        _formWasOpened = false;

        CurrentStep = WizardStep.SelectPdf;
        ErrorMessage = string.Empty;
        StatusMessage = "Wählen Sie die nächste PDF-Rechnung aus.";

        await LoadTemplateAsync(cancellationToken).ConfigureAwait(true);
    }

    private bool CanStartOver() => !IsBusy;

    /// <summary>Gibt die Abbruchquelle der Erzeugung frei.</summary>
    public void Dispose() => Generation.Dispose();
}

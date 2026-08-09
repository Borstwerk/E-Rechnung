using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Fuehrt durch die fuenf Schritte.
///
/// Dieses ViewModel bildet **keine** Fachlogik ab. Es kennt den aktuellen
/// Schritt, gibt den Weg vor und reicht das Ergebnis eines Schrittes an den
/// naechsten weiter. Gerechnet und geprueft wird ausschliesslich in
/// <see cref="IEInvoiceService"/>.
///
/// **Regel fuer jedes await: <c>ConfigureAwait(true)</c>** – siehe
/// <see cref="StepViewModel"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private Invoice? _confirmedInvoice;

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
    /// Beide Navigationsbefehle lesen ihn, also muessen auch beide
    /// benachrichtigt werden.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private WizardStep _currentStep = WizardStep.SelectPdf;

    /// <summary>
    /// Laeuft gerade eine laengere Arbeit?
    ///
    /// Alle Befehle unten lesen diesen Wert in ihrer Freigabepruefung, also
    /// muessen auch alle benachrichtigt werden. Fehlt hier ein Eintrag, bleibt
    /// die zugehoerige Schaltflaeche im zuletzt bewerteten Zustand haengen –
    /// bewacht von <c>CommandEnablementTests</c>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartOverCommand))]
    private bool _isBusy;

    /// <summary>Statuszeile fuer den Anwender.</summary>
    [ObservableProperty]
    private string _statusMessage = "Waehlen Sie die PDF-Rechnung aus, die Sie versenden moechten.";

    /// <summary>
    /// Eine unerwartete Stoerung, die nicht zu einem einzelnen Feld gehoert.
    /// Leer, solange alles in Ordnung ist.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    /// <summary>Liegt eine Stoerung an?</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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

    /// <summary>Laedt die gespeicherte Firmenvorlage beim Start.</summary>
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
            // Eine beschaedigte Vorlage darf den Start nicht verhindern.
            ErrorMessage = "Die gespeicherte Firmenvorlage konnte nicht gelesen werden. "
                           + "Die Felder bleiben leer; ueber Einstellungen koennen Sie sie neu "
                           + $"erfassen. Technisch: {exception.Message}";
        }
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
    public async Task GoForwardAsync(CancellationToken cancellationToken = default)
    {
        ErrorMessage = string.Empty;

        switch (CurrentStep)
        {
            case WizardStep.SelectPdf when PdfSelection.IsSuitable:
                CurrentStep = WizardStep.EnterData;
                StatusMessage = "Erfassen Sie die Rechnungsdaten so, wie sie in der PDF stehen.";
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
                StatusMessage = "Die E-Rechnung wurde erzeugt und geprueft.";

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
            // Das Merken des Ordners ist eine Annehmlichkeit. Schlaegt es fehl,
            // ist die erzeugte Rechnung davon unberuehrt.
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

        CurrentStep = WizardStep.SelectPdf;
        ErrorMessage = string.Empty;
        StatusMessage = "Waehlen Sie die naechste PDF-Rechnung aus.";
    }

    private bool CanStartOver() => !IsBusy;

    /// <summary>Gibt die Abbruchquelle der Erzeugung frei.</summary>
    public void Dispose() => Generation.Dispose();
}

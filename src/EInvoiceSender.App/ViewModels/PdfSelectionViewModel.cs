using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.App.Services;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 1: Die vorhandene PDF-Rechnung auswaehlen und pruefen lassen.
///
/// Die Datei wird ausschliesslich gelesen. Was hier abgelehnt wird, ist nicht
/// "kaputt", sondern nur als Grundlage einer PDF/A-3-Datei ungeeignet – der
/// Befund nennt jeweils den Grund.
/// </summary>
public sealed partial class PdfSelectionViewModel(
    IEInvoiceService service,
    IPdfPreviewService preview,
    IInvoiceDataDetector detector,
    ISettingsStore settingsStore) : StepViewModel
{
    private readonly IEInvoiceService _service = service;
    private readonly IPdfPreviewService _preview = preview;
    private readonly IInvoiceDataDetector _detector = detector;
    private readonly ISettingsStore _settingsStore = settingsStore;

    /// <summary>
    /// Das Ergebnis der Datenerkennung. Wird von Schritt 2 ausgelesen, um das
    /// Formular vorzubefuellen.
    /// </summary>
    public InvoiceDetectionResult? Detection { get; private set; }

    /// <summary>Die Zeilen der Erkennungsuebersicht, wie sie angezeigt werden.</summary>
    public ObservableCollection<DetectionNote> DetectionNotes { get; } = [];

    /// <summary>Gibt es ueberhaupt etwas zu berichten?</summary>
    public bool HasDetectionNotes => DetectionNotes.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(FileName))]
    private string _filePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSuitable))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private PdfPreflightReport? _report;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private bool _isChecking;

    /// <summary>Ist ueberhaupt eine Datei gewaehlt?</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Der Dateiname ohne Pfad.</summary>
    public string FileName => HasFile ? Path.GetFileName(FilePath) : string.Empty;

    /// <summary>Taugt die Datei als Grundlage?</summary>
    public bool IsSuitable => Report?.CanProceed == true;

    /// <summary>Kurzfassung des Pruefergebnisses fuer die Anzeige.</summary>
    public string SummaryText => Report is null
        ? "Es ist noch keine Datei geprueft."
        : Report.Verdict switch
        {
            PreflightVerdict.Suitable =>
                $"Die Datei kann verarbeitet werden – {Report.PageCount} Seite(n), {SizeText}.",
            PreflightVerdict.SuitableWithWarnings =>
                $"Die Datei kann verarbeitet werden – {Report.PageCount} Seite(n), {SizeText}. "
                + "Bitte beachten Sie die Hinweise.",
            _ => "Die Datei kann nicht verarbeitet werden. Die Liste nennt den Grund "
                 + "und was Sie tun koennen.",
        };

    private string SizeText => Report is null
        ? string.Empty
        : string.Create(
            CultureInfo.CurrentCulture,
            $"{Report.FileSizeInMegabytes.ToString("0.##", CultureInfo.CurrentCulture)} MB");

    /// <summary>Prueft die ausgewaehlte Datei.</summary>
    [RelayCommand]
    public async Task InspectAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        IsChecking = true;

        try
        {
            FilePath = path;
            Report = await _service.AnalyzePdfAsync(path, cancellationToken).ConfigureAwait(true);
            ShowFindings(Report.Findings);

            // Die Vorschau ist eine Annehmlichkeit, keine Bedingung: Schlaegt sie
            // fehl, bleibt der Ablauf trotzdem benutzbar.
            PreviewImage = await _preview.RenderFirstPageAsync(path, cancellationToken)
                .ConfigureAwait(true);

            // Die Datenerkennung laeuft erst, wenn die Datei ueberhaupt
            // verwendbar ist. Fuer eine abgelehnte PDF waere sie sinnlos.
            if (Report.CanProceed)
            {
                await DetectAsync(path, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// Wertet den PDF-Text aus und stellt zusammen, was gefunden wurde.
    ///
    /// Die Erkennung ist eine Komfortfunktion: Schlaegt sie fehl, bleibt der
    /// Ablauf unberuehrt und die Daten werden von Hand erfasst.
    /// </summary>
    private async Task DetectAsync(string path, CancellationToken cancellationToken)
    {
        CompanyTemplate template = await LoadTemplateSafelyAsync(cancellationToken).ConfigureAwait(true);

        Detection = await _detector.DetectAsync(path, template, cancellationToken).ConfigureAwait(true);

        DetectionNotes.Clear();

        if (!Detection.HasUsableText)
        {
            DetectionNotes.Add(new DetectionNote(
                DetectionNoteKind.Missing,
                "In dieser PDF wurde kein ausreichend verwertbarer Text gefunden. "
                + "Die Rechnungsdaten muessen von Hand erfasst werden."));
        }
        else
        {
            AddNote(Detection.InvoiceNumber is not null, "Rechnungsnummer");
            AddNote(Detection.IssueDate is not null, "Rechnungsdatum");
            AddNote(Detection.DeliveryDate is not null, "Leistungsdatum");
            AddNote(Detection.DueDate is not null, "Faelligkeitsdatum");
            AddNote(Detection.Seller.HasAnything, "Verkaeuferangaben");
            AddNote(Detection.Buyer.HasAnything, "Empfaengerangaben");
            AddNote(Detection.Totals.Gross is not null, "Gesamtbetrag");
            AddNote(Detection.Iban is not null, "IBAN");

            // Die Positionserkennung ist nicht umgesetzt. Der Hinweis sagt das
            // ausdruecklich, damit niemand auf eine Automatik wartet, die es
            // nicht gibt.
            DetectionNotes.Add(new DetectionNote(
                DetectionNoteKind.Missing,
                "Rechnungspositionen werden noch nicht aus der PDF uebernommen. "
                + "Bitte erfassen Sie sie im naechsten Schritt von Hand."));
        }

        OnPropertyChanged(nameof(HasDetectionNotes));
    }

    private void AddNote(bool found, string label)
        => DetectionNotes.Add(found
            ? new DetectionNote(DetectionNoteKind.Found, $"{label} erkannt")
            : new DetectionNote(DetectionNoteKind.Missing, $"{label} nicht gefunden"));

    private async Task<CompanyTemplate> LoadTemplateSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _settingsStore.LoadTemplateAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // Ohne Vorlage erkennt die Anwendung eben weniger. Kein Grund,
            // den Ablauf anzuhalten.
            return new CompanyTemplate();
        }
    }

    /// <summary>Setzt den Schritt auf den Anfangszustand zurueck.</summary>
    public void Reset()
    {
        FilePath = string.Empty;
        Report = null;
        PreviewImage = null;
        Detection = null;
        DetectionNotes.Clear();
        OnPropertyChanged(nameof(HasDetectionNotes));
        ClearFindings();
    }
}

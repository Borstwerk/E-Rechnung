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
using EInvoiceSender.Core.Text;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 1: Die vorhandene PDF-Rechnung auswählen und prüfen lassen.
///
/// Die Datei wird ausschließlich gelesen. Was hier abgelehnt wird, ist nicht
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
    /// Formular vorzubefüllen.
    /// </summary>
    public InvoiceDetectionResult? Detection { get; private set; }

    /// <summary>Die Zeilen der Erkennungsübersicht, wie sie angezeigt werden.</summary>
    public ObservableCollection<DetectionNote> DetectionNotes { get; } = [];

    /// <summary>Gibt es überhaupt etwas zu berichten?</summary>
    public bool HasDetectionNotes => DetectionNotes.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(FileName))]
    private string _filePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSuitable))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(RequiresRasterFallback))]
    [NotifyPropertyChangedFor(nameof(ShowsRasterFallbackOffer))]
    [NotifyPropertyChangedFor(nameof(RasterFallbackAccepted))]
    private PdfPreflightReport? _report;

    /// <summary>
    /// Die ausdrückliche Zustimmung zur sichtbaren Kopie.
    ///
    /// Beginnt bei jeder Datei aufs Neue bei „nein“ und wird nirgends
    /// aufbewahrt: Ob der Verlust des durchsuchbaren Textes hinnehmbar ist,
    /// entscheidet sich an der einzelnen Rechnung, nicht ein für alle Mal.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSuitable))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ShowsRasterFallbackOffer))]
    [NotifyPropertyChangedFor(nameof(RasterFallbackAccepted))]
    private bool _rasterFallbackConfirmed;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private bool _isChecking;

    /// <summary>Ist überhaupt eine Datei gewählt?</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Der Dateiname ohne Pfad.</summary>
    public string FileName => HasFile ? Path.GetFileName(FilePath) : string.Empty;

    /// <summary>
    /// Taugt die Datei als Grundlage?
    ///
    /// Beim Rasterweg reicht „technisch möglich“ nicht: Solange die Zustimmung
    /// fehlt, bleibt „Weiter“ gesperrt. Dieselbe Bedingung prüft der Kern noch
    /// einmal – hier steht sie, damit der Benutzer sieht, woran es liegt, nicht
    /// damit sie überhaupt geprüft wird.
    /// </summary>
    public bool IsSuitable
        => Report?.CanProceed == true && (!RequiresRasterFallback || RasterFallbackConfirmed);

    /// <summary>Führt der einzige Weg über eine sichtbare Kopie?</summary>
    public bool RequiresRasterFallback => Report?.RequiresRasterFallback == true;

    /// <summary>Steht die Frage nach der sichtbaren Kopie gerade offen?</summary>
    public bool ShowsRasterFallbackOffer => RequiresRasterFallback && !RasterFallbackConfirmed;

    /// <summary>Hat der Benutzer der sichtbaren Kopie zugestimmt?</summary>
    public bool RasterFallbackAccepted => RequiresRasterFallback && RasterFallbackConfirmed;

    /// <summary>
    /// Nimmt die sichtbare Kopie an.
    ///
    /// Bewusst eine eigene Handlung und kein vorbelegtes Kästchen: Eine
    /// Zustimmung, die man übersieht, ist keine.
    /// </summary>
    [RelayCommand]
    public void ConfirmRasterFallback() => RasterFallbackConfirmed = true;

    /// <summary>
    /// Kurzfassung des Prüfergebnisses für die Anzeige.
    ///
    /// Nach einer bestandenen Prüfung nennt der Satz auch den nächsten
    /// Handgriff. Vorher stand dort nur ein Befund; dass die Datei damit fertig
    /// geprüft war und es weitergehen konnte, musste man erraten.
    /// </summary>
    public string SummaryText => Report is null
        ? "Es ist noch keine Datei geprüft."
        : ShowsRasterFallbackOffer
            ? $"Die PDF wurde geprüft – {PageText}, {SizeText}. Sie lässt sich nicht "
              + "unverändert übernehmen. Entscheiden Sie, ob stattdessen eine sichtbare "
              + "Kopie verwendet werden soll."
        : RasterFallbackAccepted
            ? $"Die PDF wurde geprüft – {PageText}, {SizeText}. Es wird eine sichtbare "
              + "Kopie verwendet. Klicken Sie auf „Weiter“, um die Rechnungsdaten zu erfassen."
            : Report.Verdict switch
            {
                PreflightVerdict.Suitable =>
                    $"Die PDF wurde erfolgreich geprüft – {PageText}, {SizeText}. "
                    + "Klicken Sie auf „Weiter“, um die Rechnungsdaten zu erfassen.",
                PreflightVerdict.SuitableWithWarnings =>
                    $"Die PDF wurde geprüft – {PageText}, {SizeText}. Bitte beachten Sie die "
                    + "Hinweise und klicken Sie danach auf „Weiter“.",
                _ => "Die Datei kann nicht verarbeitet werden. Die Liste nennt den Grund "
                     + "und was Sie tun können.",
            };

    private string PageText => Plural.Count(Report?.PageCount ?? 0, "Seite", "Seiten");

    private string SizeText => Report is null
        ? string.Empty
        : string.Create(
            CultureInfo.CurrentCulture,
            $"{Report.FileSizeInMegabytes.ToString("0.##", CultureInfo.CurrentCulture)} MB");

    /// <summary>Prüft die ausgewählte Datei.</summary>
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

            // Jede neue Datei stellt die Frage neu. Eine Zustimmung, die von der
            // vorigen Datei stehen bliebe, wäre keine Zustimmung zu dieser.
            RasterFallbackConfirmed = false;

            Report = await _service.AnalyzePdfAsync(path, cancellationToken).ConfigureAwait(true);
            ShowFindings(Report.Findings);

            // Die Vorschau ist eine Annehmlichkeit, keine Bedingung: Schlägt sie
            // fehl, bleibt der Ablauf trotzdem benutzbar.
            PreviewImage = await _preview.RenderFirstPageAsync(path, cancellationToken)
                .ConfigureAwait(true);

            // Die Datenerkennung läuft erst, wenn die Datei überhaupt
            // verwendbar ist. Für eine abgelehnte PDF wäre sie sinnlos.
            //
            // Sie läuft ausdrücklich auch dann, wenn nur der Rasterweg bleibt –
            // und sie liest dabei das **Original**, nicht die spätere Kopie.
            // Der Text steckt im Original; dass seine Schriften nicht
            // eingebettet sind, ändert daran nichts. Aus der gerasterten Fassung
            // wird nie gelesen: Dort gibt es keinen Text mehr, nur ein Bild
            // davon, und Bilder liest diese Anwendung nicht (kein OCR).
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
    /// Die Erkennung ist eine Komfortfunktion: Schlägt sie fehl, bleibt der
    /// Ablauf unberührt und die Daten werden von Hand erfasst.
    /// </summary>
    private async Task DetectAsync(string path, CancellationToken cancellationToken)
    {
        CompanyTemplate template = await LoadTemplateSafelyAsync(cancellationToken).ConfigureAwait(true);

        Detection = await _detector.DetectAsync(path, template, cancellationToken).ConfigureAwait(true);

        DetectionNotes.Clear();

        foreach (DetectionEntry entry in DetectionOverview.Describe(Detection))
        {
            DetectionNotes.Add(DetectionNote.From(entry));
        }

        OnPropertyChanged(nameof(HasDetectionNotes));
    }

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

    /// <summary>Setzt den Schritt auf den Anfangszustand zurück.</summary>
    public void Reset()
    {
        FilePath = string.Empty;
        Report = null;
        RasterFallbackConfirmed = false;
        PreviewImage = null;
        Detection = null;
        DetectionNotes.Clear();
        OnPropertyChanged(nameof(HasDetectionNotes));
        ClearFindings();
    }
}

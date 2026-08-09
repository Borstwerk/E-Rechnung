using System.IO;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.App.Services;
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
    IPdfPreviewService preview) : StepViewModel
{
    private readonly IEInvoiceService _service = service;
    private readonly IPdfPreviewService _preview = preview;

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
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>Setzt den Schritt auf den Anfangszustand zurueck.</summary>
    public void Reset()
    {
        FilePath = string.Empty;
        Report = null;
        PreviewImage = null;
        ClearFindings();
    }
}

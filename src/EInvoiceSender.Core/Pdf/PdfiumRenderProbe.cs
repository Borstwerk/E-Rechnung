using System.Runtime.Versioning;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Weist nach, dass sich die Seiten einer PDF-Datei darstellen lassen.
///
/// **Warum das nachgewiesen und nicht angenommen wird.** Der Rasterweg wird dem
/// Benutzer als Ausweg angeboten. Ein Angebot, das anschließend beim Erzeugen
/// scheitert, ist schlimmer als gar keines: Der Benutzer hat dann zugestimmt,
/// gewartet und steht trotzdem mit leeren Händen da. Deshalb wird hier jede
/// Seite tatsächlich dargestellt, bevor das Angebot überhaupt erscheint.
///
/// **Warum grob.** Die Prüfung fragt nur, *ob* PDFium die Seite aufbauen kann,
/// nicht wie sie aussieht. <see cref="ProbeDpi"/> genügt dafür und ist billig:
/// Drei dicht gesetzte Seiten brauchen zusammen weniger als eine Zehntelsekunde,
/// eine gewöhnliche Rechnungsseite kaum eine Millisekunde. Die Eingangsprüfung
/// läuft, während der Benutzer die Datei gerade ausgewählt hat – sie darf sich
/// nicht anfühlen wie eine Verarbeitung.
///
/// Die Datei wird ausschließlich gelesen.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed partial class PdfiumRenderProbe(ILogger<PdfiumRenderProbe> logger) : IPdfRenderProbe
{
    private readonly ILogger<PdfiumRenderProbe> _logger = logger;

    /// <summary>
    /// Auflösung der Probe. Bewusst weit unter der Auflösung des Ergebnisses:
    /// Hier geht es um die Frage, ob es geht, nicht um das Ergebnis.
    /// </summary>
    public const int ProbeDpi = 72;

    /// <summary>
    /// PDFium ist nicht threadsicher; die Probe teilt sich die Sperre mit dem
    /// Rasterweg und der Seitenvorschau.
    /// </summary>
    private static readonly Lock PdfiumLock = new();

    /// <inheritdoc />
    public Task<PdfRenderProbeResult> ProbeAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Probe(filePath, cancellationToken), cancellationToken);
    }

    private PdfRenderProbeResult Probe(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            byte[] source = File.ReadAllBytes(filePath);

            lock (PdfiumLock)
            {
                int pageCount = Conversion.GetPageCount(source);

                if (pageCount == 0)
                {
                    return PdfRenderProbeResult.NotRenderable(
                        "PDFium meldet keine darstellbare Seite.");
                }

                for (int page = 0; page < pageCount; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using SKBitmap rendered = Conversion.ToImage(
                        source,
                        page: page,
                        options: new(Dpi: ProbeDpi, WithAspectRatio: true,
                                     BackgroundColor: SKColors.White));

                    if (rendered.Width == 0 || rendered.Height == 0)
                    {
                        return PdfRenderProbeResult.NotRenderable(
                            $"Seite {page + 1} von {pageCount} ergab kein Bild.");
                    }
                }

                LogProbed(_logger, pageCount, ProbeDpi);

                return PdfRenderProbeResult.Renderable(pageCount);
            }
        }
        // Fremde PDF-Dateien sind nicht vertrauenswürdig. Ein Fehler beim
        // Darstellen ist hier eine Antwort ("geht nicht") und kein Programmfehler
        // – die Eingangsprüfung darf daran nicht zerbrechen.
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            string reason = ex.GetType().Name;
            LogProbeFailed(_logger, reason);

            return PdfRenderProbeResult.NotRenderable($"{reason}: {ex.Message}");
        }
    }

    [LoggerMessage(
        EventId = 2040, Level = LogLevel.Information,
        Message = "Darstellbarkeit nachgewiesen: {PageCount} Seiten bei {Dpi} dpi")]
    private static partial void LogProbed(ILogger logger, int pageCount, int dpi);

    [LoggerMessage(
        EventId = 2041, Level = LogLevel.Information,
        Message = "Darstellbarkeit nicht nachweisbar ({Reason}). Der Rasterweg wird nicht angeboten.")]
    private static partial void LogProbeFailed(ILogger logger, string reason);
}

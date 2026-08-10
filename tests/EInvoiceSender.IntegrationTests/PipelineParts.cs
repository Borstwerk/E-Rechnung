using System.Runtime.Versioning;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Setzt die Bausteine der Erzeugungskette so zusammen, wie es die Anwendung
/// beim Start tut.
///
/// **Warum das an einer Stelle steht.** Die Kette hat inzwischen zwei Wege –
/// den direkten und den Rasterweg – und beide hängen an derselben
/// Eingangsprüfung. Würde jede Prüfklasse ihre eigene Fassung
/// zusammenstecken, prüfte am Ende jede eine leicht andere Anwendung.
///
/// Die Darstellbarkeitsprüfung ist bewusst die echte und keine Attrappe: Ob
/// eine Datei den Rasterweg bekommt, hängt daran, dass PDFium sie wirklich
/// darstellen kann. Eine Attrappe, die immer „ja“ sagt, prüfte genau das nicht.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class PipelineParts
{
    public static PdfAnalyzer Analyzer()
        => new(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);

    public static PdfiumRenderProbe RenderProbe()
        => new(NullLogger<PdfiumRenderProbe>.Instance);

    public static PdfPreflightService Preflight(
        IPdfAnalyzer analyzer,
        int maxFileSizeMegabytes = PdfPreflightService.DefaultMaxFileSizeMegabytes)
        => new(analyzer, RenderProbe(), NullLogger<PdfPreflightService>.Instance,
               maxFileSizeMegabytes);

    public static RasterFallbackComposer RasterComposer()
        => new(
            new RasterizedPdfBuilder(NullLogger<RasterizedPdfBuilder>.Instance),
            NullLogger<RasterFallbackComposer>.Instance);
}

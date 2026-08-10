using System.Runtime.Versioning;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Erzeugt die E-Rechnung über eine sichtbare Kopie: Die Seiten des Originals
/// werden dargestellt und als Bild zum Inhalt neuer Seiten.
///
/// **Was hier steht und was nicht.** Diese Klasse enthält keine
/// PDF/A-Erzeugung. Sie hängt <see cref="RasterizedPdfBuilder"/> – erprobt und
/// extern geprüft – an denselben Anschluss, den der direkte Weg bedient, und
/// übersetzt dessen Ergebnis in die Sprache der Erzeugungskette. Die
/// Bestandteile, die aus dem Ergebnis eine normgerechte Hybridrechnung machen,
/// stammen für beide Wege aus <see cref="PdfAInvoiceParts"/>. Ein zweiter
/// PDF/A-Erzeuger daneben wäre eine zweite Stelle, an der die Norm falsch
/// umgesetzt werden kann.
///
/// **Der Weg wird hier nicht gewählt.** Ob er in Frage kommt, hat die
/// Eingangsprüfung entschieden; ob er beschritten wird, der Benutzer. Wer diese
/// Klasse aufruft, hat beides bereits geklärt.
///
/// Das Original wird ausschließlich gelesen.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed partial class RasterFallbackComposer : IPdfARasterFallbackComposer
{
    private readonly RasterizedPdfBuilder _builder;
    private readonly ILogger<RasterFallbackComposer> _logger;

    public RasterFallbackComposer(
        RasterizedPdfBuilder builder, ILogger<RasterFallbackComposer> logger)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CompositionResult> ComposeAsync(
        PdfACompositionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var report = new ValidationReportBuilder();

        try
        {
            RasterizedPdfResult result = await Task
                .Run(() => _builder.Build(request, RasterizedPdfBuilder.DefaultDpi), cancellationToken)
                .ConfigureAwait(false);

            // Der Weg gehört in den Prüfbericht, und zwar sachlich: Der Benutzer
            // hat ihn gewählt, er ist erfolgreich gegangen worden, also ist das
            // ein Hinweis und keine Warnung.
            report.Information(
                "APP-PDF-040",
                $"Die E-Rechnung wurde als sichtbare PDF/A-Kopie erzeugt "
                + $"(Raster-Fallback, {result.Dpi} dpi). Die Ausgangsdatei war für die "
                + "direkte Übernahme nicht geeignet; die sichtbaren Seiten wurden örtlich "
                + "neu aufgebaut. Das Original wurde nicht verändert.",
                technicalDetail: $"{result.PageCount} Seiten bei {result.Dpi} dpi neu aufgebaut.");

            LogRasterComposed(_logger, result.PageCount, result.Dpi, result.PdfBytes.Length);

            return CompositionResult.Success(result.PdfBytes, report.Build());
        }
        catch (InvalidOperationException ex)
        {
            report.Error(
                "APP-PDF-041",
                "Die sichtbare Kopie konnte nicht erzeugt werden. Die Seiten der "
                + "PDF-Datei ließen sich nicht darstellen.",
                technicalDetail: ex.Message);

            return CompositionResult.Failed(report.Build());
        }
        // Fremde PDF-Dateien sind nicht vertrauenswürdig. Scheitert das
        // Darstellen, ist das ein Betriebsfall mit einer Meldung – und keine
        // Ausnahme, die den Ablauf abbricht und dem Benutzer einen Stapelabzug
        // zeigt.
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            report.Error(
                "APP-PDF-042",
                "Die sichtbare Kopie konnte nicht erzeugt werden.",
                technicalDetail: $"{ex.GetType().Name}: {ex.Message}");

            return CompositionResult.Failed(report.Build());
        }
    }

    [LoggerMessage(
        EventId = 2003, Level = LogLevel.Information,
        Message = "PDF/A-3b über den Rasterweg erzeugt: {Pages} Seiten bei {Dpi} dpi, {Size} Bytes")]
    private static partial void LogRasterComposed(ILogger logger, int pages, int dpi, int size);
}

using System.Runtime.Versioning;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PDFtoImage;
using SkiaSharp;

// PDFtoImage meldet Seitengrößen als System.Drawing.SizeF. Das ist ein reines
// Wertepaar aus System.Drawing.Primitives – kein GDI, keine Windows-Bindung.
using SizeF = System.Drawing.SizeF;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Baut aus einer PDF, die sich nicht aufwerten lässt, ein neues Dokument aus
/// den gerenderten Seitenbildern.
///
/// **Wozu das gut ist.** Der übliche Weg
/// (<see cref="PdfAInvoiceComposer"/>) übernimmt die Seiten des Originals
/// unverändert und ergänzt nur die fehlenden PDF/A-3-Bestandteile. Das setzt
/// voraus, dass die Seitenbeschreibung des Originals den Anforderungen genügt –
/// insbesondere, dass alle Schriften eingebettet sind. Ist sie das nicht, gibt
/// es keinen permissiv lizenzierten Weg, das nachzuholen (ADR-0003).
///
/// Hier wird die Seitenbeschreibung deshalb nicht repariert, sondern ersetzt:
/// PDFium rendert jede Seite zu einem Bild, und dieses Bild wird zum einzigen
/// Inhalt einer neuen Seite. Die problematischen Schriften verschwinden dabei
/// nicht durch einen Trick, sondern weil es sie im Ergebnis schlicht nicht mehr
/// gibt – es steht kein Text mehr im Dokument, nur noch ein Bild davon.
///
/// **Was das kostet.** Der sichtbare Text ist danach nicht mehr markierbar, in
/// der sichtbaren Darstellung nicht mehr durchsuchbar, und Verknüpfungen,
/// Formularfelder sowie eine vorhandene Auszeichnungsstruktur gehen verloren.
/// Maschinenlesbar bleibt die Rechnung über die eingebettete XML – die ist von
/// der Darstellung unabhängig und vollständig.
///
/// **Was hier nicht geschieht.** Das Original wird ausschließlich gelesen. Aus
/// der gerasterten Ausgabe wird niemals erneut Text erkannt; die Erkennung
/// arbeitet immer auf dem Original, bevor überhaupt gerendert wird.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed partial class RasterizedPdfBuilder(ILogger<RasterizedPdfBuilder> logger)
{
    private readonly ILogger<RasterizedPdfBuilder> _logger = logger;

    /// <summary>
    /// Die vorgesehene Auflösung. Sie steht in
    /// <see cref="RasterFallback.Dpi"/>, weil auch der Prüfbericht sie nennt.
    /// </summary>
    public const int DefaultDpi = RasterFallback.Dpi;

    /// <summary>
    /// PDFium ist nicht threadsicher. Das Rendern läuft deshalb serialisiert –
    /// derselbe Schutz, den die Vorschau in der Oberfläche schon einsetzt.
    /// Nebenläufigkeit bringt hier nichts: Eine Rechnung hat wenige Seiten.
    /// </summary>
    private static readonly Lock PdfiumLock = new();

    /// <summary>
    /// Rendert alle Seiten der Quelldatei und setzt daraus eine fertige
    /// PDF/A-3b-Datei mit eingebetteter Rechnungs-XML zusammen.
    /// </summary>
    /// <param name="request">Quelldatei, Rechnungs-XML und Metadaten.</param>
    /// <param name="dpi">Auflösung des Renderns.</param>
    /// <exception cref="InvalidOperationException">
    /// Die Quelldatei lässt sich nicht rendern – etwa weil sie verschlüsselt
    /// oder beschädigt ist.
    /// </exception>
    public RasterizedPdfResult Build(PdfACompositionRequest request, int dpi = DefaultDpi)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(dpi, 72);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dpi, 600);

        // Nur gelesen: Die Originaldatei bleibt unangetastet.
        byte[] source = File.ReadAllBytes(request.SourcePdfPath);

        var pages = new List<RasterizedPageInfo>();
        using var document = new PdfDocument();

        lock (PdfiumLock)
        {
            int pageCount = Conversion.GetPageCount(source);

            if (pageCount == 0)
            {
                throw new InvalidOperationException(
                    "Die PDF-Datei enthält keine Seite, die sich darstellen lässt.");
            }

            IList<SizeF> sizes = Conversion.GetPageSizes(source);

            for (int index = 0; index < pageCount; index++)
            {
                pages.Add(AddRenderedPage(document, source, index, sizes[index], dpi));
            }
        }

        DisableImageInterpolation(document);

        byte[] result = PdfAInvoiceParts.Finish(document, request);

        LogRasterized(_logger, pages.Count, dpi, source.Length, result.Length);

        return new RasterizedPdfResult(result, dpi, pages);
    }

    /// <summary>
    /// Rendert eine Seite und legt sie als einziges Bild auf eine neue Seite
    /// derselben sichtbaren Größe.
    /// </summary>
    private static RasterizedPageInfo AddRenderedPage(
        PdfDocument document, byte[] source, int index, SizeF size, int dpi)
    {
        // Weißer Grund, kein durchscheinender Hintergrund: Ein Rechnungsblatt
        // ist weiß. Ohne diese Vorgabe rendert PDFium auf Transparenz, und was
        // dann sichtbar wird, hängt vom Betrachter ab.
        using SKBitmap rendered = Conversion.ToImage(
            source,
            page: index,
            options: new(Dpi: dpi, WithAspectRatio: true, BackgroundColor: SKColors.White));

        using SKBitmap opaque = MakeOpaque(rendered);

        // Verlustfrei. JPEG wäre bei einer Textseite rund ein Viertel kleiner
        // (bei 300 dpi 220 statt 281 KB), setzt aber Artefakte an die
        // Schriftkanten – und die Schrift ist hier alles, was die Rechnung
        // ausmacht. Die Größe ist der Preis für eine saubere Darstellung.
        // PDFsharp übernimmt den Inhalt als FlateDecode in DeviceRGB, ohne
        // Alphakanal.
        using SKData encoded = opaque.Encode(SKEncodedImageFormat.Png, 100);

        PdfPage page = document.AddPage();
        page.Width = XUnit.FromPoint(size.Width);
        page.Height = XUnit.FromPoint(size.Height);

        using (XGraphics graphics = XGraphics.FromPdfPage(page))
        {
            XImage image = XImage.FromStream(new MemoryStream(encoded.ToArray()));
            graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }

        return new RasterizedPageInfo(size.Width, size.Height, opaque.Width, opaque.Height);
    }

    /// <summary>
    /// Schaltet die Bildinterpolation an allen erzeugten Bildern ab.
    ///
    /// PDFsharp schreibt an jedes Bild <c>/Interpolate true</c> – eine
    /// Empfehlung an den Betrachter, beim Vergrößern zu glätten. PDF/A
    /// verbietet das: ISO 19005-3, Abschnitt 6.2.8 verlangt, dass der
    /// Schlüssel, wenn er vorhanden ist, <c>false</c> ist. Der Grund ist der
    /// Sinn der Norm – wie das Dokument aussieht, soll das Dokument bestimmen
    /// und nicht der Betrachter.
    ///
    /// Das war der einzige Punkt, an dem veraPDF die gerasterten Dateien
    /// beanstandet hat (1 von 378 Prüfaussagen). Sichtbar ändert sich dadurch
    /// nichts: Das Bild hat bei 300 dpi ohnehin mehr Punkte als der Bildschirm.
    /// </summary>
    private static void DisableImageInterpolation(PdfDocument document)
    {
        foreach (PdfObject candidate in document.Internals.GetAllObjects())
        {
            if (candidate is PdfDictionary dictionary
                && dictionary.Elements.GetName("/Subtype") == "/Image")
            {
                dictionary.Elements["/Interpolate"] = new PdfBoolean(false);
            }
        }
    }

    /// <summary>
    /// Legt das Gerenderte auf weißen Grund und nimmt den Alphakanal heraus.
    ///
    /// PDFium liefert BGRA mit vormultipliziertem Alpha. Ein Alphakanal hat in
    /// einer Rechnung nichts zu suchen: Er brächte Transparenz in ein Dokument,
    /// das keine braucht, und damit eine Eigenschaft, deren Wirkung erst der
    /// Betrachter entscheidet.
    /// </summary>
    private static SKBitmap MakeOpaque(SKBitmap rendered)
    {
        var opaque = new SKBitmap(
            new SKImageInfo(rendered.Width, rendered.Height, SKColorType.Rgb888x, SKAlphaType.Opaque));

        using var canvas = new SKCanvas(opaque);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(rendered, 0, 0, SKSamplingOptions.Default, paint: null);

        return opaque;
    }

    [LoggerMessage(
        EventId = 2030, Level = LogLevel.Information,
        Message = "Rasterweg: {Pages} Seiten bei {Dpi} dpi, {SourceSize} Bytes Vorlage, {ResultSize} Bytes Ergebnis")]
    private static partial void LogRasterized(
        ILogger logger, int pages, int dpi, int sourceSize, int resultSize);
}

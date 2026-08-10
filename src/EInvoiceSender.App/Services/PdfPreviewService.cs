using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace EInvoiceSender.App.Services;

/// <summary>Rendert die erste Seite einer PDF als Bild für die Vorschau.</summary>
public interface IPdfPreviewService
{
    /// <summary>
    /// Liefert die erste Seite als Bild oder <c>null</c>, wenn sich die Datei
    /// nicht darstellen lässt. Die Vorschau ist eine Hilfe, keine Bedingung –
    /// ein Fehler darf den Ablauf nicht aufhalten.
    /// </summary>
    Task<ImageSource?> RenderFirstPageAsync(string pdfPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Vorschau über PDFium (Paket PDFtoImage).
///
/// Das Rendern läuft im Hintergrund, das fertige Bild wird eingefroren. Nur
/// eingefrorene <see cref="ImageSource"/>-Objekte dürfen über Threadgrenzen
/// hinweg an die Oberfläche gegeben werden.
/// </summary>
public sealed partial class PdfPreviewService(ILogger<PdfPreviewService> logger) : IPdfPreviewService
{
    private readonly ILogger<PdfPreviewService> _logger = logger;

    /// <inheritdoc />
    public async Task<ImageSource?> RenderFirstPageAsync(
        string pdfPath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(
                () =>
                {
                    byte[] pdf = File.ReadAllBytes(pdfPath);
                    using SKBitmap bitmap = Conversion.ToImage(pdf, page: 0, options: new(Dpi: 110));
                    using SKData encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90);

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = new MemoryStream(encoded.ToArray());
                    image.EndInit();
                    image.Freeze();

                    return (ImageSource)image;
                },
                cancellationToken).ConfigureAwait(true);
        }
        // **Die Vorschau darf niemals werfen.** Die Zusage steht oben in der
        // Schnittstelle, die Liste darunter hielt sie nicht: PDFium meldet eine
        // kennwortgeschützte Datei mit einer eigenen Ausnahme, und die stand
        // nicht darin. Sie lief bis zum letzten Auffangnetz der Anwendung durch,
        // das dem Anwender pflichtschuldig „Password required or incorrect
        // password.“ als Störungsmeldung zeigte – auf Englisch, aus einer
        // Bibliothek, und direkt neben dem verständlichen deutschen Befund, den
        // die Eingangsprüfung längst geschrieben hatte.
        //
        // Eine Aufzählung erwarteter Ausnahmen ist hier das falsche Werkzeug:
        // Was eine fremde PDF in einer nativen Bibliothek auslöst, lässt sich
        // nicht aufzählen. Gefangen wird deshalb alles, was kein Abbruch und
        // kein Zustand ist, in dem Weiterarbeiten ohnehin sinnlos wäre.
        catch (Exception exception) when (exception is not OperationCanceledException
                                             and not OutOfMemoryException
                                             and not StackOverflowException)
        {
            string reason = exception.GetType().Name;
            LogPreviewFailed(reason);

            return null;
        }
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "Die PDF-Vorschau konnte nicht erzeugt werden ({Reason}). Der Ablauf läuft ohne Vorschau weiter.")]
    private partial void LogPreviewFailed(string reason);
}

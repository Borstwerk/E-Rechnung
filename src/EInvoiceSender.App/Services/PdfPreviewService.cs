using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;

namespace EInvoiceSender.App.Services;

/// <summary>Rendert die erste Seite einer PDF als Bild fuer die Vorschau.</summary>
public interface IPdfPreviewService
{
    /// <summary>
    /// Liefert die erste Seite als Bild oder <c>null</c>, wenn sich die Datei
    /// nicht darstellen laesst. Die Vorschau ist eine Hilfe, keine Bedingung –
    /// ein Fehler darf den Ablauf nicht aufhalten.
    /// </summary>
    Task<ImageSource?> RenderFirstPageAsync(string pdfPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Vorschau ueber PDFium (Paket PDFtoImage).
///
/// Das Rendern laeuft im Hintergrund, das fertige Bild wird eingefroren. Nur
/// eingefrorene <see cref="ImageSource"/>-Objekte duerfen ueber Threadgrenzen
/// hinweg an die Oberflaeche gegeben werden.
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
        catch (Exception exception) when (exception is IOException or InvalidOperationException or NotSupportedException)
        {
            string reason = exception.GetType().Name;
            LogPreviewFailed(reason);

            return null;
        }
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "Die PDF-Vorschau konnte nicht erzeugt werden ({Reason}). Der Ablauf laeuft ohne Vorschau weiter.")]
    private partial void LogPreviewFailed(string reason);
}

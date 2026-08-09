using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Erzeugt Eingangs-PDFs fuer die Tests.
///
/// Bewusst ohne Text: Schrifteinbettung braucht eine Schriftdatei, deren
/// Lizenz und Herkunft sonst mitdokumentiert werden muessten, und der
/// Build-Agent hat keine zugesicherte Schriftausstattung. Fuer die Pruefung
/// des PDF/A-3-Wegs ist Text nicht noetig – PDF/A verlangt keinen Text,
/// sondern nur, dass alles Verwendete eingebettet ist.
///
/// Der Fall "Schrift nicht eingebettet" wird ueber ein von Hand gebautes PDF
/// mit einer der 14 Standardschriften abgedeckt.
/// </summary>
public static class TestPdfFactory
{
    /// <summary>
    /// Erzeugt eine schlichte, mehrseitige PDF nur aus Vektorgrafik.
    /// Ein solches Dokument ist zu PDF/A-3 aufwertbar.
    /// </summary>
    public static byte[] CreateSimplePdf(int pageCount = 1)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Testrechnung";

        for (int i = 0; i < pageCount; i++)
        {
            PdfPage page = document.AddPage();
            page.Width = XUnit.FromMillimeter(210);
            page.Height = XUnit.FromMillimeter(297);

            using XGraphics gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(40, 40, 300, 60));
            gfx.DrawLine(XPens.Black, 40, 120, 500, 120);
            gfx.DrawRectangle(XPens.Black, new XRect(40, 140, 460, 200));
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);

        return stream.ToArray();
    }

    /// <summary>
    /// Baut von Hand eine minimale PDF, die die Standardschrift Helvetica
    /// verwendet. Standardschriften werden nicht eingebettet – dieses Dokument
    /// muss von der Anwendung abgelehnt werden.
    /// </summary>
    public static byte[] CreatePdfWithNonEmbeddedFont()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
            + "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            null!, // wird als Stream eingesetzt
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        const string content = "BT /F1 24 Tf 72 700 Td (Testrechnung) Tj ET";

        var builder = new System.Text.StringBuilder();
        var offsets = new List<int> { 0 };

        builder.Append("%PDF-1.4\n");

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(builder.Length);
            builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n");

            if (i == 3)
            {
                builder.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"<< /Length {content.Length} >>\nstream\n{content}\nendstream\n");
            }
            else
            {
                builder.Append(objects[i]).Append('\n');
            }

            builder.Append("endobj\n");
        }

        int xrefPosition = builder.Length;
        builder.Append(
            System.Globalization.CultureInfo.InvariantCulture,
            $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");

        for (int i = 1; i <= objects.Count; i++)
        {
            builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"{offsets[i]:D10} 00000 n \n");
        }

        builder.Append(
            System.Globalization.CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF\n");

        return System.Text.Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Liefert Bytes, die zwar wie ein PDF beginnen, aber keine gueltige
    /// Struktur haben. Muss als beschaedigt erkannt werden.
    /// </summary>
    public static byte[] CreateDamagedPdf()
        => System.Text.Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 99 0 R >>\nendobj\n"
            + "hier ist die Datei abgeschnitten");

    /// <summary>Liefert Bytes, die ueberhaupt kein PDF sind.</summary>
    public static byte[] CreateNonPdf()
        => System.Text.Encoding.UTF8.GetBytes("Das ist eine ganz normale Textdatei.");

    /// <summary>Schreibt Inhalte in eine temporaere Datei und liefert den Pfad.</summary>
    public static string WriteToTempFile(byte[] content, string extension = ".pdf")
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"einvoicesender-test-{Guid.NewGuid():N}{extension}");

        File.WriteAllBytes(path, content);

        return path;
    }
}

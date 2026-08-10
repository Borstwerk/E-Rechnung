using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Erzeugt Eingangs-PDFs für die Tests.
///
/// Bewusst ohne Text: Schrifteinbettung braucht eine Schriftdatei, deren
/// Lizenz und Herkunft sonst mitdokumentiert werden müssten, und der
/// Build-Agent hat keine zugesicherte Schriftausstattung. Für die Prüfung
/// des PDF/A-3-Wegs ist Text nicht nötig – PDF/A verlangt keinen Text,
/// sondern nur, dass alles Verwendete eingebettet ist.
///
/// Der Fall "Schrift nicht eingebettet" wird über ein von Hand gebautes PDF
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
    /// Baut eine PDF mit drei verschieden geformten Seiten: A4 hoch, A4 quer
    /// und eine A4-Hochformatseite mit <c>/Rotate 90</c>.
    ///
    /// Der dritte Fall ist der lehrreiche: Die Seite ist im Datenmodell hoch
    /// und wird nur angezeigt wie ein Querformat. Ein Weg, der die
    /// MediaBox-Angabe für bare Münze nimmt, dreht die Seite still zurück.
    ///
    /// Die Schrift ist auch hier nicht eingebettet – eine solche Datei geht
    /// den direkten Weg ohnehin nicht.
    /// </summary>
    public static byte[] CreateMixedPageSizesPdf() => BuildPages(
        (595, 842, 0, "Seite 1 - A4 hoch"),
        (842, 595, 0, "Seite 2 - A4 quer"),
        (595, 842, 90, "Seite 3 - hoch, um 90 Grad gedreht"));

    /// <summary>
    /// Baut eine mehrseitige PDF mit der angegebenen Seitenzahl, jede Seite
    /// A4 hoch und erkennbar beschriftet.
    /// </summary>
    public static byte[] CreateMultiPagePdf(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageCount, 1);

        return BuildPages(
            [.. Enumerable.Range(1, pageCount).Select(i => (595, 842, 0, $"Seite {i} von {pageCount}"))]);
    }

    /// <summary>
    /// Baut eine PDF, in der zwei Hindernisse zusammentreffen: eine nicht
    /// eingebettete Schrift und eine beim Öffnen startende Aktion.
    ///
    /// Der Fall belegt, dass der Rasterweg keine Sammelfreigabe ist. Die
    /// fehlende Schrift allein wäre sein Anlass; kommt etwas hinzu, das aus
    /// anderen Gründen nicht in eine Rechnung gehört, bleibt es dabei.
    /// </summary>
    public static byte[] CreatePdfWithNonEmbeddedFontAndJavaScript()
        => BuildPages(
            "/OpenAction << /Type /Action /S /JavaScript /JS (app.alert\\(1\\);) >>",
            (595, 842, 0, "Seite mit aktivem Inhalt"));

    /// <summary>
    /// Baut eine PDF mit Besitzerkennwort und eingeschränkten Rechten.
    ///
    /// **Der Fall, der die Lücke aufdeckt.** Ein solches Dokument öffnet sich
    /// ohne Kennwort; PDFsharp meldet dafür <c>IsEncrypted == false</c>. Wer
    /// sich darauf verlässt, hält die Datei für ungeschützt – dabei steht im
    /// Trailer ein <c>/Encrypt</c>-Wörterbuch, und der Rechteinhaber hat
    /// festgelegt, was mit ihr geschehen darf.
    ///
    /// Das Kennwort ist eine erfundene Zeichenkette für diese eine, im Test
    /// erzeugte Datei.
    /// </summary>
    public static byte[] CreatePdfWithOwnerPassword()
        => CreateProtectedPdf(ownerPassword: "besitzer-testwert", userPassword: null);

    /// <summary>
    /// Baut eine PDF, die zum Öffnen ein Kennwort verlangt. Ohne das Kennwort
    /// gibt es nichts zu lesen und nichts darzustellen.
    /// </summary>
    public static byte[] CreatePdfWithUserPassword()
        => CreateProtectedPdf(ownerPassword: "besitzer-testwert", userPassword: "öffnen-testwert");

    private static byte[] CreateProtectedPdf(string ownerPassword, string? userPassword)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Geschützte Testrechnung";

        PdfPage page = document.AddPage();
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);

        using (XGraphics gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(40, 40, 300, 60));
            gfx.DrawLine(XPens.Black, 40, 120, 500, 120);
        }

        PdfSharp.Pdf.Security.PdfSecuritySettings security = document.SecuritySettings;
        security.OwnerPassword = ownerPassword;

        if (userPassword is not null)
        {
            security.UserPassword = userPassword;
        }

        security.PermitPrint = false;
        security.PermitExtractContent = false;
        security.PermitModifyDocument = false;

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);

        return stream.ToArray();
    }

    /// <summary>
    /// Setzt von Hand eine PDF aus den beschriebenen Seiten zusammen.
    ///
    /// Von Hand, weil PdfSharp für Text eine Schriftdatei bräuchte und weil
    /// genau die nicht eingebettete Standardschrift gewünscht ist. Jede Seite
    /// bekommt zusätzlich einen Rahmen: An ihm lässt sich im Sichtvergleich
    /// ablesen, ob etwas beschnitten wurde.
    /// </summary>
    private static byte[] BuildPages(params (int Width, int Height, int Rotate, string Text)[] pages)
        => BuildPages(catalogExtra: null, pages);

    /// <summary>
    /// Wie oben, ergänzt aber den Dokumentkatalog um weitere Einträge –
    /// für Dateien, deren Besonderheit nicht auf einer Seite steht.
    /// </summary>
    private static byte[] BuildPages(
        string? catalogExtra, params (int Width, int Height, int Rotate, string Text)[] pages)
    {
        var objects = new List<byte[]>();
        int fontObject = 3 + (pages.Length * 2);

        string kids = string.Join(
            " ", Enumerable.Range(0, pages.Length).Select(i => $"{3 + (i * 2)} 0 R"));

        objects.Add(Ascii(
            "<< /Type /Catalog /Pages 2 0 R"
            + (catalogExtra is null ? string.Empty : " " + catalogExtra)
            + " >>"));
        objects.Add(Ascii($"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>"));

        for (int i = 0; i < pages.Length; i++)
        {
            (int width, int height, int rotate, string text) = pages[i];

            objects.Add(Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Rotate {rotate} "
                + $"/Resources << /Font << /F1 {fontObject} 0 R >> >> /Contents {4 + (i * 2)} 0 R >>"));

            byte[] content = Ascii(
                $"BT /F1 24 Tf 40 {height - 80} Td ({text}) Tj ET\n"
                + $"2 w 20 20 {width - 40} {height - 40} re S");

            objects.Add(
            [
                .. Ascii($"<< /Length {content.Length} >>\nstream\n"),
                .. content,
                .. Ascii("\nendstream"),
            ]);
        }

        objects.Add(Ascii(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));

        var file = new MemoryStream();
        var offsets = new List<int>(objects.Count);

        file.Write(Ascii("%PDF-1.4\n"));

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add((int)file.Length);
            file.Write(Ascii($"{i + 1} 0 obj\n"));
            file.Write(objects[i]);
            file.Write(Ascii("\nendobj\n"));
        }

        int xref = (int)file.Length;

        file.Write(Ascii($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));

        foreach (int offset in offsets)
        {
            file.Write(Ascii($"{offset:D10} 00000 n \n"));
        }

        file.Write(Ascii(
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));

        return file.ToArray();
    }

    private static byte[] Ascii(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    /// <summary>
    /// Liefert Bytes, die zwar wie ein PDF beginnen, aber keine gültige
    /// Struktur haben. Muss als beschädigt erkannt werden.
    /// </summary>
    public static byte[] CreateDamagedPdf()
        => System.Text.Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 99 0 R >>\nendobj\n"
            + "hier ist die Datei abgeschnitten");

    /// <summary>Liefert Bytes, die überhaupt kein PDF sind.</summary>
    public static byte[] CreateNonPdf()
        => System.Text.Encoding.UTF8.GetBytes("Das ist eine ganz normale Textdatei.");

    /// <summary>Schreibt Inhalte in eine temporäre Datei und liefert den Pfad.</summary>
    public static string WriteToTempFile(byte[] content, string extension = ".pdf")
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"einvoicesender-test-{Guid.NewGuid():N}{extension}");

        File.WriteAllBytes(path, content);

        return path;
    }
}

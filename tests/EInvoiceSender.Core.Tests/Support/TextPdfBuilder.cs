using System.Globalization;
using System.Text;

namespace EInvoiceSender.Core.Tests.Support;

/// <summary>Ein Textfragment an einer festen Position auf einer Testseite.</summary>
public sealed record PositionedPdfText(string Text, double Left, double Top);

/// <summary>
/// Baut PDF-Dateien mit echtem, maschinenlesbarem Text – die Vorgabe für die
/// Tests der Rechnungserkennung.
///
/// Bewusst von Hand statt über PdfSharp: Textausgabe braucht dort eine
/// Schriftdatei, deren Lizenz und Herkunft dann mitzudokumentieren wären, und
/// der Build-Agent hat keine zugesicherte Schriftausstattung. Hier genügt die
/// Standardschrift Helvetica. Sie wird nicht eingebettet – für die
/// Textextraktion ist das gleichgültig, und diese Dateien durchlaufen die
/// PDF/A-Aufwertung nie.
///
/// Der Text wird in WinAnsi kodiert, damit deutsche Umlaute und das
/// Eurozeichen richtig herauskommen.
/// </summary>
public static class TextPdfBuilder
{
    private const int PageHeight = 842;
    private const int PageWidth = 595;
    private const int LeftMargin = 56;
    private const int TopMargin = 780;
    private const int LineHeight = 14;
    private const int LinesPerPage = 52;

    /// <summary>Erzeugt eine PDF, die die übergebenen Zeilen enthält.</summary>
    public static byte[] Create(params string[] lines) => Create((IEnumerable<string>)lines);

    /// <summary>Erzeugt eine PDF, die die übergebenen Zeilen enthält.</summary>
    public static byte[] Create(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<string[]> pages = [.. lines.Chunk(LinesPerPage)];

        if (pages.Count == 0)
        {
            pages.Add([string.Empty]);
        }

        // Objektnummern: 1 Katalog, 2 Seitenbaum, dann je Seite ein
        // Seitenobjekt und ein Inhaltsstrom, zuletzt die Schrift.
        int fontObject = 3 + (pages.Count * 2);
        var body = new List<byte[]>();

        string kids = string.Join(
            " ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + (i * 2)} 0 R"));

        body.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        body.Add(Ascii(
            $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>"));

        for (int i = 0; i < pages.Count; i++)
        {
            int contentObject = 4 + (i * 2);

            body.Add(Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] "
                + $"/Resources << /Font << /F1 {fontObject} 0 R >> >> /Contents {contentObject} 0 R >>"));

            byte[] content = BuildContent(pages[i]);
            byte[] header = Ascii($"<< /Length {content.Length} >>\nstream\n");
            byte[] footer = Ascii("\nendstream");

            body.Add([.. header, .. content, .. footer]);
        }

        body.Add(Ascii(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));

        return Assemble(body);
    }

    /// <summary>
    /// Erzeugt eine PDF mit zwei nebeneinander stehenden Spalten – der Fall
    /// "Verkäufer links, Käufer rechts", der in echten Rechnungen häufig
    /// ist. Die Zeilen werden paarweise auf dieselbe Grundlinie gesetzt.
    /// </summary>
    public static byte[] CreateTwoColumn(
        IReadOnlyList<string> left, IReadOnlyList<string> right, IEnumerable<string>? below = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var stream = new MemoryStream();
        Write(stream, "BT\n/F1 10 Tf\n");

        int rows = Math.Max(left.Count, right.Count);

        for (int i = 0; i < rows; i++)
        {
            int y = TopMargin - (i * LineHeight);

            if (i < left.Count)
            {
                WriteAt(stream, LeftMargin, y, left[i]);
            }

            if (i < right.Count)
            {
                WriteAt(stream, RightColumn, y, right[i]);
            }
        }

        int next = TopMargin - ((rows + 1) * LineHeight);

        foreach (string line in below ?? [])
        {
            WriteAt(stream, LeftMargin, next, line);
            next -= LineHeight;
        }

        Write(stream, "ET");

        return Assemble(SinglePageBody(stream.ToArray()));
    }

    /// <summary>
    /// Erzeugt eine Seite aus frei positionierten Textfragmenten. Damit lassen
    /// sich reale Top-/Left-/Segmentverhältnisse nachbilden, ohne eine echte
    /// Rechnung als Testdatei einzuchecken.
    /// </summary>
    public static byte[] CreatePositioned(IEnumerable<PositionedPdfText> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        var stream = new MemoryStream();
        Write(stream, "BT\n/F1 10 Tf\n");

        foreach (PositionedPdfText fragment in fragments)
        {
            WriteAt(stream, fragment.Left, PageHeight - fragment.Top, fragment.Text);
        }

        Write(stream, "ET");

        return Assemble(SinglePageBody(stream.ToArray()));
    }

    private const int RightColumn = 320;

    private static void WriteAt(Stream stream, double x, double y, string text)
    {
        Write(stream, string.Create(CultureInfo.InvariantCulture, $"1 0 0 1 {x} {y} Tm\n"));
        stream.WriteByte((byte)'(');
        stream.Write(EscapeWinAnsi(text));
        Write(stream, ") Tj\n");
    }

    private static List<byte[]> SinglePageBody(byte[] content)
    {
        byte[] header = Ascii($"<< /Length {content.Length} >>\nstream\n");
        byte[] footer = Ascii("\nendstream");

        return
        [
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] "
                  + "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"),
            [.. header, .. content, .. footer],
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
        ];
    }

    /// <summary>Baut den Zeichenstrom einer Seite.</summary>
    private static byte[] BuildContent(IReadOnlyList<string> lines)
    {
        var stream = new MemoryStream();

        Write(stream, "BT\n/F1 10 Tf\n");
        Write(stream, string.Create(
            CultureInfo.InvariantCulture, $"{LeftMargin} {TopMargin} Td\n{LineHeight} TL\n"));

        foreach (string line in lines)
        {
            stream.WriteByte((byte)'(');
            stream.Write(EscapeWinAnsi(line));
            Write(stream, ") Tj T*\n");
        }

        Write(stream, "ET");

        return stream.ToArray();
    }

    /// <summary>
    /// Kodiert eine Zeichenkette nach WinAnsi und schützt die Zeichen, die in
    /// einer PDF-Zeichenkette eine Bedeutung haben.
    /// </summary>
    private static byte[] EscapeWinAnsi(string text)
    {
        var bytes = new List<byte>(text.Length + 8);

        foreach (char c in text)
        {
            byte value = c switch
            {
                '€' => 0x80,
                '„' => 0x84,
                '“' => 0x93,
                '”' => 0x94,
                '–' => 0x96,
                '—' => 0x97,
                '•' => 0x95,
                _ => c <= 0xFF ? (byte)c : (byte)'?',
            };

            if (value is (byte)'(' or (byte)')' or (byte)'\\')
            {
                bytes.Add((byte)'\\');
            }

            bytes.Add(value);
        }

        return [.. bytes];
    }

    /// <summary>Setzt Kopf, Objekte, Querverweistabelle und Trailer zusammen.</summary>
    private static byte[] Assemble(List<byte[]> objects)
    {
        var file = new MemoryStream();
        var offsets = new List<int>(objects.Count);

        Write(file, "%PDF-1.4\n");

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add((int)file.Length);
            Write(file, string.Create(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n"));
            file.Write(objects[i]);
            Write(file, "\nendobj\n");
        }

        int xref = (int)file.Length;

        Write(file, string.Create(
            CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));

        foreach (int offset in offsets)
        {
            Write(file, string.Create(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n"));
        }

        Write(file, string.Create(
            CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));

        return file.ToArray();
    }

    private static void Write(Stream stream, string text) => stream.Write(Ascii(text));

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}

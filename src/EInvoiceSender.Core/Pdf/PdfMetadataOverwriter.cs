using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Ersetzt das XMP-Metadatenpaket einer fertig gespeicherten PDF durch das
/// eigene, PDF/A- und Factur-X-taugliche Paket.
///
/// Warum das nötig ist: PDFsharp erzeugt beim Speichern **immer** sein eigenes
/// XMP und überschreibt dabei ein zuvor gesetztes <c>/Metadata</c>-Objekt. Es
/// gibt keinen Schalter, der das abstellt. Ohne diesen Schritt fehlten der
/// Ausgabedatei die Kennzeichnung <c>pdfaid:part 3</c> und sämtliche
/// Factur-X-Felder – sie wäre damit weder PDF/A noch eine erkennbare
/// Hybridrechnung.
///
/// Umgesetzt als **inkrementelle Aktualisierung**: Das vorhandene
/// Metadatenobjekt wird unter derselben Objektnummer neu definiert und an die
/// Datei angehängt, gefolgt von einer eigenen Querverweistabelle mit
/// <c>/Prev</c>. Das ist ein regulärer PDF-Mechanismus; Leser verwenden immer
/// die zuletzt geschriebene Fassung eines Objekts. Der bereits geschriebene
/// Teil der Datei bleibt unangetastet, es werden also keine Verweise ungültig.
///
/// Die Textanalyse ist hier vertretbar, weil die Eingabe nicht von einem
/// Fremdprogramm stammt, sondern unmittelbar zuvor von PDFsharp erzeugt wurde:
/// klassische Querverweistabelle, unkomprimiertes XMP, einfacher Trailer.
/// Für fremde Dateien wird dieser Weg nie beschritten. Zusätzlich prüft der
/// Aufrufer das Ergebnis, indem er die Datei erneut öffnet und ausliest.
/// </summary>
public static partial class PdfMetadataOverwriter
{
    /// <summary>
    /// Hängt eine inkrementelle Aktualisierung an, die das Metadatenobjekt
    /// durch das übergebene XMP-Paket ersetzt.
    /// </summary>
    /// <param name="pdfBytes">Die von PDFsharp gespeicherte Datei.</param>
    /// <param name="xmp">Das einzusetzende XMP-Paket.</param>
    /// <returns>Die ergänzte Datei.</returns>
    /// <exception cref="InvalidOperationException">
    /// Wenn die erwartete Struktur nicht gefunden wurde. Der Aufrufer bricht in
    /// diesem Fall ab, statt eine unvollständige Datei auszugeben.
    /// </exception>
    public static byte[] ReplaceXmp(byte[] pdfBytes, byte[] xmp)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(xmp);

        // Latin1 bildet jedes Byte auf genau ein Zeichen ab. Nur so bleiben die
        // Byte-Versätze der Querverweistabelle beim Textvergleich korrekt.
        string text = Encoding.Latin1.GetString(pdfBytes);

        int previousStartXref = ReadStartXref(text);
        string trailer = ReadTrailerDictionary(text);
        int metadataObjectNumber = FindMetadataObjectNumber(text, trailer);
        int size = ReadTrailerInteger(trailer, "Size")
                   ?? throw new InvalidOperationException("Der Trailer enthält keinen /Size-Eintrag.");

        var output = new MemoryStream(pdfBytes.Length + xmp.Length + 512);
        output.Write(pdfBytes);

        // Ein Objekt muss an einer Zeilengrenze beginnen.
        if (pdfBytes.Length > 0 && pdfBytes[^1] is not ((byte)'\n' or (byte)'\r'))
        {
            output.WriteByte((byte)'\n');
        }

        int objectOffset = (int)output.Position;

        WriteAscii(output, string.Create(
            CultureInfo.InvariantCulture,
            $"{metadataObjectNumber} 0 obj\n<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>\nstream\n"));
        output.Write(xmp);
        WriteAscii(output, "\nendstream\nendobj\n");

        int xrefOffset = (int)output.Position;

        // Querverweistabelle mit genau zwei Abschnitten: dem Pflichteintrag
        // für Objekt 0 und dem neu definierten Metadatenobjekt.
        WriteAscii(output, "xref\n0 1\n0000000000 65535 f \n");
        WriteAscii(output, string.Create(CultureInfo.InvariantCulture, $"{metadataObjectNumber} 1\n"));
        WriteAscii(output, string.Create(CultureInfo.InvariantCulture, $"{objectOffset:D10} 00000 n \n"));

        WriteAscii(output, "trailer\n");
        WriteAscii(output, BuildTrailer(trailer, size, previousStartXref));
        WriteAscii(output, string.Create(CultureInfo.InvariantCulture, $"\nstartxref\n{xrefOffset}\n%%EOF\n"));

        return output.ToArray();
    }

    /// <summary>Liest den Versatz der bisherigen Querverweistabelle.</summary>
    private static int ReadStartXref(string text)
    {
        int index = text.LastIndexOf("startxref", StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException("Die Datei enthält kein 'startxref'.");
        }

        Match match = StartXrefValueRegex().Match(text, index);

        return match.Success
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException("Der Wert hinter 'startxref' ist unlesbar.");
    }

    /// <summary>Liest das Trailer-Wörterbuch als Text, einschließlich Klammern.</summary>
    private static string ReadTrailerDictionary(string text)
    {
        int index = text.LastIndexOf("trailer", StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Die Datei enthält keinen klassischen Trailer. "
                + "Querverweisströme werden hier nicht unterstützt.");
        }

        int start = text.IndexOf("<<", index, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("Der Trailer enthält kein Wörterbuch.");
        }

        // Verschachtelte Wörterbücher mitzählen, damit /ID-Arrays und
        // eingebettete Strukturen nicht zu einem zu frühen Ende führen.
        int depth = 0;
        for (int i = start; i < text.Length - 1; i++)
        {
            if (text[i] == '<' && text[i + 1] == '<')
            {
                depth++;
                i++;
            }
            else if (text[i] == '>' && text[i + 1] == '>')
            {
                depth--;
                i++;

                if (depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
        }

        throw new InvalidOperationException("Das Trailer-Wörterbuch ist unvollständig.");
    }

    /// <summary>
    /// Ermittelt die Objektnummer des Metadatenobjekts. Zuerst über den
    /// Katalog, weil das der verbindliche Weg ist; ersatzweise über die Suche
    /// nach einem Objekt mit <c>/Type /Metadata</c>.
    /// </summary>
    private static int FindMetadataObjectNumber(string text, string trailer)
    {
        int? rootNumber = ReadTrailerReference(trailer, "Root");

        if (rootNumber is { } root)
        {
            Match catalogMatch = Regex.Match(
                text,
                $@"(?<![0-9]){root}\s+0\s+obj(?<body>.*?)endobj",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(5));

            if (catalogMatch.Success)
            {
                Match reference = MetadataReferenceRegex().Match(catalogMatch.Groups["body"].Value);

                if (reference.Success)
                {
                    return int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }
        }

        Match direct = MetadataObjectRegex().Match(text);

        return direct.Success
            ? int.Parse(direct.Groups[1].Value, CultureInfo.InvariantCulture)
            : throw new InvalidOperationException(
                "In der erzeugten Datei wurde kein Metadatenobjekt gefunden.");
    }

    /// <summary>
    /// Baut den neuen Trailer: der bisherige Inhalt ohne ein etwaiges
    /// <c>/Prev</c>, ergänzt um den Verweis auf die vorherige Tabelle.
    /// </summary>
    private static string BuildTrailer(string trailer, int size, int previousStartXref)
    {
        string body = trailer.Trim();
        body = body[2..^2].Trim();
        body = PrevEntryRegex().Replace(body, string.Empty);
        body = SizeEntryRegex().Replace(body, string.Empty);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"<< {body.Trim()} /Size {size} /Prev {previousStartXref} >>");
    }

    private static int? ReadTrailerInteger(string trailer, string key)
    {
        Match match = Regex.Match(
            trailer, $@"/{key}\s+(\d+)", RegexOptions.None, TimeSpan.FromSeconds(5));

        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static int? ReadTrailerReference(string trailer, string key)
    {
        Match match = Regex.Match(
            trailer, $@"/{key}\s+(\d+)\s+\d+\s+R", RegexOptions.None, TimeSpan.FromSeconds(5));

        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static void WriteAscii(Stream stream, string value)
        => stream.Write(Encoding.ASCII.GetBytes(value));

    [GeneratedRegex(@"startxref\s+(\d+)", RegexOptions.None, 5000)]
    private static partial Regex StartXrefValueRegex();

    [GeneratedRegex(@"/Metadata\s+(\d+)\s+\d+\s+R", RegexOptions.None, 5000)]
    private static partial Regex MetadataReferenceRegex();

    [GeneratedRegex(@"(\d+)\s+0\s+obj\s*<<[^>]*?/Type\s*/Metadata", RegexOptions.Singleline, 5000)]
    private static partial Regex MetadataObjectRegex();

    [GeneratedRegex(@"/Prev\s+\d+", RegexOptions.None, 5000)]
    private static partial Regex PrevEntryRegex();

    [GeneratedRegex(@"/Size\s+\d+", RegexOptions.None, 5000)]
    private static partial Regex SizeEntryRegex();
}

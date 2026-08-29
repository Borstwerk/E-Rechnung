using System.IO.Compression;
using EInvoiceSender.Core.Services;
using PdfSharp.Pdf;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Entpackt einen eingebetteten Anhang mit einer harten Obergrenze.
/// </summary>
/// <remarks>
/// <para>
/// <b>Das Problem, das diese Klasse löst.</b> PDFsharp bietet nur
/// <c>PdfStream.UnfilteredValue</c>, und das entpackt vollständig in ein
/// <c>byte[]</c> – ohne Obergrenze, ohne Abbruchmöglichkeit. Nachgemessen an
/// der Version 6.2.4: Eine PDF-Datei von 67 KB trägt einen Anhang, der sich zu
/// 64 MiB entfaltet; das Verhältnis lag bei 1:1029, und mehr geht mühelos.
/// <c>PdfStream.Length</c> hilft dabei nicht, denn es meldet die
/// <b>komprimierte</b> Größe – im selben Versuch 65.235 Bytes für 67.108.864
/// Bytes Inhalt. Auch <c>/Params /Size</c> ist keine Hilfe: Der Wert stammt
/// aus der zu prüfenden Datei und ist damit genau die Angabe, der man nicht
/// glauben darf.
/// </para>
/// <para>
/// Eine Grenze <i>nach</i> dem Entpacken kommt zu spät: Der Speicher ist dann
/// bereits belegt, und eine <c>OutOfMemoryException</c> fängt der PDF-Leser
/// bewusst nicht ab. Eine fremde Rechnung könnte die Anwendung damit beenden.
/// </para>
/// <para>
/// <b>Die Lösung.</b> Für <c>/FlateDecode</c> – das Verfahren, das
/// Rechnungsanhänge praktisch ausnahmslos verwenden – wird selbst entpackt,
/// über <see cref="ZLibStream"/> aus der Basisklassenbibliothek. Gelesen wird
/// in Blöcken, und beim Überschreiten der Grenze bricht das Lesen ab. Im
/// Versuch kostete das 65 KB Speicher und 19 ms statt 64 MiB. Keine neue
/// Abhängigkeit.
/// </para>
/// <para>
/// <b>Was hier ausdrücklich nicht abgesichert ist.</b> Der <i>rohe</i>,
/// noch komprimierte Datenstrom liegt bereits im Speicher, sobald PDFsharp die
/// Datei geöffnet hat – darauf hat diese Klasse keinen Einfluss. Der
/// Speicherbedarf bleibt also proportional zur Dateigröße auf der Platte. Was
/// beseitigt ist, ist die Vervielfachung: dass aus einer kleinen Datei ein
/// beliebig großer Speicherbedarf wird. Wer auch die erste Grenze ziehen will,
/// braucht einen PDF-Leser mit strömender Objektverarbeitung – eine
/// Bibliotheksentscheidung, die nicht in diesen Slice gehört.
/// </para>
/// </remarks>
internal static class BoundedEmbeddedFileReader
{
    /// <summary>
    /// Liest den Inhalt eines Anhangs, höchstens jedoch
    /// <paramref name="maxBytes"/> Bytes.
    /// </summary>
    /// <param name="specification">Die Dateibeschreibung (Filespec) des Anhangs.</param>
    /// <param name="maxBytes">Obergrenze für den entpackten Inhalt.</param>
    public static EmbeddedFileReadResult Read(PdfDictionary specification, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(specification);

        PdfDictionary? embeddedFiles = specification.Elements.GetDictionary("/EF");
        PdfDictionary? streamDictionary = embeddedFiles?.Elements.GetDictionary("/F")
                                          ?? embeddedFiles?.Elements.GetDictionary("/UF");

        if (streamDictionary?.Stream is not { } stream)
        {
            // Ein Anhang ohne Datenstrom ist leer, nicht fehlend: Der Eintrag
            // existiert, er trägt nur nichts.
            return new EmbeddedFileReadResult(EmbeddedFileReadStatus.Read, [], null);
        }

        if (!stream.IsFiltered())
        {
            // Unkomprimiert: Die Bytes liegen ohnehin schon so vor, wie sie
            // sind. Hier ist nur die Größe zu prüfen.
            byte[] raw = stream.Value;

            return raw.Length > maxBytes
                ? EmbeddedFileReadResult.Failed(EmbeddedFileReadStatus.TooLarge)
                : new EmbeddedFileReadResult(EmbeddedFileReadStatus.Read, raw, null);
        }

        string filter = Describe(streamDictionary.Elements["/Filter"]);

        if (!IsPlainFlate(streamDictionary))
        {
            // Alles andere – verkettete Filter, LZW, ASCII-Kodierungen, oder
            // ein Prädiktor in /DecodeParms – wird nicht entpackt. Es hier
            // trotzdem über UnfilteredValue zu versuchen hieße, die Grenze
            // genau dort aufzugeben, wo eine Datei sie bewusst umgehen will.
            return EmbeddedFileReadResult.Failed(EmbeddedFileReadStatus.UnsupportedFilter, filter);
        }

        return Inflate(stream.Value, maxBytes, filter);
    }

    /// <summary>
    /// Trifft die Datei genau den Fall, den wir selbst begrenzt entpacken
    /// können: ein einzelner <c>/FlateDecode</c> ohne Dekodierparameter?
    ///
    /// Ein <c>/Predictor</c> in <c>/DecodeParms</c> verlangt eine
    /// Nachbearbeitung, die hier nicht nachgebaut wird. Bei Rechnungsanhängen
    /// kommt er nicht vor; ihn zu vermuten wäre schlimmer als ihn abzulehnen.
    /// </summary>
    private static bool IsPlainFlate(PdfDictionary streamDictionary)
    {
        if (streamDictionary.Elements["/DecodeParms"] is not null
            || streamDictionary.Elements["/DP"] is not null)
        {
            return false;
        }

        return streamDictionary.Elements["/Filter"] switch
        {
            PdfName name => name.Value == "/FlateDecode",

            // Eine Kette mit genau einem Glied ist dasselbe wie ein einzelner
            // Filter; alles darüber lehnen wir ab.
            PdfArray array => array.Elements.Count == 1
                              && array.Elements[0] is PdfName only
                              && only.Value == "/FlateDecode",

            _ => false,
        };
    }

    /// <summary>
    /// Entpackt begrenzt. Bricht ab, sobald die Grenze überschritten ist –
    /// der Rest wird nie angefasst.
    /// </summary>
    private static EmbeddedFileReadResult Inflate(byte[] compressed, int maxBytes, string filter)
    {
        // Nach der PDF-Spezifikation ist /FlateDecode zlib (RFC 1950). Manche
        // Erzeuger lassen den zlib-Kopf weg und schreiben rohes Deflate; das
        // ist verbreitet genug, um es aufzufangen, statt solche Dateien
        // abzulehnen.
        foreach (bool zlib in new[] { true, false })
        {
            try
            {
                return new EmbeddedFileReadResult(
                    EmbeddedFileReadStatus.Read, ReadBounded(compressed, maxBytes, zlib), null);
            }
            catch (BoundExceededException)
            {
                // Hier – und nur hier – ist der Abbruch tatsächlich erfolgt,
                // bevor der Rest entpackt wurde.
                return EmbeddedFileReadResult.Failed(
                    EmbeddedFileReadStatus.TooLarge, filter, stoppedAtLimit: true);
            }
            catch (InvalidDataException)
            {
                // Falscher Rahmen – der zweite Versuch entscheidet.
            }
        }

        return EmbeddedFileReadResult.Failed(EmbeddedFileReadStatus.UnsupportedFilter, filter);
    }

    private static byte[] ReadBounded(byte[] compressed, int maxBytes, bool zlib)
    {
        using var source = new MemoryStream(compressed, writable: false);
        using Stream decompressor = zlib
            ? new ZLibStream(source, CompressionMode.Decompress)
            : new DeflateStream(source, CompressionMode.Decompress);

        // Ein Byte über der Grenze genügt als Nachweis, dass die Grenze
        // überschritten ist. Mehr wird nicht gelesen.
        using var target = new MemoryStream();
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        int read;

        while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;

            if (total > maxBytes)
            {
                throw new BoundExceededException();
            }

            target.Write(buffer, 0, read);
        }

        return target.ToArray();
    }

    /// <summary>Lesbare Fassung der Filterangabe für den Bericht.</summary>
    private static string Describe(PdfItem? filter) => filter switch
    {
        null => "ohne Filterangabe",
        PdfName name => name.Value,
        PdfArray array => string.Join(
            " + ",
            Enumerable.Range(0, array.Elements.Count)
                      .Select(i => array.Elements[i]?.ToString() ?? "?")),
        _ => filter.ToString() ?? "unbekannt",
    };

    /// <summary>
    /// Rein internes Abbruchsignal. Es verlässt diese Klasse nie – nach außen
    /// gibt es nur <see cref="EmbeddedFileReadStatus.TooLarge"/>.
    /// </summary>
    private sealed class BoundExceededException : Exception;
}

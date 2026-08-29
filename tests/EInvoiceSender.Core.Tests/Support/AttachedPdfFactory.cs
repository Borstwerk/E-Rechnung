using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Baut PDF-Dateien mit beliebig vielen benannten Anhängen.
///
/// **Warum eine eigene Fabrik.** Der Erzeugungsweg der Anwendung bettet genau
/// einen Anhang ein, und zwar den richtigen – für die Prüfung fremder Dateien
/// werden aber gerade die schiefen Fälle gebraucht: zwei Rechnungsanhänge,
/// ein Anhang mit fremdem Format, ein leerer Anhang. Solche Dateien lassen
/// sich mit dem Erzeugungsweg gar nicht herstellen.
///
/// Die Struktur folgt <c>PdfAInvoiceParts.AttachInvoiceXml</c>: Namensbaum
/// <c>/Names /EmbeddedFiles</c> und Dokumentfeld <c>/AF</c> zusammen, weil
/// eine echte Hybridrechnung genau so aussieht.
/// </summary>
public static class AttachedPdfFactory
{
    /// <summary>Ein Anhang, wie er in die Testdatei geschrieben wird.</summary>
    /// <param name="FileName">Name des Anhangs.</param>
    /// <param name="Content">Inhalt des Anhangs.</param>
    /// <param name="Compress">
    /// Den Anhang mit <c>/FlateDecode</c> packen. PDFsharp speichert Anhänge
    /// von sich aus unkomprimiert; fremde Erzeuger tun das Gegenteil, und nur
    /// mit Kompression lässt sich der gefährliche Fall nachstellen – eine
    /// kleine Datei, deren Anhang sich um ein Vielfaches entfaltet.
    /// </param>
    /// <param name="WithDecodeParms">
    /// Zusätzlich <c>/DecodeParms</c> mit einem Prädiktor eintragen. Ein
    /// solcher Anhang lässt sich nicht mit Bordmitteln begrenzt entpacken; der
    /// Prüfmodus muss ihn ablehnen, statt zu raten.
    /// </param>
    public sealed record Attachment(
        string FileName, byte[] Content, bool Compress = false, bool WithDecodeParms = false);

    /// <summary>
    /// Erzeugt eine einseitige PDF mit den angegebenen Anhängen.
    /// </summary>
    public static byte[] Create(params Attachment[] attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);

        using var document = new PdfDocument();
        document.Info.Title = "Prüfrechnung";

        PdfPage page = document.AddPage();
        page.Width = XUnit.FromMillimeter(210);
        page.Height = XUnit.FromMillimeter(297);

        using (XGraphics gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(40, 40, 300, 60));
        }

        if (attachments.Length > 0)
        {
            Attach(document, attachments);
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);

        return stream.ToArray();
    }

    /// <summary>Schreibt eine PDF mit Anhängen in eine Datei.</summary>
    public static string WriteTo(string directory, string fileName, params Attachment[] attachments)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, Create(attachments));

        return path;
    }

    private static void Attach(PdfDocument document, IReadOnlyList<Attachment> attachments)
    {
        PdfDictionary catalog = document.Internals.Catalog;
        var namesArray = new PdfArray(document);
        var associatedFiles = new PdfArray(document);

        foreach (Attachment attachment in attachments)
        {
            var embeddedStream = new PdfDictionary(document);
            embeddedStream.Elements["/Type"] = new PdfName("/EmbeddedFile");
            embeddedStream.Elements["/Subtype"] = new PdfName("/text/xml");
            embeddedStream.CreateStream(attachment.Content);

            if (attachment.Compress)
            {
                embeddedStream.Stream!.Zip();
            }

            if (attachment.WithDecodeParms)
            {
                var decodeParms = new PdfDictionary(document);
                decodeParms.Elements["/Predictor"] = new PdfInteger(12);
                decodeParms.Elements["/Columns"] = new PdfInteger(4);
                embeddedStream.Elements["/DecodeParms"] = decodeParms;
            }

            document.Internals.AddObject(embeddedStream);

            var specification = new PdfDictionary(document);
            specification.Elements["/Type"] = new PdfName("/Filespec");
            specification.Elements["/F"] = new PdfString(attachment.FileName);
            specification.Elements["/UF"] = new PdfString(attachment.FileName);
            specification.Elements["/AFRelationship"] = new PdfName("/Alternative");

            var reference = new PdfDictionary(document);
            reference.Elements["/F"] = embeddedStream.Reference!;
            reference.Elements["/UF"] = embeddedStream.Reference!;
            specification.Elements["/EF"] = reference;

            document.Internals.AddObject(specification);

            namesArray.Elements.Add(new PdfString(attachment.FileName));
            namesArray.Elements.Add(specification.Reference!);
            associatedFiles.Elements.Add(specification.Reference!);
        }

        var nameTree = new PdfDictionary(document);
        nameTree.Elements["/Names"] = namesArray;

        var namesDictionary = catalog.Elements.GetDictionary("/Names") ?? new PdfDictionary(document);
        namesDictionary.Elements["/EmbeddedFiles"] = nameTree;
        catalog.Elements["/Names"] = namesDictionary;
        catalog.Elements["/AF"] = associatedFiles;
    }
}

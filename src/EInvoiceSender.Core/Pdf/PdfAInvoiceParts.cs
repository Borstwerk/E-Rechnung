using EInvoiceSender.Core.Services;
using PdfSharp.Pdf;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Die Bestandteile, die aus einem PDF-Dokument eine PDF/A-3b-Hybridrechnung
/// machen: Dokumentinformationen, Rechnungs-XML als zugeordneter Anhang,
/// sRGB-OutputIntent und das XMP-Paket.
///
/// **Warum eine eigene Klasse:** Es gibt zwei Wege zu einem fertigen
/// Dokument – die Aufwertung eines geeigneten Originals
/// (<see cref="PdfAInvoiceComposer"/>) und, falls das Original dafür nicht
/// taugt, ein neu gebautes Dokument aus gerenderten Seiten
/// (<see cref="RasterizedPdfBuilder"/>). Beide Wege unterscheiden sich
/// ausschließlich darin, woher die Seiten kommen. Alles, was das Ergebnis zu
/// einer normgerechten Datei macht, ist für beide dasselbe und steht deshalb
/// genau einmal hier.
///
/// Eine zweite Fassung dieser Bausteine wäre die gefährlichste Art von
/// Wiederholung: Sie würde erst auffallen, wenn eine der beiden Fassungen
/// still von der Norm abweicht.
/// </summary>
internal static class PdfAInvoiceParts
{
    /// <summary>Programmkennung für die PDF-Metadaten.</summary>
    public const string ProducerName = "EInvoiceSender";

    /// <summary>
    /// Ergänzt ein fertig bestücktes Dokument um alle PDF/A-3b- und
    /// ZUGFeRD-Bestandteile und liefert die fertigen Bytes.
    ///
    /// Das Dokument bringt nur die Seiten mit; woher diese stammen, ist hier
    /// ohne Bedeutung.
    /// </summary>
    public static byte[] Finish(PdfDocument document, PdfACompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);

        // PDF/A-3 setzt mindestens PDF 1.7 voraus.
        if (document.Version < 17)
        {
            document.Version = 17;
        }

        WriteDocumentInformation(document, request);
        AttachInvoiceXml(document, request.InvoiceXml, request.Attachment);
        AddOutputIntent(document);

        using var target = new MemoryStream();
        document.Save(target, closeStream: false);

        // PDFsharp schreibt beim Speichern immer sein eigenes XMP. Deshalb wird
        // das richtige Paket erst danach eingesetzt – siehe PdfMetadataOverwriter.
        byte[] xmp = XmpMetadataBuilder.Build(
            title: request.Title,
            author: request.Author,
            subject: request.Subject,
            producer: ProducerName,
            creationDate: request.CreationDate,
            embeddedFileName: request.Attachment.FileName);

        return PdfMetadataOverwriter.ReplaceXmp(target.ToArray(), xmp);
    }

    /// <summary>
    /// Setzt die Dokumentinformationen. PDF/A verlangt, dass sie mit den
    /// XMP-Angaben übereinstimmen – deshalb stammen beide aus derselben Quelle.
    /// </summary>
    private static void WriteDocumentInformation(PdfDocument document, PdfACompositionRequest request)
    {
        document.Info.Title = request.Title;
        document.Info.Author = request.Author;
        document.Info.Subject = request.Subject;
        document.Info.Creator = ProducerName;

        // /Producer ist in PdfSharp schreibgeschützt, muss aber mit der
        // XMP-Angabe übereinstimmen – PDF/A verlangt das. Deshalb direkt im
        // Info-Wörterbuch setzen.
        document.Info.Elements["/Producer"] = new PdfString(ProducerName);
        document.Info.CreationDate = request.CreationDate.UtcDateTime;
        document.Info.ModificationDate = request.CreationDate.UtcDateTime;
    }

    /// <summary>
    /// Bettet die Rechnungs-XML als Dateianhang ein und verknüpft sie über
    /// <c>/AF</c> mit dem Dokument.
    ///
    /// Beides ist nötig: Der Namensbaum <c>/Names /EmbeddedFiles</c> macht den
    /// Anhang für Betrachter sichtbar, das Feld <c>/AF</c> mit
    /// <c>/AFRelationship /Alternative</c> macht ihn für eine
    /// Rechnungsverarbeitung als zugehörige Datei erkennbar. Fehlt <c>/AF</c>,
    /// ist die Datei keine gültige Hybridrechnung.
    /// </summary>
    private static void AttachInvoiceXml(
        PdfDocument document,
        byte[] invoiceXml,
        InvoiceAttachmentDescriptor attachment)
    {
        // Der eigentliche Datenstrom des Anhangs.
        var embeddedStream = new PdfDictionary(document);
        embeddedStream.Elements["/Type"] = new PdfName("/EmbeddedFile");

        // Den MIME-Typ unverändert übergeben: PDFsharp maskiert Sonderzeichen
        // beim Schreiben selbst ("text/xml" wird zu "/text#2Fxml"). Eine eigene
        // Maskierung würde ein zweites Mal maskiert und ergäbe den ungültigen
        // Namen "/text#232Fxml" – von veraPDF als Verstoß gegen
        // ISO 19005-3, Abschnitt 6.8 beanstandet.
        embeddedStream.Elements["/Subtype"] = new PdfName("/" + attachment.MimeType);
        embeddedStream.CreateStream(invoiceXml);

        var parameters = new PdfDictionary(document);
        parameters.Elements["/Size"] = new PdfInteger(invoiceXml.Length);
        parameters.Elements["/ModDate"] = new PdfString(FormatPdfDate(DateTimeOffset.UtcNow));
        embeddedStream.Elements["/Params"] = parameters;

        document.Internals.AddObject(embeddedStream);

        // Die Dateibeschreibung (Filespec).
        var fileSpecification = new PdfDictionary(document);
        fileSpecification.Elements["/Type"] = new PdfName("/Filespec");
        fileSpecification.Elements["/F"] = new PdfString(attachment.FileName);
        fileSpecification.Elements["/UF"] = new PdfString(attachment.FileName);
        fileSpecification.Elements["/Desc"] = new PdfString(attachment.Description);
        fileSpecification.Elements["/AFRelationship"] = new PdfName("/" + attachment.Relationship);

        var embeddedFileReference = new PdfDictionary(document);
        embeddedFileReference.Elements["/F"] = embeddedStream.Reference!;
        embeddedFileReference.Elements["/UF"] = embeddedStream.Reference!;
        fileSpecification.Elements["/EF"] = embeddedFileReference;

        document.Internals.AddObject(fileSpecification);

        // Eintrag im Namensbaum /Names /EmbeddedFiles.
        PdfDictionary catalog = document.Internals.Catalog;

        var namesArray = new PdfArray(document);
        namesArray.Elements.Add(new PdfString(attachment.FileName));
        namesArray.Elements.Add(fileSpecification.Reference!);

        var embeddedFilesNameTree = new PdfDictionary(document);
        embeddedFilesNameTree.Elements["/Names"] = namesArray;

        var namesDictionary = catalog.Elements.GetDictionary("/Names") ?? new PdfDictionary(document);
        namesDictionary.Elements["/EmbeddedFiles"] = embeddedFilesNameTree;
        catalog.Elements["/Names"] = namesDictionary;

        // Zugeordnete Datei auf Dokumentebene (/AF).
        var associatedFiles = new PdfArray(document);
        associatedFiles.Elements.Add(fileSpecification.Reference!);
        catalog.Elements["/AF"] = associatedFiles;
    }

    /// <summary>
    /// Ergänzt den OutputIntent mit eingebettetem sRGB-Profil.
    /// Ohne ihn ist kein Dokument PDF/A-konform, weil die Farbwiedergabe sonst
    /// nicht eindeutig definiert wäre.
    /// </summary>
    private static void AddOutputIntent(PdfDocument document)
    {
        PdfDictionary catalog = document.Internals.Catalog;

        // Ein bereits vorhandener OutputIntent wird ersetzt: Wir kennen nur
        // für unser eigenes Profil die Zusicherung, dass es gültig ist.
        byte[] iccProfile = SRgbIccProfile.GetBytes();

        var profileStream = new PdfDictionary(document);
        profileStream.Elements["/N"] = new PdfInteger(SRgbIccProfile.ComponentCount);
        profileStream.CreateStream(iccProfile);
        document.Internals.AddObject(profileStream);

        var outputIntent = new PdfDictionary(document);
        outputIntent.Elements["/Type"] = new PdfName("/OutputIntent");
        outputIntent.Elements["/S"] = new PdfName("/GTS_PDFA1");
        outputIntent.Elements["/OutputCondition"] = new PdfString(SRgbIccProfile.ProfileDescription);
        outputIntent.Elements["/OutputConditionIdentifier"] =
            new PdfString(SRgbIccProfile.OutputConditionIdentifier);
        outputIntent.Elements["/Info"] = new PdfString(SRgbIccProfile.ProfileDescription);
        outputIntent.Elements["/DestOutputProfile"] = profileStream.Reference!;
        document.Internals.AddObject(outputIntent);

        var outputIntents = new PdfArray(document);
        outputIntents.Elements.Add(outputIntent.Reference!);
        catalog.Elements["/OutputIntents"] = outputIntents;
    }

    /// <summary>Formatiert einen Zeitpunkt in der PDF-Datumsschreibweise.</summary>
    private static string FormatPdfDate(DateTimeOffset value)
        => value.UtcDateTime.ToString(
            @"\D\:yyyyMMddHHmmss\Z", System.Globalization.CultureInfo.InvariantCulture);
}

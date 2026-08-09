using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Erzeugt aus einer vorhandenen PDF und der Rechnungs-XML eine
/// PDF/A-3b-Datei mit eingebetteter XML.
///
/// Wichtig zum Verständnis dessen, was hier geschieht – und was nicht:
///
/// Diese Klasse **konvertiert kein beliebiges PDF nach PDF/A**. Es gibt keine
/// permissiv lizenzierte .NET-Bibliothek, die Schriften nachträglich einbettet,
/// Farbräume normalisiert oder Transparenz auflöst (ADR-0003). Was hier
/// geschieht, ist eine **Aufwertung**: Ein bereits geeignetes PDF erhält die
/// fehlenden PDF/A-3-Bestandteile.
///
/// Ist das Eingangsdokument nicht geeignet, bricht der Vorgang ab und liefert
/// eine verständliche Begründung. Es wird niemals eine Datei ausgegeben, die
/// nur so aussieht, als wäre sie konform.
///
/// Die Originaldatei wird ausschließlich gelesen und nie verändert.
/// </summary>
public sealed partial class PdfAInvoiceComposer : IPdfAInvoiceComposer
{
    private readonly IPdfAnalyzer _analyzer;
    private readonly ILogger<PdfAInvoiceComposer> _logger;

    /// <summary>Programmkennung für die PDF-Metadaten.</summary>
    public const string ProducerName = "EInvoiceSender";

    public PdfAInvoiceComposer(IPdfAnalyzer analyzer, ILogger<PdfAInvoiceComposer> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CompositionResult> ComposeAsync(
        PdfACompositionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = new ValidationReportBuilder();

        PdfAnalysisResult analysis = await _analyzer
            .AnalyzeAsync(request.SourcePdfPath, cancellationToken)
            .ConfigureAwait(false);

        if (!analysis.CanBeUpgraded)
        {
            foreach (PdfUpgradeBlocker blocker in analysis.UpgradeBlockers)
            {
                report.Add(DescribeBlocker(blocker));
            }

            LogUpgradeRejected(
                _logger,
                analysis.UpgradeBlockers.Count,
                string.Join(", ", analysis.UpgradeBlockers));

            return CompositionResult.Failed(report.Build());
        }

        if (analysis.HasExistingInvoiceXml)
        {
            report.Warning(
                "APP-PDF-020",
                "Die gewählte PDF-Datei enthält bereits eine Rechnungs-XML. "
                + "Diese wird durch die neu erzeugte ersetzt.",
                technicalDetail: $"Bisheriges Profil: {analysis.ExistingInvoiceProfile ?? "unbekannt"}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            byte[] result = Upgrade(request, analysis);

            LogComposed(_logger, analysis.PageCount, result.Length);

            return CompositionResult.Success(result, report.Build());
        }
        catch (PdfReaderException ex)
        {
            report.Error(
                "APP-PDF-030",
                "Die PDF-Datei konnte nicht verarbeitet werden. Sie ist vermutlich beschädigt.",
                technicalDetail: ex.Message);

            return CompositionResult.Failed(report.Build());
        }
        catch (InvalidOperationException ex)
        {
            report.Error(
                "APP-PDF-031",
                "Die PDF-Datei konnte nicht in eine PDF/A-3-Datei umgewandelt werden.",
                technicalDetail: ex.Message);

            return CompositionResult.Failed(report.Build());
        }
    }

    private static byte[] Upgrade(PdfACompositionRequest request, PdfAnalysisResult analysis)
    {
        // Das Original wird nur gelesen. Der Datenstrom wird bewusst separat
        // geöffnet und nach dem Einlesen sofort geschlossen, damit die Datei
        // nicht länger als nötig gesperrt ist.
        using var source = new FileStream(
            request.SourcePdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PdfDocument document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);

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

        _ = analysis;

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

    [LoggerMessage(
        EventId = 2001, Level = LogLevel.Warning,
        Message = "PDF/A-3-Aufwertung abgelehnt, {Count} Hindernis(se): {Blockers}")]
    private static partial void LogUpgradeRejected(ILogger logger, int count, string blockers);

    [LoggerMessage(
        EventId = 2002, Level = LogLevel.Information,
        Message = "PDF/A-3b erzeugt: {Pages} Seite(n), {Size} Bytes")]
    private static partial void LogComposed(ILogger logger, int pages, int size);

    /// <summary>Formatiert einen Zeitpunkt in der PDF-Datumsschreibweise.</summary>
    private static string FormatPdfDate(DateTimeOffset value)
        => value.UtcDateTime.ToString(
            @"\D\:yyyyMMddHHmmss\Z", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Übersetzt ein technisches Hindernis in eine Erklärung, mit der ein
    /// Anwender etwas anfangen kann – samt Hinweis, was er tun kann.
    /// </summary>
    private static ValidationFinding DescribeBlocker(PdfUpgradeBlocker blocker) => blocker switch
    {
        PdfUpgradeBlocker.Encrypted => ValidationFinding.Error(
            "APP-PDF-001",
            "Die PDF-Datei ist verschlüsselt oder mit einem Kennwort geschützt. "
            + "Bitte speichern Sie die Rechnung ohne Schutz und wählen Sie sie erneut aus.",
            technicalDetail: "Verschlüsselte Dokumente sind nach PDF/A nicht zulässig."),

        PdfUpgradeBlocker.FontsNotEmbedded => ValidationFinding.Error(
            "APP-PDF-002",
            "In der PDF-Datei sind nicht alle Schriftarten eingebettet. "
            + "Eine normgerechte E-Rechnung setzt das voraus. Bitte stellen Sie in Ihrem "
            + "Rechnungsprogramm beim PDF-Export die Option \"Schriftarten einbetten\" ein "
            + "oder exportieren Sie direkt als PDF/A.",
            technicalDetail: "Mindestens ein Font-Objekt hat keinen FontFile-Eintrag im FontDescriptor."),

        PdfUpgradeBlocker.ActiveContent => ValidationFinding.Error(
            "APP-PDF-003",
            "Die PDF-Datei enthält aktive Inhalte wie JavaScript oder automatisch "
            + "startende Aktionen. Solche Inhalte sind in einer E-Rechnung nicht zulässig.",
            technicalDetail: "JavaScript, /OpenAction oder /Launch im Dokument gefunden."),

        PdfUpgradeBlocker.Damaged => ValidationFinding.Error(
            "APP-PDF-004",
            "Die PDF-Datei konnte nicht vollständig gelesen werden. "
            + "Sie ist vermutlich beschädigt. Bitte erzeugen Sie die Rechnung neu.",
            technicalDetail: "Der PDF-Parser konnte die Struktur nicht auflösen."),

        PdfUpgradeBlocker.DigitallySigned => ValidationFinding.Error(
            "APP-PDF-005",
            "Die PDF-Datei ist digital signiert. Durch das Einbetten der Rechnungsdaten "
            + "würde die Signatur ungültig. Bitte verwenden Sie die Fassung ohne Signatur.",
            technicalDetail: "/Sig-Feld im AcroForm des Dokuments gefunden."),

        _ => ValidationFinding.Error(
            "APP-PDF-009",
            "Die PDF-Datei kann nicht in eine normgerechte E-Rechnung umgewandelt werden."),
    };
}

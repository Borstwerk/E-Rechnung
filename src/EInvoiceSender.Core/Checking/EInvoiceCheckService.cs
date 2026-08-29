using System.Globalization;
using System.Security.Cryptography;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Checking;

/// <summary>
/// Nimmt auf, was in einer fertigen E-Rechnung steht.
/// </summary>
/// <remarks>
/// <para>
/// <b>Was dieser Dienst ausdrücklich nicht tut.</b> Er beurteilt die Rechnung
/// nicht. Er führt weder die EN-16931-Regelprüfung noch veraPDF aus, und er
/// vergleicht das sichtbare PDF nicht mit der eingebetteten XML. Kein Befund
/// aus diesem Dienst darf deshalb „normkonform“, „gültige E-Rechnung“ oder
/// „PDF/A-konform“ behaupten.
/// </para>
/// <para>
/// <b>Warum nicht die vorhandenen Prüfer.</b> Zwei liegen nahe und sind beide
/// falsch an dieser Stelle:
/// </para>
/// <list type="bullet">
///   <item><c>PdfPreflightService</c> beantwortet „kann BorstWerk diese Datei
///   verändern?“. Eine digital signierte Rechnung ist dort ein Hindernis –
///   völlig zu Recht, denn das Einbetten bräche die Signatur. Für eine bereits
///   fertige Datei ist dieselbe Signatur kein Mangel, sondern eher ein Zeichen
///   von Sorgfalt. Die Hindernisse eins zu eins als Prüfbefunde zu übernehmen
///   hieße, dem Anwender die Grenzen unseres Schreibwegs als Mängel seiner
///   Rechnung zu verkaufen.</item>
///   <item><c>En16931RuleValidator</c> prüft das Domänenmodell während der
///   Erstellung und enthält bewusste BorstWerk-Produktgrenzen – etwa die
///   kuratierten Codelisten. Ein Code außerhalb unserer Auswahl ist dort ein
///   Befund; bei einer fremden Rechnung wäre er eine Falschbeschuldigung.</item>
/// </list>
/// </remarks>
public sealed partial class EInvoiceCheckService : IEInvoiceCheckService
{
    private readonly IPdfAnalyzer _analyzer;
    private readonly IPdfAttachmentReader _attachments;
    private readonly ICiiInvoiceInspector _inspector;
    private readonly ILogger<EInvoiceCheckService> _logger;

    public EInvoiceCheckService(
        IPdfAnalyzer analyzer,
        IPdfAttachmentReader attachments,
        ICiiInvoiceInspector inspector,
        ILogger<EInvoiceCheckService> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CheckEInvoiceResult> CheckAsync(
        CheckEInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var report = new ValidationReportBuilder();
        string fileName = Path.GetFileName(request.SourcePath);

        try
        {
            return await RunAsync(request, fileName, report, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new CheckEInvoiceResult(
                Completed: false, Canceled: true, fileName, 0, null, null, null, report.Build());
        }
    }

    private async Task<CheckEInvoiceResult> RunAsync(
        CheckEInvoiceRequest request,
        string fileName,
        ValidationReportBuilder report,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(request.SourcePath);

        if (!file.Exists)
        {
            report.Error(
                CheckRuleIds.SourceMissing,
                "Die Datei wurde nicht gefunden. Bitte wählen Sie sie erneut aus.",
                "SourcePath",
                request.SourcePath);

            return Aborted(fileName, 0, null, report);
        }

        long size = file.Length;

        if (!await _analyzer.LooksLikePdfAsync(request.SourcePath, cancellationToken).ConfigureAwait(false))
        {
            report.Error(
                CheckRuleIds.NotAPdf,
                "Die Datei ist keine PDF-Datei. Geprüft werden können nur PDF-Rechnungen mit "
                + "eingebetteten Rechnungsdaten.",
                "SourcePath",
                "Die Datei beginnt nicht mit der Kennung %PDF-.");

            return Aborted(fileName, size, null, report);
        }

        // Die Prüfsumme steht vor der Auswertung: Sie hält fest, worauf sich
        // der Bericht bezieht, und macht später nachprüfbar, dass die Datei
        // dabei unverändert geblieben ist.
        string sha256 = await ComputeSha256Async(request.SourcePath, cancellationToken).ConfigureAwait(false);

        PdfAnalysisResult analysis =
            await _analyzer.AnalyzeAsync(request.SourcePath, cancellationToken).ConfigureAwait(false);

        if (analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.Encrypted))
        {
            report.Error(
                CheckRuleIds.PasswordProtected,
                "Die Datei ist kennwortgeschützt und lässt sich nicht öffnen. Ohne das Kennwort "
                + "kann sie nicht geprüft werden.",
                "SourcePath");

            return Aborted(fileName, size, sha256, report);
        }

        if (analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.Damaged))
        {
            report.Error(
                CheckRuleIds.PdfDamaged,
                "Die PDF-Datei ist beschädigt und konnte nicht gelesen werden.",
                "SourcePath");

            return Aborted(fileName, size, sha256, report);
        }

        // Ab hier ist die Datei lesbar. Was jetzt noch kommt, sind
        // Feststellungen über ihren Inhalt.
        ReportDocumentObservations(analysis, report);

        // **Zuerst die Namen, dann erst ein Inhalt.** Die Metadaten stehen
        // schon in der Analyse und kosten nichts. Jeder entpackte Anhang
        // dagegen kostet Speicher in der Größe, die die fremde Datei bestimmt
        // – gemessen 64 MiB aus einer PDF von 67 KB. Anhänge zu entpacken, die
        // für die Prüfung ohnehin keine Rolle spielen, wäre dieser Preis für
        // nichts.
        IReadOnlyList<CheckedAttachment> invoiceLike =
            CheckedAttachmentNames.SelectInvoiceLike(analysis.EmbeddedFiles);

        if (SelectAttachment(invoiceLike, report) is not { } attachment)
        {
            return Aborted(fileName, size, sha256, report, Describe(analysis, null, null, null));
        }

        // Ab hier steht fest: genau ein auswertbarer Rechnungsanhang. Erst
        // jetzt wird entpackt, und zwar nur bis zur Grenze von SecureXml.
        EmbeddedFileReadResult content = await _attachments
            .ReadEmbeddedFileAsync(
                request.SourcePath, attachment.File.FileName,
                SecureXml.MaxXmlSizeInBytes, cancellationToken)
            .ConfigureAwait(false);

        if (content.Status != EmbeddedFileReadStatus.Read)
        {
            ReportContentFailure(content, attachment, report);

            return new CheckEInvoiceResult(
                Completed: false, Canceled: false, fileName, size, sha256,
                Describe(analysis, attachment, null, null), null, report.Build());
        }

        CiiInspection inspection = _inspector.Inspect(content.Content);
        CheckedDocumentInfo info = Describe(
            analysis, attachment, content.Content.Length, inspection.Summary?.ProfileId);

        if (inspection.Status != CiiStructureStatus.Cii)
        {
            ReportStructureFailure(inspection.Status, attachment, content.Content.Length, report);

            return new CheckEInvoiceResult(
                Completed: false, Canceled: false, fileName, size, sha256, info, null, report.Build());
        }

        ReportProfile(inspection.Summary!, report);

        string attachmentKind = CheckedAttachmentNames.Describe(attachment.Kind);
        LogChecked(_logger, attachmentKind, content.Content.Length);

        return new CheckEInvoiceResult(
            Completed: true,
            Canceled: false,
            fileName,
            size,
            sha256,
            info,
            inspection.Summary,
            report.Build());
    }

    /// <summary>
    /// Meldet, warum der Inhalt des Rechnungsanhangs gar nicht erst entpackt
    /// wurde.
    /// </summary>
    private static void ReportContentFailure(
        EmbeddedFileReadResult content, CheckedAttachment attachment, ValidationReportBuilder report)
    {
        switch (content.Status)
        {
            case EmbeddedFileReadStatus.TooLarge:
                report.Error(
                    CheckRuleIds.InvoiceXmlTooLarge,
                    "Der Rechnungsanhang ist zu groß und wurde aus Sicherheitsgründen nicht "
                    + "verarbeitet.",
                    "InvoiceXml",
                    $"Anhang {attachment.File.FileName}. Grenze: {SecureXml.MaxXmlSizeInBytes} Bytes "
                    + "im entpackten Zustand; "
                    + (content.StoppedAtLimit
                        ? "das Entpacken wurde an der Grenze abgebrochen."
                        : "die Größe wurde nach dem Entpacken festgestellt."));
                break;

            case EmbeddedFileReadStatus.UnsupportedFilter:
                report.Error(
                    CheckRuleIds.InvoiceXmlUnsupportedFilter,
                    "Der Rechnungsanhang ist mit einem Verfahren gepackt, das diese Anwendung "
                    + "nicht sicher begrenzt entpacken kann. Er wurde deshalb nicht verarbeitet.",
                    "InvoiceXml",
                    $"Anhang {attachment.File.FileName}, Filter: {content.FilterDescription ?? "unbekannt"}.");
                break;

            default:
                report.Error(
                    CheckRuleIds.InvoiceXmlEmpty,
                    "Der Rechnungsanhang ließ sich nicht lesen. Die Datei nennt ihn, liefert "
                    + "aber keinen Inhalt.",
                    "InvoiceXml",
                    $"Anhang {attachment.File.FileName}.");
                break;
        }
    }

    /// <summary>
    /// Wählt den auszuwertenden Anhang – oder stellt fest, dass es keinen
    /// eindeutigen gibt.
    ///
    /// **Der Fall mit mehreren Anhängen ist der heikle.** Bei zwei
    /// rechnungsartigen Anhängen einfach den ersten zu nehmen wäre eine
    /// willkürliche Wahl mit einem sehr echten Ergebnis: Der Anwender bekäme
    /// einen ordentlich aussehenden Prüfbericht über eine Datei, die er nicht
    /// gemeint hat. Lieber gar kein Bericht als der falsche.
    /// </summary>
    private static CheckedAttachment? SelectAttachment(
        IReadOnlyList<CheckedAttachment> invoiceLike, ValidationReportBuilder report)
    {
        if (invoiceLike.Count == 0)
        {
            report.Error(
                CheckRuleIds.NoInvoiceAttachment,
                "Die PDF-Datei enthält keine strukturierten Rechnungsdaten. Sie ist damit keine "
                + "E-Rechnung, sondern eine gewöhnliche PDF-Rechnung.",
                "EmbeddedFiles",
                $"Gesucht wurde nach: {string.Join(", ", CheckedAttachmentNames.SupportedFileNames)}.");

            return null;
        }

        if (invoiceLike.Count > 1)
        {
            string found = string.Join(
                ", ", invoiceLike.Select(a => $"{a.File.FileName} ({CheckedAttachmentNames.Describe(a.Kind)})"));

            report.Error(
                CheckRuleIds.AmbiguousInvoiceAttachments,
                "Die PDF-Datei enthält mehrere Anhänge mit Rechnungsdaten. Welcher davon gilt, "
                + "lässt sich nicht entscheiden – die Prüfung wurde deshalb abgebrochen.",
                "EmbeddedFiles",
                $"Gefunden: {found}.");

            return null;
        }

        CheckedAttachment only = invoiceLike[0];

        if (!only.IsSupported)
        {
            // Ausdrücklich **nicht** „keine Rechnungsdaten gefunden“: Es sind
            // welche da, sie werden nur von dieser Fassung nicht ausgewertet.
            // Der Unterschied entscheidet darüber, ob der Anwender seine Datei
            // für kaputt hält oder für schlicht anders.
            report.Error(
                CheckRuleIds.UnsupportedInvoiceFormat,
                $"Die PDF-Datei enthält strukturierte Rechnungsdaten im Format "
                + $"{CheckedAttachmentNames.Describe(only.Kind)}. Dieses Format wird derzeit nicht "
                + "geprüft; geprüft werden ZUGFeRD- und Factur-X-Rechnungen.",
                "EmbeddedFiles",
                $"Anhang {only.File.FileName}; sein Inhalt wurde nicht entpackt.");

            return null;
        }

        return only;
    }

    /// <summary>
    /// Meldet, warum sich die eingebettete XML nicht auswerten ließ.
    ///
    /// Jeder Fall bekommt eine eigene Kennung. „Ging nicht“ ist für einen
    /// Anwender, der wissen will, was er als Nächstes tun soll, wertlos.
    /// </summary>
    private static void ReportStructureFailure(
        CiiStructureStatus status,
        CheckedAttachment attachment,
        int contentLength,
        ValidationReportBuilder report)
    {
        string field = "InvoiceXml";
        string where = $"Anhang {attachment.File.FileName}, {contentLength} Bytes.";

        switch (status)
        {
            case CiiStructureStatus.Empty:
                report.Error(
                    CheckRuleIds.InvoiceXmlEmpty,
                    "Der Rechnungsanhang ist leer. Die Datei trägt zwar einen Anhang mit dem "
                    + "erwarteten Namen, aber ohne Inhalt.",
                    field, where);
                break;

            case CiiStructureStatus.TooLarge:
                // Zweite Linie. Erreichbar ist sie nur, wenn der begrenzte
                // Leser umgangen wurde – die Grenze greift normalerweise
                // schon dort. Trotzdem hier die zutreffende Meldung und nicht
                // die allgemeine: Ein stiller Fehlgriff auf "keine
                // CII-Struktur" verschöbe die Ursache.
                report.Error(
                    CheckRuleIds.InvoiceXmlTooLarge,
                    "Der Rechnungsanhang ist zu groß und wurde aus Sicherheitsgründen nicht "
                    + "verarbeitet.",
                    field,
                    $"{where} Grenze: {SecureXml.MaxXmlSizeInBytes} Bytes; erkannt beim Einlesen "
                    + "der XML.");
                break;

            case CiiStructureStatus.NotWellFormed:
                report.Error(
                    CheckRuleIds.InvoiceXmlNotWellFormed,
                    "Der Rechnungsanhang ist keine lesbare XML-Datei. Er ist entweder beschädigt "
                    + "oder verwendet Mittel, die aus Sicherheitsgründen abgelehnt werden.",
                    field, where);
                break;

            default:
                report.Error(
                    CheckRuleIds.InvoiceXmlNotCii,
                    "Der Rechnungsanhang ist zwar eine lesbare XML-Datei, aber keine "
                    + "Cross-Industry-Invoice, die diese Anwendung auswerten kann.",
                    field, where);
                break;
        }
    }

    /// <summary>
    /// Feststellungen über das Dokument selbst. Alles hier ist ein Hinweis,
    /// nichts ist ein Mangel.
    /// </summary>
    private static void ReportDocumentObservations(
        PdfAnalysisResult analysis, ValidationReportBuilder report)
    {
        if (analysis.DeclaredPdfAPart is { Length: > 0 } part)
        {
            string level = analysis.DeclaredPdfAConformance is { Length: > 0 } conformance
                ? $"PDF/A-{part}{conformance.ToUpperInvariant()}"
                : $"PDF/A-{part}";

            // Sorgfältig formuliert: Hier steht, was die Datei über sich
            // behauptet. Ob sie es einhält, entscheidet veraPDF, und der ist
            // nicht gelaufen.
            report.Information(
                CheckRuleIds.PdfADeclarationFound,
                $"Die Datei ist als {level} gekennzeichnet. Das ist die Angabe der Datei über sich "
                + "selbst; ob sie den Standard tatsächlich einhält, wurde hier nicht geprüft.",
                "PdfA",
                $"XMP: pdfaid:part={part}, pdfaid:conformance={analysis.DeclaredPdfAConformance ?? "fehlt"}.");
        }
        else
        {
            report.Information(
                CheckRuleIds.PdfADeclarationMissing,
                "Die Datei trägt keine PDF/A-Kennzeichnung. Für den Austausch als E-Rechnung wird "
                + "PDF/A-3 erwartet.",
                "PdfA");
        }

        if (analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.DigitallySigned))
        {
            // Ausdrücklich ein Hinweis. Für die Erzeugung ist eine Signatur
            // ein Hindernis, weil das Einbetten sie bräche. Bei einer bereits
            // fertigen Rechnung ist sie kein Mangel.
            report.Information(
                CheckRuleIds.DigitallySigned,
                "Die Datei enthält eine digitale Signatur. Ob sie gültig ist, wurde hier nicht "
                + "geprüft.",
                "Signature");
        }

        if (analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.RightsRestricted))
        {
            report.Information(
                CheckRuleIds.RightsRestricted,
                "Die Datei trägt Rechteeinschränkungen des Erstellers. Gelesen werden kann sie "
                + "trotzdem.",
                "Protection");
        }
    }

    /// <summary>Meldet die gefundene Profilkennung.</summary>
    private static void ReportProfile(CiiInvoiceSummary summary, ValidationReportBuilder report)
    {
        if (summary.ProfileId is { Length: > 0 } profileId)
        {
            report.Information(
                CheckRuleIds.ProfileDetected,
                $"Erkanntes Profil: {CiiConstants.DescribeProfile(profileId)}.",
                "Profile",
                profileId);

            return;
        }

        report.Warning(
            CheckRuleIds.ProfileMissing,
            "Die Rechnungsdaten nennen kein Profil. Ohne Profilkennung ist nicht festgelegt, "
            + "welche Regeln für die Datei gelten.",
            "Profile");
    }

    private static CheckedDocumentInfo Describe(
        PdfAnalysisResult analysis,
        CheckedAttachment? attachment,
        int? contentLength,
        string? profileId)
        => new(
            PdfVersion: analysis.PdfVersion,
            PageCount: analysis.PageCount,
            DeclaredPdfAPart: analysis.DeclaredPdfAPart,
            DeclaredPdfAConformance: analysis.DeclaredPdfAConformance,
            EmbeddedFiles: analysis.EmbeddedFiles,
            InvoiceAttachmentName: attachment?.File.FileName,
            InvoiceAttachmentKind: attachment?.Kind,
            InvoiceAttachmentSizeInBytes: contentLength,
            ProfileId: profileId,
            IsEncrypted: analysis.IsEncrypted,
            IsDigitallySigned: analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.DigitallySigned));

    private static CheckEInvoiceResult Aborted(
        string fileName,
        long size,
        string? sha256,
        ValidationReportBuilder report,
        CheckedDocumentInfo? info = null)
        => new(
            Completed: false, Canceled: false, fileName, size, sha256, info, null, report.Build());

    /// <summary>
    /// Bildet die Prüfsumme der Quelldatei – ausschließlich lesend und ohne
    /// die Datei vollständig in den Speicher zu holen.
    /// </summary>
    private static async Task<string> ComputeSha256Async(
        string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);

        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        return Convert.ToHexStringLower(hash);
    }

    // Bewusst ohne Dateinamen und ohne Rechnungsdaten: Ein Dateiname trägt
    // regelmäßig den Namen des Kunden oder die Rechnungsnummer, und das
    // Protokoll liegt unverschlüsselt auf der Platte. Was hier steht, muss
    // für die Fehlersuche taugen, ohne den Vorgang zu verraten – Format und
    // Größe genügen dafür.
    [LoggerMessage(
        EventId = 2100, Level = LogLevel.Information,
        Message = "E-Rechnung geprüft: Anhangsformat {AttachmentKind}, {SizeInBytes} Bytes.")]
    private static partial void LogChecked(ILogger logger, string attachmentKind, int sizeInBytes);
}

/// <summary>
/// Die stabilen Kennungen der Prüfbefunde.
///
/// Sie stehen an einer Stelle und nicht als Zeichenketten im Ablauf verstreut:
/// Eine Kennung ist eine Zusage nach außen – sie landet im Bericht, in der
/// Dokumentation und irgendwann in einer Rückfrage eines Anwenders. Ein
/// Tippfehler in einem selten erreichten Zweig fiele sonst niemandem auf.
/// </summary>
public static class CheckRuleIds
{
    /// <summary>Die ausgewählte Datei gibt es nicht.</summary>
    public const string SourceMissing = "APP-CHK-001";

    /// <summary>Die Datei ist keine PDF.</summary>
    public const string NotAPdf = "APP-CHK-002";

    /// <summary>Die PDF ist beschädigt.</summary>
    public const string PdfDamaged = "APP-CHK-003";

    /// <summary>Ein Kennwort verhindert das Lesen.</summary>
    public const string PasswordProtected = "APP-CHK-004";

    /// <summary>Die Datei nennt eine PDF/A-Kennzeichnung.</summary>
    public const string PdfADeclarationFound = "APP-CHK-010";

    /// <summary>Die Datei trägt keine PDF/A-Kennzeichnung.</summary>
    public const string PdfADeclarationMissing = "APP-CHK-011";

    /// <summary>Die Datei enthält eine digitale Signatur.</summary>
    public const string DigitallySigned = "APP-CHK-012";

    /// <summary>Die Datei trägt Rechteeinschränkungen.</summary>
    public const string RightsRestricted = "APP-CHK-013";

    /// <summary>Kein rechnungsartiger Anhang vorhanden.</summary>
    public const string NoInvoiceAttachment = "APP-CHK-020";

    /// <summary>Rechnungsdaten gefunden, Format nicht unterstützt.</summary>
    public const string UnsupportedInvoiceFormat = "APP-CHK-021";

    /// <summary>Mehrere rechnungsartige Anhänge – keine willkürliche Auswahl.</summary>
    public const string AmbiguousInvoiceAttachments = "APP-CHK-022";

    /// <summary>Der Rechnungsanhang ist leer.</summary>
    public const string InvoiceXmlEmpty = "APP-CHK-023";

    /// <summary>Der Rechnungsanhang überschreitet die Größengrenze.</summary>
    public const string InvoiceXmlTooLarge = "APP-CHK-024";

    /// <summary>Der Rechnungsanhang ist nicht begrenzt entpackbar gepackt.</summary>
    public const string InvoiceXmlUnsupportedFilter = "APP-CHK-025";

    /// <summary>Der Rechnungsanhang ist keine wohlgeformte XML.</summary>
    public const string InvoiceXmlNotWellFormed = "APP-CHK-030";

    /// <summary>Der Rechnungsanhang ist keine auswertbare CII-Struktur.</summary>
    public const string InvoiceXmlNotCii = "APP-CHK-031";

    /// <summary>Die Profilkennung wurde gelesen.</summary>
    public const string ProfileDetected = "APP-CHK-040";

    /// <summary>Die Rechnungsdaten nennen kein Profil.</summary>
    public const string ProfileMissing = "APP-CHK-041";
}

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
///
/// Die Bestandteile, die aus dem Ergebnis eine normgerechte Hybridrechnung
/// machen – XMP, OutputIntent, eingebettete XML –, stehen in
/// <see cref="PdfAInvoiceParts"/>. Sie sind für jeden Weg zu einer fertigen
/// Datei dieselben.
/// </summary>
public sealed partial class PdfAInvoiceComposer : IPdfAInvoiceComposer
{
    private readonly IPdfAnalyzer _analyzer;
    private readonly ILogger<PdfAInvoiceComposer> _logger;

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

        // Auch hier zählt die Anwesenheit des Anhangs, nicht sein Lesbarkeit.
        if (analysis.HasExistingInvoiceAttachment)
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
            byte[] result = Upgrade(request);

            LogComposed(_logger, analysis.PageCount, result.Length);

            return CompositionResult.Success(result, report.Build());
        }
        catch (PdfReaderException ex)
        {
            // **„Vermutlich beschädigt“ war hier jahrelang zu bequem.** Eine
            // rechtebeschränkte PDF lässt sich lesen und anzeigen, aber nicht
            // ändern; PDFsharp sagt das beim Öffnen im Änderungsmodus deutlich.
            // Sie dem Anwender als kaputt zu melden, schickt ihn los, eine
            // heile Datei zu suchen, die er längst hat.
            //
            // Die Eingangsprüfung fängt diesen Fall inzwischen vorher ab. Diese
            // Stelle bleibt trotzdem ehrlich: Sie ist die letzte, an der noch
            // etwas durchkommen kann, und eine falsche Auskunft ist auch dann
            // eine falsche Auskunft.
            report.Add(ex.Message.Contains("owner password", StringComparison.OrdinalIgnoreCase)
                ? DescribeBlocker(PdfUpgradeBlocker.RightsRestricted) with { TechnicalDetail = ex.Message }
                : ValidationFinding.Error(
                    "APP-PDF-030",
                    "Die PDF-Datei konnte nicht verarbeitet werden. Sie ist vermutlich beschädigt.",
                    technicalDetail: ex.Message));

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

    /// <summary>
    /// Wertet ein geeignetes Original auf: Seiten unverändert übernehmen,
    /// PDF/A-3-Bestandteile ergänzen.
    /// </summary>
    private static byte[] Upgrade(PdfACompositionRequest request)
    {
        // Das Original wird nur gelesen. Der Datenstrom wird bewusst separat
        // geöffnet und nach dem Einlesen sofort geschlossen, damit die Datei
        // nicht länger als nötig gesperrt ist.
        using var source = new FileStream(
            request.SourcePdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PdfDocument document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);

        return PdfAInvoiceParts.Finish(document, request);
    }

    [LoggerMessage(
        EventId = 2001, Level = LogLevel.Warning,
        Message = "PDF/A-3-Aufwertung abgelehnt, {Count} Hindernis(se): {Blockers}")]
    private static partial void LogUpgradeRejected(ILogger logger, int count, string blockers);

    [LoggerMessage(
        EventId = 2002, Level = LogLevel.Information,
        Message = "PDF/A-3b erzeugt: {Pages} Seite(n), {Size} Bytes")]
    private static partial void LogComposed(ILogger logger, int pages, int size);

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

        PdfUpgradeBlocker.RightsRestricted => ValidationFinding.Error(
            "APP-PDF-006",
            "Diese PDF enthält Berechtigungseinschränkungen. Sie kann angezeigt, aber "
            + "nicht für die E-Rechnung verändert werden. Bitte verwenden Sie eine "
            + "ungeschützte Fassung der Rechnung.",
            technicalDetail: "Die Datei trägt ein /Encrypt-Wörterbuch mit Besitzerkennwort. "
                             + "Sie ist nicht beschädigt."),

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

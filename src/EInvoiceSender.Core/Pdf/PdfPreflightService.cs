using System.Diagnostics;
using System.Globalization;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Führt die Eingangsprüfung einer PDF-Datei durch.
///
/// Grundsatz für alle Meldungen: Wenn eine Datei abgelehnt wird, muss der
/// Benutzer erfahren, **was er in seinem bisherigen Programm umstellen soll**.
/// "Nicht geeignet" allein hilft niemandem weiter. Deshalb nennt jede
/// Ablehnung eine konkrete Einstellung.
///
/// Das Original wird ausschließlich gelesen.
/// </summary>
public sealed partial class PdfPreflightService : IPdfPreflightService
{
    private readonly IPdfAnalyzer _analyzer;
    private readonly IPdfRenderProbe _renderProbe;
    private readonly ILogger<PdfPreflightService> _logger;
    private readonly long _maxFileSizeInBytes;

    /// <summary>Vorgabe für die größte zulässige Eingabedatei.</summary>
    public const int DefaultMaxFileSizeMegabytes = 20;

    public PdfPreflightService(
        IPdfAnalyzer analyzer,
        IPdfRenderProbe renderProbe,
        ILogger<PdfPreflightService> logger,
        int maxFileSizeMegabytes = DefaultMaxFileSizeMegabytes)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _renderProbe = renderProbe ?? throw new ArgumentNullException(nameof(renderProbe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentOutOfRangeException.ThrowIfLessThan(maxFileSizeMegabytes, 1);
        _maxFileSizeInBytes = maxFileSizeMegabytes * 1024L * 1024L;
    }

    /// <inheritdoc />
    public async Task<PdfPreflightReport> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var stopwatch = Stopwatch.StartNew();
        var findings = new ValidationReportBuilder();
        string fileName = Path.GetFileName(filePath);

        // --- Stufe 1: Existenz und Größe, bevor irgendetwas geparst wird ---
        var info = new FileInfo(filePath);

        if (!info.Exists)
        {
            findings.Error(
                "APP-PRE-001",
                "Die Datei wurde nicht gefunden. Möglicherweise wurde sie verschoben "
                + "oder umbenannt.",
                "File");

            return NotSuitable(filePath, fileName, 0, findings);
        }

        if (info.Length == 0)
        {
            findings.Error(
                "APP-PRE-002",
                "Die Datei ist leer.",
                "File");

            return NotSuitable(filePath, fileName, 0, findings);
        }

        if (info.Length > _maxFileSizeInBytes)
        {
            findings.Error(
                "APP-PRE-003",
                $"Die Datei ist mit {FormatMegabytes(info.Length)} MB größer als die "
                + $"zulässigen {FormatMegabytes(_maxFileSizeInBytes)} MB. Verkleinern Sie "
                + "die Datei, indem Sie beim PDF-Export eine geringere Bildauflösung wählen.",
                "File",
                $"{info.Length} Bytes");

            return NotSuitable(filePath, fileName, info.Length, findings);
        }

        // --- Stufe 2: Ist es überhaupt ein PDF? ---
        bool looksLikePdf = await _analyzer.LooksLikePdfAsync(filePath, cancellationToken)
            .ConfigureAwait(false);

        if (!looksLikePdf)
        {
            findings.Error(
                "APP-PRE-004",
                "Die Datei ist keine PDF-Datei. Die Dateiendung allein entscheidet nicht – "
                + "geprüft wird der tatsächliche Inhalt. Bitte wählen Sie die "
                + "PDF-Fassung Ihrer Rechnung aus.",
                "File",
                "Die Datei beginnt nicht mit der Kennung '%PDF-'.");

            return NotSuitable(filePath, fileName, info.Length, findings);
        }

        // --- Stufe 3: Inhaltliche Analyse ---
        PdfAnalysisResult analysis = await _analyzer.AnalyzeAsync(filePath, cancellationToken)
            .ConfigureAwait(false);

        bool damaged = analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.Damaged);
        bool encrypted = analysis.IsEncrypted
                         || analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.Encrypted);
        bool signed = analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.DigitallySigned);
        bool activeContent = analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.ActiveContent);
        bool fontsEmbedded = !analysis.UpgradeBlockers.Contains(PdfUpgradeBlocker.FontsNotEmbedded);

        // Erst der Weg, dann die Meldungen: Wie ein Hindernis zu benennen ist,
        // hängt davon ab, ob es den Vorgang beendet oder nur den bequemen Weg
        // versperrt. Dieselbe fehlende Schrifteinbettung ist einmal ein Fehler
        // und einmal ein Hinweis.
        RouteDecision decision = await ChooseRouteAsync(analysis, filePath, cancellationToken)
            .ConfigureAwait(false);

        AddBlockerFindings(analysis, findings, decision);
        AddInformationalFindings(analysis, findings, decision.Route);

        PdfProcessingRoute route = decision.Route;

        PreflightVerdict verdict = route == PdfProcessingRoute.Rejected
            ? PreflightVerdict.NotSuitable
            : findings.HasWarnings()
                ? PreflightVerdict.SuitableWithWarnings
                : PreflightVerdict.Suitable;

        LogPreflight(
            _logger,
            verdict,
            route,
            analysis.UpgradeBlockers.Count,
            stopwatch.ElapsedMilliseconds);

        return new PdfPreflightReport(
            Verdict: verdict,
            Route: route,
            FilePath: filePath,
            FileName: fileName,
            FileSizeInBytes: info.Length,
            IsReadable: !damaged,
            IsEncrypted: encrypted,
            IsDamaged: damaged,
            HasDigitalSignature: signed,
            AllFontsEmbedded: fontsEmbedded,
            HasActiveContent: activeContent,
            PdfVersion: analysis.PdfVersion,
            PageCount: analysis.PageCount,
            EmbeddedFiles: analysis.EmbeddedFiles,
            ExistingInvoiceProfile: analysis.ExistingInvoiceProfile,
            HasXmpMetadata: analysis.DeclaredPdfAPart is not null
                            || analysis.DeclaredPdfAConformance is not null,
            DeclaredPdfALevel: BuildPdfALevel(analysis),
            Findings: findings.Build());
    }

    /// <summary>
    /// Entscheidet, auf welchem Weg diese Datei verarbeitet werden kann.
    ///
    /// **Die Regel ist eng gefasst, und das mit Absicht.** Es gibt keinen Satz
    /// „PDFium kann rendern, also ist alles erlaubt“. Jedes Hindernis ist
    /// einzeln beurteilt:
    ///
    /// * <b>Nicht eingebettete Schriften</b> sind ein Darstellungsproblem. Wird
    ///   die Seite als Bild neu aufgebaut, ist es weg – die Schrift kommt im
    ///   Ergebnis nicht mehr vor. Das ist der eine Fall, für den der Rasterweg
    ///   gedacht ist, und auch er nur mit nachgewiesener Darstellbarkeit.
    /// * <b>Beschädigt</b> heißt: Was auf dem Bild landet, ist ungewiss. Eine
    ///   Rechnung, deren Inhalt man raten muss, wird nicht erzeugt.
    /// * <b>Öffnungskennwort</b> heißt: Es gibt gar nichts zu rendern.
    /// * <b>Digital signiert</b> heißt: Der Unterzeichner hat für ein bestimmtes
    ///   Dokument gebürgt. Ein Abbild davon ist nicht dasselbe Dokument.
    /// * <b>Rechteeinschränkung</b> heißt: Der Rechteinhaber hat festgelegt,
    ///   was mit dem Dokument geschehen darf. Darüber setzt sich die Anwendung
    ///   nicht stillschweigend hinweg.
    /// * <b>Aktive Inhalte</b> gehören nicht in eine Rechnung. Dass sie beim
    ///   Rastern verschwänden, ist kein Grund, sie zu übergehen – erst müsste
    ///   geklärt sein, ob das Sichtbare ohne sie überhaupt vollständig ist.
    ///
    /// Kommen mehrere Hindernisse zusammen, zählt das strengste.
    /// </summary>
    private async Task<RouteDecision> ChooseRouteAsync(
        PdfAnalysisResult analysis, string filePath, CancellationToken cancellationToken)
    {
        if (analysis.CanBeUpgraded)
        {
            return new RouteDecision(PdfProcessingRoute.Direct);
        }

        bool onlyFontsMissing = analysis.UpgradeBlockers
            .All(blocker => blocker == PdfUpgradeBlocker.FontsNotEmbedded);

        if (!onlyFontsMissing)
        {
            return new RouteDecision(PdfProcessingRoute.Rejected);
        }

        // Fremde Anhänge überleben den Rasterweg nicht: Er baut ein neues
        // Dokument, und was nicht ausdrücklich hineingelegt wird, ist weg.
        if (ForeignAttachments(analysis).Count > 0)
        {
            return new RouteDecision(PdfProcessingRoute.Rejected, AttachmentsBlockRaster: true);
        }

        PdfRenderProbeResult probe = await _renderProbe
            .ProbeAsync(filePath, cancellationToken).ConfigureAwait(false);

        return probe.CanRender
            ? new RouteDecision(PdfProcessingRoute.RasterFallback)
            : new RouteDecision(PdfProcessingRoute.Rejected, RenderRefusal: probe.Reason);
    }

    /// <summary>
    /// Das Ergebnis der Wegwahl samt Begründung, falls der Rasterweg
    /// ausscheidet. Die Begründung wird gebraucht, weil dieselbe Ablehnung dem
    /// Anwender sehr verschiedene Dinge sagen muss.
    /// </summary>
    private sealed record RouteDecision(
        PdfProcessingRoute Route,
        string? RenderRefusal = null,
        bool AttachmentsBlockRaster = false);

    /// <summary>
    /// Anhänge, die nicht die Rechnungs-XML sind – Lieferscheine, Stundenzettel,
    /// Fotos. Sie gehören dem Anwender und nicht der Anwendung.
    /// </summary>
    private static List<EmbeddedFileInfo> ForeignAttachments(PdfAnalysisResult analysis)
        => [.. analysis.EmbeddedFiles
            .Where(file => !InvoiceAttachmentDescriptor.LooksLikeInvoiceFile(file.FileName))];

    /// <summary>
    /// Übersetzt die Hindernisse in Meldungen, die eine konkrete Handlung
    /// nennen. Jede Meldung beantwortet: Was ist das Problem, und was soll ich
    /// jetzt tun?
    /// </summary>
    private static void AddBlockerFindings(
        PdfAnalysisResult analysis,
        ValidationReportBuilder findings,
        RouteDecision decision)
    {
        if (decision.AttachmentsBlockRaster)
        {
            AddAttachmentsBlockRasterFinding(findings, ForeignAttachments(analysis));
        }

        foreach (PdfUpgradeBlocker blocker in analysis.UpgradeBlockers)
        {
            switch (blocker)
            {
                case PdfUpgradeBlocker.FontsNotEmbedded
                    when decision.Route == PdfProcessingRoute.RasterFallback:
                    AddRasterFallbackOffer(findings);
                    break;

                case PdfUpgradeBlocker.RightsRestricted:
                    findings.Error(
                        "APP-PRE-015",
                        "Diese PDF enthält Berechtigungseinschränkungen. Sie kann angezeigt, "
                        + "aber nicht für die E-Rechnung verändert werden. Bitte verwenden Sie "
                        + "eine ungeschützte Fassung der Rechnung – in Ihrem Programm heißt die "
                        + "Einstellung meist \"Berechtigungskennwort\" oder \"Dokument schützen\".",
                        "File",
                        "Die Datei trägt ein /Encrypt-Wörterbuch mit Besitzerkennwort. Sie lässt "
                        + "sich ohne Kennwort öffnen, aber nicht ändern. Verschlüsselte "
                        + "Dokumente sind nach PDF/A ohnehin nicht zulässig. "
                        + "Die Datei ist nicht beschädigt.");
                    break;

                case PdfUpgradeBlocker.Encrypted:
                    findings.Error(
                        "APP-PRE-010",
                        "Die PDF-Datei ist verschlüsselt oder mit einem Kennwort geschützt. "
                        + "Speichern Sie die Rechnung in Ihrem Programm ohne Kennwortschutz "
                        + "und ohne Berechtigungseinschränkungen erneut.",
                        "File",
                        "Verschlüsselte Dokumente sind nach PDF/A nicht zulässig.");
                    break;

                case PdfUpgradeBlocker.FontsNotEmbedded:
                    findings.Error(
                        "APP-PRE-011",
                        "In der PDF-Datei sind nicht alle Schriftarten eingebettet. "
                        + "Stellen Sie beim PDF-Export die Option \"Schriftarten einbetten\" "
                        + "ein – in vielen Programmen heißt sie \"Alle Schriften einbetten\" "
                        + "oder \"Fonts embedden\". Am einfachsten ist es, direkt als PDF/A "
                        + "zu exportieren; dann ist die Einstellung automatisch gesetzt.",
                        "File",
                        "Mindestens ein Font-Objekt hat keinen FontFile-Eintrag im FontDescriptor. "
                        + "Betroffen sind auch die 14 Standardschriften wie Helvetica oder Arial."
                        + (decision.RenderRefusal is null
                            ? string.Empty
                            : " Eine sichtbare Kopie kam nicht in Frage: " + decision.RenderRefusal));
                    break;

                case PdfUpgradeBlocker.ActiveContent:
                    findings.Error(
                        "APP-PRE-012",
                        "Die PDF-Datei enthält aktive Inhalte wie JavaScript oder eine "
                        + "automatisch startende Aktion. Solche Inhalte sind in einer "
                        + "E-Rechnung nicht zulässig. Exportieren Sie die Rechnung ohne "
                        + "Formularfunktionen und ohne Skripte.",
                        "File",
                        "/OpenAction oder /Names /JavaScript im Dokumentkatalog gefunden.");
                    break;

                case PdfUpgradeBlocker.Damaged:
                    findings.Error(
                        "APP-PRE-013",
                        "Die PDF-Datei konnte nicht vollständig gelesen werden. Sie ist "
                        + "wahrscheinlich beschädigt oder wurde beim Speichern abgeschnitten. "
                        + "Erzeugen Sie die Rechnung in Ihrem Programm neu.",
                        "File",
                        "Die Querverweistabelle oder die Objektstruktur ist nicht auflösbar.");
                    break;

                case PdfUpgradeBlocker.DigitallySigned:
                    findings.Error(
                        "APP-PRE-014",
                        "Die PDF-Datei ist digital signiert. Durch das Einbetten der "
                        + "Rechnungsdaten würde die Signatur ungültig. Verwenden Sie die "
                        + "unsignierte Fassung; signieren Sie erst die fertige E-Rechnung.",
                        "File",
                        "Signaturfelder im AcroForm gefunden (/SigFlags ungleich null).");
                    break;

                default:
                    findings.Error(
                        "APP-PRE-019",
                        "Die PDF-Datei kann nicht zu einer normgerechten E-Rechnung "
                        + "aufgewertet werden.",
                        "File",
                        $"Unbekanntes Hindernis: {blocker}");
                    break;
            }
        }
    }

    /// <summary>
    /// Beschreibt den angebotenen Rasterweg – als Warnung, nicht als Fehler.
    ///
    /// Ein roter Fehler wäre hier schlicht unwahr: Es gibt einen erprobten Weg,
    /// und er führt zu einer vollständig geprüften E-Rechnung. Was der Benutzer
    /// wissen muss, ist nicht „geht nicht“, sondern was er dabei aufgibt – und
    /// zwar bevor er zustimmt und nicht danach.
    ///
    /// Zwei Meldungen, weil sie zwei Fragen beantworten: was geschieht und was
    /// es kostet.
    /// </summary>
    private static void AddRasterFallbackOffer(ValidationReportBuilder findings)
    {
        findings.Warning(
            "APP-PRE-011",
            "Diese PDF kann nicht direkt übernommen werden, weil nicht alle Schriftarten "
            + "eingebettet sind. BorstWerk E-Rechnung kann stattdessen örtlich eine "
            + "sichtbare PDF/A-Kopie erzeugen. Das Original bleibt unverändert.",
            "File",
            "Mindestens ein Font-Objekt hat keinen FontFile-Eintrag im FontDescriptor. "
            + "Betroffen sind auch die 14 Standardschriften wie Helvetica oder Arial. "
            + "Alle Seiten ließen sich probeweise darstellen.");

        findings.Information(
            "APP-PRE-016",
            "Was die sichtbare Kopie kostet: Der Text der Seiten ist danach nicht mehr "
            + "markierbar und in der Anzeige nicht mehr durchsuchbar. Verknüpfungen und "
            + "Formularfunktionen gehen verloren. Die Datei kann größer werden. Die "
            + "Rechnungsdaten selbst bleiben maschinenlesbar – sie stecken in der "
            + "eingebetteten XML und sind von der Darstellung unabhängig.",
            "File");
    }

    /// <summary>
    /// Erklärt, warum es hier keine sichtbare Kopie gibt.
    ///
    /// **Der Rasterweg baut ein neues Dokument.** Er setzt die Seiten aus
    /// gerenderten Bildern zusammen und legt die Rechnungs-XML dazu. Was sonst
    /// noch an der Vorlage hing – ein Lieferschein, ein Stundenzettel, ein Foto –
    /// ist danach nicht mehr da.
    ///
    /// Zwei Sätze weiter oben verspricht die Eingangsprüfung zu jedem solchen
    /// Anhang „Er bleibt erhalten.“ Auf dem direkten Weg stimmt das. Den
    /// Rasterweg trotzdem anzubieten hieße, dieses Versprechen zu brechen, und
    /// zwar unbemerkt: Dem Ergebnis sieht niemand an, dass etwas fehlt, das
    /// vorher da war. Lieber kein Angebot als ein stiller Verlust.
    /// </summary>
    private static void AddAttachmentsBlockRasterFinding(
        ValidationReportBuilder findings, List<EmbeddedFileInfo> attachments)
        => findings.Error(
            "APP-PRE-017",
            $"Für diese Datei wird keine sichtbare Kopie angeboten: Sie enthält "
            + $"{(attachments.Count == 1 ? "einen weiteren Anhang" : $"{attachments.Count} weitere Anhänge")} "
            + $"({string.Join(", ", attachments.Select(a => a.FileName))}). Eine sichtbare "
            + "Kopie wird als neues Dokument aufgebaut; diese Anhänge wären darin nicht "
            + "mehr enthalten. Verwenden Sie eine PDF-Datei mit eingebetteten Schriftarten, "
            + "damit die Anhänge erhalten bleiben.",
            "File",
            "Der Rasterweg übernimmt nur die dargestellten Seiten und die Rechnungs-XML.");

    /// <summary>
    /// Ergänzt Hinweise und Warnungen, die eine Verarbeitung nicht verhindern.
    /// </summary>
    private static void AddInformationalFindings(
        PdfAnalysisResult analysis, ValidationReportBuilder findings, PdfProcessingRoute route)
    {
        // **Anwesenheit, nicht Lesbarkeit.** Die Warnung hängt am Anhang, nicht
        // daran, ob er sich auswerten ließ. Ein unlesbarer Anhang ist eher ein
        // Grund mehr, den Benutzer zu fragen, als einer weniger.
        if (analysis.HasExistingInvoiceAttachment)
        {
            // Bewusst eine Warnung, kein stiller Ersatz: Der Benutzer muss
            // wissen, dass hier bereits Rechnungsdaten stecken.
            findings.Warning(
                "APP-PRE-020",
                "Die Datei enthält bereits eine eingebettete Rechnung. Wenn Sie "
                + "fortfahren, werden diese Daten durch die neu erfassten ersetzt. "
                + "Prüfen Sie, ob Sie wirklich die Ausgangsrechnung ausgewählt haben "
                + "und nicht eine bereits erzeugte E-Rechnung.",
                "File",
                $"Gefundenes Profil: {analysis.ExistingInvoiceProfile ?? "unbekannt"}");
        }

        // „Er bleibt erhalten“ gilt nur für den direkten Weg – dort werden die
        // Seiten des Originals samt allem, was daran hängt, übernommen. Auf
        // jedem anderen Weg wäre der Satz eine Zusage, die niemand einhält;
        // dann steht hier nur, was gefunden wurde.
        foreach (EmbeddedFileInfo file in ForeignAttachments(analysis))
        {
            findings.Information(
                "APP-PRE-021",
                $"Die Datei enthält den Anhang '{file.FileName}'."
                + (route == PdfProcessingRoute.Direct ? " Er bleibt erhalten." : string.Empty),
                "File",
                $"{file.SizeInBytes} Bytes, Typ {file.MimeType ?? "unbekannt"}");
        }

        if (analysis.DeclaredPdfAPart is { } part)
        {
            findings.Information(
                "APP-PRE-022",
                $"Die Datei ist bereits als PDF/A-{part}{analysis.DeclaredPdfAConformance} "
                + "gekennzeichnet.",
                "File");
        }

        if (analysis.PageCount > 20)
        {
            findings.Information(
                "APP-PRE-023",
                $"Die Rechnung hat {analysis.PageCount} Seiten.",
                "File");
        }
    }

    private static string? BuildPdfALevel(PdfAnalysisResult analysis)
        => analysis.DeclaredPdfAPart is null
            ? null
            : analysis.DeclaredPdfAPart + (analysis.DeclaredPdfAConformance ?? string.Empty);

    /// <summary>Baut einen Bericht für eine Datei, die gar nicht erst geöffnet wurde.</summary>
    private static PdfPreflightReport NotSuitable(
        string filePath, string fileName, long size, ValidationReportBuilder findings)
        => new(
            Verdict: PreflightVerdict.NotSuitable,
            Route: PdfProcessingRoute.Rejected,
            FilePath: filePath,
            FileName: fileName,
            FileSizeInBytes: size,
            IsReadable: false,
            IsEncrypted: false,
            IsDamaged: false,
            HasDigitalSignature: false,
            AllFontsEmbedded: false,
            HasActiveContent: false,
            PdfVersion: "unbekannt",
            PageCount: 0,
            EmbeddedFiles: [],
            ExistingInvoiceProfile: null,
            HasXmpMetadata: false,
            DeclaredPdfALevel: null,
            Findings: findings.Build());

    private static string FormatMegabytes(long bytes)
        => (bytes / 1024.0 / 1024.0).ToString("0.#", CultureInfo.GetCultureInfo("de-DE"));

    [LoggerMessage(
        EventId = 2020, Level = LogLevel.Information,
        Message = "Eingangsprüfung abgeschlossen: {Verdict}, Weg {Route}, {BlockerCount} Hindernisse, {Milliseconds} ms")]
    private static partial void LogPreflight(
        ILogger logger,
        PreflightVerdict verdict,
        PdfProcessingRoute route,
        int blockerCount,
        long milliseconds);
}

using System.Globalization;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Infrastructure.PdfA;

/// <summary>
/// Fuehrt die Eingangspruefung einer PDF-Datei durch.
///
/// Grundsatz fuer alle Meldungen: Wenn eine Datei abgelehnt wird, muss der
/// Benutzer erfahren, **was er in seinem bisherigen Programm umstellen soll**.
/// "Nicht geeignet" allein hilft niemandem weiter. Deshalb nennt jede
/// Ablehnung eine konkrete Einstellung.
///
/// Das Original wird ausschliesslich gelesen.
/// </summary>
public sealed partial class PdfPreflightService : IPdfPreflightService
{
    private readonly IPdfAnalyzer _analyzer;
    private readonly ILogger<PdfPreflightService> _logger;
    private readonly long _maxFileSizeInBytes;

    /// <summary>Vorgabe fuer die groesste zulaessige Eingabedatei.</summary>
    public const int DefaultMaxFileSizeMegabytes = 20;

    public PdfPreflightService(
        IPdfAnalyzer analyzer,
        ILogger<PdfPreflightService> logger,
        int maxFileSizeMegabytes = DefaultMaxFileSizeMegabytes)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
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

        var findings = new ValidationReportBuilder();
        string fileName = Path.GetFileName(filePath);

        // --- Stufe 1: Existenz und Groesse, bevor irgendetwas geparst wird ---
        var info = new FileInfo(filePath);

        if (!info.Exists)
        {
            findings.Error(
                "APP-PRE-001",
                "Die Datei wurde nicht gefunden. Moeglicherweise wurde sie verschoben "
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
                $"Die Datei ist mit {FormatMegabytes(info.Length)} MB groesser als die "
                + $"zulaessigen {FormatMegabytes(_maxFileSizeInBytes)} MB. Verkleinern Sie "
                + "die Datei, indem Sie beim PDF-Export eine geringere Bildaufloesung waehlen.",
                "File",
                $"{info.Length} Bytes");

            return NotSuitable(filePath, fileName, info.Length, findings);
        }

        // --- Stufe 2: Ist es ueberhaupt ein PDF? ---
        bool looksLikePdf = await _analyzer.LooksLikePdfAsync(filePath, cancellationToken)
            .ConfigureAwait(false);

        if (!looksLikePdf)
        {
            findings.Error(
                "APP-PRE-004",
                "Die Datei ist keine PDF-Datei. Die Dateiendung allein entscheidet nicht – "
                + "geprueft wird der tatsaechliche Inhalt. Bitte waehlen Sie die "
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

        AddBlockerFindings(analysis, findings);
        AddInformationalFindings(analysis, findings);

        PreflightVerdict verdict = analysis.CanBeUpgraded
            ? findings.HasWarnings()
                ? PreflightVerdict.SuitableWithWarnings
                : PreflightVerdict.Suitable
            : PreflightVerdict.NotSuitable;

        LogPreflight(_logger, fileName, verdict, analysis.UpgradeBlockers.Count);

        return new PdfPreflightReport(
            Verdict: verdict,
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
    /// Uebersetzt die Hindernisse in Meldungen, die eine konkrete Handlung
    /// nennen. Jede Meldung beantwortet: Was ist das Problem, und was soll ich
    /// jetzt tun?
    /// </summary>
    private static void AddBlockerFindings(PdfAnalysisResult analysis, ValidationReportBuilder findings)
    {
        foreach (PdfUpgradeBlocker blocker in analysis.UpgradeBlockers)
        {
            switch (blocker)
            {
                case PdfUpgradeBlocker.Encrypted:
                    findings.Error(
                        "APP-PRE-010",
                        "Die PDF-Datei ist verschluesselt oder mit einem Kennwort geschuetzt. "
                        + "Speichern Sie die Rechnung in Ihrem Programm ohne Kennwortschutz "
                        + "und ohne Berechtigungseinschraenkungen erneut.",
                        "File",
                        "Verschluesselte Dokumente sind nach PDF/A nicht zulaessig.");
                    break;

                case PdfUpgradeBlocker.FontsNotEmbedded:
                    findings.Error(
                        "APP-PRE-011",
                        "In der PDF-Datei sind nicht alle Schriftarten eingebettet. "
                        + "Stellen Sie beim PDF-Export die Option \"Schriftarten einbetten\" "
                        + "ein – in vielen Programmen heisst sie \"Alle Schriften einbetten\" "
                        + "oder \"Fonts embedden\". Am einfachsten ist es, direkt als PDF/A "
                        + "zu exportieren; dann ist die Einstellung automatisch gesetzt.",
                        "File",
                        "Mindestens ein Font-Objekt hat keinen FontFile-Eintrag im FontDescriptor. "
                        + "Betroffen sind auch die 14 Standardschriften wie Helvetica oder Arial.");
                    break;

                case PdfUpgradeBlocker.ActiveContent:
                    findings.Error(
                        "APP-PRE-012",
                        "Die PDF-Datei enthaelt aktive Inhalte wie JavaScript oder eine "
                        + "automatisch startende Aktion. Solche Inhalte sind in einer "
                        + "E-Rechnung nicht zulaessig. Exportieren Sie die Rechnung ohne "
                        + "Formularfunktionen und ohne Skripte.",
                        "File",
                        "/OpenAction oder /Names /JavaScript im Dokumentkatalog gefunden.");
                    break;

                case PdfUpgradeBlocker.Damaged:
                    findings.Error(
                        "APP-PRE-013",
                        "Die PDF-Datei konnte nicht vollstaendig gelesen werden. Sie ist "
                        + "wahrscheinlich beschaedigt oder wurde beim Speichern abgeschnitten. "
                        + "Erzeugen Sie die Rechnung in Ihrem Programm neu.",
                        "File",
                        "Die Querverweistabelle oder die Objektstruktur ist nicht aufloesbar.");
                    break;

                case PdfUpgradeBlocker.DigitallySigned:
                    findings.Error(
                        "APP-PRE-014",
                        "Die PDF-Datei ist digital signiert. Durch das Einbetten der "
                        + "Rechnungsdaten wuerde die Signatur ungueltig. Verwenden Sie die "
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
    /// Ergaenzt Hinweise und Warnungen, die eine Verarbeitung nicht verhindern.
    /// </summary>
    private static void AddInformationalFindings(
        PdfAnalysisResult analysis, ValidationReportBuilder findings)
    {
        if (analysis.HasExistingInvoiceXml)
        {
            // Bewusst eine Warnung, kein stiller Ersatz: Der Benutzer muss
            // wissen, dass hier bereits Rechnungsdaten stecken.
            findings.Warning(
                "APP-PRE-020",
                "Die Datei enthaelt bereits eine eingebettete Rechnung. Wenn Sie "
                + "fortfahren, werden diese Daten durch die neu erfassten ersetzt. "
                + "Pruefen Sie, ob Sie wirklich die Ausgangsrechnung ausgewaehlt haben "
                + "und nicht eine bereits erzeugte E-Rechnung.",
                "File",
                $"Gefundenes Profil: {analysis.ExistingInvoiceProfile ?? "unbekannt"}");
        }

        foreach (EmbeddedFileInfo file in analysis.EmbeddedFiles)
        {
            if (InvoiceAttachmentDescriptor.LooksLikeInvoiceFile(file.FileName))
            {
                continue;
            }

            findings.Information(
                "APP-PRE-021",
                $"Die Datei enthaelt den Anhang '{file.FileName}'. Er bleibt erhalten.",
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

    /// <summary>Baut einen Bericht fuer eine Datei, die gar nicht erst geoeffnet wurde.</summary>
    private static PdfPreflightReport NotSuitable(
        string filePath, string fileName, long size, ValidationReportBuilder findings)
        => new(
            Verdict: PreflightVerdict.NotSuitable,
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
        Message = "Eingangspruefung {FileName}: {Verdict}, {BlockerCount} Hindernis(se)")]
    private static partial void LogPreflight(
        ILogger logger, string fileName, PreflightVerdict verdict, int blockerCount);
}

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.Xml;
using EInvoiceSender.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Validation.External;

/// <summary>
/// Einstellungen des externen Validators.
/// </summary>
/// <param name="JavaExecutable">Pfad oder Name der Java-Laufzeit.</param>
/// <param name="JarPath">Pfad zur Mustang-CLI-JAR.</param>
/// <param name="Timeout">Zeitlimit fuer einen Pruefdurchlauf.</param>
public sealed record MustangOptions(string JavaExecutable, string JarPath, TimeSpan Timeout)
{
    /// <summary>Vorgabewerte: Java aus dem Suchpfad, zwei Minuten Zeitlimit.</summary>
    public static MustangOptions ForJar(string jarPath)
        => new("java", jarPath, TimeSpan.FromMinutes(2));
}

/// <summary>
/// Bindet die Mustangproject-CLI als externen Pruefer an.
///
/// Mustang fuehrt in einem Durchlauf zwei getrennte Pruefungen aus:
/// das offizielle CEN-Schematron fuer EN 16931 und – bei PDF-Dateien –
/// veraPDF fuer PDF/A. Das Werkzeug laeuft vollstaendig offline; es werden
/// keine Rechnungsdaten uebertragen.
///
/// **Dieser Validator ist das Freigabegate.** Die eigene Regelpruefung
/// (<see cref="Rules.En16931RuleValidator"/>) dient der fruehen Benutzerfuehrung
/// und ersetzt ihn ausdruecklich nicht.
///
/// Zwei Fallstricke, die hier bewusst behandelt werden:
///
/// 1. **Jede Teilzusammenfassung zaehlt einzeln.** Der Bericht enthaelt mehrere
///    <c>&lt;summary status="..."&gt;</c>-Elemente. Die oberste kann "valid"
///    melden, obwohl die PDF/A-Pruefung fehlgeschlagen ist – genau so ist es
///    waehrend der Entwicklung aufgetreten. Ein Ergebnis gilt nur als gueltig,
///    wenn **keine** Teilzusammenfassung "invalid" meldet.
/// 2. **Unlesbare Ausgabe ist ein Fehler, kein Erfolg.** Laesst sich der Bericht
///    nicht auswerten, wird das als Fehler gemeldet. Andernfalls wuerde ein
///    abgestuerztes Werkzeug wie eine bestandene Pruefung wirken.
/// </summary>
public sealed partial class MustangValidator : IExternalDocumentValidator
{
    private readonly IProcessRunner _processRunner;
    private readonly MustangOptions _options;
    private readonly ILogger<MustangValidator> _logger;

    public MustangValidator(
        IProcessRunner processRunner,
        MustangOptions options,
        ILogger<MustangValidator> logger)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Mustangproject CLI (CEN-Schematron und veraPDF)";

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(_options.JarPath));

    /// <inheritdoc />
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_options.JarPath))
        {
            return null;
        }

        ProcessResult result = await RunAsync(["--help"], cancellationToken).ConfigureAwait(false);

        // Die erste Zeile der Hilfe lautet "Mustangproject.org <Version>".
        foreach (string line in result.StandardOutput.Split('\n'))
        {
            if (line.StartsWith("Mustangproject.org", StringComparison.Ordinal))
            {
                return line.Trim();
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ValidationReport> ValidateAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var report = new ValidationReportBuilder();

        if (!File.Exists(_options.JarPath))
        {
            report.Warning(
                "APP-EXT-001",
                "Die zusaetzliche Pruefung mit dem externen Validator wurde uebersprungen, "
                + "weil das Werkzeug auf diesem Rechner nicht eingerichtet ist. "
                + "Die Datei wurde nur mit den eingebauten Pruefungen kontrolliert.",
                technicalDetail: $"Erwartete Datei: {_options.JarPath}");

            return report.Build();
        }

        ProcessResult result;

        try
        {
            result = await RunAsync(
                ["--action", "validate", "--source", filePath],
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            report.Error(
                "APP-EXT-002",
                "Der externe Validator konnte nicht gestartet werden. "
                + "Die Datei gilt damit als ungeprueft.",
                technicalDetail: $"{ex.GetType().Name}: {ex.Message}");

            return report.Build();
        }

        if (result.TimedOut)
        {
            report.Error(
                "APP-EXT-003",
                "Die externe Pruefung wurde abgebrochen, weil sie zu lange gedauert hat. "
                + "Die Datei gilt damit als ungeprueft.",
                technicalDetail:
                    $"Zeitlimit {_options.Timeout.TotalSeconds:F0} s ueberschritten.");

            return report.Build();
        }

        Evaluate(result, report);

        return report.Build();
    }

    /// <summary>
    /// Wertet die Ausgabe aus. Streng: Nur ein vollstaendig gelesener Bericht
    /// ohne einzige ungueltige Teilzusammenfassung gilt als bestanden.
    /// </summary>
    private void Evaluate(ProcessResult result, ValidationReportBuilder report)
    {
        XElement? validation = TryExtractReport(result.StandardOutput)
                               ?? TryExtractReport(result.StandardError);

        if (validation is null)
        {
            report.Error(
                "APP-EXT-004",
                "Die Rueckmeldung des externen Validators konnte nicht ausgewertet werden. "
                + "Die Datei gilt damit als ungeprueft.",
                technicalDetail: BuildTail(result));

            return;
        }

        // Jede Teilzusammenfassung wird einzeln bewertet.
        var summaries = validation
            .Descendants()
            .Where(e => e.Name.LocalName == "summary")
            .Select(e => new
            {
                Section = e.Parent?.Name.LocalName ?? "gesamt",
                Status = (string?)e.Attribute("status") ?? "unbekannt",
            })
            .ToList();

        if (summaries.Count == 0)
        {
            report.Error(
                "APP-EXT-005",
                "Der externe Validator hat kein auswertbares Ergebnis geliefert. "
                + "Die Datei gilt damit als ungeprueft.",
                technicalDetail: BuildTail(result));

            return;
        }

        var invalidSections = summaries
            .Where(s => !string.Equals(s.Status, "valid", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var section in invalidSections)
        {
            report.Error(
                "APP-EXT-010",
                DescribeSection(section.Section),
                technicalDetail:
                    $"Mustang-Abschnitt '{section.Section}' meldet Status '{section.Status}'.");
        }

        // Einzelbefunde des Schematron als technische Details anhaengen.
        foreach (XElement message in validation.Descendants()
                     .Where(e => e.Name.LocalName is "error" or "message"))
        {
            string text = message.Value.Trim();

            if (text.Length == 0)
            {
                continue;
            }

            report.Add(new ValidationFinding(
                Severity: invalidSections.Count > 0
                    ? Domain.Values.FindingSeverity.Error
                    : Domain.Values.FindingSeverity.Warning,
                RuleId: (string?)message.Attribute("criterion") is { Length: > 0 }
                    ? "EXT-SCHEMATRON"
                    : "EXT-MELDUNG",
                Message: "Der externe Validator hat einen Verstoss gemeldet.",
                FieldPath: (string?)message.Attribute("location") ?? string.Empty,
                TechnicalDetail: Shorten(text)));
        }

        // Der Exitcode wird ergaenzend gewertet, aber nie allein.
        if (result.ExitCode != 0 && invalidSections.Count == 0)
        {
            report.Error(
                "APP-EXT-011",
                "Der externe Validator hat die Datei beanstandet.",
                technicalDetail: $"Exitcode {result.ExitCode} bei formal gueltigem Bericht.");
        }

        if (invalidSections.Count == 0 && result.ExitCode == 0)
        {
            LogValidationPassed(_logger, summaries.Count);

            report.Information(
                "APP-EXT-000",
                "Die externe Pruefung wurde bestanden.",
                technicalDetail: string.Join(
                    ", ",
                    summaries.Select(s => $"{s.Section}={s.Status}")));
        }
    }

    /// <summary>Uebersetzt den Abschnittsnamen in eine verstaendliche Aussage.</summary>
    private static string DescribeSection(string section) => section switch
    {
        "pdf" => "Die erzeugte Datei entspricht nicht dem Standard PDF/A-3. "
                 + "Sie wird von manchen Empfaengern zurueckgewiesen.",
        "xml" => "Die strukturierten Rechnungsdaten entsprechen nicht der Norm EN 16931.",
        _ => "Die externe Pruefung der erzeugten Datei ist fehlgeschlagen.",
    };

    /// <summary>
    /// Sucht das <c>&lt;validation&gt;</c>-Element in der Ausgabe. Mustang
    /// schreibt davor Protokollzeilen, die uebersprungen werden muessen.
    /// Gelesen wird ueber den abgesicherten XML-Leser.
    /// </summary>
    private static XElement? TryExtractReport(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        int start = output.IndexOf("<validation", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        int end = output.LastIndexOf("</validation>", StringComparison.Ordinal);
        if (end < start)
        {
            return null;
        }

        string xml = output[start..(end + "</validation>".Length)];

        try
        {
            using XmlReader reader = SecureXml.CreateReader(Encoding.UTF8.GetBytes(xml));
            return XDocument.Load(reader).Root;
        }
        catch (XmlException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private Task<ProcessResult> RunAsync(string[] extraArguments, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "-jar", _options.JarPath };
        arguments.AddRange(extraArguments);

        return _processRunner.RunAsync(
            _options.JavaExecutable,
            arguments,
            _options.Timeout,
            workingDirectory: null,
            cancellationToken);
    }

    /// <summary>
    /// Baut einen kurzen technischen Auszug fuer den Detailbereich. Bewusst
    /// begrenzt: Die Ausgabe kann sehr lang werden und darf den Bericht nicht
    /// unlesbar machen.
    /// </summary>
    private static string BuildTail(ProcessResult result)
    {
        string combined = string.Concat(result.StandardError, "\n", result.StandardOutput).Trim();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Exitcode {result.ExitCode}. Ausgabe: {Shorten(combined)}");
    }

    private static string Shorten(string value, int maxLength = 400)
        => value.Length <= maxLength ? value : value[..maxLength] + " ...";

    [LoggerMessage(
        EventId = 4001, Level = LogLevel.Information,
        Message = "Externe Pruefung bestanden, {SummaryCount} Teilzusammenfassung(en) gueltig.")]
    private static partial void LogValidationPassed(ILogger logger, int summaryCount);
}

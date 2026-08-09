using System.Collections.ObjectModel;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Sammlung von Pruefbefunden eines Pruefschritts.
/// Unveraenderlich; zum Aufbauen dient <see cref="ValidationReportBuilder"/>.
/// </summary>
public sealed class ValidationReport
{
    /// <summary>Ein Bericht ohne Befunde.</summary>
    public static ValidationReport Empty { get; } = new([]);

    public ValidationReport(IEnumerable<ValidationFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        Findings = new ReadOnlyCollection<ValidationFinding>([.. findings]);
    }

    /// <summary>Alle Befunde in der Reihenfolge ihrer Ermittlung.</summary>
    public IReadOnlyList<ValidationFinding> Findings { get; }

    /// <summary>Enthaelt der Bericht mindestens einen Fehler?</summary>
    public bool HasErrors => Findings.Any(f => f.Severity == FindingSeverity.Error);

    /// <summary>Enthaelt der Bericht mindestens eine Warnung?</summary>
    public bool HasWarnings => Findings.Any(f => f.Severity == FindingSeverity.Warning);

    /// <summary>Anzahl der Fehler.</summary>
    public int ErrorCount => Findings.Count(f => f.Severity == FindingSeverity.Error);

    /// <summary>Anzahl der Warnungen.</summary>
    public int WarningCount => Findings.Count(f => f.Severity == FindingSeverity.Warning);

    /// <summary>Fasst diesen Bericht mit weiteren zusammen.</summary>
    public ValidationReport Concat(params ValidationReport[] others)
    {
        ArgumentNullException.ThrowIfNull(others);
        return new ValidationReport(Findings.Concat(others.SelectMany(o => o.Findings)));
    }
}

/// <summary>
/// Aufbauhilfe fuer <see cref="ValidationReport"/>. Bewusst nicht threadsicher –
/// jede Pruefung baut ihren eigenen Bericht.
/// </summary>
public sealed class ValidationReportBuilder
{
    private readonly List<ValidationFinding> _findings = [];

    /// <summary>Aktuelle Zahl der gesammelten Befunde.</summary>
    public int Count => _findings.Count;

    /// <summary>Wurde bereits ein Fehler gemeldet?</summary>
    public bool HasErrors => _findings.Any(f => f.Severity == FindingSeverity.Error);

    /// <summary>Wurde bereits eine Warnung gemeldet?</summary>
    public bool HasWarnings() => _findings.Any(f => f.Severity == FindingSeverity.Warning);

    /// <summary>Nimmt einen Befund auf.</summary>
    public ValidationReportBuilder Add(ValidationFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        _findings.Add(finding);
        return this;
    }

    /// <summary>Nimmt einen Fehler auf.</summary>
    public ValidationReportBuilder Error(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null,
        string? normRule = null)
        => Add(ValidationFinding.Error(ruleId, message, fieldPath, technicalDetail, normRule));

    /// <summary>Nimmt eine Warnung auf.</summary>
    public ValidationReportBuilder Warning(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null,
        string? normRule = null)
        => Add(ValidationFinding.Warning(ruleId, message, fieldPath, technicalDetail, normRule));

    /// <summary>Nimmt einen Hinweis auf.</summary>
    public ValidationReportBuilder Information(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null,
        string? normRule = null)
        => Add(ValidationFinding.Information(ruleId, message, fieldPath, technicalDetail, normRule));

    /// <summary>Nimmt alle Befunde eines anderen Berichts auf.</summary>
    public ValidationReportBuilder AddRange(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _findings.AddRange(report.Findings);
        return this;
    }

    /// <summary>Schliesst den Bericht ab.</summary>
    public ValidationReport Build() => new(_findings);
}

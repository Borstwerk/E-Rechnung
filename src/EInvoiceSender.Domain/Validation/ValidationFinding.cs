using EInvoiceSender.Domain.Values;

namespace EInvoiceSender.Domain.Validation;

/// <summary>
/// Ein einzelner Pruefbefund. Traegt immer beides: die technische Regel-ID fuer
/// den Detailbereich und einen deutschen Satz, den ein normaler Anwender
/// versteht. Siehe docs/SPECIFICATION.md, Abschnitt 6.
/// </summary>
/// <param name="Severity">Schweregrad. Nur <c>Error</c> verhindert die Erzeugung.</param>
/// <param name="RuleId">
/// Regelkennung, moeglichst die der Norm (z. B. <c>BR-CO-13</c>). Eigene
/// Vorpruefungen ohne Entsprechung in EN 16931 tragen das Praefix <c>APP-</c>.
/// </param>
/// <param name="Message">Verstaendliche deutsche Erlaeuterung fuer den Anwender.</param>
/// <param name="FieldPath">
/// Feldbezug fuer die Oberflaeche, z. B. <c>Lines[2].Quantity</c>. Leer, wenn
/// der Befund das ganze Dokument betrifft.
/// </param>
/// <param name="TechnicalDetail">
/// Optionaler technischer Zusatz fuer den aufklappbaren Detailbereich,
/// etwa die konkrete Rechnung mit Ist- und Sollwert.
/// </param>
public sealed record ValidationFinding(
    FindingSeverity Severity,
    string RuleId,
    string Message,
    string FieldPath = "",
    string? TechnicalDetail = null)
{
    /// <summary>Erzeugt einen Fehlerbefund.</summary>
    public static ValidationFinding Error(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null)
        => new(FindingSeverity.Error, ruleId, message, fieldPath, technicalDetail);

    /// <summary>Erzeugt eine Warnung.</summary>
    public static ValidationFinding Warning(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null)
        => new(FindingSeverity.Warning, ruleId, message, fieldPath, technicalDetail);

    /// <summary>Erzeugt einen Hinweis.</summary>
    public static ValidationFinding Information(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null)
        => new(FindingSeverity.Information, ruleId, message, fieldPath, technicalDetail);
}

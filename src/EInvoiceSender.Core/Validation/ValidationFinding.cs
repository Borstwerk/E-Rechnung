using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Ein einzelner Prüfbefund. Trägt immer beides: die technische Regel-ID für
/// den Detailbereich und einen deutschen Satz, den ein normaler Anwender
/// versteht. Siehe docs/SPECIFICATION.md, Abschnitt 6.
/// </summary>
/// <param name="Severity">Schweregrad. Nur <c>Error</c> verhindert die Erzeugung.</param>
/// <param name="RuleId">
/// **Stabile interne Kennung** des Befundes, z. B. <c>APP-SUM-003</c>. Sie
/// ändert sich nicht mehr, sobald sie vergeben ist, und ist der Schlüssel
/// für Dokumentation, Übersetzung und Fehlersuche. Befunde externer
/// Werkzeuge tragen das Präfix <c>EXT-</c>.
/// </param>
/// <param name="Message">Verständliche deutsche Erläuterung für den Anwender.</param>
/// <param name="FieldPath">
/// Feldbezug für die Oberfläche, z. B. <c>Lines[2].Quantity</c>. Leer, wenn
/// der Befund das ganze Dokument betrifft.
/// </param>
/// <param name="TechnicalDetail">
/// Optionaler technischer Zusatz für den aufklappbaren Detailbereich,
/// etwa die konkrete Rechnung mit Ist- und Sollwert.
/// </param>
/// <param name="NormRule">
/// Die zugehörige Regel der Norm EN 16931 (z. B. <c>BR-CO-13</c>), soweit es
/// eine gibt. Bleibt leer, wenn die Prüfung eine reine Benutzerführung ohne
/// Entsprechung in der Norm ist. Wird im Detailbereich angezeigt.
///
/// Wichtig: Eine bestandene eigene Prüfung ersetzt **nicht** die Prüfung
/// durch das offizielle Schematron. Die Freigabe erteilen ausschließlich die
/// externen Validatoren (docs/DECISIONS.md, ADR-0004).
/// </param>
public sealed record ValidationFinding(
    FindingSeverity Severity,
    string RuleId,
    string Message,
    string FieldPath = "",
    string? TechnicalDetail = null,
    string? NormRule = null)
{
    /// <summary>Erzeugt einen Fehlerbefund.</summary>
    public static ValidationFinding Error(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null,
        string? normRule = null)
        => new(FindingSeverity.Error, ruleId, message, fieldPath, technicalDetail, normRule);

    /// <summary>Erzeugt eine Warnung.</summary>
    public static ValidationFinding Warning(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null,
        string? normRule = null)
        => new(FindingSeverity.Warning, ruleId, message, fieldPath, technicalDetail, normRule);

    /// <summary>Erzeugt einen Hinweis.</summary>
    public static ValidationFinding Information(
        string ruleId,
        string message,
        string fieldPath = "",
        string? technicalDetail = null,
        string? normRule = null)
        => new(FindingSeverity.Information, ruleId, message, fieldPath, technicalDetail, normRule);

    /// <summary>
    /// Vollständige technische Beschreibung für den aufklappbaren
    /// Detailbereich: interne Kennung, Normregel und Zusatzangabe.
    /// </summary>
    public string BuildTechnicalSummary()
    {
        var parts = new List<string>(3) { $"Kennung {RuleId}" };

        if (!string.IsNullOrWhiteSpace(NormRule))
        {
            parts.Add($"Regel {NormRule}");
        }

        if (!string.IsNullOrWhiteSpace(TechnicalDetail))
        {
            parts.Add(TechnicalDetail);
        }

        return string.Join(" · ", parts);
    }
}

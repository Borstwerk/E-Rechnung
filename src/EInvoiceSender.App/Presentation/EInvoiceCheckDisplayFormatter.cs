using System.Globalization;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.App.Presentation;

/// <summary>
/// Deterministische deutsche Darstellung gelesener Rechnungswerte und
/// technischer Befundangaben. Fachwerte und Regelkennungen werden nicht
/// verändert.
/// </summary>
internal static class EInvoiceCheckDisplayFormatter
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Formatiert BT-2 als deutsches Datum ohne Zeitanteil.</summary>
    public static string FormatDate(DateOnly? date)
        => date?.ToString("dd.MM.yyyy", GermanCulture) ?? "–";

    /// <summary>
    /// Formatiert einen gelesenen Rechnungsbetrag mit genau zwei deutschen
    /// Nachkommastellen und der unveränderten Währung der Rechnung.
    /// </summary>
    public static string FormatMoney(decimal? amount, string? currency)
    {
        if (amount is null)
        {
            return "–";
        }

        string number = amount.Value.ToString("N2", GermanCulture);

        return string.IsNullOrWhiteSpace(currency)
            ? number
            : $"{number} {currency.Trim()}";
    }

    /// <summary>
    /// Stellt interne und belegte Normkennungen als nachrangige, verständlich
    /// beschriftete Detailzeile dar, ohne den Befundtext zu wiederholen.
    /// </summary>
    public static string FormatTechnicalDetails(ValidationFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        string identifier = string.IsNullOrWhiteSpace(finding.NormRule)
            ? $"Interne Kennung {finding.RuleId}"
            : $"{DescribeNormRule(finding.NormRule)} · interne Kennung {finding.RuleId}";

        return string.IsNullOrWhiteSpace(finding.TechnicalDetail)
            ? $"Technische Details: {identifier}"
            : $"Technische Details: {identifier} · {finding.TechnicalDetail}";
    }

    private static string DescribeNormRule(string normRule)
        => normRule switch
        {
            "BR-CO-26" => "EN 16931 – Verkäuferidentifikation (BR-CO-26)",
            _ => $"EN 16931 – Regel {normRule}",
        };
}

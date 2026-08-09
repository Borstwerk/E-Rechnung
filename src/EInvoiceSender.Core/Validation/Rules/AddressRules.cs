using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zur Anschrift.
///
/// Von Verkäufer und Käufer gemeinsam genutzt - die Anforderungen an
/// eine Anschrift sind für beide dieselben.
/// </summary>
internal static class AddressRules
{
    public static void Validate(
        PostalAddress address, string field, string owner, string ruleId,
        ValidationReportBuilder report)
    {
        // EN 16931 verlangt zwingend nur das Land. Ort und Straße fehlen in der
        // Praxis aber fast nie mit Absicht, deshalb hier als Warnung.
        if (string.IsNullOrWhiteSpace(address.City))
        {
            report.Warning(
                ruleId,
                $"Im Anschriftsfeld {owner} fehlt der Ort.",
                $"{field}.City");
        }

        if (string.IsNullOrWhiteSpace(address.Street))
        {
            report.Warning(
                ruleId,
                $"Im Anschriftsfeld {owner} fehlt die Straße.",
                $"{field}.Street");
        }
    }

    // --------------------------------------------------------------- Positionen
}

using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zur Umsatzsteueraufschluesselung.
/// </summary>
internal static class VatRules
{
    public static void Validate(
        Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        if (totals.VatBreakdown.Count == 0)
        {
            report.Error(
                "APP-VAT-010",
                "Die Rechnung enthaelt keine Steueraufschluesselung.",
                "VatBreakdown", normRule: "BR-CO-18");

            return;
        }

        foreach (VatBreakdownEntry entry in totals.VatBreakdown)
        {
            string label = $"Steuersatz {SharedRules.Format(entry.Rate)} Prozent, Kategorie {entry.Category.ToCode()}";

            // BR-CO-17: Steuerbetrag = Steuerbasis mal Satz, auf zwei Stellen.
            decimal expected = Amounts.Round(entry.TaxableAmount * entry.Rate / 100m);

            if (entry.TaxAmount != expected)
            {
                report.Error(
                    "APP-VAT-011",
                    $"Der Steuerbetrag fuer {label} passt nicht zur Steuerbasis.",
                    "VatBreakdown",
                    $"Erwartet {SharedRules.Format(expected)}, berechnet {SharedRules.Format(entry.TaxAmount)}",
                    "BR-CO-17");
            }

            if (!entry.Category.RequiresExemptionReason())
            {
                continue;
            }

            VatExemptionReason? reason = invoice.ExemptionReasons
                .FirstOrDefault(r => r.Category == entry.Category);

            if (reason is null || string.IsNullOrWhiteSpace(reason.Reason))
            {
                report.Error(
                    "APP-VAT-012",
                    $"Fuer die Steuerkategorie '{entry.Category.ToCode()}' fehlt die "
                    + "Begruendung der Steuerbefreiung. Ohne sie ist die Rechnung "
                    + "nicht normgerecht.",
                    "ExemptionReasons",
                    $"Betroffen: {label}", "BR-E-10");
            }
            else if (!string.IsNullOrWhiteSpace(reason.ReasonCode)
                     && !VatExemptionReasonCodes.IsValidOrKnownSubcode(reason.ReasonCode))
            {
                report.Warning(
                    "APP-VAT-013",
                    "Der angegebene Code fuer die Steuerbefreiung ist nicht bekannt. "
                    + "Der Begruendungstext wird trotzdem uebernommen.",
                    "ExemptionReasons",
                    $"Code: {reason.ReasonCode}");
            }
        }
    }

    // ------------------------------------------------------------------ Summen

}

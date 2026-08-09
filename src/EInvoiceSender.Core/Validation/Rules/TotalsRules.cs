using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zu den Dokumentsummen.
/// </summary>
internal static class TotalsRules
{
    public static void Validate(
        Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        // Die Summen werden hier gegen eine unabhängige Neuberechnung geprüft.
        // Das fängt den Fall ab, dass ein veraltetes Summenobjekt zu einer
        // inzwischen geänderten Rechnung gehört.
        InvoiceTotals recomputed = InvoiceCalculator.Calculate(invoice);

        CompareTotal(totals.LineTotal, recomputed.LineTotal,
            "APP-SUM-001", "Die Summe der Rechnungspositionen", "BR-CO-10", report);
        CompareTotal(totals.TaxBasisTotal, recomputed.TaxBasisTotal,
            "APP-SUM-002", "Die Nettosumme", "BR-CO-13", report);
        CompareTotal(totals.TaxTotal, recomputed.TaxTotal,
            "APP-SUM-003", "Der Gesamtbetrag der Umsatzsteuer", "BR-CO-14", report);
        CompareTotal(totals.GrandTotal, recomputed.GrandTotal,
            "APP-SUM-004", "Die Bruttosumme", "BR-CO-15", report);
        CompareTotal(totals.DuePayableAmount, recomputed.DuePayableAmount,
            "APP-SUM-005", "Der offene Zahlbetrag", "BR-CO-16", report);

        // BR-DEC-*: Betragsfelder dürfen höchstens zwei Nachkommastellen haben.
        foreach ((string label, decimal value, string field) in new[]
                 {
                     ("Summe der Positionen", totals.LineTotal, "LineTotal"),
                     ("Nettosumme", totals.TaxBasisTotal, "TaxBasisTotal"),
                     ("Gesamtsteuer", totals.TaxTotal, "TaxTotal"),
                     ("Bruttosumme", totals.GrandTotal, "GrandTotal"),
                     ("bereits gezahlter Betrag", totals.PaidAmount, "PaidAmount"),
                     ("Rundungsbetrag", totals.RoundingAmount, "RoundingAmount"),
                     ("offener Zahlbetrag", totals.DuePayableAmount, "DuePayableAmount"),
                 })
        {
            if (!Amounts.HasAtMostDecimals(value, Amounts.AmountDecimals))
            {
                report.Error(
                    "APP-SUM-006",
                    $"Der Betrag '{label}' hat mehr als zwei Nachkommastellen.",
                    field,
                    $"Gelesen: {value.ToString(CultureInfo.InvariantCulture)}", "BR-DEC-09");
            }
        }

        if (invoice.PaidAmount < 0m)
        {
            report.Error(
                "APP-SUM-007",
                "Der bereits gezahlte Betrag darf nicht negativ sein.",
                "PaidAmount");
        }
        else if (invoice.PaidAmount > totals.GrandTotal + SharedRules.ToleranceInCurrency)
        {
            report.Warning(
                "APP-SUM-008",
                "Der bereits gezahlte Betrag ist größer als die Bruttosumme. "
                + "Der offene Zahlbetrag wird dadurch negativ.",
                "PaidAmount",
                $"Gezahlt {SharedRules.Format(invoice.PaidAmount)}, Brutto {SharedRules.Format(totals.GrandTotal)}");
        }

        if (Math.Abs(totals.RoundingAmount) > 0.05m)
        {
            report.Warning(
                "APP-SUM-009",
                "Der Rundungsbetrag ist ungewöhnlich hoch. Üblich sind wenige Cent.",
                "RoundingAmount",
                $"Gelesen: {SharedRules.Format(totals.RoundingAmount)}");
        }

        // Eine Handelsrechnung mit negativem Zahlbetrag ist fast immer ein
        // Bedienfehler; gemeint ist dann die Rechnungsart 381.
        if (!invoice.TypeCode.IsCreditNote()
            && totals.DuePayableAmount < 0m
            && invoice.PaidAmount == 0m)
        {
            report.Error(
                "APP-SUM-010",
                "Der Zahlbetrag ist negativ, die Rechnungsart ist aber eine normale "
                + "Rechnung. Für eine Erstattung wählen Sie die Rechnungsart "
                + "'Gutschrift' (381).",
                "DuePayableAmount",
                $"Zahlbetrag {SharedRules.Format(totals.DuePayableAmount)}");
        }
    }

    public static void CompareTotal(
        decimal actual, decimal expected, string ruleId, string label, string normRule,
        ValidationReportBuilder report)
    {
        if (actual == expected)
        {
            return;
        }

        report.Error(
            ruleId,
            $"{label} stimmt nicht mit den erfassten Daten überein.",
            label,
            $"Erwartet {SharedRules.Format(expected)}, übergeben {SharedRules.Format(actual)}, "
            + $"Abweichung {SharedRules.Format(actual - expected)}",
            normRule);
    }

    // ---------------------------------------------------------------- Zahlung
}

using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zu den Rechnungspositionen.
/// </summary>
internal static class InvoiceLineRules
{
    public static void Validate(Invoice invoice, ValidationReportBuilder report)
    {
        if (invoice.Lines.Count == 0)
        {
            report.Error(
                "APP-LIN-001",
                "Die Rechnung enthält keine Positionen. Mindestens eine ist erforderlich.",
                "Lines", normRule: "BR-16");

            return;
        }

        var seenNumbers = new HashSet<int>();

        for (int index = 0; index < invoice.Lines.Count; index++)
        {
            InvoiceLine line = invoice.Lines[index];
            string field = $"Lines[{index}]";
            string label = $"Position {line.Number}";

            if (!seenNumbers.Add(line.Number))
            {
                report.Error(
                    "APP-LIN-002",
                    $"Die Positionsnummer {line.Number} kommt mehrfach vor. "
                    + "Jede Position braucht eine eigene Nummer.",
                    $"{field}.Number", normRule: "BR-21");
            }

            if (string.IsNullOrWhiteSpace(line.Name))
            {
                report.Error(
                    "APP-LIN-003",
                    $"{label} hat keine Bezeichnung.",
                    $"{field}.Name", normRule: "BR-25");
            }

            if (line.Quantity == 0m)
            {
                report.Error(
                    "APP-LIN-004",
                    $"{label} hat die Menge null.",
                    $"{field}.Quantity", normRule: "BR-22");
            }

            // Die Erzeugung wird weiterhin angehalten: Eine Einheit
            // durchzulassen, deren Bedeutung hier niemand geprüft hat, wäre
            // schlimmer als eine abgelehnte Rechnung.
            //
            // Der Befund trägt aber bewusst **keinen** Normverweis mehr.
            // Rec. 20/21 kennt mehrere hundert Codes; welche davon gültig
            // sind, entscheidet diese Anwendung nicht. Ein hier abgelehnter
            // Code kann normgerecht sein – er wird nur nicht unterstützt.
            if (!UnitCodeList.IsSupported(line.Unit.Value))
            {
                report.Error(
                    "APP-LIN-005",
                    $"Die Mengeneinheit '{line.Unit.Value}' in {label} wird von dieser "
                    + "Anwendung nicht unterstützt.",
                    $"{field}.Unit",
                    "Unterstützt sind die Codes der Auswahlliste; über die Gültigkeit nach "
                    + "UN/ECE-Empfehlung 20 und 21 trifft diese Prüfung keine Aussage.");
            }

            if (line.NetUnitPrice < 0m)
            {
                report.Error(
                    "APP-LIN-006",
                    $"Der Einzelpreis in {label} ist negativ. Verwenden Sie für "
                    + "Gutschriften die Rechnungsart 381 statt negativer Preise.",
                    $"{field}.NetUnitPrice", normRule: "BR-27");
            }

            if (line.PriceBaseQuantity <= 0m)
            {
                report.Error(
                    "APP-LIN-007",
                    $"Die Preisbasismenge in {label} muss größer als null sein.",
                    $"{field}.PriceBaseQuantity",
                    $"Gelesen: {SharedRules.Format(line.PriceBaseQuantity)}", "BR-DEC-24");
            }

            if (line.AllowanceAmount < 0m || line.ChargeAmount < 0m)
            {
                report.Error(
                    "APP-LIN-008",
                    $"Rabatt und Zuschlag in {label} müssen als positive Beträge "
                    + "angegeben werden.",
                    $"{field}.AllowanceAmount", normRule: "BR-31");
            }

            decimal lineNet = InvoiceCalculator.CalculateLineNetAmount(line);

            if (lineNet < 0m && line.NetUnitPrice >= 0m && line.Quantity > 0m)
            {
                report.Error(
                    "APP-LIN-009",
                    $"Der Rabatt in {label} ist größer als der Positionsbetrag.",
                    $"{field}.AllowanceAmount",
                    $"Positionsbetrag {SharedRules.Format(lineNet)}");
            }

            ValidateVatCategoryAndRate(
                line.VatCategory, line.VatRate, $"{field}.VatRate", label, report);

            DocumentRules.ValidatePeriod(
                line.ServicePeriodStart, line.ServicePeriodEnd,
                "APP-LIN-010", $"{field}.ServicePeriod", $"Der Leistungszeitraum in {label}", report);
        }
    }

    private static void ValidateVatCategoryAndRate(
        VatCategory category, decimal rate, string field, string label,
        ValidationReportBuilder report)
    {
        if (rate < 0m)
        {
            report.Error(
                "APP-VAT-001",
                $"Der Steuersatz in {label} ist negativ.",
                field, normRule: "BR-CO-17");

            return;
        }

        if (rate > 100m)
        {
            report.Error(
                "APP-VAT-002",
                $"Der Steuersatz in {label} ist größer als 100 Prozent.",
                field,
                $"Gelesen: {SharedRules.Format(rate)}");

            return;
        }

        if (category.RequiresPositiveRate() && rate <= 0m)
        {
            report.Error(
                "APP-VAT-003",
                $"{label} ist als regelbesteuert gekennzeichnet, hat aber den Steuersatz null. "
                + "Wählen Sie entweder einen Steuersatz größer null oder eine andere "
                + "Steuerkategorie.",
                field, normRule: "BR-S-05");
        }

        if (category == VatCategory.ZeroRated && rate != 0m)
        {
            report.Error(
                "APP-VAT-004",
                $"{label} ist als nullsatzbesteuert gekennzeichnet, hat aber einen "
                + "Steuersatz größer null.",
                field, normRule: "BR-Z-05");
        }

        if (category is VatCategory.Exempt or VatCategory.ReverseCharge
            or VatCategory.IntraCommunitySupply or VatCategory.ExportOutsideEu
            or VatCategory.OutsideScope
            && rate != 0m)
        {
            report.Error(
                "APP-VAT-005",
                $"{label} ist steuerbefreit, hat aber einen Steuersatz größer null.",
                field,
                $"Kategorie {category.ToCode()}, Satz {SharedRules.Format(rate)}", "BR-E-05");
        }
    }

}

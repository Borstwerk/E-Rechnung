using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Calculation;

/// <summary>
/// Berechnet alle abgeleiteten Beträge einer Rechnung nach EN 16931.
///
/// Die Reihenfolge der Rundungen ist bewusst festgelegt und darf nicht
/// verändert werden, ohne die Golden-Master-Tests neu zu bewerten:
/// zuerst jede Position auf zwei Nachkommastellen, dann die Steuerbasis je
/// Kategorie und Satz, danach der Steuerbetrag je Aufschlüsselung, zuletzt die
/// Gesamtsummen. Genau so rechnet auch das CEN-Schematron
/// (BR-CO-10, BR-CO-13 bis BR-CO-17).
///
/// Die Klasse ist zustandslos und wirft nicht: unplausible Eingaben (etwa eine
/// Preisbasismenge von null) werden hier neutral behandelt und von
/// <c>EInvoiceSender.Validation</c> als verständlicher Fehler gemeldet.
/// Ein Absturz während der Eingabe wäre für den Anwender wertlos.
/// </summary>
public static class InvoiceCalculator
{
    /// <summary>
    /// Berechnet den Nettobetrag einer einzelnen Position (BT-131).
    /// Formel nach EN 16931:
    /// <c>BT-131 = (BT-129 × BT-146 / BT-149) − BT-136 + BT-141</c>.
    /// Gerundet wird erst am Ende, damit sich Zwischenrundungen nicht
    /// aufaddieren.
    /// </summary>
    public static decimal CalculateLineNetAmount(InvoiceLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        // Eine Preisbasismenge von null oder kleiner ist fachlich unzulässig.
        // Statt hier zu werfen, rechnen wir mit 1 weiter; die Regelprüfung
        // meldet den Fehler mit Feldbezug, bevor irgendetwas erzeugt wird.
        decimal baseQuantity = line.PriceBaseQuantity > 0m ? line.PriceBaseQuantity : 1m;

        decimal gross = line.Quantity * line.NetUnitPrice / baseQuantity;
        decimal net = gross - line.AllowanceAmount + line.ChargeAmount;

        return Amounts.Round(net);
    }

    /// <summary>
    /// Berechnet das vollständige Summenbild der Rechnung.
    /// </summary>
    public static InvoiceTotals Calculate(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        // 1) Positionsnettobeträge (BT-131) und Summe (BT-106).
        decimal[] lineNetAmounts = [.. invoice.Lines.Select(CalculateLineNetAmount)];
        decimal lineTotal = Amounts.Round(lineNetAmounts.Sum());

        // 2) Nachlässe (BT-107) und Zuschläge (BT-108) auf Dokumentebene.
        decimal allowanceTotal = Amounts.Round(
            invoice.AllowancesAndCharges.Where(a => !a.IsCharge).Sum(a => a.Amount));
        decimal chargeTotal = Amounts.Round(
            invoice.AllowancesAndCharges.Where(a => a.IsCharge).Sum(a => a.Amount));

        // 3) Nettosumme (BT-109) nach BR-CO-13.
        decimal taxBasisTotal = Amounts.Round(lineTotal - allowanceTotal + chargeTotal);

        // 4) Steueraufschlüsselung (BG-23) nach BR-S-08 und BR-CO-17.
        var breakdown = BuildVatBreakdown(invoice, lineNetAmounts);

        // 5) Gesamtsteuer (BT-110) nach BR-CO-14.
        decimal taxTotal = Amounts.Round(breakdown.Sum(b => b.TaxAmount));

        // 6) Bruttosumme (BT-112) nach BR-CO-15.
        decimal grandTotal = Amounts.Round(taxBasisTotal + taxTotal);

        // 7) Offener Zahlbetrag (BT-115) nach BR-CO-16.
        decimal paid = Amounts.Round(invoice.PaidAmount);
        decimal rounding = Amounts.Round(invoice.RoundingAmount);
        decimal duePayable = Amounts.Round(grandTotal - paid + rounding);

        return new InvoiceTotals(
            LineTotal: lineTotal,
            AllowanceTotal: allowanceTotal,
            ChargeTotal: chargeTotal,
            TaxBasisTotal: taxBasisTotal,
            TaxTotal: taxTotal,
            GrandTotal: grandTotal,
            PaidAmount: paid,
            RoundingAmount: rounding,
            DuePayableAmount: duePayable,
            LineNetAmounts: lineNetAmounts,
            VatBreakdown: breakdown);
    }

    /// <summary>
    /// Bildet die Steueraufschlüsselung je Kombination aus Kategorie und Satz.
    ///
    /// BR-S-08: Die Steuerbasis einer Gruppe ist die Summe der zugehörigen
    /// Positionsnettobeträge zuzüglich der Zuschläge und abzüglich der
    /// Nachlässe mit derselben Kategorie und demselben Satz.
    /// BR-CO-17: Der Steuerbetrag ist die auf zwei Stellen gerundete Steuerbasis
    /// multipliziert mit dem Satz.
    ///
    /// Die Reihenfolge ist deterministisch (Kategoriecode, dann Satz), damit die
    /// erzeugte XML zwischen zwei Läufen byte-identisch bleibt.
    /// </summary>
    private static List<VatBreakdownEntry> BuildVatBreakdown(
        Invoice invoice,
        decimal[] lineNetAmounts)
    {
        var groups = new Dictionary<(VatCategory Category, decimal Rate), decimal>();

        for (int i = 0; i < invoice.Lines.Count; i++)
        {
            InvoiceLine line = invoice.Lines[i];
            var key = (line.VatCategory, NormalizeRate(line.VatRate));
            groups[key] = groups.GetValueOrDefault(key) + lineNetAmounts[i];
        }

        foreach (DocumentAllowanceCharge item in invoice.AllowancesAndCharges)
        {
            var key = (item.VatCategory, NormalizeRate(item.VatRate));
            decimal signedAmount = item.IsCharge ? item.Amount : -item.Amount;
            groups[key] = groups.GetValueOrDefault(key) + signedAmount;
        }

        return [.. groups
            .OrderBy(g => g.Key.Category.ToCode(), StringComparer.Ordinal)
            .ThenBy(g => g.Key.Rate)
            .Select(g =>
            {
                decimal taxableAmount = Amounts.Round(g.Value);
                decimal taxAmount = Amounts.Round(taxableAmount * g.Key.Rate / 100m);
                return new VatBreakdownEntry(g.Key.Category, g.Key.Rate, taxableAmount, taxAmount);
            })];
    }

    /// <summary>
    /// Vereinheitlicht den Steuersatz für die Gruppierung auf vier
    /// Nachkommastellen. Die unterschiedliche Schreibweise von 19 und 19,00
    /// stört dabei nicht: <c>decimal</c> vergleicht wertgleich und liefert für
    /// wertgleiche Zahlen denselben Hashcode, sodass beide in dieselbe
    /// Aufschlüsselung fallen.
    /// </summary>
    private static decimal NormalizeRate(decimal rate)
        => decimal.Round(rate, 4, MidpointRounding.AwayFromZero);
}

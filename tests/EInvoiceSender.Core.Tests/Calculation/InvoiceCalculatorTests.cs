using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Tests.TestData;
using Xunit;

namespace EInvoiceSender.Core.Tests.Calculation;

/// <summary>
/// Prüft den Berechnungskern gegen die Rechenregeln aus
/// docs/E-INVOICE-STANDARD.md, Abschnitt 3.
/// </summary>
public sealed class InvoiceCalculatorTests
{
    [Fact]
    public void EinfacheDienstleistungMit19Prozent()
    {
        Invoice invoice = TestInvoices.Create([TestInvoices.Line(1, quantity: 10m, netUnitPrice: 100m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(1000.00m, totals.LineTotal);
        Assert.Equal(1000.00m, totals.TaxBasisTotal);
        Assert.Equal(190.00m, totals.TaxTotal);
        Assert.Equal(1190.00m, totals.GrandTotal);
        Assert.Equal(1190.00m, totals.DuePayableAmount);

        VatBreakdownEntry entry = Assert.Single(totals.VatBreakdown);
        Assert.Equal(VatCategory.StandardRate, entry.Category);
        Assert.Equal(19m, entry.Rate);
        Assert.Equal(1000.00m, entry.TaxableAmount);
        Assert.Equal(190.00m, entry.TaxAmount);
    }

    [Fact]
    public void ErmäßigterSatzMit7Prozent()
    {
        // 3 x 33,33 = 99,99; Steuer = round(99,99 * 0,07) = round(6,9993) = 7,00
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 3m, netUnitPrice: 33.33m, vatRate: 7m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(99.99m, totals.LineTotal);
        Assert.Equal(7.00m, totals.TaxTotal);
        Assert.Equal(106.99m, totals.GrandTotal);
    }

    [Fact]
    public void MehrereSteuersätzeWerdenGetrenntAufgeschlüsselt()
    {
        Invoice invoice = TestInvoices.Create(
        [
            TestInvoices.Line(1, quantity: 1m, netUnitPrice: 100m, vatRate: 19m),
            TestInvoices.Line(2, quantity: 1m, netUnitPrice: 50m, vatRate: 7m),
        ]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(150.00m, totals.LineTotal);
        Assert.Equal(22.50m, totals.TaxTotal);
        Assert.Equal(172.50m, totals.GrandTotal);

        Assert.Equal(2, totals.VatBreakdown.Count);

        // Sortierung: gleiche Kategorie, aufsteigender Satz.
        Assert.Equal(7m, totals.VatBreakdown[0].Rate);
        Assert.Equal(50.00m, totals.VatBreakdown[0].TaxableAmount);
        Assert.Equal(3.50m, totals.VatBreakdown[0].TaxAmount);

        Assert.Equal(19m, totals.VatBreakdown[1].Rate);
        Assert.Equal(100.00m, totals.VatBreakdown[1].TaxableAmount);
        Assert.Equal(19.00m, totals.VatBreakdown[1].TaxAmount);
    }

    [Fact]
    public void SteuerfreiePositionErzeugtEigeneAufschlüsselungOhneSteuer()
    {
        Invoice invoice = TestInvoices.Create(
        [
            TestInvoices.Line(1, quantity: 1m, netUnitPrice: 100m, vatRate: 19m),
            TestInvoices.Line(2, quantity: 1m, netUnitPrice: 200m, vatRate: 0m, category: VatCategory.Exempt),
        ]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(300.00m, totals.LineTotal);
        Assert.Equal(19.00m, totals.TaxTotal);
        Assert.Equal(319.00m, totals.GrandTotal);

        Assert.Equal(2, totals.VatBreakdown.Count);

        // Sortierung nach Kategoriecode: 'E' vor 'S'.
        Assert.Equal(VatCategory.Exempt, totals.VatBreakdown[0].Category);
        Assert.Equal(200.00m, totals.VatBreakdown[0].TaxableAmount);
        Assert.Equal(0.00m, totals.VatBreakdown[0].TaxAmount);

        Assert.Equal(VatCategory.StandardRate, totals.VatBreakdown[1].Category);
    }

    [Fact]
    public void PositionsrabattMindertDenPositionsbetrag()
    {
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 2m, netUnitPrice: 100m, allowance: 20m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(180.00m, totals.LineTotal);
        Assert.Equal(34.20m, totals.TaxTotal);
        Assert.Equal(214.20m, totals.GrandTotal);
    }

    [Fact]
    public void PositionszuschlagErhöhtDenPositionsbetrag()
    {
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 2m, netUnitPrice: 100m, charge: 15m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(215.00m, totals.LineTotal);
        Assert.Equal(40.85m, totals.TaxTotal);
    }

    [Fact]
    public void RundungAufHalbemCentGehtVonDerNullWeg()
    {
        // 3 x 0,335 = 1,005 – exakt auf der Grenze.
        // Das CEN-Schematron rundet kaufmännisch, also auf 1,01.
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 3m, netUnitPrice: 0.335m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(1.01m, totals.LineTotal);
        Assert.Equal(0.19m, totals.TaxTotal);
        Assert.Equal(1.20m, totals.GrandTotal);
    }

    [Fact]
    public void NachlassUndZuschlagAufDokumentebene()
    {
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 10m, netUnitPrice: 100m)],
            allowancesAndCharges:
            [
                new DocumentAllowanceCharge(
                    IsCharge: false, Amount: 100m, Reason: "Treuerabatt",
                    VatCategory: VatCategory.StandardRate, VatRate: 19m),
                new DocumentAllowanceCharge(
                    IsCharge: true, Amount: 50m, Reason: "Versandkosten",
                    VatCategory: VatCategory.StandardRate, VatRate: 19m),
            ]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(1000.00m, totals.LineTotal);
        Assert.Equal(100.00m, totals.AllowanceTotal);
        Assert.Equal(50.00m, totals.ChargeTotal);
        Assert.Equal(950.00m, totals.TaxBasisTotal);

        // BR-S-08: Steuerbasis der Gruppe = Positionen + Zuschläge - Nachlässe
        VatBreakdownEntry entry = Assert.Single(totals.VatBreakdown);
        Assert.Equal(950.00m, entry.TaxableAmount);
        Assert.Equal(180.50m, entry.TaxAmount);

        Assert.Equal(1130.50m, totals.GrandTotal);
    }

    [Fact]
    public void AnzahlungMindertDenOffenenZahlbetrag()
    {
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 10m, netUnitPrice: 100m)],
            paidAmount: 500m);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(1190.00m, totals.GrandTotal);
        Assert.Equal(500.00m, totals.PaidAmount);
        Assert.Equal(690.00m, totals.DuePayableAmount);
    }

    [Fact]
    public void RundungsbetragWirdAufDenZahlbetragAngerechnet()
    {
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 1m, netUnitPrice: 99.99m)],
            roundingAmount: 0.02m);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        // 99,99 + 19,00 = 118,99; plus Rundung 0,02 = 119,01
        Assert.Equal(118.99m, totals.GrandTotal);
        Assert.Equal(0.02m, totals.RoundingAmount);
        Assert.Equal(119.01m, totals.DuePayableAmount);
    }

    [Fact]
    public void PreisbasismengeTeiltDenEinzelpreis()
    {
        // 1000 Stück zu 12,50 je 100 Stück = 125,00
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 1000m, netUnitPrice: 12.50m, priceBaseQuantity: 100m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(125.00m, totals.LineTotal);
    }

    [Fact]
    public void PreisbasismengeNullFührtNichtZumAbsturz()
    {
        // Fachlich unzulässig; die Regelprüfung meldet den Fehler.
        // Der Rechner darf dabei nicht werfen, sonst bricht die Eingabemaske ab.
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 2m, netUnitPrice: 50m, priceBaseQuantity: 0m)]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(100.00m, totals.LineTotal);
    }

    [Fact]
    public void GleicherSatzInUnterschiedlicherSchreibweiseErgibtEineAufschlüsselung()
    {
        // 19 und 19,00 sind wertgleich und müssen in dieselbe Gruppe fallen,
        // sonst entstehen zwei BG-23-Blöcke und die Datei wird abgelehnt.
        Invoice invoice = TestInvoices.Create(
        [
            TestInvoices.Line(1, quantity: 1m, netUnitPrice: 100m, vatRate: 19m),
            TestInvoices.Line(2, quantity: 1m, netUnitPrice: 100m, vatRate: 19.00m),
        ]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        VatBreakdownEntry entry = Assert.Single(totals.VatBreakdown);
        Assert.Equal(200.00m, entry.TaxableAmount);
        Assert.Equal(38.00m, entry.TaxAmount);
    }

    [Fact]
    public void PositionsbeträgeWerdenInDerReihenfolgeDerPositionenGeliefert()
    {
        Invoice invoice = TestInvoices.Create(
        [
            TestInvoices.Line(1, quantity: 1m, netUnitPrice: 10m),
            TestInvoices.Line(2, quantity: 2m, netUnitPrice: 20m),
            TestInvoices.Line(3, quantity: 3m, netUnitPrice: 30m),
        ]);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal([10.00m, 40.00m, 90.00m], totals.LineNetAmounts);
    }

    [Fact]
    public void GutschriftRechnetMitPositivenBeträgen()
    {
        // Bei Rechnungsart 381 bleiben die Beträge positiv; die Gutschrift
        // ergibt sich aus dem Dokumenttyp, nicht aus dem Vorzeichen.
        Invoice invoice = TestInvoices.Create(
            [TestInvoices.Line(1, quantity: 1m, netUnitPrice: 250m)],
            typeCode: InvoiceTypeCode.CreditNote);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(250.00m, totals.LineTotal);
        Assert.Equal(297.50m, totals.GrandTotal);
        Assert.True(totals.DuePayableAmount > 0m);
    }

    [Fact]
    public void BerechnungIstWiederholbar()
    {
        Invoice invoice = TestInvoices.Create(
        [
            TestInvoices.Line(1, quantity: 7m, netUnitPrice: 13.37m, vatRate: 19m),
            TestInvoices.Line(2, quantity: 3m, netUnitPrice: 4.99m, vatRate: 7m),
        ]);

        InvoiceTotals first = InvoiceCalculator.Calculate(invoice);
        InvoiceTotals second = InvoiceCalculator.Calculate(invoice);

        // Bewusst feldweise verglichen: InvoiceTotals ist ein record mit
        // Listenmitgliedern, und record-Gleichheit vergleicht Listen über die
        // Referenz. Ein Vergleich der beiden Objekte selbst würde also auch
        // dann fehlschlagen, wenn alle Werte übereinstimmen.
        Assert.Equal(first.LineTotal, second.LineTotal);
        Assert.Equal(first.TaxBasisTotal, second.TaxBasisTotal);
        Assert.Equal(first.TaxTotal, second.TaxTotal);
        Assert.Equal(first.GrandTotal, second.GrandTotal);
        Assert.Equal(first.DuePayableAmount, second.DuePayableAmount);
        Assert.Equal(first.LineNetAmounts, second.LineNetAmounts);
        Assert.Equal(first.VatBreakdown, second.VatBreakdown);
    }

    /// <summary>
    /// Prüft an mehreren Rechnungen, dass die Summenregeln der Norm in sich
    /// stimmig sind. Damit ist jede künftige Änderung am Rechenweg sofort
    /// sichtbar, auch wenn sie in keinem der Einzelfälle auffällt.
    /// </summary>
    [Theory]
    [MemberData(nameof(Konsistenzfälle))]
    public void SummenregelnSindInSichStimmig(Invoice invoice)
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        // BR-CO-10
        Assert.Equal(Amounts.Round(totals.LineNetAmounts.Sum()), totals.LineTotal);

        // BR-CO-13
        Assert.Equal(
            Amounts.Round(totals.LineTotal - totals.AllowanceTotal + totals.ChargeTotal),
            totals.TaxBasisTotal);

        // BR-CO-14
        Assert.Equal(Amounts.Round(totals.VatBreakdown.Sum(v => v.TaxAmount)), totals.TaxTotal);

        // BR-CO-15
        Assert.Equal(Amounts.Round(totals.TaxBasisTotal + totals.TaxTotal), totals.GrandTotal);

        // BR-CO-16
        Assert.Equal(
            Amounts.Round(totals.GrandTotal - totals.PaidAmount + totals.RoundingAmount),
            totals.DuePayableAmount);

        // BR-CO-17 je Aufschlüsselung
        foreach (VatBreakdownEntry entry in totals.VatBreakdown)
        {
            Assert.Equal(Amounts.Round(entry.TaxableAmount * entry.Rate / 100m), entry.TaxAmount);
        }

        // BR-DEC-*: höchstens zwei Nachkommastellen in allen Betragsfeldern
        Assert.True(Amounts.HasAtMostDecimals(totals.LineTotal, 2));
        Assert.True(Amounts.HasAtMostDecimals(totals.TaxBasisTotal, 2));
        Assert.True(Amounts.HasAtMostDecimals(totals.TaxTotal, 2));
        Assert.True(Amounts.HasAtMostDecimals(totals.GrandTotal, 2));
        Assert.True(Amounts.HasAtMostDecimals(totals.DuePayableAmount, 2));
    }

    public static TheoryData<Invoice> Konsistenzfälle()
    {
        var data = new TheoryData<Invoice>
        {
            TestInvoices.Create([TestInvoices.Line(1, 1m, 100m)]),
            TestInvoices.Create([TestInvoices.Line(1, 3m, 33.33m, vatRate: 7m)]),
            TestInvoices.Create(
            [
                TestInvoices.Line(1, 1m, 100m, vatRate: 19m),
                TestInvoices.Line(2, 2m, 49.99m, vatRate: 7m),
                TestInvoices.Line(3, 1m, 75m, vatRate: 0m, category: VatCategory.ReverseCharge),
            ]),
            TestInvoices.Create(
                [TestInvoices.Line(1, 17m, 3.79m, allowance: 4.13m, charge: 1.07m)]),
            TestInvoices.Create(
                [TestInvoices.Line(1, 10m, 100m)],
                allowancesAndCharges:
                [
                    new DocumentAllowanceCharge(false, 33.33m, "Rabatt", VatCategory.StandardRate, 19m),
                    new DocumentAllowanceCharge(true, 12.95m, "Porto", VatCategory.StandardRate, 19m),
                ],
                paidAmount: 250m,
                roundingAmount: 0.01m),
            TestInvoices.Create([TestInvoices.Line(1, 1000m, 0.001m)]),
        };

        return data;
    }
}

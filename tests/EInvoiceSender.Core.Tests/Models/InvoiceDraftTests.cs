using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Prueft das Eingabeformular.
///
/// Der Entwurf haelt jedes Feld als Zeichenkette, damit eine halb getippte
/// Eingabe die Oberflaeche nicht blockiert. Erst
/// <see cref="InvoiceDraft.TryBuildInvoice"/> macht daraus eine Rechnung – und
/// meldet jeden unlesbaren Wert als verstaendlichen Befund statt mit einer
/// Ausnahme.
/// </summary>
public sealed class InvoiceDraftTests
{
    [Fact]
    public void VollstaendigerEntwurfWirdZuEinerRechnung()
    {
        InvoiceDraft draft = FilledDraft();

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);
        Assert.Equal("RE-2026-0001", invoice.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 3, 15), invoice.IssueDate);
        Assert.Single(invoice.Lines);
    }

    [Fact]
    public void SummenWerdenAusDenPositionenBerechnet()
    {
        InvoiceDraft draft = FilledDraft();
        draft.TryBuildInvoice(out Invoice? invoice);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice!);

        Assert.Equal(1000.00m, totals.LineTotal);
        Assert.Equal(190.00m, totals.TaxTotal);
        Assert.Equal(1190.00m, totals.GrandTotal);
    }

    [Fact]
    public void UnlesbareZahlErzeugtEinenVerstaendlichenBefundStattEinerAusnahme()
    {
        InvoiceDraft draft = FilledDraft();
        draft.Lines[0].NetUnitPrice = "hundert Euro";

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.Null(invoice);
        Assert.True(report.HasErrors);
        Assert.Contains(
            report.Findings,
            f => f.Message.Contains("keine gueltige Zahl", StringComparison.Ordinal));
    }

    /// <summary>
    /// Arbeitsteilung: Der Entwurf meldet nur, was er nicht **lesen** kann. Ob
    /// eine Pflichtangabe **fehlt**, entscheidet das Regelwerk EN 16931. Eine
    /// leere Rechnungsnummer laesst sich lesen – sie ist eben leer – und wird
    /// deshalb erst dort beanstandet.
    /// </summary>
    [Fact]
    public void FehlendeRechnungsnummerBeanstandetDasRegelwerk()
    {
        InvoiceDraft draft = FilledDraft();
        draft.InvoiceNumber = "   ";

        ValidationReport buildReport = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(buildReport.HasErrors, "Eine leere Zeichenkette ist lesbar.");
        Assert.NotNull(invoice);

        ValidationReport ruleReport = new En16931RuleValidator()
            .Validate(invoice, InvoiceCalculator.Calculate(invoice));

        Assert.True(ruleReport.HasErrors);
        Assert.Contains(ruleReport.Findings, f => f.NormRule is "BR-02");
    }

    [Fact]
    public void PositionenLassenSichHinzufuegenUndNeuNummerieren()
    {
        var draft = new InvoiceDraft();

        draft.AddLine();
        draft.AddLine();
        draft.AddLine();

        Assert.Equal([1, 2, 3], draft.Lines.Select(l => l.Number));

        draft.Lines.RemoveAt(1);
        draft.RenumberLines();

        Assert.Equal([1, 2], draft.Lines.Select(l => l.Number));
    }


    public static InvoiceDraft FilledDraft()
    {
        var draft = new InvoiceDraft
        {
            InvoiceNumber = "RE-2026-0001",
            IssueDate = new DateOnly(2026, 3, 15),
            DueDate = new DateOnly(2026, 3, 29),
            SellerName = "Musterbetrieb Beispiel GmbH",
            SellerStreet = "Beispielweg 1",
            SellerPostalCode = "10115",
            SellerCity = "Berlin",
            SellerVatId = "DE123456789",
            SellerEmail = "rechnung@example.invalid",
            BuyerName = "Beispielkunde AG",
            BuyerStreet = "Kundenstrasse 7",
            BuyerPostalCode = "20095",
            BuyerCity = "Hamburg",
            BuyerCountry = "DE",
            BuyerEmail = "einkauf@example.invalid",
            BankAccountHolder = "Musterbetrieb Beispiel GmbH",
            BankIban = "DE89370400440532013000",
            PaymentTerms = "Zahlbar innerhalb von 14 Tagen.",
        };

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Beratungsleistung";
        line.Quantity = "10";
        line.Unit = "HUR";
        line.NetUnitPrice = "100,00";
        line.VatRate = "19";

        return draft;
    }

    private static string Describe(ValidationReport report)
        => string.Join(" | ", report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));
}

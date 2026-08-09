using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Prüft die Summen des Eingabeformulars.
///
/// **Der Fehler aus dem manuellen Test:** Der Anwender trug drei Positionen
/// ein, drückte „Summen neu berechnen“ – und sah nichts. Erst „Weiter“ liess
/// die Zahlen erscheinen.
///
/// Die Ursache lag nicht an der Anzeige: Die Berechnung lief über den Bau
/// einer **vollständigen** Rechnung. Solange Rechnungsnummer, Käufer oder
/// Käuferland fehlten, kam gar kein Ergebnis heraus, und die Anzeige blieb
/// leer. „Weiter“ half nur, weil der Anwender bis dahin auch den Rest
/// ausgefüllt hatte.
///
/// Die Summen hängen aber allein an den Positionen. Genau das prüfen diese
/// Tests.
/// </summary>
public sealed class DraftTotalsTests
{
    /// <summary>Der Testfall aus dem Fehlerbericht, Zahl für Zahl.</summary>
    [Fact]
    public void DreiPositionenErgebenDieErwartetenSummen()
    {
        InvoiceDraft draft = DraftWithThreeLines();

        InvoiceTotals totals = Assert.IsType<InvoiceTotals>(draft.TryCalculateTotals());

        Assert.Equal(600.00m, totals.TaxBasisTotal);
        Assert.Equal(114.00m, totals.TaxTotal);
        Assert.Equal(714.00m, totals.GrandTotal);
        Assert.Equal(714.00m, totals.DuePayableAmount);
    }

    /// <summary>
    /// Der Kern der Sache: Die Summen entstehen, obwohl Rechnungsnummer,
    /// Verkäufer, Käufer und Käuferland fehlen. Vorher war genau das der Grund
    /// für die leere Anzeige.
    /// </summary>
    [Fact]
    public void DieSummenEntstehenAuchOhneDieÜbrigenAngaben()
    {
        InvoiceDraft draft = DraftWithThreeLines();

        Assert.Empty(draft.InvoiceNumber);
        Assert.Empty(draft.BuyerName);
        Assert.Empty(draft.BuyerCountry);
        Assert.True(draft.TryBuildInvoice(out _).HasErrors, "Die Rechnung ist absichtlich unvollständig.");

        Assert.NotNull(draft.TryCalculateTotals());
    }

    /// <summary>
    /// Die Positionsnettobeträge werden mitgeliefert – der Anwender muss sie
    /// nicht ausrechnen.
    /// </summary>
    [Fact]
    public void DiePositionsnettobeträgeWerdenBerechnet()
    {
        InvoiceTotals totals = Assert.IsType<InvoiceTotals>(DraftWithThreeLines().TryCalculateTotals());

        Assert.Equal([340.00m, 190.00m, 70.00m], totals.LineNetAmounts);
    }

    /// <summary>
    /// „Weiter“ darf an einer bereits angezeigten Summe nichts mehr ändern.
    /// Anzeige und Freigabe müssen aus demselben Formularstand dieselbe Zahl
    /// bekommen.
    /// </summary>
    [Fact]
    public void AnzeigeUndFreigabeRechnenGleich()
    {
        InvoiceDraft draft = CompleteDraft();

        InvoiceTotals angezeigt = Assert.IsType<InvoiceTotals>(draft.TryCalculateTotals());

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, "Der Entwurf sollte vollständig sein.");

        InvoiceTotals freigegeben = InvoiceCalculator.Calculate(invoice!);

        // Verglichen werden die Beträge. Der Datensatz selbst trägt ein Feld
        // mit einer Liste; zwei gleiche Listen sind für seine Gleichheit
        // trotzdem verschieden.
        Assert.Equal(freigegeben.TaxBasisTotal, angezeigt.TaxBasisTotal);
        Assert.Equal(freigegeben.TaxTotal, angezeigt.TaxTotal);
        Assert.Equal(freigegeben.GrandTotal, angezeigt.GrandTotal);
        Assert.Equal(freigegeben.DuePayableAmount, angezeigt.DuePayableAmount);
        Assert.Equal(freigegeben.LineNetAmounts, angezeigt.LineNetAmounts);
    }

    /// <summary>
    /// Ohne Positionen gibt es nichts zu rechnen – aber auch keinen Grund, die
    /// Anzeige leer zu lassen. Null Euro ist hier die richtige Antwort.
    /// </summary>
    [Fact]
    public void OhnePositionenStehenNullen()
    {
        InvoiceTotals totals = Assert.IsType<InvoiceTotals>(new InvoiceDraft().TryCalculateTotals());

        Assert.Equal(0m, totals.GrandTotal);
    }

    /// <summary>
    /// Eine halb ausgefüllte Position liefert keine Summe. Eine falsche wäre
    /// schlimmer als keine.
    /// </summary>
    [Theory]
    [InlineData("", "85,00", "19")]
    [InlineData("4", "", "19")]
    [InlineData("4", "85,00", "")]
    [InlineData("vier", "85,00", "19")]
    public void EineUnvollständigePositionLiefertKeineSumme(string quantity, string price, string vatRate)
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft line = draft.AddLine();

        line.Name = "Systemadministration";
        line.Quantity = quantity;
        line.NetUnitPrice = price;
        line.VatRate = vatRate;

        Assert.Null(draft.TryCalculateTotals());
    }

    /// <summary>
    /// Ein Steuersatz von null ist etwas anderes als kein Steuersatz. Wird die
    /// Null eingetragen, entsteht eine Summe.
    /// </summary>
    [Fact]
    public void EinAusdrücklichEingetragenerNullsatzIstEinWert()
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft line = draft.AddLine();

        line.Name = "Innergemeinschaftliche Lieferung";
        line.Quantity = "1";
        line.NetUnitPrice = "100,00";
        line.VatRate = "0";
        line.VatCategory = VatCategory.IntraCommunitySupply;

        InvoiceTotals totals = Assert.IsType<InvoiceTotals>(draft.TryCalculateTotals());

        Assert.Equal(100.00m, totals.TaxBasisTotal);
        Assert.Equal(0m, totals.TaxTotal);
    }

    /// <summary>Die drei Positionen aus dem Fehlerbericht.</summary>
    private static InvoiceDraft DraftWithThreeLines()
    {
        var draft = new InvoiceDraft();

        Add(draft, "Systemadministration", "4", "85,00");
        Add(draft, "Netzwerkkonfiguration", "2", "95,00");
        Add(draft, "Technische Dokumentation", "1", "70,00");

        return draft;
    }

    private static void Add(InvoiceDraft draft, string name, string quantity, string price)
    {
        InvoiceLineDraft line = draft.AddLine();

        line.Name = name;
        line.Quantity = quantity;
        line.NetUnitPrice = price;
        line.VatRate = "19";
    }

    /// <summary>Derselbe Entwurf, aber mit allen übrigen Pflichtangaben.</summary>
    private static InvoiceDraft CompleteDraft()
    {
        InvoiceDraft draft = DraftWithThreeLines();

        draft.InvoiceNumber = "RE-2026-0815";
        draft.SellerName = "Muster IT GmbH";
        draft.SellerStreet = "Musterstraße 10";
        draft.SellerPostalCode = "18055";
        draft.SellerCity = "Rostock";
        draft.SellerVatId = "DE123456789";
        draft.BuyerName = "Nordlicht Handel GmbH";
        draft.BuyerStreet = "Hafenstraße 22";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerCountry = "DE";
        draft.PaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.";

        return draft;
    }
}

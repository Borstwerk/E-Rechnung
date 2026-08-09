using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Prueft, dass ein unbekanntes Kaeuferland unbekannt bleibt.
///
/// **Warum das zaehlt:** Ein still auf "DE" gesetztes Kaeuferland erzeugt bei
/// einem oesterreichischen oder niederlaendischen Kunden eine formal gueltige,
/// inhaltlich falsche Rechnung – ohne dass irgendwo eine Warnung erschiene.
/// Das Land steuert unter anderem, ob eine innergemeinschaftliche Lieferung
/// vorliegt.
/// </summary>
public sealed class BuyerCountryTests
{
    [Theory]
    [InlineData("DE")]
    [InlineData("AT")]
    [InlineData("NL")]
    public void EinAngegebenesKaeuferlandWirdUebernommen(string country)
    {
        InvoiceDraft draft = InvoiceDraftTests.FilledDraft();
        draft.BuyerCountry = country;

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);
        Assert.Equal(country, invoice.Buyer.Address.Country.Value);
    }

    /// <summary>
    /// Der eigentliche Punkt: Ohne Angabe gibt es kein Land – und die Pruefung
    /// sagt das, statt Deutschland anzunehmen.
    /// </summary>
    [Fact]
    public void EinUnbekanntesKaeuferlandWirdBeanstandetStattAngenommen()
    {
        InvoiceDraft draft = InvoiceDraftTests.FilledDraft();
        draft.BuyerCountry = string.Empty;

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.Null(invoice);
        Assert.True(report.HasErrors);
        ValidationFinding finding = Assert.Single(
            report.Findings, f => f.FieldPath == "Buyer.Country");

        Assert.Contains("fehlt", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein neues Formular hat kein Kaeuferland. Frueher stand dort "DE", ohne
    /// dass jemand es behauptet haette.
    /// </summary>
    [Fact]
    public void EinNeuesFormularHatKeinKaeuferland()
        => Assert.Equal(string.Empty, new InvoiceDraft().BuyerCountry);

    /// <summary>
    /// Beim eigenen Land ist eine Vorgabe vertretbar: Der Anwender stellt seine
    /// eigenen Rechnungen aus und aendert den Wert einmalig ueber die Vorlage.
    /// </summary>
    [Fact]
    public void DasEigeneLandDarfVorbelegtSeinUndGiltAlsProgrammstandard()
    {
        var draft = new InvoiceDraft();

        Assert.Equal("DE", draft.SellerCountry);
        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.SellerCountry)));
    }

    private static string Describe(ValidationReport report)
        => string.Join(" | ", report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));
}

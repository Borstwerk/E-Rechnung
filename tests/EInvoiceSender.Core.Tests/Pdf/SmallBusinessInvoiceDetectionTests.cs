using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Regressionen für ER-020-DET-02. Die frei positionierte Test-PDF bildet die
/// relevanten Spalten, Abstände und Segmentgrenzen der anonymisierten
/// Referenzrechnung nach, ohne die Originaldatei ins Repository zu übernehmen.
/// </summary>
public sealed class SmallBusinessInvoiceDetectionTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task AnonymisierteReferenzstrukturWirdKonservativErkannt()
    {
        InvoiceDetectionResult result = await Detect(ReferenceInvoice());

        Assert.Equal("04-2026", result.InvoiceNumber?.Value);
        Assert.Equal(DetectionConfidence.High, result.InvoiceNumber?.Confidence);
        Assert.Equal(new DateOnly(2026, 6, 9), result.IssueDate?.Value);
        Assert.Equal(DetectionConfidence.Medium, result.IssueDate?.Confidence);
        Assert.Null(result.DeliveryDate);
        Assert.Equal(new DateOnly(2026, 4, 1), result.BillingPeriodStart?.Value);
        Assert.Equal(new DateOnly(2026, 4, 7), result.BillingPeriodEnd?.Value);
        Assert.Equal("EUR", result.Currency?.Value);

        Assert.Equal("Klaus Muster", result.Seller.Name?.Value);
        Assert.Equal("Musterstraße 1", result.Seller.Street?.Value);
        Assert.Equal("12345", result.Seller.PostalCode?.Value);
        Assert.Equal("Muster", result.Seller.City?.Value);
        Assert.Equal("012 / 345 / 678911", result.Seller.TaxNumber?.Value);
        Assert.Equal("ersteller@muster.com", result.Seller.Email?.Value);

        Assert.Equal("Musterkunde GmbH & Co. KG", result.Buyer.Name?.Value);
        Assert.Equal("Musterstraße 1", result.Buyer.Street?.Value);
        Assert.Equal("17438", result.Buyer.PostalCode?.Value);
        Assert.Equal("Musterhausen", result.Buyer.City?.Value);
        Assert.Null(result.Buyer.Country);
        Assert.Null(result.Buyer.VatId);
        Assert.Null(result.Buyer.Email);

        Assert.Equal(1020.00m, result.Totals.Net?.Value);
        Assert.Equal(193.80m, result.Totals.Tax?.Value);
        Assert.Equal(1213.80m, result.Totals.Gross?.Value);
        Assert.Null(result.Totals.Payable);
        Assert.Contains(result.Totals.VatRates, rate => rate.Value == 19m);

        Assert.Null(result.Iban);
        Assert.Equal("COBADEFFXXX", result.Bic?.Value);
        DetectedOwnCompanyProposal proposal = Assert.IsType<DetectedOwnCompanyProposal>(
            result.OwnCompanyProposal);
        Assert.Null(proposal.Field(DetectedOwnCompanyFieldKind.BankIban));
        Assert.Null(proposal.Field(DetectedOwnCompanyFieldKind.BankBic));

        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, result);

        Assert.Equal(new DateOnly(2026, 4, 1), draft.BillingPeriodStart);
        Assert.Equal(new DateOnly(2026, 4, 7), draft.BillingPeriodEnd);
        Assert.Equal(FieldOrigin.DetectedReliably,
            draft.OriginOf(nameof(draft.BillingPeriodStart)));
        Assert.Equal(FieldOrigin.DetectedReliably,
            draft.OriginOf(nameof(draft.BillingPeriodEnd)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.IssueDate)));

        Assert.Equal("Klaus Muster", draft.SellerName);
        Assert.Equal("Musterstraße 1", draft.SellerStreet);
        Assert.Equal("12345", draft.SellerPostalCode);
        Assert.Equal("Muster", draft.SellerCity);
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.SellerName)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.SellerStreet)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.SellerPostalCode)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.SellerCity)));

        Assert.Equal("Musterkunde GmbH & Co. KG", draft.BuyerName);
        Assert.Equal("Musterstraße 1", draft.BuyerStreet);
        Assert.Equal("17438", draft.BuyerPostalCode);
        Assert.Equal("Musterhausen", draft.BuyerCity);
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerName)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerStreet)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerPostalCode)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerCity)));
        Assert.NotEqual(draft.SellerName, draft.BuyerName);
        Assert.NotEqual(draft.SellerPostalCode, draft.BuyerPostalCode);
    }

    [Theory]
    [InlineData("RECHNUNG 04-2026", "04-2026")]
    [InlineData("Rechnung 04/2026", "04/2026")]
    [InlineData("Rechnung RE-2026-004", "RE-2026-004")]
    public async Task EindeutigeRechnungsüberschriftLiefertReferenz(
        string heading, string expected)
    {
        InvoiceDetectionResult result = await Detect(SimpleDocument(heading));

        Assert.Equal(expected, result.InvoiceNumber?.Value);
        Assert.Equal(DetectionConfidence.High, result.InvoiceNumber?.Confidence);
    }

    [Fact]
    public async Task AlleinstehendesRechnungUndMehrereReferenzenBleibenLeer()
    {
        InvoiceDetectionResult alone = await Detect(SimpleDocument("RECHNUNG"));
        InvoiceDetectionResult ambiguous = await Detect(SimpleDocument(
            "RECHNUNG 04-2026", "RECHNUNG RE-2026-005"));

        Assert.Null(alone.InvoiceNumber);
        Assert.Null(ambiguous.InvoiceNumber);
    }

    [Fact]
    public async Task KlassischeUndÜberschriftReferenzWerdenGemeinsamDedupliziert()
    {
        InvoiceDetectionResult same = await Detect(SimpleDocument(
            "Rechnungsnummer: 04-2026", "RECHNUNG 04-2026"));
        InvoiceDetectionResult conflicting = await Detect(SimpleDocument(
            "Rechnungsnummer: 04-2026", "RECHNUNG RE-2026-005"));

        Assert.Equal("04-2026", same.InvoiceNumber?.Value);
        Assert.Equal(DetectionConfidence.High, same.InvoiceNumber?.Confidence);
        Assert.Null(conflicting.InvoiceNumber);
    }

    [Fact]
    public async Task EinzelpreiskontextSchlägtAuchStarkesLabelUndSchlichtesNettoBleibtErkennbar()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("RECHNUNG 04-2026", 100, 100),
            P("Nettobetrag je Arbeitsstunde 40,00 EUR", 100, 300),
            P("Netto 1.020,00 EUR", 100, 500),
            P("Ausführliche technische Leistungsbeschreibung für ausreichend auswertbaren PDF-Text.", 100, 600),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal(1020.00m, result.Totals.Net?.Value);
        Assert.Equal(DetectionConfidence.Medium, result.Totals.Net?.Confidence);
    }

    [Fact]
    public async Task IsoliertesSchlichtesNettoBleibtEinUnsichererSummenkandidat()
    {
        byte[] pdf = TextPdfBuilder.Create(
        [
            "RECHNUNG 04-2026",
            "Netto 1.020,00 EUR",
            "Umsatzsteuer 19 % 193,80 EUR",
            "Gesamt Brutto 1.213,80 EUR",
            "Ausführliche technische Leistungsbeschreibung für ausreichend auswertbaren PDF-Text.",
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal(1020.00m, result.Totals.Net?.Value);
        Assert.Equal(DetectionConfidence.Medium, result.Totals.Net?.Confidence);
    }

    [Fact]
    public async Task GleichStarkeWidersprüchlicheNettoangabenLeerenNurNetto()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("RECHNUNG 04-2026", 100, 100),
            P("Gesamt Netto 100,00 EUR", 100, 250),
            P("Gesamt Netto 200,00 EUR", 100, 350),
            P("Umsatzsteuer 19 % 19,00 EUR", 100, 500),
            P("Gesamt Brutto 119,00 EUR", 100, 515),
            P("Ausführliche technische Leistungsbeschreibung für ausreichend auswertbaren PDF-Text.", 100, 600),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Null(result.Totals.Net);
        Assert.Equal(19.00m, result.Totals.Tax?.Value);
        Assert.Equal(119.00m, result.Totals.Gross?.Value);
    }

    [Fact]
    public async Task KohärenterSummenblockLöstSemantischenGleichstandAuf()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("RECHNUNG 04-2026", 100, 100),
            P("Gesamt Netto 100,00 EUR", 100, 300),
            P("Umsatzsteuer 19 % 19,00 EUR", 100, 315),
            P("Gesamt Brutto 119,00 EUR", 100, 330),
            P("Gesamt Netto 200,00 EUR", 100, 500),
            P("Ausführliche technische Leistungsbeschreibung für ausreichend auswertbaren PDF-Text.", 100, 600),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal(100.00m, result.Totals.Net?.Value);
        Assert.Equal(19.00m, result.Totals.Tax?.Value);
        Assert.Equal(119.00m, result.Totals.Gross?.Value);
    }

    [Fact]
    public async Task ExpliziterBuyerankerHatVorrangVorAnkerlosemBlock()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            .. AnchorlessAddress("Fensterkunde GmbH", "Fensterstraße 1", "10115 Berlin", 100),
            P("RECHNUNG 04-2026", 100, 220),
            P("Rechnung an", 320, 250),
            P("Explizitkunde AG", 320, 265),
            P("Kundenweg 7", 320, 280),
            P("20095 Hamburg", 320, 295),
            P("Nettobetrag 100,00 EUR", 100, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 100, 415),
            P("Gesamtbetrag 119,00 EUR", 100, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Explizitkunde AG", result.Buyer.Name?.Value);
        Assert.Equal("Hamburg", result.Buyer.City?.Value);
    }

    [Fact]
    public async Task ZweiPlausibleAnkerloseEmpfängerBleibenMehrdeutig()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            .. AnchorlessAddress("Erster Kunde GmbH", "Erster Weg 1", "10115 Berlin", 70),
            .. AnchorlessAddress("Zweiter Kunde AG", "Zweiter Weg 2", "20095 Hamburg", 150),
            P("RECHNUNG 04-2026", 105, 250),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Buyer.HasAnything);
    }

    [Fact]
    public async Task ZweiIdentitätenImSelbenAnkerlosenAdressblockBleibenLeer()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Erster Kunde GmbH", 105, 100),
            P("Zweiter Kunde AG", 105, 115),
            P("Kundenweg 2", 105, 130),
            P("20095 Hamburg", 105, 145),
            P("RECHNUNG 04-2026", 105, 220),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Buyer.HasAnything);
    }

    [Fact]
    public async Task LieferregionWirdVomAnkerlosenBuyerFallbackAusgeschlossen()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Lieferanschrift", 105, 60),
            P("Lager Nord", 105, 75),
            P("Lagerweg 9", 105, 90),
            P("18055 Rostock", 105, 105),
            .. AnchorlessAddress("Rechnungskunde GmbH", "Kundenweg 2", "20095 Hamburg", 150),
            P("RECHNUNG 04-2026", 105, 240),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Rechnungskunde GmbH", result.Buyer.Name?.Value);
        Assert.Equal("Hamburg", result.Buyer.City?.Value);
        Assert.NotEqual("Lager Nord", result.Buyer.Name?.Value);
    }

    [Fact]
    public async Task LeistungsortWirdNieZumAnkerlosenBuyer()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Leistungsort:", 105, 70),
            P("Baustelle GmbH", 105, 85),
            P("Baustellenweg 1", 105, 100),
            P("12345 Baustadt", 105, 115),
            P("RECHNUNG 04-2026", 105, 200),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Buyer.HasAnything);
    }

    [Fact]
    public async Task UnbeschrifteterSellerkopfWirdNichtDurchBuyerFallbackVerdrängt()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Klaus Muster", 105, 60),
            P("Musterstraße 1", 105, 75),
            P("12345 Muster", 105, 90),
            P("Steuernummer: 012 / 345 / 678911", 105, 120),
            P("RECHNUNG 04-2026", 105, 200),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Klaus Muster", result.Seller.Name?.Value);
        Assert.Equal("12345", result.Seller.PostalCode?.Value);
        Assert.False(result.Buyer.HasAnything);
    }

    [Fact]
    public async Task SellerUndAnkerloserBuyerWerdenUnabhängigErkannt()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Klaus Muster", 350, 30),
            P("Musterstraße 1", 350, 45),
            P("12345 Muster", 350, 60),
            P("Steuernummer: 012 / 345 / 678911", 350, 85),
            .. AnchorlessAddress("Rechnungskunde GmbH", "Kundenweg 2", "20095 Hamburg", 120),
            P("RECHNUNG 04-2026", 105, 220),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Klaus Muster", result.Seller.Name?.Value);
        Assert.Equal("Rechnungskunde GmbH", result.Buyer.Name?.Value);
        Assert.NotEqual(result.Seller.PostalCode?.Value, result.Buyer.PostalCode?.Value);
    }

    [Fact]
    public async Task DerselbePlausibleSellerUndBuyerBlockBleibtBeiderseitsLeer()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Musterbetrieb GmbH", 105, 80),
            P("Musterstraße 1", 105, 95),
            P("12345 Muster", 105, 110),
            P("USt-ID: DE123456789", 105, 125),
            P("RECHNUNG 04-2026", 105, 200),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Seller.HasAnything);
        Assert.False(result.Buyer.HasAnything);
        Assert.Null(result.OwnCompanyProposal);
    }

    [Fact]
    public async Task AnkerloserBuyerÜbernimmtLandVatUndMailBisZurRechnungsüberschrift()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Musterkunde GmbH", 105, 220),
            P("Kundenweg 1", 105, 235),
            P("17438 Musterhausen", 105, 250),
            P("Land: DE", 105, 265),
            P("USt-ID: DE123456789", 105, 280),
            P("Mail: rechnung@kunde.de", 105, 295),
            P("RECHNUNG 04-2026", 105, 330),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Musterkunde GmbH", result.Buyer.Name?.Value);
        Assert.Equal("Kundenweg 1", result.Buyer.Street?.Value);
        Assert.Equal("17438", result.Buyer.PostalCode?.Value);
        Assert.Equal("Musterhausen", result.Buyer.City?.Value);
        Assert.Equal("DE", result.Buyer.Country?.Value);
        Assert.Equal("DE123456789", result.Buyer.VatId?.Value);
        Assert.Equal("rechnung@kunde.de", result.Buyer.Email?.Value);
        Assert.False(result.Seller.HasAnything);

        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, result);

        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerCountry)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerVatId)));
        Assert.Equal(FieldOrigin.DetectedUncertain,
            draft.OriginOf(nameof(draft.BuyerEmail)));
    }

    [Fact]
    public async Task GenerischeKontaktdatenMachenUnbeschriftetenSellerkopfNieZumBuyer()
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Musterbetrieb GmbH", 105, 80),
            P("Musterstraße 1", 105, 95),
            P("12345 Muster", 105, 110),
            P("Land: DE", 105, 125),
            P("USt-ID: DE123456789", 105, 140),
            P("Mail: info@musterbetrieb.de", 105, 155),
            P("RECHNUNG 04-2026", 105, 190),
            P("Nettobetrag 100,00 EUR", 105, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 105, 415),
            P("Gesamtbetrag 119,00 EUR", 105, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Buyer.HasAnything);
        Assert.False(result.Seller.HasAnything);
        Assert.Null(result.OwnCompanyProposal);
    }

    [Fact]
    public async Task ZweiPlausibleNamensköpfeLassenMehrspaltigenSellerLeer()
    {
        byte[] pdf = ReferenceInvoice(
            P("Zweiter Anbieter; ebenfalls ein plausibler Namenskopf", 70, 20));

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Seller.HasAnything);
        Assert.Null(result.OwnCompanyProposal);
        Assert.Equal("Musterkunde GmbH & Co. KG", result.Buyer.Name?.Value);
    }

    [Fact]
    public async Task ZweiVollständigeKontaktblöckeLassenMehrspaltigenSellerLeer()
    {
        byte[] pdf = ReferenceInvoice(
            P("Zweiter Weg 2 • 54321 Zweitort", 440, 69),
            P("Steuernummer: 999 / 888 / 777", 440, 114));

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.False(result.Seller.HasAnything);
        Assert.Null(result.OwnCompanyProposal);
    }

    [Theory]
    [InlineData("•")]
    [InlineData("|")]
    [InlineData("\uF09F")]
    public async Task KombinierteSelleradresseIstNichtAnEinTrennzeichenGebunden(
        string separator)
    {
        byte[] pdf = TextPdfBuilder.CreatePositioned(
        [
            P("Klaus Muster; Technische Dienstleistungen", 70, 30),
            P($"Musterstraße 1 {separator} 12345 Muster", 340, 65),
            P("Steuernummer: 012 / 345 / 678911", 340, 95),
            P("Rechnung an", 100, 140),
            P("Kunde GmbH", 100, 155),
            P("Kundenweg 2", 100, 170),
            P("20095 Hamburg", 100, 185),
            P("RECHNUNG 04-2026", 100, 240),
            P("Nettobetrag 100,00 EUR", 100, 400),
            P("Umsatzsteuer 19 % 19,00 EUR", 100, 415),
            P("Gesamtbetrag 119,00 EUR", 100, 430),
        ]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Klaus Muster", result.Seller.Name?.Value);
        Assert.Equal("Musterstraße 1", result.Seller.Street?.Value);
        Assert.Equal("12345", result.Seller.PostalCode?.Value);
        Assert.Equal("Muster", result.Seller.City?.Value);
    }

    [Fact]
    public async Task MehrereOrtsdatenUndJahresübergreifendeKurzformWerdenNichtGeraten()
    {
        InvoiceDetectionResult dates = await Detect(SimpleDocument(
            "RECHNUNG 04-2026", "Muster, 09.06.2026", "Berlin, 10.06.2026"));
        InvoiceDetectionResult period = await Detect(SimpleDocument(
            "RECHNUNG 04-2026", "Leistungszeitraum: 20.12. - 05.01. 2026"));

        Assert.Null(dates.IssueDate);
        Assert.Null(period.BillingPeriodStart);
        Assert.Null(period.BillingPeriodEnd);
        Assert.Null(period.DeliveryDate);
    }

    [Theory]
    [InlineData("Leistungszeitraum: 01.04.2026 - 07.04.2026", 2026, 4, 1, 2026, 4, 7)]
    [InlineData("Leistungszeitraum: 20.12.2025 - 05.01.2026", 2025, 12, 20, 2026, 1, 5)]
    public async Task VollständigeLeistungszeiträumeWerdenMitExplizitenJahrenÜbernommen(
        string line, int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        InvoiceDetectionResult result = await Detect(SimpleDocument(
            "RECHNUNG 04-2026", line));

        Assert.Equal(
            new DateOnly(startYear, startMonth, startDay),
            result.BillingPeriodStart?.Value);
        Assert.Equal(
            new DateOnly(endYear, endMonth, endDay),
            result.BillingPeriodEnd?.Value);
        Assert.Equal(DetectionConfidence.High, result.BillingPeriodStart?.Confidence);
        Assert.Equal(DetectionConfidence.High, result.BillingPeriodEnd?.Confidence);
        Assert.Null(result.DeliveryDate);
    }

    [Fact]
    public async Task ExplizitesRechnungsdatumBleibtHighUndSchlägtOrtsdatum()
    {
        InvoiceDetectionResult result = await Detect(SimpleDocument(
            "RECHNUNG 04-2026",
            "Rechnungsdatum: 08.06.2026",
            "Muster, 09.06.2026"));

        Assert.Equal(new DateOnly(2026, 6, 8), result.IssueDate?.Value);
        Assert.Equal(DetectionConfidence.High, result.IssueDate?.Confidence);
    }

    [Theory]
    [InlineData("Vertrag, 13.08.2025")]
    [InlineData("Lieferung, 08.06.2026")]
    [InlineData("Fälligkeit, 30.06.2026")]
    public async Task FachlichAndereOrtsähnlicheDatumszeilenWerdenAusgeschlossen(
        string line)
    {
        InvoiceDetectionResult result = await Detect(SimpleDocument(
            "RECHNUNG 04-2026", line));

        Assert.Null(result.IssueDate);
    }

    private async Task<InvoiceDetectionResult> Detect(byte[] pdf)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        return await _detector.DetectAsync(path, null, TestContext.Current.CancellationToken);
    }

    private static byte[] SimpleDocument(params string[] distinguishingLines)
        => TextPdfBuilder.Create(
        [
            .. distinguishingLines,
            "Ausführliche Leistungsbeschreibung für einen ausreichend langen maschinenlesbaren Rechnungstext.",
            "Nettobetrag 100,00 EUR",
            "Umsatzsteuer 19 % 19,00 EUR",
            "Gesamtbetrag 119,00 EUR",
        ]);

    private static byte[] ReferenceInvoice(params PositionedPdfText[] additional)
        => TextPdfBuilder.CreatePositioned(
        [
            .. additional,
            P("Klaus Muster;", 71, 32),
            P("Dienstleister im Bereich Telekommunikation, Messebau,", 177, 32),
            P("Trockenbau, Transport", 177, 44),
            P("Musterstraße 1 • 12345 Muster", 349, 69),
            P("Telefon 0151111111", 448, 82),
            P("Mail: ersteller@muster.com", 354, 92),
            P("Steuernummer: 012 / 345 / 678911", 354, 114),
            P("Musterkunde GmbH & Co. KG", 106, 139),
            P("Abt. Finanz- und Rechnungswesen", 106, 154),
            P("Musterstraße 1", 106, 167),
            P("17438 Musterhausen", 106, 179),
            P("RECHNUNG 04-2026", 106, 242),
            P("Leistungsort: Musterkunde, Musterstraße 1, Musterhausen", 106, 269),
            P("Tätigkeit:", 106, 282),
            P("Mustertätigkeit an einem Fluss - Aus- und Einbau Kompensatoren", 213, 282),
            P("Leistungszeitraum: 01.04. - 07.04. 2026", 106, 294),
            P("Für nachfolgend benannte Leistung berechne ich Ihnen wie folgt:", 106, 347),
            P("Bestellung: 202604/1234/01", 106, 387),
            P("Auftrag:", 106, 399),
            P("202604 - Mustertätigkeit an einem Fluss Musterhausen 2025", 213, 399),
            P("gemäß Vertrag vom 13.08.2025", 106, 412),
            P("Arbeitsleistung Netto 40,00 EUR / je Arbeitsstunde", 106, 437),
            P("25,5 Arbeitsstunden (Nachweis lt. Anlage)", 106, 449),
            P("Netto EUR 40,00 x 25,5 Std.", 106, 462),
            P("Gesamt Netto", 106, 499),
            P("1.020,00 EUR", 354, 499),
            P("Zzgl. 19% MwSt.", 106, 512),
            P("193,80 EUR", 362, 512),
            P("Gesamt Brutto", 106, 524),
            P("1.213,80 EUR", 354, 524),
            P("Vielen Dank für den Auftrag.", 106, 562),
            P("Viele Grüße", 106, 589),
            P("Klaus Muster", 106, 614),
            P("Muster, 09.06.2026", 106, 639),
            P("Bankverbindung:", 354, 689),
            P("DKB Deutsche Kreditbank", 354, 699),
            P("IBAN: DE11 1234 0000 1234 12345 12", 354, 712),
            P("SWIFT: COBADEFFXXX", 354, 722),
        ]);

    private static IEnumerable<PositionedPdfText> AnchorlessAddress(
        string name, string street, string place, double top)
    {
        yield return P(name, 105, top);
        yield return P(street, 105, top + 15);
        yield return P(place, 105, top + 30);
    }

    private static PositionedPdfText P(string text, double left, double top)
        => new(text, left, top);

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

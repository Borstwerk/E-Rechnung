using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Sichert die festen DET-01-Mindestkombinationen und vor allem deren
/// Ausschlussregeln. Ein leerer Seller ist bei Mehrdeutigkeit das richtige
/// Ergebnis.
/// </summary>
public sealed class SellerDetectionTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task ManuellerRegressionsfallFülltSellerOhneVorlageVor()
    {
        InvoiceDetectionResult result = await Detect(TestInvoice.CreatePdf());

        Assert.Equal(TestInvoice.SellerName, result.Seller.Name?.Value);
        Assert.Equal(TestInvoice.SellerVatId, result.Seller.VatId?.Value);
        Assert.Equal(TestInvoice.BuyerName, result.Buyer.Name?.Value);
        Assert.NotNull(result.OwnCompanyProposal);
    }

    [Fact]
    public async Task EinzelunternehmerOhneRechtsformWirdMitVollerMindestkombinationErkannt()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(
            "Max Mustermann",
            "Werkstraße 7",
            "18055 Rostock",
            "Steuernummer: 12/345/67890",
            "Rechnung an",
            "Kundenkontor AG",
            "Hafenstraße 22",
            "20095 Hamburg",
            "Rechnungsnummer: RE-1000",
            "Rechnungsdatum: 13.08.2026",
            "Gesamtbetrag 119,00 EUR"));

        Assert.Equal("Max Mustermann", result.Seller.Name?.Value);
        Assert.Equal("12/345/67890", result.Seller.TaxNumber?.Value);
    }

    [Fact]
    public async Task KäuferObenLinksUndSellerRechtsBleibenImZweispaltigenLayoutGetrennt()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.CreateTwoColumn(
            left:
            [
                "Rechnung an",
                "Käufer Nord AG",
                "Kundenstraße 7",
                "20095 Hamburg",
                "USt-IdNr.: DE987654321",
            ],
            right:
            [
                "Rostock Digital",
                "Werkstraße 9",
                "18055 Rostock",
                "USt-IdNr.: DE123456789",
                "",
            ],
            below:
            [
                "Rechnungsnummer: RE-2000",
                "Rechnungsdatum: 13.08.2026",
                "Gesamtbetrag 238,00 EUR",
            ]));

        Assert.Equal("Rostock Digital", result.Seller.Name?.Value);
        Assert.Equal("DE123456789", result.Seller.VatId?.Value);
        Assert.Equal("Käufer Nord AG", result.Buyer.Name?.Value);
        Assert.NotEqual(result.Buyer.Name?.Value, result.Seller.Name?.Value);
    }

    [Fact]
    public async Task LieferanschriftLinksDarfNichtZumSellerWerden()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.CreateTwoColumn(
            left:
            [
                "Lieferanschrift",
                "Empfangslager West GmbH",
                "Lagerstraße 4",
                "50667 Köln",
                "USt-IdNr.: DE777777777",
            ],
            right:
            [
                "Rostock Digital",
                "Werkstraße 9",
                "18055 Rostock",
                "USt-IdNr.: DE123456789",
                "",
            ],
            below:
            [
                "Rechnungsnummer: RE-3000",
                "Rechnungsdatum: 13.08.2026",
                "Gesamtbetrag 357,00 EUR",
            ]));

        Assert.Equal("Rostock Digital", result.Seller.Name?.Value);
        Assert.NotEqual("Empfangslager West GmbH", result.Seller.Name?.Value);
        Assert.NotEqual("DE777777777", result.Seller.VatId?.Value);
    }

    [Fact]
    public async Task ZweiGleichStarkeFirmenLassenSellerUndProposalLeer()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.CreateTwoColumn(
            left:
            [
                "Alpha Werk GmbH",
                "Alphastraße 1",
                "10115 Berlin",
                "USt-IdNr.: DE111111111",
            ],
            right:
            [
                "Beta Werk GmbH",
                "Betastraße 2",
                "20095 Hamburg",
                "USt-IdNr.: DE222222222",
            ],
            below:
            [
                "Rechnungsnummer: RE-4000",
                "Rechnungsdatum: 13.08.2026",
                "Gesamtbetrag 476,00 EUR",
            ]));

        Assert.False(result.Seller.HasAnything);
        Assert.Null(result.OwnCompanyProposal);
    }

    [Fact]
    public async Task ErsteAdresseOhneSteuermerkmalWirdNichtGeraten()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(
            "Adresse im Briefkopf",
            "Irgendstraße 1",
            "10115 Berlin",
            "Rechnung an",
            "Kundenkontor AG",
            "Hafenstraße 22",
            "20095 Hamburg",
            "Rechnungsnummer: RE-5000",
            "Rechnungsdatum: 13.08.2026",
            "Nettobetrag 500,00 EUR",
            "Gesamtbetrag 595,00 EUR"));

        Assert.False(result.Seller.HasAnything);
        Assert.Null(result.OwnCompanyProposal);
    }

    [Fact]
    public async Task AusdrücklicherSellerBlockBrauchtKeineRechtsformOderSteuernummer()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(
            "Verkäufer",
            "Max Mustermann",
            "Werkstraße 7",
            "18055 Rostock",
            "Rechnung an",
            "Kundenkontor AG",
            "Hafenstraße 22",
            "20095 Hamburg",
            "Rechnungsnummer: RE-6000",
            "Rechnungsdatum: 13.08.2026",
            "Gesamtbetrag 714,00 EUR"));

        Assert.Equal("Max Mustermann", result.Seller.Name?.Value);
        Assert.Equal(DetectionConfidence.High, result.Seller.Name?.Confidence);
    }

    [Fact]
    public async Task SellerUndBuyerUstIdWerdenNichtVertauscht()
    {
        InvoiceDetectionResult result = await Detect(TestInvoice.CreatePdf());

        Assert.Equal(TestInvoice.SellerVatId, result.Seller.VatId?.Value);
        Assert.NotEqual("DE987654321", result.Seller.VatId?.Value);
        Assert.Equal(
            TestInvoice.SellerVatId,
            result.OwnCompanyProposal?.Field(DetectedOwnCompanyFieldKind.SellerVatId)?.Value);
    }

    [Fact]
    public async Task MehrereGültigeIbanWerdenNichtInDasSellerProposalAufgenommen()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(
            "Muster IT GmbH",
            "Musterstraße 10",
            "18055 Rostock",
            "USt-IdNr.: DE123456789",
            "Rechnung an",
            "Kundenkontor AG",
            "Hafenstraße 22",
            "20095 Hamburg",
            "Rechnungsnummer: RE-7000",
            "Rechnungsdatum: 13.08.2026",
            "Gesamtbetrag 833,00 EUR",
            "IBAN DE89 3704 0044 0532 0130 00",
            "IBAN GB82 WEST 1234 5698 7654 32",
            "BIC COBADEFFXXX"));

        Assert.NotNull(result.OwnCompanyProposal);
        Assert.Null(result.Iban);
        Assert.Null(result.Bic);
        Assert.Null(result.OwnCompanyProposal.Field(DetectedOwnCompanyFieldKind.BankIban));
        Assert.Null(result.OwnCompanyProposal.Field(DetectedOwnCompanyFieldKind.BankBic));
    }

    [Fact]
    public async Task UnmittelbarBenachbarteIbanUndBicWerdenGemeinsamVorgeschlagen()
    {
        InvoiceDetectionResult result = await Detect(TestInvoice.CreatePdf());
        DetectedOwnCompanyProposal proposal = Assert.IsType<DetectedOwnCompanyProposal>(
            result.OwnCompanyProposal);

        Assert.Equal(TestInvoice.Iban,
            proposal.Field(DetectedOwnCompanyFieldKind.BankIban)?.Value);
        Assert.Equal(TestInvoice.Bic,
            proposal.Field(DetectedOwnCompanyFieldKind.BankBic)?.Value);
    }

    [Fact]
    public async Task EinzelneBicInAndererSpalteWirdNichtDerIbanZugeordnet()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.CreateTwoColumn(
            left:
            [
                "Muster IT GmbH",
                "Musterstraße 10",
                "18055 Rostock",
                "USt-IdNr.: DE123456789",
                "Rechnungsnummer: RE-7100",
                "Rechnungsdatum: 13.08.2026",
                "Gesamtbetrag 833,00 EUR",
                "IBAN DE89 3704 0044 0532 0130 00",
            ],
            right:
            [
                "", "", "", "", "", "", "",
                "BIC COBADEFFXXX",
            ]));
        DetectedOwnCompanyProposal proposal = Assert.IsType<DetectedOwnCompanyProposal>(
            result.OwnCompanyProposal);

        Assert.Equal(TestInvoice.Iban,
            proposal.Field(DetectedOwnCompanyFieldKind.BankIban)?.Value);
        Assert.Null(proposal.Field(DetectedOwnCompanyFieldKind.BankBic));
    }

    [Fact]
    public async Task BicAufAndererSeiteWirdNichtDerIbanZugeordnet()
    {
        var lines = new List<string>
        {
            "Muster IT GmbH",
            "Musterstraße 10",
            "18055 Rostock",
            "USt-IdNr.: DE123456789",
            "Rechnungsnummer: RE-7200",
            "Rechnungsdatum: 13.08.2026",
            "Gesamtbetrag 833,00 EUR",
            "IBAN DE89 3704 0044 0532 0130 00",
        };

        while (lines.Count < 52)
        {
            lines.Add($"Technische Füllzeile {lines.Count}");
        }

        lines.Add("BIC COBADEFFXXX");

        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(lines));
        DetectedOwnCompanyProposal proposal = Assert.IsType<DetectedOwnCompanyProposal>(
            result.OwnCompanyProposal);

        Assert.Equal(TestInvoice.Iban,
            proposal.Field(DetectedOwnCompanyFieldKind.BankIban)?.Value);
        Assert.Null(proposal.Field(DetectedOwnCompanyFieldKind.BankBic));
    }

    [Fact]
    public async Task MehrereBicKandidatenLassenBicAusDemProposalHeraus()
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(
            "Muster IT GmbH",
            "Musterstraße 10",
            "18055 Rostock",
            "USt-IdNr.: DE123456789",
            "Rechnungsnummer: RE-7300",
            "Rechnungsdatum: 13.08.2026",
            "Gesamtbetrag 833,00 EUR",
            "IBAN DE89 3704 0044 0532 0130 00",
            "BIC COBADEFFXXX",
            "BIC DEUTDEFFXXX"));
        DetectedOwnCompanyProposal proposal = Assert.IsType<DetectedOwnCompanyProposal>(
            result.OwnCompanyProposal);

        Assert.Equal(TestInvoice.Iban,
            proposal.Field(DetectedOwnCompanyFieldKind.BankIban)?.Value);
        Assert.Null(proposal.Field(DetectedOwnCompanyFieldKind.BankBic));
    }

    [Fact]
    public async Task ProposalEnthältKonkreteWerteConfidenceUndEvidenz()
    {
        InvoiceDetectionResult result = await Detect(TestInvoice.CreatePdf());
        DetectedOwnCompanyProposal proposal = Assert.IsType<DetectedOwnCompanyProposal>(
            result.OwnCompanyProposal);
        DetectedOwnCompanyField name = Assert.IsType<DetectedOwnCompanyField>(
            proposal.Field(DetectedOwnCompanyFieldKind.SellerName));

        Assert.Equal(TestInvoice.SellerName, name.Value);
        Assert.Equal(DetectionConfidence.Medium, name.Confidence);
        Assert.NotEmpty(name.Evidence);
        Assert.Equal(TestInvoice.Iban,
            proposal.Field(DetectedOwnCompanyFieldKind.BankIban)?.Value);
    }

    private async Task<InvoiceDetectionResult> Detect(byte[] pdf)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        return await _detector.DetectAsync(path, null, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

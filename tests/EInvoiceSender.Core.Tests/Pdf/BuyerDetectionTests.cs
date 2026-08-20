using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Regressionstests für BUY-01/02/03. Zusatzfelder dürfen ausschließlich aus
/// einem eindeutigen Rechnungsempfängerbereich stammen; Liefer-, Seller- und
/// Fußbereiche sind keine Ersatzquelle.
/// </summary>
public sealed class BuyerDetectionTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task SechsBuyerZeilenMitIsoLandVatUndMailWerdenGemeinsamErkannt()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an",
            "Nordlicht Handel GmbH",
            "Hafenstraße 22",
            "20095 Hamburg",
            "AT",
            "USt-IdNr. ATU12345678",
            "E-Mail einkauf@nordlicht.example",
        ]);

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
        Assert.Equal("AT", result.Buyer.Country?.Value);
        Assert.Equal("ATU12345678", result.Buyer.VatId?.Value);
        Assert.Equal("einkauf@nordlicht.example", result.Buyer.Email?.Value);
    }

    [Fact]
    public async Task KleingeschriebeneBuyerVatWirdErkanntUndNormalisiert()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
            "USt-IdNr. atu12345678",
        ]);

        Assert.Equal("ATU12345678", result.Buyer.VatId?.Value);
    }

    [Theory]
    [InlineData("AT", "AT")]
    [InlineData("Österreich", "AT")]
    [InlineData("Deutschland", "DE")]
    public async Task BuyerlandWirdNurAusCodeOderExaktemLändernamenAufgelöst(
        string documentValue, string expected)
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22",
            "20095 Hamburg", documentValue,
        ]);

        Assert.Equal(expected, result.Buyer.Country?.Value);
    }

    [Fact]
    public async Task FehlendesBuyerlandErzeugtKeineStilleDeutschlandVorgabe()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
        ]);

        Assert.Null(result.Buyer.Country);
    }

    [Fact]
    public async Task GlobalerLändernameOhneBuyerbereichWirdNichtAlsBuyerErkannt()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Muster IT GmbH", "Musterstraße 10", "18055 Rostock", "Deutschland",
            "USt-IdNr. DE123456789",
        ]);

        Assert.False(result.Buyer.HasAnything);
    }

    [Fact]
    public void CountryAlleinGiltAlsErkannterPartywert()
    {
        var party = new DetectedParty
        {
            Country = new DetectedValue<string>("AT", DetectionConfidence.Medium),
        };

        Assert.True(party.HasAnything);
    }

    [Fact]
    public async Task SellerUndBuyerZusatzfelderBleibenGetrenntUndVatPräfixMussNichtZumLandPassen()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Muster IT GmbH", "Musterstraße 10", "18055 Rostock",
            "USt-IdNr. DE123456789", "E-Mail seller@muster.example",
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
            "AT", "USt-IdNr. DE987654321", "E-Mail buyer@nordlicht.example",
        ]);

        Assert.Equal("DE123456789", result.Seller.VatId?.Value);
        Assert.Equal("AT", result.Buyer.Country?.Value);
        Assert.Equal("DE987654321", result.Buyer.VatId?.Value);
        Assert.Equal("buyer@nordlicht.example", result.Buyer.Email?.Value);
        Assert.NotEqual(result.Seller.VatId?.Value, result.Buyer.VatId?.Value);
        Assert.NotEqual(result.Seller.Email?.Value, result.Buyer.Email?.Value);
    }

    [Fact]
    public async Task SellerVatUndMailAusKopfOderFußWerdenNieZuBuyerfeldern()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Muster IT GmbH", "Musterstraße 10", "18055 Rostock",
            "USt-IdNr. DE123456789", "E-Mail kopf@muster.example",
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
            "Rechnungsnummer RE-2026-1001", "Leistungsdatum 12.08.2026",
            "USt-IdNr. DE111111111", "E-Mail footer@muster.example",
        ]);

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
        Assert.Null(result.Buyer.VatId);
        Assert.Null(result.Buyer.Email);
    }

    [Fact]
    public async Task LieferanschriftVorRechnungsempfängerWirdNichtZumBuyer()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Lieferanschrift", "Falsches Lager GmbH", "Lagerweg 9", "28195 Bremen",
            "DE", "USt-IdNr. DE111111111", "lager@falsch.example",
            "Rechnung an", "Richtiger Käufer GmbH", "Kundenweg 8", "50667 Köln",
            "AT", "USt-IdNr. ATU12345678", "rechnung@richtig.example",
        ]);

        Assert.Equal("Richtiger Käufer GmbH", result.Buyer.Name?.Value);
        Assert.Equal("AT", result.Buyer.Country?.Value);
        Assert.Equal("ATU12345678", result.Buyer.VatId?.Value);
        Assert.Equal("rechnung@richtig.example", result.Buyer.Email?.Value);
    }

    [Fact]
    public async Task ZweiGleichPlausibleBuyerblöckeBleibenMehrdeutig()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an", "Buyer Eins GmbH", "Erster Weg 1", "10115 Berlin",
            "Rechnung an", "Buyer Zwei GmbH", "Zweiter Weg 2", "20095 Hamburg",
        ]);

        Assert.False(result.Buyer.HasAnything);
    }

    [Theory]
    [InlineData("AT", "DE", BuyerField.Country)]
    [InlineData("USt-IdNr. ATU12345678", "USt-IdNr. DE987654321", BuyerField.VatId)]
    [InlineData("mail-eins@buyer.example", "mail-zwei@buyer.example", BuyerField.Email)]
    public async Task MehrdeutigesZusatzfeldLöschtNichtDieÜbrigenBuyerfelder(
        string first, string second, BuyerField ambiguousField)
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
            first, second,
        ]);

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
        Assert.Equal("20095", result.Buyer.PostalCode?.Value);

        switch (ambiguousField)
        {
            case BuyerField.Country:
                Assert.Null(result.Buyer.Country);
                break;
            case BuyerField.VatId:
                Assert.Null(result.Buyer.VatId);
                break;
            case BuyerField.Email:
                Assert.Null(result.Buyer.Email);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ambiguousField));
        }
    }

    [Fact]
    public async Task MehrdeutigeVatLässtEindeutigesLandUndEindeutigeEmailErhalten()
    {
        InvoiceDetectionResult result = await DetectSingleColumn(
        [
            "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
            "Österreich",
            "einkauf@nordlicht.example",
            "USt-IdNr. ATU12345678 / DE987654321",
        ]);

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
        Assert.Equal("AT", result.Buyer.Country?.Value);
        Assert.Null(result.Buyer.VatId);
        Assert.Equal("einkauf@nordlicht.example", result.Buyer.Email?.Value);
    }

    [Fact]
    public async Task ZweispaltigesLayoutVerwendetNurDieBuyerSpalte()
    {
        byte[] pdf = TextPdfBuilder.CreateTwoColumn(
            left:
            [
                "Rechnung an", "Nordlicht Handel GmbH", "Hafenstraße 22", "20095 Hamburg",
                "Österreich", "USt-IdNr. EL123456789", "buyer@nordlicht.example",
            ],
            right:
            [
                "Muster IT GmbH", "Musterstraße 10", "18055 Rostock", "Deutschland",
                "USt-IdNr. DE123456789", "seller@muster.example", "Rechnungsnummer RE-2026-1001",
            ],
            below: DocumentTail());

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
        Assert.Equal("AT", result.Buyer.Country?.Value);
        Assert.Equal("EL123456789", result.Buyer.VatId?.Value);
        Assert.Equal("buyer@nordlicht.example", result.Buyer.Email?.Value);
    }

    private async Task<InvoiceDetectionResult> DetectSingleColumn(IReadOnlyList<string> relevant)
        => await Detect(TextPdfBuilder.Create([.. relevant, .. DocumentTail()]));

    private async Task<InvoiceDetectionResult> Detect(byte[] pdf)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        return await _detector.DetectAsync(path, null, TestContext.Current.CancellationToken);
    }

    private static string[] DocumentTail() =>
    [
        "Rechnungsnummer RE-2026-1001", "Rechnungsdatum 13.08.2026",
        "Leistungsdatum 12.08.2026", "Position Beratung 1 Stück 100,00 EUR",
        "Nettobetrag 100,00 EUR", "Umsatzsteuer 19 % 19,00 EUR",
        "Gesamtbetrag 119,00 EUR", "Zahlbetrag 119,00 EUR",
    ];

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    public enum BuyerField
    {
        Country,
        VatId,
        Email,
    }
}

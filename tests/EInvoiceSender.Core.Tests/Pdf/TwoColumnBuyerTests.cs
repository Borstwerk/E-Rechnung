using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Baut den Fehlfall aus dem echten Testlauf nach.
///
/// In der Testrechnung steht links der Empfängerblock und rechts daneben die
/// Rechnungsdaten. Als Käufername wurde „Währung: EUR“ erkannt – der Text aus
/// der rechten Spalte.
///
/// Die Ursache lag im Extraktor: Er fasste alle Wörter einer Grundlinie zu
/// einer Textzeile zusammen, ohne den waagerechten Abstand zu beachten. Der
/// Adressblock wurde danach rein nach Lesereihenfolge gelesen und griff damit
/// in die falsche Spalte.
/// </summary>
public sealed class TwoColumnBuyerTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task ImZweispaltigenLayoutWirdDerEchteKäufernameErkannt()
    {
        InvoiceDetectionResult result = await DetectTestInvoice();

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
    }

    /// <summary>Der beobachtete Fehlwert, ausdrücklich als solcher geprüft.</summary>
    [Fact]
    public async Task DieWährungsangabeWirdNichtZumKäufernamen()
    {
        InvoiceDetectionResult result = await DetectTestInvoice();

        Assert.NotEqual("Währung: EUR", result.Buyer.Name?.Value);
    }

    [Fact]
    public async Task DieAnschriftDesKäufersStammtAusDerLinkenSpalte()
    {
        InvoiceDetectionResult result = await DetectTestInvoice();

        Assert.Equal("20095", result.Buyer.PostalCode?.Value);
        Assert.Equal("Hamburg", result.Buyer.City?.Value);
    }

    /// <summary>
    /// Die rechte Spalte muss weiterhin vollständig gelesen werden – die
    /// Trennung darf keine Kopfdaten verlieren.
    /// </summary>
    [Fact]
    public async Task DieRechnungsdatenAusDerRechtenSpalteBleibenVollständig()
    {
        InvoiceDetectionResult result = await DetectTestInvoice();

        Assert.Equal("RE-2026-0815", result.InvoiceNumber?.Value);
        Assert.Equal(new DateOnly(2026, 8, 9), result.IssueDate?.Value);
        Assert.Equal(new DateOnly(2026, 8, 8), result.DeliveryDate?.Value);
        Assert.Equal(new DateOnly(2026, 8, 23), result.DueDate?.Value);
        Assert.Equal("EUR", result.Currency?.Value);
    }

    [Fact]
    public async Task SummenUndBankverbindungWerdenWeiterhinGelesen()
    {
        InvoiceDetectionResult result = await DetectTestInvoice();

        Assert.Equal(600.00m, result.Totals.Net?.Value);
        Assert.Equal(114.00m, result.Totals.Tax?.Value);
        Assert.Equal(714.00m, result.Totals.Gross?.Value);
        Assert.Equal(714.00m, result.Totals.Payable?.Value);
        Assert.Contains(result.Totals.VatRates, r => r.Value == 19m);
        Assert.Equal("DE89370400440532013000", result.Iban?.Value);
        Assert.Equal("COBADEFFXXX", result.Bic?.Value);
    }

    /// <summary>
    /// Auch ohne Spaltentrennung darf keine Rechnungsangabe zum Firmennamen
    /// werden. Hier steht die Angabe unmittelbar unter dem Schlüsselwort, also
    /// in derselben Spalte – nur die Begriffssperre kann das noch abfangen.
    /// </summary>
    [Theory]
    [InlineData("Rechnungsnummer: RE-2026-0815")]
    [InlineData("Rechnungsdatum: 09.08.2026")]
    [InlineData("Währung: EUR")]
    [InlineData("Gesamtbetrag: 714,00 EUR")]
    [InlineData("Zahlbetrag: 714,00 EUR")]
    [InlineData("IBAN DE89 3704 0044 0532 0130 00")]
    [InlineData("BIC COBADEFFXXX")]
    public async Task RechnungsangabenWerdenNieZumFirmennamen(string metadata)
    {
        InvoiceDetectionResult result = await Detect(TextPdfBuilder.Create(
        [
            "Muster IT GmbH", "Musterstraße 10", "18055 Rostock",
            "Rechnung an",
            metadata,
            "Nordlicht Handel GmbH",
            "Hafenstraße 22",
            "20095 Hamburg",
            "Nettobetrag 600,00 EUR",
            "Gesamtbetrag 714,00 EUR",
        ]));

        Assert.Equal("Nordlicht Handel GmbH", result.Buyer.Name?.Value);
    }

    /// <summary>
    /// Baut die Testrechnung nach: links der Empfängerblock, rechts die
    /// Rechnungsdaten auf denselben Grundlinien.
    /// </summary>
    private Task<InvoiceDetectionResult> DetectTestInvoice()
        => Detect(TextPdfBuilder.CreateTwoColumn(
            left:
            [
                "Muster IT GmbH",
                "Musterstraße 10",
                "18055 Rostock",
                "USt-IdNr.: DE123456789",
                string.Empty,
                "Rechnung an",
                "Nordlicht Handel GmbH",
                "Hafenstraße 22",
                "20095 Hamburg",
                "Deutschland",
                "USt-IdNr.: DE987654321",
            ],
            right:
            [
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "Rechnungsnummer: RE-2026-0815",
                "Rechnungsdatum: 09.08.2026",
                "Leistungsdatum: 08.08.2026",
                "Fällig am: 23.08.2026",
                "Währung: EUR",
            ],
            below:
            [
                "Pos Bezeichnung Menge Einheit Einzelpreis Betrag",
                "1 IT-Beratung 4 Std 100,00 400,00",
                "2 Projektleitung 2 Std 100,00 200,00",
                "Nettobetrag 600,00 EUR",
                "Umsatzsteuer 19 % 114,00 EUR",
                "Gesamtbetrag 714,00 EUR",
                "Zahlbetrag 714,00 EUR",
                "IBAN DE89 3704 0044 0532 0130 00",
                "BIC COBADEFFXXX",
            ]));

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

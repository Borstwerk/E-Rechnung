using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Prüft die Rechnungserkennung.
///
/// Der größere Teil dieser Tests prüft nicht, **was** erkannt wird, sondern
/// was **nicht** erkannt werden darf. Das ist Absicht: Ein leeres Feld kostet
/// den Anwender ein paar Tastenanschläge, ein falsch gefülltes Feld, das er
/// übersieht, kostet eine fehlerhafte Rechnung.
/// </summary>
public sealed class InvoiceDataDetectorTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    // ============================================== Fall 1: normale Rechnung

    [Fact]
    public async Task NormaleRechnungLiefertKopfdatenUndSummen()
    {
        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines());

        Assert.True(result.HasUsableText);
        Assert.Equal("RE-2026-0815", result.InvoiceNumber?.Value);
        Assert.Equal(DetectionConfidence.High, result.InvoiceNumber?.Confidence);
        Assert.Equal(new DateOnly(2026, 8, 9), result.IssueDate?.Value);
        Assert.Equal(new DateOnly(2026, 8, 8), result.DeliveryDate?.Value);
        Assert.Equal(new DateOnly(2026, 8, 23), result.DueDate?.Value);
        Assert.Equal("EUR", result.Currency?.Value);
        Assert.Equal(1000.00m, result.Totals.Net?.Value);
        Assert.Equal(190.00m, result.Totals.Tax?.Value);
        Assert.Equal(1190.00m, result.Totals.Gross?.Value);
    }

    // ================================= Fälle 2-4: Steuersätze und Beträge

    [Theory]
    [InlineData("19", 19)]
    [InlineData("7", 7)]
    [InlineData("0", 0)]
    public async Task GängigeSteuersätzeWerdenErkannt(string text, int expected)
    {
        InvoiceDetectionResult result = await Detect(
        [
            .. Kopf(),
            $"Nettobetrag 1.000,00 EUR",
            $"Umsatzsteuer {text} % 190,00 EUR",
            "Gesamtbetrag 1.190,00 EUR",
        ]);

        Assert.Contains(result.Totals.VatRates, r => r.Value == expected);
    }

    [Fact]
    public async Task MehrereSteuersätzeWerdenAlleErfasst()
    {
        InvoiceDetectionResult result = await Detect(
        [
            .. Kopf(),
            "Nettobetrag 1.100,00 EUR",
            "Umsatzsteuer 19 % 190,00 EUR",
            "Umsatzsteuer 7 % 7,00 EUR",
            "Gesamtbetrag 1.297,00 EUR",
        ]);

        Assert.Contains(result.Totals.VatRates, r => r.Value == 19m);
        Assert.Contains(result.Totals.VatRates, r => r.Value == 7m);
    }

    // ================================== Fall 5: Rechnungsnummernformate

    [Theory]
    [InlineData("Rechnungsnummer: RE-2026-0815", "RE-2026-0815")]
    [InlineData("Rechnungs-Nr. 2026/0815", "2026/0815")]
    [InlineData("Rechnungsnr. R00042", "R00042")]
    [InlineData("Invoice No. INV-2026-0815", "INV-2026-0815")]
    [InlineData("Belegnummer 2026_0815", "2026_0815")]
    public async Task VerschiedeneNummernformateWerdenErkannt(string line, string expected)
    {
        InvoiceDetectionResult result = await Detect([.. Kopf(), line, .. Summen()]);

        Assert.Equal(expected, result.InvoiceNumber?.Value);
    }

    // ==================================== Fälle 6-7: Datumsformate

    [Theory]
    [InlineData("Rechnungsdatum: 09.08.2026", 2026, 8, 9)]
    [InlineData("Rechnungsdatum: 9.8.2026", 2026, 8, 9)]
    [InlineData("Rechnungsdatum: 09.08.26", 2026, 8, 9)]
    [InlineData("Rechnungsdatum: 2026-08-09", 2026, 8, 9)]
    public async Task GängigeDatumsformateWerdenErkannt(string line, int y, int m, int d)
    {
        InvoiceDetectionResult result = await Detect(
            ["Muster IT GmbH", "Musterstraße 10", "18055 Rostock", line, .. Summen()]);

        Assert.Equal(new DateOnly(y, m, d), result.IssueDate?.Value);
    }

    [Fact]
    public async Task UnmöglichesDatumWirdVerworfen()
    {
        InvoiceDetectionResult result = await Detect(
            [.. Kopf(), "Rechnungsdatum: 32.13.2026", .. Summen()]);

        Assert.Null(result.IssueDate);
    }

    // =============================================== Fall 8: Zahlenformate

    [Theory]
    [InlineData("Gesamtbetrag 1.190,00 EUR", 1190.00)]
    [InlineData("Gesamtbetrag 1190,00 EUR", 1190.00)]
    [InlineData("Gesamtbetrag 12.345.678,90 EUR", 12345678.90)]
    [InlineData("Gesamtbetrag 99,00 €", 99.00)]
    public async Task DeutscheZahlenformateWerdenGelesen(string line, double expected)
    {
        InvoiceDetectionResult result = await Detect([.. Kopf(), .. KopfDaten(), line]);

        Assert.Equal((decimal)expected, result.Totals.Gross?.Value);
    }

    // ====================================================== Fall 9: IBAN

    [Fact]
    public async Task GültigeIbanWirdErkanntUndBestätigt()
    {
        InvoiceDetectionResult result = await Detect(
            [.. Kopf(), .. KopfDaten(), .. Summen(), "IBAN DE89 3704 0044 0532 0130 00"]);

        Assert.Equal("DE89370400440532013000", result.Iban?.Value);
        Assert.Equal(DetectionConfidence.High, result.Iban?.Confidence);
    }

    /// <summary>
    /// Der wichtigste Negativfall der IBAN-Erkennung: Das Muster passt, die
    /// Prüfsumme nicht. So etwas darf nie übernommen werden – eine falsche
    /// Bankverbindung auf einer Rechnung ist teuer.
    /// </summary>
    [Fact]
    public async Task IbanMitFalscherPrüfsummeWirdNichtÜbernommen()
    {
        InvoiceDetectionResult result = await Detect(
            [.. Kopf(), .. KopfDaten(), .. Summen(), "IBAN DE00 3704 0044 0532 0130 00"]);

        Assert.Null(result.Iban);
    }

    [Fact]
    public async Task VonMehrerenIbanÄhnlichenWertenGewinntDieGültige()
    {
        InvoiceDetectionResult result = await Detect(
        [
            .. Kopf(),
            .. KopfDaten(),
            .. Summen(),
            "Alte Bankverbindung DE00 1111 2222 3333 4444 55",
            "IBAN DE89 3704 0044 0532 0130 00",
        ]);

        Assert.Equal("DE89370400440532013000", result.Iban?.Value);
    }

    // ======================================= Fälle 10/11/17: Parteien

    [Fact]
    public async Task KäuferWirdUnterRechnungAnErkannt()
    {
        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines());

        Assert.Equal("Beispielkunde AG", result.Buyer.Name?.Value);
        Assert.Equal("20095", result.Buyer.PostalCode?.Value);
        Assert.Equal("Hamburg", result.Buyer.City?.Value);
    }

    /// <summary>
    /// Die gespeicherte Vorlage ist das stärkste Signal für den Verkäufer:
    /// Man stellt seine eigenen Rechnungen selbst aus.
    /// </summary>
    [Fact]
    public async Task GespeicherteVorlageErkenntDenVerkäufer()
    {
        var template = new CompanyTemplate
        {
            SellerName = "Muster IT GmbH",
            SellerStreet = "Musterstraße 10",
            SellerPostalCode = "18055",
            SellerCity = "Rostock",
            SellerVatId = "DE123456789",
        };

        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines(), template);

        Assert.Equal("Muster IT GmbH", result.Seller.Name?.Value);
        Assert.Equal(DetectionConfidence.High, result.Seller.Name?.Confidence);
        Assert.Contains("USt-IdNr", result.Seller.Name?.Reason ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ohne Vorlage und ohne Schlüsselwort bleibt der Verkäufer leer. Eine
    /// geratene Zuordnung wäre schlimmer: Vertauschte Parteien ergeben eine
    /// formal gültige, inhaltlich falsche Rechnung.
    /// </summary>
    [Fact]
    public async Task OhneVorlageWirdKeinVerkäuferGeraten()
    {
        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines());

        Assert.False(result.Seller.HasAnything);
    }

    [Fact]
    public async Task DieEigeneFirmaWirdNichtZumKäufer()
    {
        var template = new CompanyTemplate { SellerName = "Muster IT GmbH" };

        InvoiceDetectionResult result = await Detect(
        [
            .. Kopf(),
            "Rechnung an",
            "Muster IT GmbH",
            "Beispielkunde AG",
            "20095 Hamburg",
            .. KopfDaten(),
            .. Summen(),
        ], template);

        Assert.NotEqual("Muster IT GmbH", result.Buyer.Name?.Value);
    }

    // ================================ Fälle 12/13: fehlende Angaben

    [Fact]
    public async Task RechnungOhneEmailLiefertTrotzdemDieÜbrigenDaten()
    {
        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines());

        Assert.Null(result.Buyer.Email);
        Assert.NotNull(result.InvoiceNumber);
        Assert.NotNull(result.Totals.Gross);
    }

    [Fact]
    public async Task PdfOhneTextWirdSauberGemeldet()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf());

        InvoiceDetectionResult result = await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);

        Assert.False(result.HasUsableText);
        Assert.False(result.HasAnything);
    }

    [Fact]
    public async Task BeschädigtePdfBlockiertDenAblaufNicht()
    {
        string path = Temp(TestPdfFactory.CreateDamagedPdf());

        InvoiceDetectionResult result = await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);

        Assert.False(result.HasUsableText);
    }

    // ================================== Fälle 18/19: Fehlzuordnungen

    /// <summary>
    /// Der Klassiker: In einer Zeile stehen Telefon- und Kundennummer. Ohne
    /// Sperre würde eine davon zur Rechnungsnummer.
    /// </summary>
    [Fact]
    public async Task TelefonUndKundennummerWerdenNichtZurRechnungsnummer()
    {
        InvoiceDetectionResult result = await Detect(
        [
            "Muster IT GmbH",
            "Telefon 0381 1234567 Rechnung Kundennummer 4711",
            "Seite 1 von 2",
            "Musterstraße 10",
            "18055 Rostock",
            .. Summen(),
        ]);

        Assert.Null(result.InvoiceNumber);
    }

    [Fact]
    public async Task EineReineZifferfolgeOhneSchlüsselwortWirdIgnoriert()
    {
        InvoiceDetectionResult result = await Detect(
            [.. Kopf(), "0381 1234567", "4711 0815 2342", .. Summen()]);

        Assert.Null(result.InvoiceNumber);
    }

    /// <summary>
    /// Postleitzahlen sind fünfstellige Zahlen ohne Nachkommastellen. Sie
    /// dürfen nie als Betrag durchgehen.
    /// </summary>
    [Fact]
    public async Task PostleitzahlWirdNichtAlsBetragGelesen()
    {
        InvoiceDetectionResult result = await Detect(
        [
            .. Kopf(),
            "Rechnung an",
            "Beispielkunde AG",
            "Kundenstraße 7",
            "20095 Hamburg",
            .. KopfDaten(),
            "Gesamtbetrag 1.190,00 EUR",
        ]);

        Assert.Equal(1190.00m, result.Totals.Gross?.Value);
        Assert.NotEqual(18055m, result.Totals.Gross?.Value);
        Assert.NotEqual(20095m, result.Totals.Gross?.Value);
    }

    /// <summary>
    /// Ein Rabatt von 3 % ist kein Steuersatz. Ohne die Kontextprüfung würde
    /// jeder Prozentwert im Dokument als Umsatzsteuer gelesen.
    /// </summary>
    [Fact]
    public async Task RabattProzentWirdNichtZumSteuersatz()
    {
        InvoiceDetectionResult result = await Detect(
        [
            .. Kopf(),
            .. KopfDaten(),
            "Nettobetrag 1.000,00 EUR",
            "Rabatt 3 % -30,00 EUR",
            "Skonto 2 % bei Zahlung binnen 10 Tagen",
            "Umsatzsteuer 19 % 184,30 EUR",
            "Gesamtbetrag 1.154,30 EUR",
        ]);

        Assert.Contains(result.Totals.VatRates, r => r.Value == 19m);
        Assert.DoesNotContain(result.Totals.VatRates, r => r.Value == 3m);
        Assert.DoesNotContain(result.Totals.VatRates, r => r.Value == 2m);
    }

    /// <summary>
    /// In "Umsatzsteuer 19 % 190,00" ist 190,00 der Betrag, nicht 19. Der
    /// Prozentwert wird vor der Betragssuche entfernt.
    /// </summary>
    [Fact]
    public async Task ProzentwertWirdNichtZumSteuerbetrag()
    {
        InvoiceDetectionResult result = await Detect(
            [.. Kopf(), .. KopfDaten(), "Umsatzsteuer 19 % 190,00 EUR", "Gesamtbetrag 1.190,00 EUR"]);

        Assert.Equal(190.00m, result.Totals.Tax?.Value);
    }

    // ============================================== Fall 16/20: Positionen

    /// <summary>
    /// Die Positionserkennung ist nicht umgesetzt. Dieser Test hält fest, dass
    /// die übrigen Angaben trotzdem vollständig gelesen werden – die fehlende
    /// Positionserkennung darf den Rest nicht mitreißen.
    /// </summary>
    [Fact]
    public async Task OhnePositionserkennungWerdenDieÜbrigenAngabenGelesen()
    {
        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines());

        Assert.NotNull(result.InvoiceNumber);
        Assert.NotNull(result.Totals.Gross);
        Assert.NotNull(result.Iban);
    }

    [Fact]
    public async Task MehrseitigeRechnungWirdVollständigAusgewertet()
    {
        var lines = new List<string>(Kopf());

        for (int i = 1; i <= 90; i++)
        {
            lines.Add($"{i} Sammelposition {i} 1 Stk 10,00 10,00");
        }

        lines.AddRange(KopfDaten());
        lines.AddRange(Summen());

        InvoiceDetectionResult result = await Detect(lines);

        Assert.True(result.PageCount >= 2);
        Assert.Equal("RE-2026-0815", result.InvoiceNumber?.Value);
        Assert.Equal(1190.00m, result.Totals.Gross?.Value);
    }

    // ================================================================ Aufbau

    private static string[] Kopf() =>
    [
        "Muster IT GmbH", "Musterstraße 10", "18055 Rostock",
        "Telefon 0381 1234567", "USt-IdNr. DE123456789",
    ];

    private static string[] KopfDaten() =>
    [
        "Rechnungsnummer: RE-2026-0815",
        "Rechnungsdatum: 09.08.2026",
    ];

    private static string[] Summen() =>
    [
        "Nettobetrag 1.000,00 EUR",
        "Umsatzsteuer 19 % 190,00 EUR",
        "Gesamtbetrag 1.190,00 EUR",
    ];

    private async Task<InvoiceDetectionResult> Detect(
        IEnumerable<string> lines, CompanyTemplate? template = null)
    {
        string path = Temp(TextPdfBuilder.Create(lines));

        return await _detector.DetectAsync(path, template, TestContext.Current.CancellationToken);
    }

    private string Temp(byte[] content)
    {
        string path = TestPdfFactory.WriteToTempFile(content);
        _temporaryFiles.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

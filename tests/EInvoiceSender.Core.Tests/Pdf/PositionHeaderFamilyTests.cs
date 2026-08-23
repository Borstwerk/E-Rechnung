using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C7 – die Beschriftungen, die reale Rechnungen tatsächlich verwenden.
///
/// Jede hier ergänzte Familie ist eine konkret beobachtete Schreibweise, kein
/// erfundener Sonderfall. Es gibt **kein** unscharfes Vergleichen: kein Fuzzy
/// Matching, keine Levenshtein-Distanz, kein „klingt ähnlich“. Eine
/// Beschriftung wird verstanden oder sie wird es nicht; im zweiten Fall bleibt
/// die Tabelle liegen.
///
/// Die Währungs- und Prozentzeichen in <c>Preis in €</c> oder <c>USt in %</c>
/// werden ausdrücklich **nicht** allgemein weggeworfen. Sie gehören zur
/// jeweiligen Beschriftung und werden nur innerhalb dieser Familien
/// behandelt – ein global ignoriertes Zeichen wäre eine Hintertür für
/// beliebige unbekannte Köpfe.
/// </summary>
public sealed class PositionHeaderFamilyTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    [Theory]
    [InlineData("Beschreibung")]
    [InlineData("Bezeichnung")]
    [InlineData("Leistung")]
    [InlineData("Artikelbeschreibung")]
    [InlineData("Name/Beschreibung")]
    public Task DieBeschreibungsfamilieWirdVerstanden(string header)
        => AssertRecognised(description: header);

    [Theory]
    [InlineData("Einzelpreis")]
    [InlineData("EP")]
    [InlineData("E-Preis")]
    [InlineData("Preis")]
    [InlineData("Preis in €")]
    public Task DieEinzelpreisfamilieWirdVerstanden(string header)
        => AssertRecognised(unitPrice: header);

    [Theory]
    [InlineData("Gesamt")]
    [InlineData("Gesamtpreis")]
    [InlineData("Betrag")]
    [InlineData("Netto")]
    public Task DieGesamtpreisfamilieWirdVerstanden(string header)
        => AssertRecognised(lineTotal: header);

    [Theory]
    [InlineData("MwSt")]
    [InlineData("USt")]
    [InlineData("USt %")]
    [InlineData("USt in %")]
    [InlineData("Steuersatz")]
    public Task DieSteuerfamilieWirdVerstanden(string header)
        => AssertRecognised(vat: header);

    /// <summary>
    /// **Die Gegenprobe zur ganzen Familienerweiterung.** Eine Beschriftung,
    /// die keiner Familie angehört, wird nicht wohlwollend zugeordnet – auch
    /// dann nicht, wenn sie einer bekannten ähnlich sieht.
    /// </summary>
    [Theory]
    [InlineData("Beschreibunk")]
    [InlineData("Artikeltext")]
    [InlineData("Bemerkung")]
    public async Task EineUnbekannteBeschriftungWirdNichtErraten(string header)
        => Assert.Empty((await Detect(description: header)).Lines);

    // ------------------------------------------------------------- Hilfsmittel

    private async Task AssertRecognised(
        string description = "Beschreibung",
        string unitPrice = "Einzelpreis",
        string lineTotal = "Gesamt",
        string vat = "MwSt")
    {
        PositionDetectionResult result = await Detect(description, unitPrice, lineTotal, vat);

        DetectedInvoiceLine line = Assert.Single(result.Lines);

        Assert.Equal("Beratung", line.Name);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(100.00m, line.NetUnitPrice);
        Assert.Equal(200.00m, line.ExplicitLineTotal);
        Assert.Equal(19m, line.VatRate);
    }

    private async Task<PositionDetectionResult> Detect(
        string description = "Beschreibung",
        string unitPrice = "Einzelpreis",
        string lineTotal = "Gesamt",
        string vat = "MwSt")
    {
        byte[] pdf = LayoutTablePdf.Create(
            [
                TableColumn.Text(description, 60),
                TableColumn.Text("Menge", 250),
                TableColumn.Money(unitPrice, 320, 370),
                TableColumn.Money(lineTotal, 420, 470),
                TableColumn.Text(vat, 510),
            ],
            [["Beratung", "2", "100,00", "200,00", "19 %"]],
            net: "200,00", tax: "38,00", gross: "238,00");

        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        PdfTextResult text = await _extractor.ExtractAsync(
            path, TestContext.Current.CancellationToken);

        return PositionDetector.Detect(
            text.Lines, DetectedTotalsFixture.High(200m, 38m, 238m));
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C8 – Tabellen mit ausgeschriebenen Kontrollbeträgen je Position.
///
/// Der Aufbau <c>Bezeichnung | Menge | Preis in € | Netto | USt in % |
/// USt in € | Brutto</c> schreibt zu jeder Position das Ergebnis gleich mit.
///
/// **Diese Beträge werden geprüft, nicht übernommen.** Sie sind
/// Erkennungsevidenz und keine zweite Rechnungswahrheit: Gerechnet wird
/// weiterhin ausschließlich aus Menge, Einzelpreis und Steuersatz durch den
/// bestehenden Rechner. Was die Spalten leisten, ist eine Gegenprobe – stimmt
/// eine davon nicht, ist die Tabelle nicht verstanden und wird verworfen.
///
/// Gerundet wird dabei mit denselben Regeln wie in der Rechnung selbst. Eine
/// eigene Rundungsdefinition an dieser Stelle wäre der Anfang zweier
/// Wahrheiten.
/// </summary>
public sealed class PositionFinancialEvidenceTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    private static readonly TableColumn[] WithEvidence =
    [
        TableColumn.Text("Bezeichnung", 45),
        TableColumn.Text("Menge", 190),
        TableColumn.Money("Preis in €", 240, 300),
        TableColumn.Money("Netto", 330, 380),
        TableColumn.Text("USt in %", 410),
        TableColumn.Money("USt in €", 470, 520),
        TableColumn.Money("Brutto", 540, 585),
    ];

    [Fact]
    public async Task StimmigeKontrollbeträgeLassenDieTabelleZu()
    {
        PositionDetectionResult result = await Detect("200,00", "38,00", "238,00");

        DetectedInvoiceLine line = Assert.Single(result.Lines);

        Assert.Equal("Beratung", line.Name);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(100.00m, line.NetUnitPrice);
        Assert.Equal(19m, line.VatRate);
    }

    /// <summary>
    /// Der Steuerbetrag und der Bruttobetrag bleiben Evidenz. Der erkannte
    /// Positionswert ist der Nettogesamtpreis – mehr trägt die Position nicht
    /// weiter, und mehr soll sie auch nicht tragen.
    /// </summary>
    [Fact]
    public async Task DieKontrollbeträgeWerdenNichtZuEinemZweitenRechnungsfeld()
    {
        DetectedInvoiceLine line = Assert.Single(
            (await Detect("200,00", "38,00", "238,00")).Lines);

        Assert.Equal(200.00m, line.ExplicitLineTotal);
    }

    /// <summary>Ein falscher Positionsnettobetrag verwirft die Tabelle.</summary>
    [Fact]
    public async Task EinFalscherNettobetragVerwirftDieTabelle()
        => Assert.Empty((await Detect("210,00", "38,00", "238,00")).Lines);

    /// <summary>Ein falscher Steuerbetrag verwirft die Tabelle.</summary>
    [Fact]
    public async Task EinFalscherSteuerbetragVerwirftDieTabelle()
        => Assert.Empty((await Detect("200,00", "40,00", "238,00")).Lines);

    /// <summary>Ein falscher Bruttobetrag verwirft die Tabelle.</summary>
    [Fact]
    public async Task EinFalscherBruttobetragVerwirftDieTabelle()
        => Assert.Empty((await Detect("200,00", "38,00", "240,00")).Lines);

    // ------------------------------------------------------------- Hilfsmittel

    private async Task<PositionDetectionResult> Detect(
        string lineNet, string lineVat, string lineGross)
    {
        byte[] pdf = LayoutTablePdf.Create(
            WithEvidence,
            [["Beratung", "2", "100,00", lineNet, "19 %", lineVat, lineGross]],
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

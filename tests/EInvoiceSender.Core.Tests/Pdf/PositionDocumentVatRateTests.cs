using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Der arithmetisch bestätigte Dokumentsteuersatz.
///
/// **Warum es diese Ergänzung gibt:** Führt eine Tabelle keine eigene
/// Steuerspalte, muss der Satz aus dem Dokument kommen. Der bisherige Weg
/// verlangte dafür die Vertrauensstufe <c>High</c> – die der
/// <c>TotalsDetector</c> für Steuersätze nie vergibt. Der Rückfall war damit
/// auf dem produktiven Weg unerreichbar, und jede Rechnung ohne Steuerspalte
/// ergab null Positionen.
///
/// **Die Lösung lockert nichts, sie beweist mehr.** Ein einzelner
/// <c>Medium</c>-Satz wird nur dann verwendet, wenn die sicher gelesenen
/// Dokumentsummen ihn bestätigen: Die Steuer muss sich aus Netto und Satz
/// ergeben, und Brutto muss Netto plus Steuer sein. Damit entsteht eine
/// zweistufige Beweiskette – erst wird der Satz gegen die Dokumentsummen
/// belegt, dann werden die Positionen gegen dieselben Summen belegt.
///
/// Ein bloßes „Medium genügt jetzt“ wäre etwas völlig anderes gewesen und ist
/// ausdrücklich nicht gemeint. Genau das sichern die Negativfälle hier ab.
/// </summary>
public sealed class PositionDocumentVatRateTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    /// <summary><c>Beschreibung | Menge | Einzelpreis | Gesamt</c> – ohne Steuerspalte.</summary>
    private static readonly TableColumn[] WithoutVatColumn =
    [
        TableColumn.Text("Beschreibung", 60),
        TableColumn.Text("Menge", 250),
        TableColumn.Money("Einzelpreis", 320, 370),
        TableColumn.Money("Gesamt", 420, 470),
    ];

    [Fact]
    public async Task EinBestätigterMediumSatzVon19ProzentTrägtDieTabelle()
    {
        PositionDetectionResult result = await Detect(
            DetectedTotalsFixture.Custom(200m, 38m, 238m, rate: 19m));

        Assert.Equal(19m, Assert.Single(result.Lines).VatRate);
    }

    [Fact]
    public async Task EinBestätigterMediumSatzVon7ProzentTrägtDieTabelle()
    {
        PositionDetectionResult result = await Detect(
            DetectedTotalsFixture.Custom(200m, 14m, 214m, rate: 7m));

        Assert.Equal(7m, Assert.Single(result.Lines).VatRate);
    }

    /// <summary>
    /// **Der Kern der Absicherung.** Stimmt die Steuerarithmetik nicht, ist
    /// der Satz nicht bestätigt – und ein unbestätigter Medium-Satz bleibt
    /// verboten. 19 % auf 200,00 sind 38,00 und nicht 40,00.
    /// </summary>
    [Fact]
    public async Task EinFalscherSteuerbetragBestätigtDenSatzNicht()
        => Assert.Empty(
            (await Detect(DetectedTotalsFixture.Custom(200m, 40m, 240m, rate: 19m))).Lines);

    /// <summary>Auch die Bruttoarithmetik muss aufgehen.</summary>
    [Fact]
    public async Task EinFalscherBruttobetragBestätigtDenSatzNicht()
        => Assert.Empty(
            (await Detect(DetectedTotalsFixture.Custom(200m, 38m, 250m, rate: 19m))).Lines);

    /// <summary>
    /// Die Bestätigung ist nur so viel wert wie die Summen, die sie tragen.
    /// Ist eine davon selbst unsicher gelesen, beweist die Rechnung nichts.
    /// </summary>
    [Theory]
    [InlineData("net")]
    [InlineData("tax")]
    [InlineData("gross")]
    public async Task EineUnsichereDokumentsummeTrägtKeineBestätigung(string weak)
    {
        DetectedTotals totals = DetectedTotalsFixture.Custom(
            200m, 38m, 238m,
            rate: 19m,
            netConfidence: Confidence(weak, "net"),
            taxConfidence: Confidence(weak, "tax"),
            grossConfidence: Confidence(weak, "gross"));

        Assert.Empty((await Detect(totals)).Lines);
    }

    /// <summary>
    /// Mehrere Dokumentsteuersätze bleiben mehrdeutig. Sich einen davon
    /// auszusuchen wäre Raten, und arithmetisch ließe sich ohnehin nicht
    /// entscheiden, welcher zu welcher Position gehört.
    /// </summary>
    [Fact]
    public async Task MehrereDokumentsteuersätzeBleibenMehrdeutig()
        => Assert.Empty(
            (await Detect(DetectedTotalsFixture.WithVatRates(200m, 38m, 238m, 7m, 19m))).Lines);

    /// <summary>
    /// Ein anderer Satz als 7 oder 19 wird nicht übernommen – auch dann nicht,
    /// wenn die Arithmetik aufgeht. Diese Anwendung deckt nur die beiden
    /// deutschen Regelsätze ab.
    /// </summary>
    [Fact]
    public async Task EinAndererSatzAlsSiebenOderNeunzehnBleibtDraußen()
        => Assert.Empty(
            (await Detect(DetectedTotalsFixture.Custom(200m, 32m, 232m, rate: 16m))).Lines);

    /// <summary>
    /// Der bestehende High-Weg bleibt, wie er war. Die Ergänzung tritt neben
    /// ihn und nicht an seine Stelle.
    /// </summary>
    [Fact]
    public async Task EinSicherGelesenerSatzVerhältSichUnverändert()
    {
        PositionDetectionResult result = await Detect(
            DetectedTotalsFixture.WithVatRate(200m, 38m, 238m, 19m));

        Assert.Equal(19m, Assert.Single(result.Lines).VatRate);
    }

    /// <summary>
    /// Ohne jeden Dokumentsteuersatz bleibt es dabei: keine Steuerspalte,
    /// kein Satz, keine Tabelle.
    /// </summary>
    [Fact]
    public async Task OhneDokumentsteuersatzBleibtEsLeer()
        => Assert.Empty((await Detect(DetectedTotalsFixture.High(200m, 38m, 238m))).Lines);

    // ------------------------------------------------------------- Hilfsmittel

    private static DetectionConfidence Confidence(string weak, string field)
        => weak == field ? DetectionConfidence.Medium : DetectionConfidence.High;

    private static string Money(DetectedValue<decimal>? value)
        => value!.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            .Replace('.', ',');

    /// <summary>
    /// Baut die Seite mit denselben Summen, die auch als Erkennungsergebnis
    /// übergeben werden. Eine Seite, die etwas anderes behauptet als der
    /// Testaufbau, wäre ein irreführender Zeuge.
    /// </summary>
    private async Task<PositionDetectionResult> Detect(DetectedTotals totals)
    {
        byte[] pdf = LayoutTablePdf.Create(
            WithoutVatColumn,
            [["Beratung", "2", "100,00", "200,00"]],
            net: Money(totals.Net),
            tax: Money(totals.Tax),
            gross: Money(totals.Gross),
            taxLabel: "Umsatzsteuer");

        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        PdfTextResult text = await _extractor.ExtractAsync(
            path, TestContext.Current.CancellationToken);

        return PositionDetector.Detect(text.Lines, totals);
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C3 – Menge und Einheit stehen in derselben Zelle.
///
/// Viele Rechnungen führen keine Einheitsspalte, schreiben die Einheit aber
/// trotzdem – direkt hinter die Menge: <c>4,00 HUR</c>, <c>2,5 Stunden</c>,
/// <c>10 m</c>. Diese Angabe ist vorhanden und darf deshalb nicht als
/// „fehlend“ behandelt werden.
///
/// **Und genau deshalb ist der Negativfall hier der wichtigere Test.**
/// <c>4,00 FASS</c> nennt eine Einheit, die diese Anwendung nicht kennt. Sie
/// als „keine Einheit angegeben“ durchzuwinken hieße, eine im Dokument
/// stehende Aussage zu unterschlagen. Die Tabelle wird verworfen – vollständig.
/// </summary>
public sealed class PositionCombinedUnitTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    /// <summary><c>Pos | Beschreibung | Menge | Einzelpreis | Gesamt</c>.</summary>
    private static readonly TableColumn[] CombinedLayout =
    [
        TableColumn.Text("Pos.", 30),
        TableColumn.Text("Beschreibung", 80),
        TableColumn.Text("Menge", 300),
        TableColumn.Money("Einzelpreis", 410, 465),
        TableColumn.Money("Gesamt", 490, 548),
    ];

    /// <summary>
    /// Die Zeilen- und Dokumentsummen stehen je Fall ausdrücklich dabei. Sie
    /// müssen zur Menge passen – eine Tabelle, deren Zeilensumme nicht zu
    /// Menge mal Preis passt, wird zu Recht verworfen, und der Test prüfte
    /// dann etwas anderes, als er behauptet.
    /// </summary>
    [Theory]
    [InlineData("4,00 HUR", 4, "HUR", "400,00", "76,00", "476,00")]
    [InlineData("4,00 Std.", 4, "HUR", "400,00", "76,00", "476,00")]
    [InlineData("4 Stk.", 4, "C62", "400,00", "76,00", "476,00")]
    [InlineData("4 Stunden", 4, "HUR", "400,00", "76,00", "476,00")]
    [InlineData("4 kg", 4, "KGM", "400,00", "76,00", "476,00")]
    [InlineData("4 m", 4, "MTR", "400,00", "76,00", "476,00")]
    [InlineData("1,00 C62", 1, "C62", "100,00", "19,00", "119,00")]
    public async Task EineEinheitHinterDerMengeWirdVerstanden(
        string cell,
        int quantity,
        string expectedUnit,
        string lineTotal,
        string tax,
        string gross)
    {
        PositionDetectionResult result = await Detect(
            LayoutTablePdf.Create(
                CombinedLayout,
                [["1", "Beratung", cell, "100,00", lineTotal]],
                net: lineTotal, tax: tax, gross: gross),
            net: decimal.Parse(lineTotal, GermanNumber),
            tax: decimal.Parse(tax, GermanNumber),
            gross: decimal.Parse(gross, GermanNumber));

        DetectedInvoiceLine line = Assert.Single(result.Lines);

        Assert.Equal(quantity, line.Quantity);
        Assert.Equal(expectedUnit, line.UnitCode);
        Assert.Equal(100.00m, line.NetUnitPrice);
    }

    /// <summary>Gebrochene Mengen bleiben gebrochen.</summary>
    [Fact]
    public async Task EineGebrocheneMengeMitEinheitBleibtErhalten()
    {
        PositionDetectionResult result = await Detect(
            Table("2,5 Stunden", price: "160,00", total: "400,00"));

        DetectedInvoiceLine line = Assert.Single(result.Lines);

        Assert.Equal(2.5m, line.Quantity);
        Assert.Equal("HUR", line.UnitCode);
    }

    /// <summary>
    /// **Der eigentliche Schutz.** Eine genannte, aber nicht unterstützte
    /// Einheit verwirft die ganze Tabelle. Sie ist etwas anderes als eine
    /// nicht genannte Einheit, und die Anwendung darf die beiden nie
    /// verwechseln.
    /// </summary>
    [Fact]
    public async Task EineUnbekannteEinheitHinterDerMengeVerwirftDieTabelle()
        => Assert.Empty((await Detect(Table("4,00 FASS"))).Lines);

    /// <summary>
    /// Auch eine einzelne unbekannte Einheit unter mehreren guten Zeilen
    /// verwirft alles. Vier von fünf Positionen sähen vollständig aus.
    /// </summary>
    [Fact]
    public async Task EineEinzigeUnbekannteEinheitVerwirftDieGanzeTabelle()
    {
        PositionDetectionResult result = await Detect(
            LayoutTablePdf.Create(
                CombinedLayout,
                [
                    ["1", "Beratung", "2 Std.", "100,00", "200,00"],
                    ["2", "Sonderposten", "1 FASS", "100,00", "100,00"],
                ],
                net: "300,00", tax: "57,00", gross: "357,00"),
            net: 300m, tax: 57m, gross: 357m);

        Assert.Empty(result.Lines);
    }

    /// <summary>
    /// Die Kontrollrechnung mischt Buchstabeneinheiten und den Zifferncode
    /// <c>C62</c> in einer Tabelle.
    ///
    /// **Warum das ein eigener Test ist:** Der Einheitenteil hinter der Menge
    /// nahm ursprünglich nur Buchstaben an. <c>HUR</c> ging damit durch,
    /// <c>C62</c> nicht – und weil eine nicht verstandene Zeile die ganze
    /// Tabelle verwirft, fiel an dieser einen Zeile die vollständige
    /// Kontrollrechnung. Ein Test mit lauter Buchstabeneinheiten hätte den
    /// Fehler nie gezeigt.
    /// </summary>
    [Fact]
    public async Task EineTabelleMitBuchstabenUndZifferncodesWirdVollständigErkannt()
    {
        PositionDetectionResult result = await Detect(
            LayoutTablePdf.Create(
                CombinedLayout,
                [
                    ["1", "Beratung", "4,00 HUR", "100,00", "400,00"],
                    ["2", "Schulung", "2,00 HUR", "100,00", "200,00"],
                    ["3", "Handbuch", "1,00 C62", "50,00", "50,00"],
                ],
                net: "650,00", tax: "123,50", gross: "773,50"),
            net: 650m, tax: 123.50m, gross: 773.50m);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(["HUR", "HUR", "C62"], result.Lines.Select(l => l.UnitCode));
        Assert.Equal([4m, 2m, 1m], result.Lines.Select(l => l.Quantity));
    }

    /// <summary>
    /// Ohne Einheit hinter der Menge bleibt es bei „nicht angegeben“. Der
    /// kombinierte Fall darf den Fall aus Slice C2 nicht verdrängen.
    /// </summary>
    [Fact]
    public async Task EineBloßeMengeBleibtOhneEinheit()
        => Assert.Null(Assert.Single((await Detect(Table("4"))).Lines).UnitCode);

    // ------------------------------------------------------------- Hilfsmittel

    private static readonly System.Globalization.CultureInfo GermanNumber =
        System.Globalization.CultureInfo.GetCultureInfo("de-DE");

    private static byte[] Table(
        string quantityCell, string price = "100,00", string total = "400,00")
        => LayoutTablePdf.Create(
            CombinedLayout,
            [["1", "Beratung", quantityCell, price, total]],
            net: "400,00", tax: "76,00", gross: "476,00");

    private Task<PositionDetectionResult> Detect(byte[] pdf)
        => Detect(pdf, 400m, 76m, 476m);

    private async Task<PositionDetectionResult> Detect(
        byte[] pdf, decimal net, decimal tax, decimal gross)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        PdfTextResult text = await _extractor.ExtractAsync(
            path, TestContext.Current.CancellationToken);

        // Ohne eigene Steuerspalte muss der Satz aus dem Dokument kommen – genau
        // ein sicher gelesener, sonst bleibt die Tabelle liegen.
        return PositionDetector.Detect(
            text.Lines, DetectedTotalsFixture.WithVatRate(net, tax, gross, 19m));
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

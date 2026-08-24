using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C2 – Tabellen ohne eigene Einheitsspalte.
///
/// **Warum das nötig wurde:** Phase A verlangte eine Einheitsspalte. Sehr
/// viele reale Rechnungen haben keine – sie schreiben schlicht
/// <c>Beschreibung | Menge | Preis | Gesamt | USt</c>. Bisher ergab das null
/// Positionen, und zwar nicht wegen einer Unsicherheit, sondern weil das
/// Modell den Fall nicht kannte.
///
/// Die Einheit fehlt dann **im Dokument**. Das ist eine sichere Aussage und
/// keine unsichere Erkennung. Alles andere – Menge, Preis, Steuersatz, das
/// Summen-Gate – bleibt unverändert streng.
/// </summary>
public sealed class PositionWithoutUnitColumnTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    /// <summary><c>Beschreibung | Menge | Preis | Gesamt | USt</c>.</summary>
    private static readonly TableColumn[] WithoutUnit =
    [
        TableColumn.Text("Beschreibung", 60),
        TableColumn.Text("Menge", 300),
        TableColumn.Money("Preis", 360, 415),
        TableColumn.Money("Gesamt", 450, 515),
        TableColumn.Text("USt", 550),
    ];

    [Fact]
    public async Task OhneEinheitsspalteWerdenDiePositionenTrotzdemErkannt()
    {
        PositionDetectionResult result = await Detect(
            LayoutTablePdf.Create(
                WithoutUnit,
                [
                    ["Beratung", "2", "100,00", "200,00", "19 %"],
                    ["Dokumentation", "1", "50,00", "50,00", "19 %"],
                ],
                net: "250,00", tax: "47,50", gross: "297,50"),
            Totals(250m, 47.50m, 297.50m));

        Assert.Equal(2, result.Lines.Count);

        Assert.Equal(1, result.Lines[0].Number);
        Assert.Equal("Beratung", result.Lines[0].Name);
        Assert.Equal(2m, result.Lines[0].Quantity);
        Assert.Equal(100.00m, result.Lines[0].NetUnitPrice);
        Assert.Equal(200.00m, result.Lines[0].ExplicitLineTotal);
        Assert.Equal(19m, result.Lines[0].VatRate);

        Assert.Equal(2, result.Lines[1].Number);
        Assert.Equal("Dokumentation", result.Lines[1].Name);
        Assert.Equal(1m, result.Lines[1].Quantity);
    }

    /// <summary>
    /// **Der Kern des Schnitts.** Ohne Einheitsspalte steht die Einheit nicht
    /// in der Rechnung – also steht sie auch nicht im Ergebnis. Kein
    /// Platzhalter, keine Ableitung aus der ganzzahligen Menge, kein „ist
    /// bestimmt Stück“.
    /// </summary>
    [Fact]
    public async Task OhneEinheitsspalteBleibtDieEinheitLeer()
    {
        PositionDetectionResult result = await Detect(
            LayoutTablePdf.Create(
                WithoutUnit,
                [["Beratung", "2", "100,00", "200,00", "19 %"]],
                net: "200,00", tax: "38,00", gross: "238,00"),
            Totals(200m, 38m, 238m));

        Assert.Null(Assert.Single(result.Lines).UnitCode);
    }

    /// <summary>
    /// Das Summen-Gate bleibt auch ohne Einheitsspalte verbindlich. Eine
    /// Tabelle, deren Positionen nicht zur Dokumentsumme passen, ist keine
    /// erkannte Tabelle – die fehlende Einheit lockert daran nichts.
    /// </summary>
    [Fact]
    public async Task DasSummenGateGiltOhneEinheitsspalteUnverändert()
    {
        PositionDetectionResult result = await Detect(
            LayoutTablePdf.Create(
                WithoutUnit,
                [["Beratung", "2", "100,00", "200,00", "19 %"]],
                net: "999,00", tax: "189,81", gross: "1188,81"),
            Totals(999m, 189.81m, 1188.81m));

        Assert.Empty(result.Lines);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private async Task<PositionDetectionResult> Detect(byte[] pdf, DetectedTotals totals)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        PdfTextResult text = await _extractor.ExtractAsync(
            path, TestContext.Current.CancellationToken);

        return PositionDetector.Detect(text.Lines, totals);
    }

    private static DetectedTotals Totals(decimal net, decimal tax, decimal gross) => new()
    {
        Net = High(net),
        Tax = High(tax),
        Gross = High(gross),
        VatRates = [],
    };

    private static DetectedValue<decimal> High(decimal value)
        => new(value, DetectionConfidence.High, "Synthetische Summenzeile", "Eindeutiger Testwert");

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

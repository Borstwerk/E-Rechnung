using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice 1 – der produktive Weg trägt die gegateten Positionen.
///
/// Phase A hat die Erkennung isoliert nachgewiesen. Hier geht es um die eine
/// Frage, die dort offenblieb: Kommt das Ergebnis überhaupt bis zum
/// <see cref="InvoiceDetectionResult"/>, und zwar ausschließlich dann, wenn
/// das Phase-A-Gate gehalten hat?
///
/// Die Positionen reisen über eine **interne** Eigenschaft. Alle Verbraucher –
/// Detektor, Übersicht, Vorbefüllung – liegen im Kern; eine öffentliche
/// Erkennungs-API entsteht dafür nicht.
/// </summary>
public sealed class PositionTransportTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);
    private readonly InvoiceDataDetector _detector;

    public PositionTransportTests()
        => _detector = new InvoiceDataDetector(_extractor, NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task UnterstützteTabelleErreichtDasErkennungsergebnis()
    {
        InvoiceDetectionResult detection = await Detect(PositionTablePdf.TwoLines());

        Assert.Equal(2, detection.Lines.Count);

        DetectedInvoiceLine first = detection.Lines[0];
        Assert.Equal(1, first.Number);
        Assert.Equal("Beratung", first.Name);
        Assert.Equal(2m, first.Quantity);
        Assert.Equal("HUR", first.UnitCode);
        Assert.Equal(100.00m, first.NetUnitPrice);
        Assert.Equal(19m, first.VatRate);

        DetectedInvoiceLine second = detection.Lines[1];
        Assert.Equal(2, second.Number);
        Assert.Equal("Schulungsunterlagen", second.Name);
        Assert.Equal(3m, second.Quantity);
        Assert.Equal("C62", second.UnitCode);
        Assert.Equal(20.00m, second.NetUnitPrice);
    }

    /// <summary>
    /// Die Musterrechnung ohne Tabellenkopf bleibt die Negativreferenz. Sie
    /// darf auch über den produktiven Weg keine einzige Position erzeugen.
    /// </summary>
    [Fact]
    public async Task MusterrechnungOhneTabellenkopfBleibtOhnePositionen()
    {
        byte[] pdf = TextPdfBuilder.Create(
            "RECHNUNG 04-2026",
            "Für nachfolgend benannte Leistung berechne ich Ihnen wie folgt:",
            "Arbeitsleistung Netto 40,00 EUR / je Arbeitsstunde",
            "25,5 Arbeitsstunden (Nachweis laut Anlage)",
            "Gesamt Netto 1.020,00 EUR",
            "Zzgl. 19% MwSt. 193,80 EUR",
            "Gesamt Brutto 1.213,80 EUR");

        InvoiceDetectionResult detection = await Detect(pdf);

        Assert.Empty(detection.Lines);
    }

    /// <summary>
    /// Eine unbekannte Einheit verwirft in Phase A die ganze Tabelle. Über den
    /// produktiven Weg darf davon nichts übrig bleiben – auch nicht die
    /// Zeilen, deren Einheit für sich genommen zulässig wäre.
    /// </summary>
    [Fact]
    public async Task UnbekannteEinheitLässtKeineTeilmengeDurch()
    {
        byte[] pdf = PositionTablePdf.Create(
            net: "260,00",
            tax: "49,40",
            gross: "309,40",
            rows:
            [
                new PositionTableRow("Beratung", "2", "Std", "100,00", "200,00", "19 %"),
                new PositionTableRow("Sonderposten", "3", "Fass", "20,00", "60,00", "19 %"),
            ]);

        InvoiceDetectionResult detection = await Detect(pdf);

        Assert.Empty(detection.Lines);
    }

    /// <summary>
    /// Erkannte Positionen sind ein brauchbarer Fund. Ohne diese Ergänzung
    /// könnte eine Tabelle erkannt sein, während das Ergebnis meldet, es sei
    /// nichts gefunden worden.
    /// </summary>
    [Fact]
    public void ErkanntePositionenZählenAlsFund()
    {
        var detection = new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line()],
        };

        Assert.True(detection.HasAnything);
    }

    [Fact]
    public void OhnePositionenUndOhneFelderBleibtEsBeiNichtsGefunden()
        => Assert.False(new InvoiceDetectionResult { HasUsableText = true }.HasAnything);

    private static DetectedInvoiceLine Line() => new(
        1, "Beratung", null, 1m, "HUR", 100m, 100m,
        EInvoiceSender.Core.Models.VatCategory.StandardRate, 19m, []);

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

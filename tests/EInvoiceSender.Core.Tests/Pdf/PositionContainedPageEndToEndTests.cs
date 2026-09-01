using System.Globalization;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Der echte PDF-Pfad für mehrseitige Dokumente, deren eine unterstützte
/// Positionstabelle vollständig auf genau einer Seite liegt.
/// </summary>
public sealed class PositionContainedPageEndToEndTests : IDisposable
{
    private const double PositionLeft = 30;
    private const double DescriptionLeft = 100;
    private const double QuantityLeft = 310;
    private const double UnitLeft = 350;
    private const double UnitPriceRight = 465;
    private const double LineTotalRight = 548;
    private const double VatLeft = 560;

    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task VollständigeTabelleUndZahlungshinweisAufGetrenntenSeitenWerdenErkannt()
    {
        byte[] pdf = TextPdfBuilder.CreatePositionedPages(
            TablePage(),
            [new PositionedPdfText("Zahlbar innerhalb von 14 Tagen.", 56, 60)]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal(2, result.PageCount);
        DetectedInvoiceLine line = Assert.Single(result.Lines);
        Assert.Equal("Beratung", line.Name);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal("HUR", line.UnitCode);
        Assert.Equal(100m, line.NetUnitPrice);
        Assert.Equal(19m, line.VatRate);
    }

    [Fact]
    public async Task HeaderloseFortsetzungAufZweiterSeiteVerwirftDiePositionen()
    {
        byte[] pdf = TextPdfBuilder.CreatePositionedPages(
            TablePage(),
            [.. Row(2, "Dokumentation", "1", "Std", "50,00", "50,00", "19 %", 80)]);

        InvoiceDetectionResult result = await Detect(pdf);

        Assert.Equal(2, result.PageCount);
        Assert.Empty(result.Lines);
    }

    private async Task<InvoiceDetectionResult> Detect(byte[] pdf)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        return await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);
    }

    private static IReadOnlyList<PositionedPdfText> TablePage()
        =>
        [
            new("Muster IT GmbH · Musterstraße 10 · 18055 Rostock", 56, 32),
            new("Rechnungsnummer: RE-2026-0815 · Rechnungsdatum: 09.08.2026", 56, 50),
            new("Pos.", PositionLeft, 100),
            new("Beschreibung", DescriptionLeft, 100),
            new("Menge", QuantityLeft, 100),
            new("Einheit", UnitLeft, 100),
            new("Einzelpreis", 400, 100),
            new("Gesamtpreis", 490, 100),
            new("MwSt", VatLeft, 100),
            .. Row(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %", 126),
            new("Gesamt Netto", DescriptionLeft, 150),
            Right("100,00", LineTotalRight, 150),
            new("Umsatzsteuer 19 %", DescriptionLeft, 168),
            Right("19,00", LineTotalRight, 168),
            new("Gesamtbetrag", DescriptionLeft, 186),
            Right("119,00", LineTotalRight, 186),
        ];

    private static IEnumerable<PositionedPdfText> Row(
        int number,
        string description,
        string quantity,
        string unit,
        string unitPrice,
        string lineTotal,
        string vat,
        double top)
    {
        yield return new PositionedPdfText(
            number.ToString(CultureInfo.InvariantCulture), PositionLeft, top);
        yield return new PositionedPdfText(description, DescriptionLeft, top);
        yield return new PositionedPdfText(quantity, QuantityLeft, top);
        yield return new PositionedPdfText(unit, UnitLeft, top);
        yield return Right(unitPrice, UnitPriceRight, top);
        yield return Right(lineTotal, LineTotalRight, top);
        yield return new PositionedPdfText(vat, VatLeft, top);
    }

    private static PositionedPdfText Right(string text, double right, double top)
        => new(text, right - Width(text), top);

    private static double Width(string text)
        => text.Sum(character => character is '.' or ',' ? 2.78 : 5.56);

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

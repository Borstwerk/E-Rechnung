using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice 8 – der ganze Weg an einem Stück: PDF, Erkennung, Vorbefüllung,
/// Entwurf, Rechnung, Berechnung, CII.
///
/// **Warum trotz aller Einzeltests noch dieser:** Jede Stufe ist für sich
/// geprüft, und trotzdem kann der Weg brechen. Eine Menge, die als „2,5“ im
/// Formular steht, muss der Entwurf zurücklesen; eine Einheit muss durch das
/// Regelwerk kommen; eine Beschreibung muss als BT-154 in der CII-Datei
/// landen. Genau diese Übergänge prüft niemand sonst.
///
/// Die Seite enthält absichtlich vier Einheiten (HUR, C62, KGM, MTR) und zwei
/// Steuersätze (19 % und 7 %). Ein einziger Steuersatz und eine einzige
/// Einheit hätten jeden Vertauscher unsichtbar gelassen.
/// </summary>
public sealed class PositionEndToEndTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    /// <summary>
    /// Vier Positionen, zwei Steuersätze:
    ///
    /// <list type="bullet">
    ///   <item>2 Std à 100,00 = 200,00 zu 19 %</item>
    ///   <item>3 Stk à 20,00 = 60,00 zu 7 %</item>
    ///   <item>5 kg à 4,00 = 20,00 zu 7 %</item>
    ///   <item>10 m à 2,50 = 25,00 zu 19 %</item>
    /// </list>
    ///
    /// Netto 305,00 · Steuer 42,75 (19 % auf 225,00) + 5,60 (7 % auf 80,00)
    /// = 48,35 · Brutto 353,35.
    /// </summary>
    private static byte[] MixedTable() => PositionTablePdf.Create(
        net: "305,00",
        tax: "48,35",
        gross: "353,35",
        taxLabel: "Umsatzsteuer",
        rows:
        [
            new PositionTableRow(
                "Beratung", "2", "Std", "100,00", "200,00", "19 %",
                Continuation: "Vor-Ort und Nachsorge"),
            new PositionTableRow("Schulungsunterlagen", "3", "Stk", "20,00", "60,00", "7 %"),
            new PositionTableRow("Verpackungsmaterial", "5", "kg", "4,00", "20,00", "7 %"),
            new PositionTableRow("Kabel", "10", "m", "2,50", "25,00", "19 %"),
        ]);

    [Fact]
    public async Task DieErkannteTabelleErreichtDenEntwurfVollständig()
    {
        InvoiceDraft draft = await PrefilledFromPdf();

        Assert.Equal(4, draft.Lines.Count);

        Assert.Equal(
            ["HUR", "C62", "KGM", "MTR"],
            draft.Lines.Select(l => l.Unit));

        Assert.Equal(
            ["19", "7", "7", "19"],
            draft.Lines.Select(l => l.VatRate));

        Assert.Equal(
            ["2", "3", "5", "10"],
            draft.Lines.Select(l => l.Quantity));

        Assert.Equal(
            ["100,00", "20,00", "4,00", "2,50"],
            draft.Lines.Select(l => l.NetUnitPrice));
    }

    /// <summary>
    /// Die Fortsetzungszeile wird zur Beschreibung. Sie ist der einzige Wert
    /// der Tabelle, den man im Formular ohne eigene Spalte nicht sähe – und
    /// steht deshalb seit Slice 6 in einer.
    /// </summary>
    [Fact]
    public async Task DieFortsetzungszeileWirdZurBeschreibung()
    {
        InvoiceDraft draft = await PrefilledFromPdf();

        Assert.Equal("Vor-Ort und Nachsorge", draft.Lines[0].Description);
        Assert.Equal(string.Empty, draft.Lines[1].Description);
    }

    /// <summary>
    /// Der Entwurf muss seine eigenen Zeichenketten zurücklesen können. Tut er
    /// es nicht, steht eine vollständig aussehende Tabelle im Formular, aus der
    /// sich keine Rechnung bauen lässt.
    /// </summary>
    [Fact]
    public async Task AusDemVorbefülltenEntwurfEntstehtEineRechnungOhneBefund()
    {
        InvoiceDraft draft = await CompleteDraft();

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);
        Assert.Equal(4, invoice.Lines.Count);
    }

    /// <summary>
    /// **Die eigentliche Probe.** Der Rechner kommt auf dieselben Summen, die
    /// in der PDF stehen. Damit ist der Weg von der gelesenen Zeile bis zur
    /// gerechneten Summe geschlossen – und nicht nur jede Stufe für sich
    /// plausibel.
    /// </summary>
    [Fact]
    public async Task DieGerechnetenSummenTreffenDieDerPdf()
    {
        InvoiceDraft draft = await CompleteDraft();
        draft.TryBuildInvoice(out Invoice? invoice);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice!);

        Assert.Equal(305.00m, totals.LineTotal);
        Assert.Equal(48.35m, totals.TaxTotal);
        Assert.Equal(353.35m, totals.GrandTotal);

        Assert.Equal(
            [(7m, 80.00m, 5.60m), (19m, 225.00m, 42.75m)],
            totals.VatBreakdown
                .OrderBy(e => e.Rate)
                .Select(e => (e.Rate, e.TaxableAmount, e.TaxAmount)));
    }

    /// <summary>
    /// Das Regelwerk EN 16931 nimmt die Rechnung ohne Beanstandung an. Eine
    /// Einheit oder ein Steuersatz, den die Erkennung durchgelassen hätte und
    /// die Norm nicht kennt, fiele spätestens hier auf.
    /// </summary>
    [Fact]
    public async Task DasRegelwerkNimmtDieVorbefüllteRechnungAn()
    {
        InvoiceDraft draft = await CompleteDraft();
        draft.TryBuildInvoice(out Invoice? invoice);

        ValidationReport report = new En16931RuleValidator().Validate(
            invoice!, InvoiceCalculator.Calculate(invoice!));

        Assert.False(report.HasErrors, Describe(report));
    }

    /// <summary>
    /// In der CII-Datei stehen alle vier Einheiten, beide Steuersätze und die
    /// Beschreibung als BT-154.
    /// </summary>
    [Fact]
    public async Task DieCiiDateiTrägtEinheitenSteuersätzeUndBeschreibung()
    {
        InvoiceDraft draft = await CompleteDraft();
        draft.TryBuildInvoice(out Invoice? invoice);

        string xml = Encoding.UTF8.GetString(
            new CiiInvoiceWriter().Write(invoice!, InvoiceCalculator.Calculate(invoice!)));

        Assert.Contains("unitCode=\"HUR\"", xml, StringComparison.Ordinal);
        Assert.Contains("unitCode=\"C62\"", xml, StringComparison.Ordinal);
        Assert.Contains("unitCode=\"KGM\"", xml, StringComparison.Ordinal);
        Assert.Contains("unitCode=\"MTR\"", xml, StringComparison.Ordinal);

        Assert.Contains(
            "<ram:Description>Vor-Ort und Nachsorge</ram:Description>",
            xml,
            StringComparison.Ordinal);

        Assert.Contains(">7</ram:RateApplicablePercent>", xml, StringComparison.Ordinal);
        Assert.Contains(">19</ram:RateApplicablePercent>", xml, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private async Task<InvoiceDraft> PrefilledFromPdf()
    {
        string path = TestPdfFactory.WriteToTempFile(MixedTable());
        _temporaryFiles.Add(path);

        InvoiceDetectionResult detection = await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);

        Assert.Equal(4, detection.Lines.Count);

        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, detection);

        return draft;
    }

    /// <summary>
    /// Der vorbefüllte Entwurf plus die Angaben, die keine Positionstabelle
    /// liefert. Sie werden hier gesetzt und nicht erkannt: Geprüft wird der Weg
    /// der Positionen, nicht der der Kopfdaten.
    /// </summary>
    private async Task<InvoiceDraft> CompleteDraft()
    {
        InvoiceDraft draft = await PrefilledFromPdf();

        draft.InvoiceNumber = "RE-2026-0815";
        draft.IssueDate = new DateOnly(2026, 8, 9);

        draft.SellerName = "Muster IT GmbH";
        draft.SellerStreet = "Musterstraße 10";
        draft.SellerPostalCode = "18055";
        draft.SellerCity = "Rostock";
        draft.SellerCountry = "DE";
        draft.SellerVatId = "DE123456789";

        draft.BuyerName = "Nordlicht Handel GmbH";
        draft.BuyerStreet = "Hafenweg 3";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerCountry = "DE";

        draft.BankAccountHolder = "Muster IT GmbH";
        draft.BankIban = "DE89370400440532013000";
        draft.PaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.";
        draft.DueDate = new DateOnly(2026, 8, 23);

        return draft;
    }

    private static string Describe(ValidationReport report)
        => string.Join(
            Environment.NewLine,
            report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

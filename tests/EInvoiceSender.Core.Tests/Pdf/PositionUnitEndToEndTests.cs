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
/// Slice C10 – der ganze Weg für die beiden Phase-C-Fälle.
///
/// **Fall 1:** Die Rechnung nennt ihre Einheiten hinter der Menge und führt
/// keine Steuerspalte. PDF, Erkennung, Vorbefüllung, Rechnung, Berechnung,
/// CII – ohne Zutun.
///
/// **Fall 2:** Die Rechnung nennt gar keine Einheit. Der Weg muss an genau
/// einer Stelle stehen bleiben und dort auf den Menschen warten. Danach läuft
/// er normal weiter.
///
/// Der zweite Fall ist der eigentliche Beweis dieser Phase: **Eine fehlende
/// Information wird nicht zu einer erfundenen.** Sie bleibt sichtbar leer,
/// die bestehende Prüfung hält die Rechnung auf, und erst die Ergänzung durch
/// den Anwender macht den Weg frei.
/// </summary>
public sealed class PositionUnitEndToEndTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    /// <summary>Einheit hinter der Menge, keine Steuerspalte.</summary>
    private static byte[] CombinedUnitTable() => LayoutTablePdf.Create(
        [
            TableColumn.Text("Pos.", 30),
            TableColumn.Text("Beschreibung", 80),
            TableColumn.Text("Menge", 300),
            TableColumn.Money("Einzelpreis", 410, 465),
            TableColumn.Money("Gesamt", 490, 548),
        ],
        [
            ["1", "Beratung", "2 Std", "100,00", "200,00"],
            ["2", "Kabel", "10 m", "5,00", "50,00"],
        ],
        net: "250,00", tax: "47,50", gross: "297,50");

    /// <summary>Nirgends eine Mengeneinheit.</summary>
    private static byte[] UnitlessTable() => LayoutTablePdf.Create(
        [
            TableColumn.Text("Beschreibung", 60),
            TableColumn.Text("Menge", 250),
            TableColumn.Money("Einzelpreis", 320, 370),
            TableColumn.Money("Gesamt", 420, 470),
        ],
        [["Beratung", "2", "100,00", "200,00"]],
        net: "200,00", tax: "38,00", gross: "238,00");

    // ------------------------------------------------- Fall 1: vollständig

    [Fact]
    public async Task MitGenannterEinheitLäuftDerWegOhneZutunDurch()
    {
        InvoiceDraft draft = await Complete(CombinedUnitTable());

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);
        Assert.Equal(["HUR", "MTR"], invoice.Lines.Select(l => l.Unit.Value));

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(250.00m, totals.LineTotal);
        Assert.Equal(47.50m, totals.TaxTotal);
        Assert.Equal(297.50m, totals.GrandTotal);

        string xml = Encoding.UTF8.GetString(new CiiInvoiceWriter().Write(invoice, totals));

        Assert.Contains("unitCode=\"HUR\"", xml, StringComparison.Ordinal);
        Assert.Contains("unitCode=\"MTR\"", xml, StringComparison.Ordinal);
    }

    // ------------------------------------------- Fall 2: fehlende Einheit

    /// <summary>
    /// Der Entwurf trägt die Lücke offen. Kein <c>C62</c>, kein Platzhalter –
    /// ein leeres Feld, das man sieht.
    /// </summary>
    [Fact]
    public async Task OhneGenannteEinheitBleibtDasFeldImEntwurfLeer()
    {
        InvoiceDraft draft = await Complete(UnitlessTable());

        InvoiceLineDraft line = Assert.Single(draft.Lines);

        Assert.Equal("Beratung", line.Name);
        Assert.Equal("2", line.Quantity);
        Assert.Equal("100,00", line.NetUnitPrice);
        Assert.Equal(string.Empty, line.Unit);
    }

    /// <summary>Und die Rechnung entsteht so nicht.</summary>
    [Fact]
    public async Task OhneGenannteEinheitEntstehtZunächstKeineRechnung()
    {
        InvoiceDraft draft = await Complete(UnitlessTable());

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.Null(invoice);
        Assert.True(report.HasErrors);
    }

    /// <summary>
    /// **Der Abschluss der Beweiskette.** Nach der Ergänzung durch den
    /// Anwender läuft derselbe Weg vollständig durch – bis in die CII-Datei.
    /// Die Lücke hat aufgehalten, nicht gesperrt, und sie wurde nie gefüllt,
    /// ohne dass ein Mensch sie gefüllt hat.
    /// </summary>
    [Fact]
    public async Task NachDemErgänzenLäuftDerWegVollständigDurch()
    {
        InvoiceDraft draft = await Complete(UnitlessTable());

        draft.Lines[0].Unit = "HUR";

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);

        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        Assert.Equal(200.00m, totals.LineTotal);
        Assert.Equal(38.00m, totals.TaxTotal);
        Assert.Equal(238.00m, totals.GrandTotal);

        ValidationReport rules = new En16931RuleValidator().Validate(invoice, totals);
        Assert.False(rules.HasErrors, Describe(rules));

        string xml = Encoding.UTF8.GetString(new CiiInvoiceWriter().Write(invoice, totals));
        Assert.Contains("unitCode=\"HUR\"", xml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Schritt 1 und Schritt 2 sagen beide, dass die Einheit fehlt. Ohne diese
    /// Sätze stünde der Anwender vor einem leeren Feld und hielte es für einen
    /// Fehler der Anwendung.
    /// </summary>
    [Fact]
    public async Task BeideSchritteBenennenDieFehlendeEinheit()
    {
        InvoiceDetectionResult detection = await Detect(UnitlessTable());

        string schritt1 = Assert.Single(
            DetectionOverview.Describe(detection),
            e => e.Text.Contains("Rechnungspositionen", StringComparison.Ordinal)).Text;

        Assert.Contains("keine Mengeneinheit angegeben", schritt1, StringComparison.Ordinal);

        var draft = new InvoiceDraft();
        PrefillSummary summary = DraftPrefiller.Apply(draft, detection);

        Assert.Equal(1, summary.LinesMissingUnit);
        Assert.Contains(
            "fehlt die Mengeneinheit",
            PrefillNotice.Describe(summary),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private async Task<InvoiceDetectionResult> Detect(byte[] pdf)
    {
        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        return await _detector.DetectAsync(path, null, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Der vorbefüllte Entwurf plus die Kopfdaten, die keine Positionstabelle
    /// liefert. Geprüft wird der Weg der Positionen, nicht der der Kopfdaten.
    /// </summary>
    private async Task<InvoiceDraft> Complete(byte[] pdf)
    {
        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, await Detect(pdf));

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

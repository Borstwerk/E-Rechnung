using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C9 – vier Tabellenfamilien, wie sie in echten Rechnungen vorkommen,
/// über den **produktiven** Erkennungsweg.
///
/// Die vorangegangenen Schnitte prüfen den Positionsdetektor mit von Hand
/// gesetzten Dokumentsummen. Hier läuft alles über
/// <see cref="InvoiceDataDetector"/>: echter PDF-Extractor, echte
/// Summenerkennung, echtes Summen-Gate. Erst damit ist gezeigt, dass die
/// Familien nicht nur im Labor funktionieren.
///
/// Die Seiten sind sämtlich synthetisch. Es liegt keine fremde Rechnung, kein
/// Screenshot und keine echte Kundendatei im Repository.
/// </summary>
public sealed class PositionLayoutFamilyTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    /// <summary>
    /// P1 – <c>Name/Beschreibung | Menge | Preis | USt. | Gesamtpreis</c>.
    /// Die Rechnung nennt keine Mengeneinheit.
    /// </summary>
    [Fact]
    public async Task P1OhneMengeneinheitMitEigenerSteuerspalte()
    {
        InvoiceDetectionResult detection = await Detect(
            [
                TableColumn.Text("Name/Beschreibung", 60),
                TableColumn.Text("Menge", 250),
                TableColumn.Money("Preis", 320, 370),
                TableColumn.Text("USt.", 420),
                TableColumn.Money("Gesamtpreis", 470, 540),
            ],
            [
                ["Beratung", "2", "100,00", "19 %", "200,00"],
                ["Schulung", "1", "50,00", "19 %", "50,00"],
            ],
            net: "250,00", tax: "47,50", gross: "297,50");

        Assert.Equal(2, detection.Lines.Count);
        Assert.All(detection.Lines, line => Assert.Null(line.UnitCode));
        Assert.Equal([2m, 1m], detection.Lines.Select(l => l.Quantity));
        Assert.Equal([100.00m, 50.00m], detection.Lines.Select(l => l.NetUnitPrice));
    }

    /// <summary>
    /// P2 – <c>Pos. | Artikelbeschreibung | Menge | Preis | Betrag</c>. Weder
    /// Mengeneinheit noch Steuerspalte: Der Satz stammt aus dem einen
    /// Dokumentsteuersatz, den die Dokumentsummen arithmetisch bestätigen.
    /// </summary>
    [Fact]
    public async Task P2OhneMengeneinheitUndOhneSteuerspalte()
    {
        InvoiceDetectionResult detection = await Detect(
            [
                TableColumn.Text("Pos.", 30),
                TableColumn.Text("Artikelbeschreibung", 70),
                TableColumn.Text("Menge", 300),
                TableColumn.Money("Preis", 360, 415),
                TableColumn.Money("Betrag", 460, 540),
            ],
            [
                ["1", "Beratung", "2", "100,00", "200,00"],
                ["2", "Schulung", "1", "50,00", "50,00"],
            ],
            net: "250,00", tax: "47,50", gross: "297,50");

        Assert.Equal(2, detection.Lines.Count);
        Assert.All(detection.Lines, line => Assert.Null(line.UnitCode));
        Assert.Equal([19m, 19m], detection.Lines.Select(l => l.VatRate));
    }

    /// <summary>
    /// P3 – <c>Pos. | Art.-Nr. | Bezeichnung | Menge | Einheit | Lieferdatum |
    /// E-Preis | Gesamt</c>. Mit Einheitsspalte und einer fachlich inerten
    /// Zusatzspalte.
    /// </summary>
    [Fact]
    public async Task P3MitEinheitUndLieferdatum()
    {
        InvoiceDetectionResult detection = await Detect(
            [
                TableColumn.Text("Pos.", 28),
                TableColumn.Text("Art.-Nr.", 55),
                TableColumn.Text("Bezeichnung", 120),
                TableColumn.Text("Menge", 255),
                TableColumn.Text("Einheit", 300),
                TableColumn.Text("Lieferdatum", 350),
                TableColumn.Money("E-Preis", 430, 480),
                TableColumn.Money("Gesamt", 505, 560),
            ],
            [
                ["1", "ART-1", "Beratung", "2", "Std", "15.03.2026", "100,00", "200,00"],
                ["2", "ART-2", "Kabel", "10", "m", "16.03.2026", "5,00", "50,00"],
            ],
            net: "250,00", tax: "47,50", gross: "297,50");

        Assert.Equal(2, detection.Lines.Count);
        Assert.Equal(["HUR", "MTR"], detection.Lines.Select(l => l.UnitCode));
    }

    /// <summary>
    /// P4 – <c>Bezeichnung | Menge | Preis in € | Netto | USt in % | USt in € |
    /// Brutto</c>. Mit ausgeschriebenen Kontrollbeträgen je Position.
    /// </summary>
    [Fact]
    public async Task P4MitAusgeschriebenenKontrollbeträgen()
    {
        InvoiceDetectionResult detection = await Detect(
            [
                TableColumn.Text("Bezeichnung", 45),
                TableColumn.Text("Menge", 190),
                TableColumn.Money("Preis in €", 240, 300),
                TableColumn.Money("Netto", 330, 380),
                TableColumn.Text("USt in %", 410),
                TableColumn.Money("USt in €", 470, 520),
                TableColumn.Money("Brutto", 540, 585),
            ],
            [
                ["Beratung", "2", "100,00", "200,00", "19 %", "38,00", "238,00"],
                ["Schulung", "1", "50,00", "50,00", "19 %", "9,50", "59,50"],
            ],
            net: "250,00", tax: "47,50", gross: "297,50");

        Assert.Equal(2, detection.Lines.Count);
        Assert.All(detection.Lines, line => Assert.Null(line.UnitCode));
        Assert.Equal([200.00m, 50.00m], detection.Lines.Select(l => l.ExplicitLineTotal));
    }

    /// <summary>
    /// **Die verbindliche Gegenreferenz.** Die anonymisierte Struktur einer
    /// echten Kundenrechnung hat keinen Tabellenkopf. Sie muss auch nach allen
    /// Erweiterungen null Positionen ergeben – sonst hätte Phase C genau das
    /// aufgeweicht, was sie schützen soll.
    /// </summary>
    [Fact]
    public async Task DieMusterstrukturOhneTabellenkopfBleibtOhnePositionen()
    {
        byte[] pdf = TextPdfBuilder.Create(
            "RECHNUNG 04-2026",
            "Für nachfolgend benannte Leistung berechne ich Ihnen wie folgt:",
            "Arbeitsleistung Netto 40,00 EUR / je Arbeitsstunde",
            "25,5 Arbeitsstunden (Nachweis laut Anlage)",
            "Gesamt Netto 1.020,00 EUR",
            "Zzgl. 19% MwSt. 193,80 EUR",
            "Gesamt Brutto 1.213,80 EUR");

        Assert.Empty((await DetectPdf(pdf)).Lines);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private Task<InvoiceDetectionResult> Detect(
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string net,
        string tax,
        string gross)
        => DetectPdf(LayoutTablePdf.Create(columns, rows, net, tax, gross));

    private async Task<InvoiceDetectionResult> DetectPdf(byte[] pdf)
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

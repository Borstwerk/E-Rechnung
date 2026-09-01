using System.Globalization;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Das Phase-A-Gate für die isolierte Positionserkennung. Positive Vorgaben
/// müssen vollständig stimmen; jeder unsichere oder widersprüchliche Aufbau
/// liefert genau null Positionen.
/// </summary>
public sealed class PositionDetectorTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    [Fact]
    public async Task KlassischeTabelleWirdVollständigErkannt()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "IT-Beratung", "2", "Std.", "100,00", "200,00", "19 %");
        fixture.AddRow(2, "Dokumentation", "1", "Stk.", "50,00", "50,00", "19 %");
        fixture.AddTotals("250,00", "47,50", "297,50");

        PositionDetectionResult result = await Detect(
            fixture, Totals(250m, 47.50m, 297.50m));

        Assert.Equal(2, result.Lines.Count);
        AssertLine(result.Lines[0], 1, "IT-Beratung", null, 2m, "HUR", 100m, 200m, 19m);
        AssertLine(result.Lines[1], 2, "Dokumentation", null, 1m, "C62", 50m, 50m, 19m);
    }

    [Fact]
    public async Task AlternativeUndMehrteiligeHeaderfamilienSindEindeutig()
    {
        TableFixture fixture = TableFixture.WithAlternativeHeader();

        PositionDetectionResult result = await Detect(fixture, Totals(100m, 19m, 119m));

        AssertLine(
            Assert.Single(result.Lines),
            1, "Beratung", null, 1m, "HUR", 100m, 100m, 19m);
    }

    [Fact]
    public async Task PositionsnummernspalteIstOptionalUndWirdDeterministischErsetzt()
    {
        TableFixture fixture = TableFixture.WithoutPositionColumn();

        PositionDetectionResult result = await Detect(fixture, Totals(150m, 28.50m, 178.50m));

        Assert.Equal([1, 2], result.Lines.Select(line => line.Number));
    }

    [Fact]
    public async Task RechtsbündigeUnterschiedlichLangePreiseBleibenInIhrenSpalten()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Kurzleistung", "1", "Std", "40,00", "40,00", "19 %");
        fixture.AddRow(2, "Langbetrag", "25,5", "Std", "1.240,00", "31.620,00", "19 %");
        fixture.AddTotals("31.660,00", "6.015,40", "37.675,40");

        PdfTextResult text = await Extract(fixture);
        PdfTextLine[] rows =
        [
            .. text.Lines.Where(line => line.Text.Contains("Kurzleistung", StringComparison.Ordinal)
                                        || line.Text.Contains("Langbetrag", StringComparison.Ordinal)),
        ];

        PdfTextToken shortPrice = rows[0].Tokens
            .Where(token => token.Text == "40,00")
            .OrderBy(token => token.Left)
            .First();
        PdfTextToken longPrice = rows[1].Tokens
            .Where(token => token.Text == "1.240,00")
            .OrderBy(token => token.Left)
            .First();
        Assert.True(longPrice.Left < shortPrice.Left);
        Assert.InRange(Math.Abs(longPrice.Right - shortPrice.Right), 0d, 0.1d);

        PositionDetectionResult result = PositionDetector.Detect(
            text.Lines, Totals(31660m, 6015.40m, 37675.40m));

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(40m, result.Lines[0].NetUnitPrice);
        Assert.Equal(1240m, result.Lines[1].NetUnitPrice);
        Assert.Equal(31620m, result.Lines[1].ExplicitLineTotal);
    }

    [Fact]
    public async Task MehrereBeschreibungszeilenWerdenNurDerVorherigenPositionZugeordnet()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Systempflege", "2", "Std", "80,00", "160,00", "19 %");
        fixture.AddDescription("einschließlich Dokumentation");
        fixture.AddDescription("und kontrollierter Übergabe");
        fixture.AddRow(2, "Abschlussprüfung", "1", "Std", "40,00", "40,00", "19 %");
        fixture.AddTotals("200,00", "38,00", "238,00");

        PositionDetectionResult result = await Detect(fixture, Totals(200m, 38m, 238m));

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(
            "einschließlich Dokumentation und kontrollierter Übergabe",
            result.Lines[0].Description);
        Assert.Null(result.Lines[1].Description);
    }

    [Fact]
    public async Task HarmloseArtikelnummernspalteDarfZusätzlichVorhandenSein()
    {
        var fixture = new TableFixture(hasArticleNumber: true);
        fixture.AddRow(
            1, "Netzwerkprüfung", "3", "Std", "70,00", "210,00", "19 %",
            articleNumber: "ART-2026-17");
        fixture.AddTotals("210,00", "39,90", "249,90");

        PositionDetectionResult result = await Detect(fixture, Totals(210m, 39.90m, 249.90m));

        DetectedInvoiceLine line = Assert.Single(result.Lines);
        Assert.Equal("Netzwerkprüfung", line.Name);
        Assert.DoesNotContain("ART-2026-17", line.Name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Stk.", "C62")]
    [InlineData("Stück", "C62")]
    [InlineData("Std.", "HUR")]
    [InlineData("Stunden", "HUR")]
    [InlineData("kg", "KGM")]
    [InlineData("Meter", "MTR")]
    public async Task FreigegebeneEinheitenWerdenExplizitAbgebildet(
        string pdfUnit, string expectedCode)
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Prüfleistung", "2", pdfUnit, "50,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        PositionDetectionResult result = await Detect(fixture, Totals(100m, 19m, 119m));

        Assert.Equal(expectedCode, Assert.Single(result.Lines).UnitCode);
    }

    [Fact]
    public async Task SiebenUndNeunzehnProzentBleibenPositionsbezogenGetrennt()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Dokumentation", "1", "Stk", "100,00", "100,00", "7 %");
        fixture.AddRow(2, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("200,00", "26,00", "226,00");

        PositionDetectionResult result = await Detect(
            fixture, Totals(200m, 26m, 226m, rates: [High(7m), High(19m)]));

        Assert.Equal([7m, 19m], result.Lines.Select(line => line.VatRate));
        Assert.All(result.Lines,
            line => Assert.Equal(VatCategory.StandardRate, line.VatCategory));
    }

    [Fact]
    public async Task FehlendeVatSpalteNutztGenauEinenSicherenDokumentsatz()
    {
        var fixture = new TableFixture(hasVat: false);
        fixture.AddRow(1, "Beratung", "2", "Std", "75,00", "150,00", null);
        fixture.AddTotals("150,00", "28,50", "178,50");

        PositionDetectionResult result = await Detect(
            fixture, Totals(150m, 28.50m, 178.50m, rates: [High(19m)]));

        Assert.Equal(19m, Assert.Single(result.Lines).VatRate);
    }

    [Fact]
    public async Task ExpliziterPositionsgesamtpreisIstKeinePflichtspalte()
    {
        var fixture = new TableFixture(hasLineTotal: false);
        fixture.AddRow(1, "Beratung", "2", "Std", "75,00", null, "19 %");
        fixture.AddTotals("150,00", "28,50", "178,50");

        PositionDetectionResult result = await Detect(
            fixture, Totals(150m, 28.50m, 178.50m));

        Assert.Null(Assert.Single(result.Lines).ExplicitLineTotal);
    }

    [Fact]
    public async Task NettoDarfDurchSichereSteuerUndBruttosummeErsetztWerden()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        PositionDetectionResult result = await Detect(
            fixture,
            new DetectedTotals { Tax = High(19m), Gross = High(119m) });

        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task SichereSteuerUndBruttosummeMüssenOhneNettoBeideStimmen()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(
            fixture,
            new DetectedTotals { Tax = High(18.97m), Gross = High(119m) });
    }

    [Fact]
    public async Task UnbekannteEinheitVerwirftDieGanzeTabelle()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Eimer", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task NullProzentErfindetKeineSteuerkategorie()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Leistung", "1", "Std", "100,00", "100,00", "0 %");
        fixture.AddTotals("100,00", "0,00", "100,00");

        await AssertEmpty(fixture, Totals(100m, 0m, 100m));
    }

    [Fact]
    public async Task MehrereDokumentsätzeOhneVatSpalteSindMehrdeutig()
    {
        var fixture = new TableFixture(hasVat: false);
        fixture.AddRow(1, "Leistung", "1", "Std", "100,00", "100,00", null);
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(
            fixture,
            Totals(100m, 19m, 119m, rates: [High(7m), High(19m)]));
    }

    [Fact]
    public async Task UnvollständigeZeileVerwirftAuchVollständigeNachbarzeilen()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Vollständig", "1", "Std", "50,00", "50,00", "19 %");
        fixture.AddIncompleteRow("Ohne Preis", "2", "Std");
        fixture.AddTotals("50,00", "9,50", "59,50");

        await AssertEmpty(fixture, Totals(50m, 9.50m, 59.50m));
    }

    [Fact]
    public async Task FremdesTokenMachtEineBeschreibungNichtZurFortsetzung()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddDescriptionWithPriceToken("vermeintliche Fortsetzung", "Hinweis");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task RäumlichEntfernteTextzeileIstKeineBeschreibungsfortsetzung()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddVerticalGap(40);
        fixture.AddDescription("räumlich entfernter sonstiger Dokumenttext");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Theory]
    [InlineData("Rechnungsnummer RE-2026-0815")]
    [InlineData("Bestellnummer PO-4711")]
    [InlineData("Zahlungsziel 14 Tage")]
    public async Task DokumentmetadatenImBeschreibungsbereichVerwerfenDieTabelle(
        string metadata)
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddDescription(metadata);
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task WidersprüchlicherPositionsbetragVerwirftDieGanzeTabelle()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "2", "Std", "100,00", "199,99", "19 %");
        fixture.AddTotals("200,00", "38,00", "238,00");

        await AssertEmpty(fixture, Totals(200m, 38m, 238m));
    }

    [Fact]
    public async Task FalscheDokumentnettosummeVerwirftDieGanzeTabelle()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "2", "Std", "100,00", "200,00", "19 %");
        fixture.AddTotals("200,00", "38,00", "238,00");

        await AssertEmpty(fixture, Totals(199.97m, 38m, 238m));
    }

    [Fact]
    public async Task JedeWeitereSichereDokumentsummeMussStimmen()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(
            fixture,
            new DetectedTotals
            {
                Net = High(100m),
                Tax = High(18.97m),
                Gross = High(119m),
            });
    }

    [Fact]
    public async Task ZwischensummeVorWeitererPositionVerwirftDieTeilmenge()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Erster Block", "1", "Std", "50,00", "50,00", "19 %");
        fixture.AddBoundary("Zwischensumme", "50,00");
        fixture.AddRow(2, "Zweiter Block", "1", "Std", "50,00", "50,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Theory]
    [InlineData("Bankverbindung 2026")]
    [InlineData("IBAN DE89 3704 0044 0532 0130 00")]
    [InlineData("Rechnungsdatum 09.08.2026")]
    [InlineData("Steuernummer 012/345/67890")]
    public async Task ZahlenImFooterInnerhalbDesTabellenbereichsWerdenNichtIgnoriert(
        string footerLine)
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddFreeLine(footerLine, 100);
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task MehrseitigePositionsgeometrieBleibtAußerhalbDesPhaseAScopes()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        PdfTextResult text = await Extract(fixture);

        // Der Wächter braucht eine echte fortgesetzte Position. Ein bloßer
        // Dankes- oder Zahlungstext auf Seite 2 ist nach POS-01 zulässig.
        PdfTextLine row = Assert.Single(
            text.Lines, line => line.Text.Contains("Beratung", StringComparison.Ordinal));
        PdfTextLine[] pages =
        [
            .. text.Lines,
            row with { PageNumber = 2, Top = 80 },
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task VollständigeTabelleAufSeiteEinsVerträgtZahlungshinweisAufSeiteZwei()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        fixture.AddFreeLine("Zahlbar innerhalb von 14 Tagen.", 100);
        PdfTextResult text = await Extract(fixture);
        PdfTextLine[] pages =
        [
            .. text.Lines.Select(line => line.Text.StartsWith("Zahlbar", StringComparison.Ordinal)
                ? line with { PageNumber = 2, Top = 60 }
                : line),
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Equal(1, result.PageNumber);
        AssertLine(
            Assert.Single(result.Lines),
            1, "Beratung", null, 1m, "HUR", 100m, 100m, 19m);
    }

    [Fact]
    public async Task DeckseiteVorVollständigerTabelleAufSeiteZweiWirdIgnoriert()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        PdfTextResult text = await Extract(fixture);
        PdfTextLine[] pages =
        [
            .. text.Lines.Select(line => line.Top > 80
                ? line with { PageNumber = 2 }
                : line),
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Equal(2, result.PageNumber);
        AssertLine(
            Assert.Single(result.Lines),
            1, "Beratung", null, 1m, "HUR", 100m, 100m, 19m);
    }

    [Fact]
    public async Task WiederholterTabellenkopfMitFortsetzungAufSeiteZweiIstMehrdeutig()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        PdfTextResult text = await Extract(fixture);
        PdfTextLine header = Assert.Single(
            text.Lines, line => line.Text.Contains("Beschreibung", StringComparison.Ordinal));
        PdfTextLine row = Assert.Single(
            text.Lines, line => line.Text.Contains("Beratung", StringComparison.Ordinal));
        PdfTextLine[] pages =
        [
            .. text.Lines,
            header with { PageNumber = 2, Top = 60 },
            row with { PageNumber = 2, Top = 86 },
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task ZweiterUnterstützterTabellenkopfAufFremderSeiteIstMehrdeutig()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        PdfTextResult text = await Extract(fixture);
        PdfTextLine header = Assert.Single(
            text.Lines, line => line.Text.Contains("Beschreibung", StringComparison.Ordinal));
        PdfTextLine[] pages =
        [
            .. text.Lines,
            header with { PageNumber = 2, Top = 100 },
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task SummengrenzeAufFremderSeiteVervollständigtDieTabelleNicht()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        PdfTextResult text = await Extract(fixture);
        PdfTextLine firstTotal = Assert.Single(
            text.Lines, line => line.Text.Contains("Gesamt Netto", StringComparison.Ordinal));
        PdfTextLine[] pages =
        [
            .. text.Lines.Select(line => line.Top >= firstTotal.Top
                ? line with { PageNumber = 2 }
                : line),
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task ZahlenhaltigerHinweisAufFremderSeiteIstKeinePosition()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        fixture.AddFreeLine("Zahlungsziel 14 Tage · Rechnungsnummer RE-2026-0815", 100);
        PdfTextResult text = await Extract(fixture);
        PdfTextLine[] pages =
        [
            .. text.Lines.Select(line => line.Text.StartsWith("Zahlungsziel", StringComparison.Ordinal)
                ? line with { PageNumber = 2, Top = 60 }
                : line),
        ];

        PositionDetectionResult result = PositionDetector.Detect(
            pages, Totals(100m, 19m, 119m));

        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task ZweiTabellenköpfeSindMehrdeutig()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Erste Tabelle", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");
        fixture.AddSecondHeader();

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task DoppelteHeaderrolleWirdNichtPerFirstMatchAufgelöst()
    {
        var fixture = TableFixture.WithAmbiguousQuantityHeader();

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task UnbekannteZusätzlicheHeaderspalteVerwirftDieTabelle()
    {
        var fixture = new TableFixture();
        fixture.AddHeaderToken("Kategorie", 220);
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task DatentokenÜberEinerSpaltengrenzeWirdNichtZugeordnet()
    {
        var fixture = new TableFixture();
        fixture.AddBoundaryCrossingRow();
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task BarePreisüberschriftOhneGesamtspalteBleibtMehrdeutig()
    {
        TableFixture fixture = TableFixture.WithBarePriceHeader();

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task MittlereDokumentsummenÖffnenDasHighGateNicht()
    {
        var fixture = new TableFixture();
        fixture.AddRow(1, "Beratung", "1", "Std", "100,00", "100,00", "19 %");
        fixture.AddTotals("100,00", "19,00", "119,00");

        await AssertEmpty(
            fixture,
            new DetectedTotals
            {
                Net = new DetectedValue<decimal>(100m, DetectionConfidence.Medium),
                Tax = new DetectedValue<decimal>(19m, DetectionConfidence.Medium),
                Gross = new DetectedValue<decimal>(119m, DetectionConfidence.Medium),
            });
    }

    [Fact]
    public async Task RabattspalteBleibtAußerhalbDesPhaseAScopes()
    {
        var fixture = TableFixture.WithDiscountColumn();

        await AssertEmpty(fixture, Totals(90m, 17.10m, 107.10m));
    }

    [Fact]
    public async Task RechnungOhneTabelleErzeugtKeinePositionen()
    {
        var fixture = TableFixture.WithoutTable(
            "Rechnungsnummer: RE-2026-0815",
            "Für meine Beratungsleistung berechne ich 100,00 EUR.",
            "Gesamt Netto 100,00 EUR",
            "Umsatzsteuer 19,00 EUR",
            "Gesamtbetrag 119,00 EUR");

        await AssertEmpty(fixture, Totals(100m, 19m, 119m));
    }

    [Fact]
    public async Task AnonymisierteMusterstrukturOhneHeaderBleibtNegativreferenz()
    {
        var fixture = TableFixture.WithoutTable(
            "RECHNUNG 04-2026",
            "Für nachfolgend benannte Leistung berechne ich Ihnen wie folgt:",
            "Arbeitsleistung Netto 40,00 EUR / je Arbeitsstunde",
            "25,5 Arbeitsstunden (Nachweis laut Anlage)",
            "Netto EUR 40,00 x 25,5 Std.",
            "Gesamt Netto 1.020,00 EUR",
            "Zzgl. 19% MwSt. 193,80 EUR",
            "Gesamt Brutto 1.213,80 EUR");

        await AssertEmpty(fixture, Totals(1020m, 193.80m, 1213.80m));
    }

    private async Task AssertEmpty(TableFixture fixture, DetectedTotals totals)
        => Assert.Empty((await Detect(fixture, totals)).Lines);

    private async Task<PositionDetectionResult> Detect(
        TableFixture fixture,
        DetectedTotals totals)
    {
        PdfTextResult text = await Extract(fixture);
        return PositionDetector.Detect(text.Lines, totals);
    }

    private async Task<PdfTextResult> Extract(TableFixture fixture)
    {
        string path = TestPdfFactory.WriteToTempFile(
            TextPdfBuilder.CreatePositioned(fixture.Fragments));
        _temporaryFiles.Add(path);

        return await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);
    }

    private static DetectedTotals Totals(
        decimal net,
        decimal tax,
        decimal gross,
        IReadOnlyList<DetectedValue<decimal>>? rates = null)
        => new()
        {
            Net = High(net),
            Tax = High(tax),
            Gross = High(gross),
            VatRates = rates ?? [],
        };

    private static DetectedValue<decimal> High(decimal value)
        => new(value, DetectionConfidence.High, "Synthetische Summenzeile", "Eindeutiger Testwert");

    private static void AssertLine(
        DetectedInvoiceLine actual,
        int number,
        string name,
        string? description,
        decimal quantity,
        string unit,
        decimal unitPrice,
        decimal lineTotal,
        decimal vatRate)
    {
        Assert.Equal(number, actual.Number);
        Assert.Equal(name, actual.Name);
        Assert.Equal(description, actual.Description);
        Assert.Equal(quantity, actual.Quantity);
        Assert.Equal(unit, actual.UnitCode);
        Assert.Equal(unitPrice, actual.NetUnitPrice);
        Assert.Equal(lineTotal, actual.ExplicitLineTotal);
        Assert.Equal(VatCategory.StandardRate, actual.VatCategory);
        Assert.Equal(vatRate, actual.VatRate);
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private sealed class TableFixture
    {
        private const double PositionLeft = 30;
        private const double ArticleLeft = 60;
        private const double DescriptionWithoutArticleLeft = 100;
        private const double DescriptionWithArticleLeft = 160;
        private const double QuantityLeft = 310;
        private const double UnitLeft = 350;
        private const double UnitPriceRight = 465;
        private const double LineTotalRight = 548;
        private const double VatWithTotalLeft = 560;
        private const double VatWithoutTotalLeft = 530;

        private readonly bool _hasArticleNumber;
        private readonly bool _hasLineTotal;
        private readonly bool _hasVat;
        private double _nextTop = 126;

        public TableFixture(
            bool hasArticleNumber = false,
            bool hasLineTotal = true,
            bool hasVat = true)
        {
            _hasArticleNumber = hasArticleNumber;
            _hasLineTotal = hasLineTotal;
            _hasVat = hasVat;

            Fragments.AddRange(Intro());
            AddHeader(100);
        }

        private TableFixture(IEnumerable<PositionedPdfText> fragments)
            => Fragments.AddRange(fragments);

        public List<PositionedPdfText> Fragments { get; } = [];

        private double DescriptionLeft
            => _hasArticleNumber ? DescriptionWithArticleLeft : DescriptionWithoutArticleLeft;

        public void AddRow(
            int number,
            string name,
            string quantity,
            string unit,
            string price,
            string? total,
            string? vat,
            string? articleNumber = null)
        {
            double top = TakeTop();
            Fragments.Add(P(number.ToString(CultureInfo.InvariantCulture), PositionLeft, top));

            if (_hasArticleNumber)
            {
                Fragments.Add(P(articleNumber ?? $"ART-{number}", ArticleLeft, top));
            }

            Fragments.Add(P(name, DescriptionLeft, top));
            Fragments.Add(P(quantity, QuantityLeft, top));
            Fragments.Add(P(unit, UnitLeft, top));
            Fragments.Add(Right(price, UnitPriceRight, top));

            if (_hasLineTotal && total is not null)
            {
                Fragments.Add(Right(total, LineTotalRight, top));
            }

            if (_hasVat && vat is not null)
            {
                Fragments.Add(P(
                    vat,
                    _hasLineTotal ? VatWithTotalLeft : VatWithoutTotalLeft,
                    top));
            }
        }

        public void AddIncompleteRow(string name, string quantity, string unit)
        {
            double top = TakeTop();
            Fragments.Add(P("2", PositionLeft, top));
            Fragments.Add(P(name, DescriptionLeft, top));
            Fragments.Add(P(quantity, QuantityLeft, top));
            Fragments.Add(P(unit, UnitLeft, top));
        }

        public void AddDescription(string text)
            => Fragments.Add(P(text, DescriptionLeft, TakeTop()));

        public void AddHeaderToken(string text, double left)
            => Fragments.Add(P(text, left, 100));

        public void AddVerticalGap(double points) => _nextTop += points;

        public void AddDescriptionWithPriceToken(string text, string priceAreaText)
        {
            double top = TakeTop();
            Fragments.Add(P(text, DescriptionLeft, top));
            Fragments.Add(P(priceAreaText, 410, top));
        }

        public void AddBoundaryCrossingRow()
        {
            double top = TakeTop();
            Fragments.Add(P("1", PositionLeft, top));
            Fragments.Add(P("Beratung", DescriptionLeft, top));
            Fragments.Add(P("123,45", 332, top));
            Fragments.Add(P("Std", UnitLeft, top));
            Fragments.Add(Right("100,00", UnitPriceRight, top));
            Fragments.Add(Right("100,00", LineTotalRight, top));
            Fragments.Add(P("19 %", VatWithTotalLeft, top));
        }

        public void AddBoundary(string label, string amount)
        {
            double top = TakeTop();
            Fragments.Add(P(label, DescriptionLeft, top));
            Fragments.Add(Right(amount, LineTotalRight, top));
        }

        public void AddFreeLine(string text, double left)
            => Fragments.Add(P(text, left, TakeTop()));

        public void AddTotals(string net, string tax, string gross)
        {
            AddBoundary("Gesamt Netto", net);
            AddBoundary("Umsatzsteuer 19 %", tax);
            AddBoundary("Gesamtbetrag", gross);
            Fragments.Add(P("Vielen Dank für Ihren Auftrag und das entgegengebrachte Vertrauen.", 100, TakeTop()));
        }

        public void AddSecondHeader() => AddHeader(TakeTop());

        public static TableFixture WithAmbiguousQuantityHeader()
        {
            var fragments = new List<PositionedPdfText>(Intro())
            {
                P("Pos.", 30, 100),
                P("Beschreibung", 100, 100),
                P("Menge", 270, 100),
                P("Anzahl", 310, 100),
                P("Einheit", 350, 100),
                P("Einzelpreis", 400, 100),
                P("Gesamtpreis", 490, 100),
                P("MwSt", 560, 100),
                P("1", 30, 126),
                P("Beratung", 100, 126),
                P("1", 310, 126),
                P("Std", 350, 126),
                Right("100,00", UnitPriceRight, 126),
                Right("100,00", LineTotalRight, 126),
                P("19 %", VatWithTotalLeft, 126),
                P("Gesamt Netto 100,00 EUR", 100, 160),
            };

            return new TableFixture(fragments);
        }

        public static TableFixture WithDiscountColumn()
        {
            var fragments = new List<PositionedPdfText>(Intro())
            {
                P("Pos.", 25, 100),
                P("Beschreibung", 80, 100),
                P("Menge", 270, 100),
                P("Einheit", 315, 100),
                P("Einzelpreis", 365, 100),
                P("Rabatt", 445, 100),
                P("Gesamtpreis", 490, 100),
                P("MwSt", 560, 100),
                P("1 Beratung 1 Std 100,00 10,00 90,00 19 %", 25, 126),
                P("Gesamt Netto 90,00 EUR", 80, 160),
            };

            return new TableFixture(fragments);
        }

        public static TableFixture WithAlternativeHeader()
        {
            var fragments = new List<PositionedPdfText>(Intro())
            {
                P("Position", 30, 100),
                P("Leistung", 100, 100),
                P("Anzahl", 310, 100),
                P("Einh.", 350, 100),
                P("EP", 440, 100),
                P("Gesamt", 490, 100),
                P("Preis", 525, 100),
                P("USt", 560, 100),
                P("%", 584, 100),
                P("1", 30, 126),
                P("Beratung", 100, 126),
                P("1", 310, 126),
                P("Std", 350, 126),
                Right("100,00", UnitPriceRight, 126),
                Right("100,00", LineTotalRight, 126),
                P("19 %", VatWithTotalLeft, 126),
                P("Gesamt Netto 100,00 EUR", 100, 162),
                P("Umsatzsteuer 19,00 EUR", 100, 180),
                P("Gesamtbetrag 119,00 EUR", 100, 198),
            };

            return new TableFixture(fragments);
        }

        public static TableFixture WithoutPositionColumn()
        {
            var fragments = new List<PositionedPdfText>(Intro())
            {
                P("Beschreibung", 80, 100),
                P("Menge", 310, 100),
                P("Einheit", 350, 100),
                P("Einzelpreis", 400, 100),
                P("Gesamtpreis", 490, 100),
                P("MwSt", 560, 100),
                P("Beratung", 80, 126),
                P("1", 310, 126),
                P("Std", 350, 126),
                Right("100,00", UnitPriceRight, 126),
                Right("100,00", LineTotalRight, 126),
                P("19 %", VatWithTotalLeft, 126),
                P("Dokumentation", 80, 144),
                P("1", 310, 144),
                P("Stk", 350, 144),
                Right("50,00", UnitPriceRight, 144),
                Right("50,00", LineTotalRight, 144),
                P("19 %", VatWithTotalLeft, 144),
                P("Gesamt Netto 150,00 EUR", 80, 180),
                P("Umsatzsteuer 28,50 EUR", 80, 198),
                P("Gesamtbetrag 178,50 EUR", 80, 216),
            };

            return new TableFixture(fragments);
        }

        public static TableFixture WithBarePriceHeader()
        {
            var fragments = new List<PositionedPdfText>(Intro())
            {
                P("Pos.", 30, 100),
                P("Beschreibung", 100, 100),
                P("Menge", 310, 100),
                P("Einheit", 350, 100),
                P("Preis", 400, 100),
                P("MwSt", 530, 100),
                P("1", 30, 126),
                P("Beratung", 100, 126),
                P("1", 310, 126),
                P("Std", 350, 126),
                Right("100,00", UnitPriceRight, 126),
                P("19 %", VatWithoutTotalLeft, 126),
                P("Gesamt Netto 100,00 EUR", 100, 162),
            };

            return new TableFixture(fragments);
        }

        public static TableFixture WithoutTable(params string[] lines)
        {
            var fragments = new List<PositionedPdfText>(Intro());
            double top = 100;

            foreach (string line in lines)
            {
                fragments.Add(P(line, 100, top));
                top += 15;
            }

            return new TableFixture(fragments);
        }

        private void AddHeader(double top)
        {
            Fragments.Add(P("Pos.", PositionLeft, top));

            if (_hasArticleNumber)
            {
                Fragments.Add(P("Art.-Nr.", ArticleLeft, top));
            }

            Fragments.Add(P("Beschreibung", DescriptionLeft, top));
            Fragments.Add(P("Menge", QuantityLeft, top));
            Fragments.Add(P("Einheit", UnitLeft, top));
            Fragments.Add(P("Einzelpreis", 400, top));

            if (_hasLineTotal)
            {
                Fragments.Add(P("Gesamtpreis", 490, top));
            }

            if (_hasVat)
            {
                Fragments.Add(P(
                    "MwSt",
                    _hasLineTotal ? VatWithTotalLeft : VatWithoutTotalLeft,
                    top));
            }
        }

        private double TakeTop()
        {
            double top = _nextTop;
            _nextTop += 18;
            return top;
        }

        private static IEnumerable<PositionedPdfText> Intro()
        {
            yield return P("Muster IT GmbH · Musterstraße 10 · 18055 Rostock", 56, 32);
            yield return P("Rechnungsnummer: RE-2026-0815 · Rechnungsdatum: 09.08.2026", 56, 50);
            yield return P("Leistungsübersicht der digital erstellten Rechnung", 56, 68);
        }

        private static PositionedPdfText Right(string text, double right, double top)
            => P(text, right - HelveticaWidth(text), top);

        private static double HelveticaWidth(string text)
            => text.Sum(character => character switch
            {
                >= '0' and <= '9' => 5.56,
                '.' or ',' => 2.78,
                '-' => 3.33,
                _ => 5.56,
            });

        private static PositionedPdfText P(string text, double left, double top)
            => new(text, left, top);
    }
}

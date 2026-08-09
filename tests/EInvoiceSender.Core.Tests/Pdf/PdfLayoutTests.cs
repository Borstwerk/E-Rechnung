using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Haelt fest, was der Extraktor bei verschiedenen Seitenaufteilungen
/// tatsaechlich leistet.
///
/// **Diese Tests sind zuerst Dokumentation, nicht Anforderung.** Sie
/// beschreiben den heutigen Stand, damit ein spaeterer Ausbau der
/// Layouterkennung sieht, wovon er ausgeht – und damit ein Rueckschritt
/// auffaellt. Wo der Extraktor an eine Grenze stoesst, steht das ausdruecklich
/// im Test, statt die Erwartung stillschweigend abzusenken.
/// </summary>
public sealed class PdfLayoutTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);
    private readonly InvoiceDataDetector _detector;

    public PdfLayoutTests()
        => _detector = new InvoiceDataDetector(_extractor, NullLogger<InvoiceDataDetector>.Instance);

    /// <summary>
    /// Zwei Spalten auf derselben Grundlinie werden zu **einer** Textzeile
    /// zusammengefasst – von links nach rechts. Das ist die Folge daraus, dass
    /// der Extraktor nach Grundlinien gruppiert und keine Spalten kennt.
    /// </summary>
    [Fact]
    public async Task ZweiSpaltenLandenInEinerZeile()
    {
        string path = Temp(TextPdfBuilder.CreateTwoColumn(
            left: ["Muster IT GmbH", "Musterstrasse 10", "18055 Rostock"],
            right: ["Rechnung an", "Beispielkunde AG", "20095 Hamburg"],
            below: ["Rechnungsnummer: RE-2026-0815", "Gesamtbetrag 1.190,00 EUR"]));

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.Contains(
            result.Lines,
            l => l.Text.Contains("Muster IT GmbH", StringComparison.Ordinal)
                 && l.Text.Contains("Rechnung an", StringComparison.Ordinal));
    }

    /// <summary>
    /// Folge daraus: Im zweispaltigen Briefkopf steht hinter "Rechnung an" die
    /// naechste Zeile – die ebenfalls beide Spalten enthaelt. Der Kaeufername
    /// wird deshalb mit dem Verkaeuferteil vermengt.
    ///
    /// Das ist die derzeitige Grenze und der Grund, warum der Kaeufer aus einem
    /// zweispaltigen Kopf nur mittlere Sicherheit erreicht. Ein Ausbau muss die
    /// Wortpositionen auswerten, die der Extraktor bereits mitliefert.
    /// </summary>
    [Fact]
    public async Task ImZweispaltigenKopfWirdDerKaeufernameNochNichtSauberGetrennt()
    {
        string path = Temp(TextPdfBuilder.CreateTwoColumn(
            left: ["Muster IT GmbH", "Musterstrasse 10", "18055 Rostock", "USt-IdNr. DE123456789"],
            right: ["Rechnung an", "Beispielkunde AG", "Kundenstrasse 7", "20095 Hamburg"],
            below: ["Rechnungsnummer: RE-2026-0815", "Gesamtbetrag 1.190,00 EUR"]));

        InvoiceDetectionResult result = await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);

        // Die Kopfdaten kommen unabhaengig vom Layout sauber durch.
        Assert.Equal("RE-2026-0815", result.InvoiceNumber?.Value);
        Assert.Equal(1190.00m, result.Totals.Gross?.Value);

        // Der Kaeufername traegt noch den Verkaeuferteil derselben Zeile mit.
        Assert.NotNull(result.Buyer.Name);
        Assert.Contains("Beispielkunde AG", result.Buyer.Name.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Der einspaltige Adressblock – die haeufigere Form – wird sauber
    /// getrennt. Das ist der Fall, fuer den die Erkennung ausgelegt ist.
    /// </summary>
    [Fact]
    public async Task ImEinspaltigenBlockWirdDerKaeuferSauberErkannt()
    {
        InvoiceDetectionResult result = await Detect(PdfTextExtractorTests.FullInvoiceLines());

        Assert.Equal("Beispielkunde AG", result.Buyer.Name?.Value);
        Assert.Equal("20095", result.Buyer.PostalCode?.Value);
        Assert.Equal("Hamburg", result.Buyer.City?.Value);
    }

    /// <summary>
    /// Eine mehrspaltige Positionstabelle wird als Text vollstaendig gelesen –
    /// nur eben nicht in Felder zerlegt. Der Test haelt beides fest: Der Text
    /// ist da, die Positionserkennung fehlt.
    /// </summary>
    [Fact]
    public async Task PositionstabelleWirdGelesenAberNichtZerlegt()
    {
        var lines = new List<string>
        {
            "Muster IT GmbH", "Musterstrasse 10", "18055 Rostock",
            "Rechnungsnummer: RE-2026-0815",
            "Pos Bezeichnung Menge Einheit Einzelpreis Betrag",
            "1 IT-Beratung 10 Std 100,00 1.000,00",
            "2 Projektleitung 4 Std 120,00 480,00",
            "Nettobetrag 1.480,00 EUR",
            "Umsatzsteuer 19 % 281,20 EUR",
            "Gesamtbetrag 1.761,20 EUR",
        };

        string path = Temp(TextPdfBuilder.Create(lines));

        PdfTextResult text = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.Contains(text.Lines, l => l.Text.Contains("IT-Beratung", StringComparison.Ordinal));
        Assert.Contains(text.Lines, l => l.Text.Contains("Projektleitung", StringComparison.Ordinal));

        InvoiceDetectionResult result = await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);

        Assert.Equal(1761.20m, result.Totals.Gross?.Value);
    }

    /// <summary>
    /// Bei einer mehrseitigen Rechnung stehen die Summen auf der letzten Seite.
    /// Sie muessen trotzdem gefunden werden.
    /// </summary>
    [Fact]
    public async Task BeiMehrerenSeitenWerdenDieSummenAufDerLetztenSeiteGefunden()
    {
        var lines = new List<string>
        {
            "Muster IT GmbH", "Musterstrasse 10", "18055 Rostock",
            "Rechnungsnummer: RE-2026-0815", "Rechnungsdatum: 09.08.2026",
        };

        for (int i = 1; i <= 100; i++)
        {
            lines.Add($"{i} Sammelposition {i} 1 Stk 10,00 10,00");
        }

        lines.AddRange(["Nettobetrag 1.000,00 EUR", "Gesamtbetrag 1.190,00 EUR"]);

        string path = Temp(TextPdfBuilder.Create(lines));

        PdfTextResult text = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);
        InvoiceDetectionResult result = await _detector.DetectAsync(
            path, null, TestContext.Current.CancellationToken);

        Assert.True(text.PageCount >= 2, $"Erwartet mehrere Seiten, waren {text.PageCount}.");
        Assert.Equal("RE-2026-0815", result.InvoiceNumber?.Value);
        Assert.Equal(1190.00m, result.Totals.Gross?.Value);
    }

    private async Task<InvoiceDetectionResult> Detect(
        IEnumerable<string> lines, CompanyTemplate? template = null)
        => await _detector.DetectAsync(
            Temp(TextPdfBuilder.Create(lines)), template, TestContext.Current.CancellationToken);

    private string Temp(byte[] content)
    {
        string path = TestPdfFactory.WriteToTempFile(content);
        _temporaryFiles.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

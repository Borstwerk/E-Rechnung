using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Prueft die Textextraktion.
///
/// Sie ist die Grundlage der gesamten Erkennung: Kommt hier verfaelschter oder
/// unvollstaendiger Text heraus, ist jede nachfolgende Regel wertlos.
/// </summary>
public sealed class PdfTextExtractorTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    [Fact]
    public async Task TextWirdZeilenweiseGelesen()
    {
        string path = Temp(TextPdfBuilder.Create(FullInvoiceLines()));

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.HasUsableText);
        Assert.Equal(1, result.PageCount);
        Assert.Contains(result.Lines, l => l.Text.Contains("RE-2026-0815", StringComparison.Ordinal));
        Assert.Contains(result.Lines, l => l.Text.Contains("1.190,00", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ein paar Zeichen aus einem Wasserzeichen oder Seitenzaehler machen eine
    /// eingescannte Rechnung nicht auswertbar. Die Untergrenze verhindert, dass
    /// die Erkennung an solchen Bruchstuecken herumraet.
    /// </summary>
    [Fact]
    public async Task EinzelneWoerterGeltenNochNichtAlsAuswertbar()
    {
        string path = Temp(TextPdfBuilder.Create("Seite 1", "Kopie"));

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.HasUsableText);
    }

    [Fact]
    public async Task DeutscheUmlauteUndEurozeichenBleibenErhalten()
    {
        string path = Temp(TextPdfBuilder.Create(
            "Fälligkeit: 23.08.2026",
            "Gesamtbetrag 1.190,00 €",
            "Grüße aus Rostock, Übergabe erfolgt"));

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);
        string text = result.FullText;

        Assert.Contains("Fälligkeit", text, StringComparison.Ordinal);
        Assert.Contains("€", text, StringComparison.Ordinal);
        Assert.Contains("Grüße", text, StringComparison.Ordinal);
        Assert.Contains("Übergabe", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WoerterEinerZeileBleibenZusammen()
    {
        string path = Temp(TextPdfBuilder.Create(FullInvoiceLines()));

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        // Entscheidend fuer die Erkennung: Schluesselwort und Wert muessen in
        // derselben Zeile landen, sonst greift keine Kontextregel.
        Assert.Contains(
            result.Lines,
            l => l.Text.Contains("Rechnungsnummer", StringComparison.Ordinal)
                 && l.Text.Contains("RE-2026-0815", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MehrereSeitenWerdenVollstaendigGelesen()
    {
        var lines = new List<string>();

        for (int i = 1; i <= 120; i++)
        {
            lines.Add($"Zeile {i} mit etwas Text zur Fuellung der Seite");
        }

        lines.Add("Gesamtbetrag 4.999,00 EUR");

        string path = Temp(TextPdfBuilder.Create(lines));

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.PageCount >= 3, $"Erwartet mehrere Seiten, waren {result.PageCount}.");
        Assert.Contains(result.Lines, l => l.Text.Contains("4.999,00", StringComparison.Ordinal));
        Assert.Contains(result.Lines, l => l.PageNumber > 1);
    }

    /// <summary>
    /// Eine eingescannte Rechnung enthaelt keinen Text. Das ist kein Fehler,
    /// sondern muss als solches gemeldet werden, damit die Oberflaeche zur
    /// Handerfassung auffordern kann.
    /// </summary>
    [Fact]
    public async Task PdfOhneTextWirdAlsNichtAuswertbarGemeldet()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf());

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.HasUsableText);
    }

    /// <summary>
    /// Die Erkennung ist eine Komfortfunktion. Eine beschaedigte Datei darf
    /// keine Ausnahme nach oben durchreichen, sonst bricht der Ablauf ab.
    /// </summary>
    [Fact]
    public async Task BeschaedigtePdfLiefertLeeresErgebnisStattAusnahme()
    {
        string path = Temp(TestPdfFactory.CreateDamagedPdf());

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.HasUsableText);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task KeinPdfLiefertLeeresErgebnisStattAusnahme()
    {
        string path = Temp(TestPdfFactory.CreateNonPdf());

        PdfTextResult result = await _extractor.ExtractAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.HasUsableText);
    }

    internal static string[] FullInvoiceLines() =>
    [
        "Muster IT GmbH",
        "Musterstrasse 10",
        "18055 Rostock",
        "Telefon 0381 1234567",
        "USt-IdNr. DE123456789",
        "",
        "Rechnung an",
        "Beispielkunde AG",
        "Kundenstrasse 7",
        "20095 Hamburg",
        "",
        "Rechnungsnummer: RE-2026-0815",
        "Rechnungsdatum: 09.08.2026",
        "Leistungsdatum: 08.08.2026",
        "Faellig am 23.08.2026",
        "",
        "Pos Bezeichnung Menge Einheit Einzelpreis Betrag",
        "1 IT-Beratung 10 Std 100,00 1.000,00",
        "",
        "Nettobetrag 1.000,00 EUR",
        "Umsatzsteuer 19 % 190,00 EUR",
        "Gesamtbetrag 1.190,00 EUR",
        "",
        "IBAN DE89 3704 0044 0532 0130 00",
        "BIC COBADEFFXXX",
    ];

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

using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Verfolgt die Testrechnung über den ganzen Weg: PDF → Erkennung → Entwurf.
///
/// **Warum es diese Tests gibt:** Die Einzelschritte waren jeder für sich
/// geprüft, und trotzdem standen im manuellen Testlauf Felder leer, deren Wert
/// die Erkennung sauber gelesen hatte. Der Fehler lag zwischen den Schritten.
/// Diese Tests prüfen deshalb nicht einen Schritt, sondern den Übergang: Was
/// in der PDF steht, muss anschließend im Formularfeld stehen – und dort als
/// erkannt gekennzeichnet sein.
///
/// Die Positionen bleiben ausdrücklich außen vor: Sie werden nicht aus der
/// PDF übernommen, und der letzte Test hält genau das fest.
/// </summary>
public sealed class TestInvoiceEndToEndTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private readonly InvoiceDataDetector _detector = new(
        new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
        NullLogger<InvoiceDataDetector>.Instance);

    [Fact]
    public async Task DieKopfdatenStehenAnschließendImEntwurf()
    {
        InvoiceDraft draft = await PrefilledDraft();

        Assert.Equal(TestInvoice.InvoiceNumber, draft.InvoiceNumber);
        Assert.Equal(TestInvoice.Currency, draft.Currency);
        Assert.Equal(TestInvoice.IssueDate, draft.IssueDate);
        Assert.Equal(TestInvoice.DeliveryDate, draft.DeliveryDate);
        Assert.Equal(TestInvoice.DueDate, draft.DueDate);
    }

    [Fact]
    public async Task DerEmpfängerStehtAnschließendImEntwurf()
    {
        InvoiceDraft draft = await PrefilledDraft();

        Assert.Equal(TestInvoice.BuyerName, draft.BuyerName);
        Assert.Equal(TestInvoice.BuyerPostalCode, draft.BuyerPostalCode);
        Assert.Equal(TestInvoice.BuyerCity, draft.BuyerCity);
    }

    /// <summary>
    /// Regression des manuellen Funds: Auch auf einer Erstinstallation ohne
    /// Firmenvorlage muss der räumlich klar getrennte Seller vorausgefüllt sein.
    /// </summary>
    [Fact]
    public async Task OhneFirmenvorlageStehtDerEindeutigeSellerImEntwurf()
    {
        InvoiceDetectionResult detection = await Detect();
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, detection);

        Assert.Equal(TestInvoice.SellerName, draft.SellerName);
        Assert.Equal(TestInvoice.SellerVatId, draft.SellerVatId);
        Assert.NotNull(detection.OwnCompanyProposal);
        Assert.NotEqual(FieldOrigin.Manual, draft.OriginOf(nameof(draft.SellerName)));
    }

    [Fact]
    public async Task DieBankverbindungStehtAnschließendImEntwurf()
    {
        InvoiceDraft draft = await PrefilledDraft();

        Assert.Equal(TestInvoice.Iban, draft.BankIban);
        Assert.Equal(TestInvoice.Bic, draft.BankBic);
    }

    /// <summary>
    /// Der Kern der Beanstandung aus dem Testlauf: Ein übernommener Wert muss
    /// als übernommen erkennbar sein. Ohne Kennzeichnung sähe er aus wie eine
    /// eigene Eingabe – und niemand prüfte ihn nach.
    /// </summary>
    [Theory]
    [InlineData(nameof(InvoiceDraft.InvoiceNumber))]
    [InlineData(nameof(InvoiceDraft.Currency))]
    [InlineData(nameof(InvoiceDraft.IssueDate))]
    [InlineData(nameof(InvoiceDraft.DeliveryDate))]
    [InlineData(nameof(InvoiceDraft.DueDate))]
    [InlineData(nameof(InvoiceDraft.BuyerName))]
    [InlineData(nameof(InvoiceDraft.BuyerPostalCode))]
    [InlineData(nameof(InvoiceDraft.BuyerCity))]
    [InlineData(nameof(InvoiceDraft.SellerName))]
    [InlineData(nameof(InvoiceDraft.SellerVatId))]
    [InlineData(nameof(InvoiceDraft.BankIban))]
    [InlineData(nameof(InvoiceDraft.BankBic))]
    public async Task JederÜbernommeneWertIstAlsErkanntGekennzeichnet(string field)
    {
        InvoiceDraft draft = await PrefilledDraft();

        Assert.NotEqual(FieldOrigin.Default, draft.OriginOf(field));
        Assert.NotEqual(FieldOrigin.Manual, draft.OriginOf(field));
    }

    /// <summary>
    /// Was der Anwender selbst eingetragen hat, bleibt stehen. Sonst wäre die
    /// Vorbefüllung eine Falle statt einer Hilfe.
    /// </summary>
    [Fact]
    public async Task EineEigeneEingabeWirdNichtÜberschrieben()
    {
        var draft = new InvoiceDraft { BuyerName = "Von Hand erfasst" };

        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.BuyerName)));

        DraftPrefiller.Apply(draft, await Detect());

        Assert.Equal("Von Hand erfasst", draft.BuyerName);
    }

    /// <summary>
    /// Die Übersicht aus Schritt 1 und die Feldwerte aus Schritt 2 müssen
    /// dasselbe erzählen. Steht in der Übersicht ein anderer Empfänger als im
    /// Formular, ist eine der beiden Anzeigen falsch.
    /// </summary>
    [Fact]
    public async Task DieÜbersichtInSchrittEinsNenntDieselbenWerte()
    {
        InvoiceDetectionResult detection = await Detect();
        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, detection);

        string[] zeilen = [.. DetectionOverview.Describe(detection).Select(e => e.Text)];

        // Auf den Wert kann ein "– bitte prüfen" folgen, wenn er nicht
        // zweifelsfrei gelesen wurde. Der Wert selbst muss derselbe sein.
        Assert.Contains(zeilen, z => z.StartsWith(
            $"Rechnungsnummer erkannt: {draft.InvoiceNumber}", StringComparison.Ordinal));
        Assert.Contains(zeilen, z => z.StartsWith(
            $"Empfänger erkannt: {draft.BuyerName}", StringComparison.Ordinal));
        Assert.Contains(zeilen, z => z.StartsWith(
            $"Währung erkannt: {draft.Currency}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Positionen werden nicht übernommen. Das ist keine Lücke im Test,
    /// sondern die getroffene Entscheidung – ein falsch gelesener Betrag wäre
    /// schlimmer als ein leeres Feld.
    /// </summary>
    [Fact]
    public async Task DiePositionenBleibenLeer()
    {
        InvoiceDraft draft = await PrefilledDraft();

        Assert.Empty(draft.Lines);
    }

    private async Task<InvoiceDraft> PrefilledDraft()
    {
        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, await Detect());

        return draft;
    }

    private async Task<InvoiceDetectionResult> Detect()
    {
        string path = TestPdfFactory.WriteToTempFile(TestInvoice.CreatePdf());
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

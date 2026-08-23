using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C6 – eine zusätzliche, fachlich inerte Spalte.
///
/// Lieferscheinnahe Rechnungen führen häufig ein Lieferdatum je Position. Die
/// Spalte ist harmlos: Sie erzeugt kein Rechnungsfeld und verändert keinen
/// Betrag. Trotzdem darf sie nicht einfach übergangen werden – der Detektor
/// leitet seine Spaltengrenzen aus dem Kopf ab, und ein unbekannter Kopfteil
/// macht die ganze Zuordnung unsicher.
///
/// **Deshalb wird sie ausdrücklich aufgenommen und nicht allgemein
/// ignoriert.** Das Prinzip bleibt: Jede Spalte muss benannt und verstanden
/// sein. Eine beliebige unbekannte Spalte verwirft die Tabelle weiterhin –
/// sonst hätte diese Erweiterung die Tür für alles geöffnet.
/// </summary>
public sealed class PositionDeliveryDateColumnTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfTextExtractor _extractor = new(NullLogger<PdfTextExtractor>.Instance);

    /// <summary>
    /// <c>Pos | Art.-Nr. | Bezeichnung | Menge | Einheit | Lieferdatum |
    /// E-Preis | Gesamt</c>.
    /// </summary>
    private static readonly TableColumn[] WithDeliveryDate =
    [
        TableColumn.Text("Pos.", 28),
        TableColumn.Text("Art.-Nr.", 55),
        TableColumn.Text("Bezeichnung", 120),
        TableColumn.Text("Menge", 255),
        TableColumn.Text("Einheit", 300),
        TableColumn.Text("Lieferdatum", 350),
        TableColumn.Money("E-Preis", 430, 480),
        TableColumn.Money("Gesamt", 505, 560),
    ];

    [Fact]
    public async Task EineLieferdatumsspalteVerhindertDieErkennungNicht()
    {
        PositionDetectionResult result = await Detect(WithDeliveryDate);

        Assert.Equal(2, result.Lines.Count);

        Assert.Equal("Beratung", result.Lines[0].Name);
        Assert.Equal(2m, result.Lines[0].Quantity);
        Assert.Equal("HUR", result.Lines[0].UnitCode);
        Assert.Equal(100.00m, result.Lines[0].NetUnitPrice);

        Assert.Equal("Dokumentation", result.Lines[1].Name);
        Assert.Equal("C62", result.Lines[1].UnitCode);
        Assert.Equal(50.00m, result.Lines[1].NetUnitPrice);
    }

    /// <summary>
    /// Das Lieferdatum bleibt, was es ist: Tabellenschmuck. Es darf in keinem
    /// Wert der erkannten Position auftauchen – weder als Beschreibung noch
    /// irgendwo sonst.
    /// </summary>
    [Fact]
    public async Task DasLieferdatumErzeugtKeinRechnungsfeld()
    {
        PositionDetectionResult result = await Detect(WithDeliveryDate);

        foreach (DetectedInvoiceLine line in result.Lines)
        {
            Assert.DoesNotContain("2026", line.Name, StringComparison.Ordinal);
            Assert.Null(line.Description);
        }
    }

    /// <summary>
    /// **Die Gegenprobe.** Derselbe Aufbau mit einer beliebigen unbekannten
    /// Spalte bleibt leer. Erlaubt ist genau das eine, benannte Lieferdatum –
    /// keine allgemeine Nachsicht gegenüber Unbekanntem.
    /// </summary>
    [Fact]
    public async Task EineBeliebigeUnbekannteSpalteVerwirftDieTabelleWeiterhin()
    {
        TableColumn[] withUnknown =
        [
            .. WithDeliveryDate.Select(
                column => column.Header == "Lieferdatum"
                    ? column with { Header = "Foo" }
                    : column),
        ];

        Assert.Empty((await Detect(withUnknown)).Lines);
    }

    /// <summary>
    /// **Die schärfere Gegenprobe.** Der vorige Test ist doppelt abgesichert:
    /// Selbst ohne Kopfprüfung fiele die Tabelle, weil die Datumszelle dann in
    /// einer fremden Spalte landet. Er beweist die Kopfprüfung also nicht.
    ///
    /// Hier bleibt die Spalte deshalb **leer**. Nur der Kopf trägt ein
    /// unbekanntes Wort. Fällt die Tabelle trotzdem, kann das nur an der
    /// Prüfung liegen, dass jedes Kopf-Token einer bekannten Spalte gehört.
    ///
    /// Dieser Test entstand aus einer Brechprobe: Die Prüfung probeweise zu
    /// entfernen ließ den vorigen Test grün – ein Wächter, der nichts bewacht.
    /// </summary>
    [Fact]
    public async Task EinUnbekanntesKopfwortAlleinVerwirftDieTabelleBereits()
    {
        TableColumn[] withUnknownHeaderOnly =
        [
            .. WithDeliveryDate.Select(
                column => column.Header == "Lieferdatum"
                    ? column with { Header = "Foo" }
                    : column),
        ];

        byte[] pdf = LayoutTablePdf.Create(
            withUnknownHeaderOnly,
            [
                ["1", "ART-1", "Beratung", "2", "Std", "", "100,00", "200,00"],
                ["2", "ART-2", "Dokumentation", "1", "Stk", "", "50,00", "50,00"],
            ],
            net: "250,00", tax: "47,50", gross: "297,50");

        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        PdfTextResult text = await _extractor.ExtractAsync(
            path, TestContext.Current.CancellationToken);

        Assert.Empty(
            PositionDetector.Detect(
                text.Lines,
                DetectedTotalsFixture.WithVatRate(250m, 47.50m, 297.50m, 19m)).Lines);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private async Task<PositionDetectionResult> Detect(IReadOnlyList<TableColumn> columns)
    {
        byte[] pdf = LayoutTablePdf.Create(
            columns,
            [
                ["1", "ART-1", "Beratung", "2", "Std", "15.03.2026", "100,00", "200,00"],
                ["2", "ART-2", "Dokumentation", "1", "Stk", "16.03.2026", "50,00", "50,00"],
            ],
            net: "250,00", tax: "47,50", gross: "297,50");

        string path = TestPdfFactory.WriteToTempFile(pdf);
        _temporaryFiles.Add(path);

        PdfTextResult text = await _extractor.ExtractAsync(
            path, TestContext.Current.CancellationToken);

        // Der Aufbau führt keine Steuerspalte; der Satz kommt aus dem Dokument.
        return PositionDetector.Detect(
            text.Lines, DetectedTotalsFixture.WithVatRate(250m, 47.50m, 297.50m, 19m));
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice 7 – vorbefüllte Positionen verhalten sich wie getippte.
///
/// **Warum diese Tests:** Nach der Vorbefüllung liegen Zeilen im Entwurf, die
/// niemand angelegt hat. Alles, was der Anwender danach mit ihnen tut –
/// bearbeiten, ergänzen, löschen, neu nummerieren, „Neue Rechnung“ –, muss
/// unverändert funktionieren. Die Vorbefüllung ist eine Schreibhilfe und darf
/// keinen zweiten Zustand einführen, in dem die Tabelle anders reagiert.
///
/// Diese Tests messen den Entwurf, nicht die Oberfläche. Sie sind damit die
/// Hälfte, die ohne Windows prüfbar ist; die andere Hälfte – dass die
/// Schaltflächen eine offene Zellenbearbeitung abschließen – steht in
/// <c>LineEditingTests</c>.
/// </summary>
public sealed class PrefilledLineEditingTests
{
    [Fact]
    public void EineVorbefüllteZeileLässtSichBearbeiten()
    {
        InvoiceDraft draft = Prefilled();

        draft.Lines[0].Name = "Andere Leistung";
        draft.Lines[0].Quantity = "4";

        Assert.Equal("Andere Leistung", draft.Lines[0].Name);
        Assert.Equal("4", draft.Lines[0].Quantity);
    }

    /// <summary>
    /// Eine ergänzte Position schließt an die erkannten Nummern an. Mit den
    /// unverändert übernommenen Nummern 10, 20, 30 heißt das 31 – nicht 4.
    /// </summary>
    [Fact]
    public void EineErgänzteZeileSchließtAnDieErkanntenNummernAn()
    {
        InvoiceDraft draft = Prefilled(10, 20, 30);

        InvoiceLineDraft neu = draft.AddLine();

        Assert.Equal(31, neu.Number);
        Assert.Equal(4, draft.Lines.Count);
    }

    [Fact]
    public void EineVorbefüllteZeileLässtSichEntfernen()
    {
        InvoiceDraft draft = Prefilled();

        draft.Lines.RemoveAt(0);

        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal("Zweite", draft.Lines[0].Name);
    }

    /// <summary>
    /// Das Neunummerieren nimmt den erkannten Nummern ihre Herkunft und
    /// beginnt wieder bei eins. Das ist gewollt: Der Anwender hat die Tabelle
    /// verändert, damit gilt seine Reihenfolge und nicht mehr die der PDF.
    /// </summary>
    [Fact]
    public void DasNeunummerierenGreiftAuchNachDerVorbefüllung()
    {
        InvoiceDraft draft = Prefilled(10, 20, 30);

        draft.Lines.RemoveAt(1);
        draft.RenumberLines();

        Assert.Equal([1, 2], draft.Lines.Select(l => l.Number));
    }

    /// <summary>
    /// „Neue Rechnung“ räumt auch vorbefüllte Positionen weg. Blieben sie
    /// stehen, trüge die nächste Rechnung die Zeilen der vorigen.
    /// </summary>
    [Fact]
    public void NeueRechnungRäumtAuchVorbefülltePositionenWeg()
    {
        InvoiceDraft draft = Prefilled();

        draft.Reset();

        Assert.Empty(draft.Lines);
    }

    /// <summary>
    /// Nach dem Zurücksetzen ist der Entwurf wieder leer – und damit auch
    /// wieder aufnahmebereit. Sonst hinge eine einmal geschützte Tabelle für
    /// immer.
    /// </summary>
    [Fact]
    public void NachDemZurücksetzenNimmtDerEntwurfWiederPositionenAn()
    {
        InvoiceDraft draft = Prefilled();
        draft.Reset();

        PrefillSummary summary = DraftPrefiller.Apply(draft, Detection(1, 2, 3));

        Assert.Equal(3, draft.Lines.Count);
        Assert.Equal(3, summary.FilledLines);
        Assert.Equal(0, summary.SkippedExistingLines);
    }

    /// <summary>
    /// Die geschriebenen Zeichenketten müssen rechenbar sein. Wären sie es
    /// nicht, stünde eine vollständig aussehende Tabelle im Formular, unter
    /// der keine Summe erscheint.
    /// </summary>
    [Fact]
    public void ÜberVorbefülltePositionenLässtSichRechnen()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines =
            [
                new DetectedInvoiceLine(
                    1, "Beratung", null, 2m, "HUR", 100.00m, 200.00m,
                    VatCategory.StandardRate, 19m, []),
                new DetectedInvoiceLine(
                    2, "Schulungsunterlagen", null, 3m, "C62", 20.00m, 60.00m,
                    VatCategory.StandardRate, 7m, []),
            ],
        });

        InvoiceTotals totals = Assert.IsType<InvoiceTotals>(draft.TryCalculateTotals());

        Assert.Equal(260.00m, totals.LineTotal);
        Assert.Equal(42.20m, totals.TaxTotal);
        Assert.Equal(302.20m, totals.GrandTotal);
    }

    /// <summary>
    /// Eine bearbeitete vorbefüllte Zeile rechnet mit dem neuen Wert. Der
    /// erkannte Zeilenbetrag aus der PDF darf hier nirgends mehr auftauchen –
    /// er war Erkennungsevidenz und nie eine Rechengrösse.
    /// </summary>
    [Fact]
    public void EineBearbeiteteZeileRechnetMitDemNeuenWert()
    {
        InvoiceDraft draft = Prefilled();

        draft.Lines[0].Quantity = "2";

        InvoiceTotals totals = Assert.IsType<InvoiceTotals>(draft.TryCalculateTotals());

        Assert.Equal(400.00m, totals.LineTotal);
    }

    private static InvoiceDraft Prefilled(params int[] numbers)
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(
            draft, Detection(numbers.Length == 0 ? [1, 2, 3] : numbers));

        return draft;
    }

    private static InvoiceDetectionResult Detection(params int[] numbers) => new()
    {
        HasUsableText = true,
        Lines =
        [
            .. numbers.Select((number, index) => new DetectedInvoiceLine(
                number,
                index switch { 0 => "Erste", 1 => "Zweite", _ => "Dritte" },
                null,
                1m,
                "HUR",
                100.00m,
                100.00m,
                VatCategory.StandardRate,
                19m,
                [])),
        ],
    };
}

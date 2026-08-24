using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice 3 und 4 – die Übernahme erkannter Positionen in das Formular.
///
/// Zwei Eigenschaften stehen hier im Mittelpunkt, und beide sind
/// Sicherheitsgrenzen und keine Bequemlichkeit:
///
/// * **Alles oder nichts.** Ein halb gefüllter Entwurf wäre schlimmer als ein
///   leerer: Er sieht fertig aus.
/// * **Benutzerarbeit ist unantastbar.** Wer bereits Positionen erfasst hat,
///   bekommt sie nicht ergänzt, überschrieben oder vermischt.
/// </summary>
public sealed class PositionPrefillTests
{
    [Fact]
    public void VollständigeTabelleLandetGeschlossenImLeerenEntwurf()
    {
        var draft = new InvoiceDraft();

        PrefillSummary summary = DraftPrefiller.Apply(draft, Detection());

        Assert.Equal(2, draft.Lines.Count);
        Assert.Equal(2, summary.FilledLines);
        Assert.Equal(0, summary.SkippedExistingLines);
    }

    /// <summary>
    /// Jeder fachliche Wert wird ausdrücklich gesetzt – auch dort, wo der
    /// Programmstandard zufällig passt. Ein Default ist keine erkannte
    /// Information.
    /// </summary>
    [Fact]
    public void JederFachlicheWertStehtImEntwurf()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, Detection());

        InvoiceLineDraft first = draft.Lines[0];
        Assert.Equal(1, first.Number);
        Assert.Equal("Beratung", first.Name);
        Assert.Equal("Vor-Ort-Termin und Nachbereitung", first.Description);
        Assert.Equal("2,5", first.Quantity);
        Assert.Equal("HUR", first.Unit);
        Assert.Equal("100,00", first.NetUnitPrice);
        Assert.Equal(VatCategory.StandardRate, first.VatCategory);
        Assert.Equal("19", first.VatRate);

        InvoiceLineDraft second = draft.Lines[1];
        Assert.Equal(2, second.Number);
        Assert.Equal("Schulungsunterlagen", second.Name);
        Assert.Equal(string.Empty, second.Description);
        Assert.Equal("3", second.Quantity);
        Assert.Equal("C62", second.Unit);
        Assert.Equal("20,00", second.NetUnitPrice);
        Assert.Equal("7", second.VatRate);
    }

    /// <summary>
    /// **Die Einheit ist der gefährlichste Wert.** <c>InvoiceLineDraft</c>
    /// beginnt mit <c>C62</c>; eine vergessene Zuweisung fiele bei einer
    /// Stückliste nicht auf und machte aus Stunden stillschweigend Stück.
    /// </summary>
    [Theory]
    [InlineData("HUR")]
    [InlineData("KGM")]
    [InlineData("MTR")]
    public void DieEinheitStammtNieAusDemProgrammstandard(string unit)
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(1, "Leistung", quantity: 1m, unit: unit)],
        });

        Assert.Equal(unit, Assert.Single(draft.Lines).Unit);
    }

    /// <summary>
    /// Die geschriebenen Zeichenketten müssen die bestehenden Draft-Parser
    /// verlustfrei wieder hergeben. Sonst steht im Formular etwas, das beim
    /// Erzeugen der Rechnung als Fehler auffällt – oder schlimmer: gerundet
    /// durchgeht.
    ///
    /// Insbesondere darf kein Tausenderpunkt entstehen:
    /// <c>InvoiceDraft.TryParseDecimal</c> unterstützt ihn ausdrücklich nicht.
    /// </summary>
    [Theory]
    [InlineData(1234.5678, "1234,5678")]
    [InlineData(1000, "1000")]
    [InlineData(2.5, "2,5")]
    [InlineData(1, "1")]
    public void MengenBleibenVerlustfreiLesbar(double raw, string expected)
    {
        var quantity = (decimal)raw;
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(1, "Leistung", quantity: quantity)],
        });

        string written = Assert.Single(draft.Lines).Quantity;

        Assert.Equal(expected, written);
        Assert.True(InvoiceDraft.TryParseDecimal(written, out decimal readBack));
        Assert.Equal(quantity, readBack);
    }

    [Theory]
    [InlineData(1234.56, "1234,56")]
    [InlineData(20, "20,00")]
    [InlineData(0.05, "0,05")]
    public void EinzelpreiseBleibenVerlustfreiLesbar(double raw, string expected)
    {
        var price = (decimal)raw;
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(1, "Leistung", unitPrice: price)],
        });

        string written = Assert.Single(draft.Lines).NetUnitPrice;

        Assert.Equal(expected, written);
        Assert.True(InvoiceDraft.TryParseDecimal(written, out decimal readBack));
        Assert.Equal(price, readBack);
    }

    /// <summary>
    /// Eine ausdrücklich erkannte Positionsnummer wird unverändert
    /// übernommen. Sie steht so in der PDF; sie beim Vorbefüllen still zu
    /// normieren hieße, dem Anwender eine andere Rechnung zu zeigen als die,
    /// die er vor sich hat.
    /// </summary>
    [Fact]
    public void ErkanntePositionsnummernBleibenUnverändert()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(10, "Erste"), Line(20, "Zweite"), Line(30, "Dritte")],
        });

        Assert.Equal([10, 20, 30], draft.Lines.Select(l => l.Number));
    }

    /// <summary>
    /// Scheitert die Umwandlung einer einzigen Zeile, wird keine einzige
    /// übernommen. Ein Entwurf mit zwei von drei Positionen wäre eine falsche
    /// Rechnung, die vollständig aussieht.
    /// </summary>
    [Fact]
    public void EineUnbrauchbareZeileVerhindertJedeÜbernahme()
    {
        var draft = new InvoiceDraft();

        PrefillSummary summary = DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines =
            [
                Line(1, "Beratung"),
                Line(2, "Sonderposten", unit: "XXX"),
                Line(3, "Schulung"),
            ],
        });

        Assert.Empty(draft.Lines);
        Assert.Equal(0, summary.FilledLines);
    }

    /// <summary>Ohne erkannte Tabelle bleibt der Entwurf unberührt.</summary>
    [Fact]
    public void OhneErkannteTabellePassiertNichts()
    {
        var draft = new InvoiceDraft();

        PrefillSummary summary = DraftPrefiller.Apply(
            draft, new InvoiceDetectionResult { HasUsableText = true });

        Assert.Empty(draft.Lines);
        Assert.Equal(0, summary.FilledLines);
        Assert.Equal(0, summary.SkippedExistingLines);
    }

    // ---------------------------------------------------------------- Slice 4

    /// <summary>
    /// Ist bereits eine Position erfasst, wird **keine** erkannte übernommen –
    /// weder ergänzt noch ersetzt noch vermischt. Was der Anwender getippt
    /// hat, gehört ihm.
    /// </summary>
    [Fact]
    public void VorhandeneBenutzerpositionVerhindertJedeÜbernahme()
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft manual = draft.AddLine();
        manual.Name = "Von Hand erfasst";

        PrefillSummary summary = DraftPrefiller.Apply(draft, Detection());

        InvoiceLineDraft remaining = Assert.Single(draft.Lines);
        Assert.Same(manual, remaining);
        Assert.Equal("Von Hand erfasst", remaining.Name);

        Assert.Equal(0, summary.FilledLines);
        Assert.Equal(2, summary.SkippedExistingLines);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private static InvoiceDetectionResult Detection() => new()
    {
        HasUsableText = true,
        Lines =
        [
            new DetectedInvoiceLine(
                1, "Beratung", "Vor-Ort-Termin und Nachbereitung", 2.5m, "HUR", 100.00m,
                250.00m, VatCategory.StandardRate, 19m, []),
            new DetectedInvoiceLine(
                2, "Schulungsunterlagen", null, 3m, "C62", 20.00m,
                60.00m, VatCategory.StandardRate, 7m, []),
        ],
    };

    private static DetectedInvoiceLine Line(
        int number,
        string name,
        decimal quantity = 1m,
        string unit = "HUR",
        decimal unitPrice = 100.00m)
        => new(number, name, null, quantity, unit, unitPrice, null,
               VatCategory.StandardRate, 19m, []);
}

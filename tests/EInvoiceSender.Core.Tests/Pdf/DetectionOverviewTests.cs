using System.Globalization;
using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Prüft die Übersicht „PDF analysiert“ aus Schritt 1.
///
/// **Warum es diese Tests gibt:** Die Übersicht meldete nur „Käuferangaben
/// erkannt“. Im echten Testlauf war der erkannte Käufer aber „Währung: EUR“ –
/// zu sehen erst zwei Schritte später. Steht der gelesene Wert daneben, fällt
/// so etwas sofort auf. Genau deshalb prüfen diese Tests die Werte und nicht
/// nur, dass überhaupt eine Zeile entsteht.
/// </summary>
public sealed class DetectionOverviewTests : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

    /// <summary>
    /// Beträge und Datumsangaben erscheinen in der Kultur des Anwenders. Der
    /// Test legt sie fest, sonst hinge sein Ergebnis am Rechner, auf dem er
    /// läuft.
    /// </summary>
    public DetectionOverviewTests()
        => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

    public void Dispose() => CultureInfo.CurrentCulture = _previousCulture;

    [Fact]
    public void DieÜbersichtNenntDieGelesenenWerte()
    {
        string[] zeilen = Describe(Complete());

        Assert.Contains("Rechnungsnummer erkannt: RE-2026-0815", zeilen);
        Assert.Contains("Empfänger erkannt: Nordlicht Handel GmbH", zeilen);
        Assert.Contains("Währung erkannt: EUR", zeilen);
        Assert.Contains("Rechnungsdatum erkannt: 09.08.2026", zeilen);
        Assert.Contains("Gesamtbetrag erkannt: 714,00", zeilen);
        Assert.Contains("Umsatzsteuersatz erkannt: 19 %", zeilen);
    }

    /// <summary>
    /// Die Bankverbindung steht offen im Fenster, oft während einer
    /// Bildschirmübertragung. Vollständig gehört sie dort nicht hin.
    /// </summary>
    [Fact]
    public void DieIbanErscheintNurMaskiert()
    {
        string[] zeilen = Describe(Complete());

        Assert.Contains(zeilen, z => z.StartsWith("IBAN erkannt: DE89", StringComparison.Ordinal));
        Assert.DoesNotContain(zeilen, z => z.Contains("DE89370400440532013000", StringComparison.Ordinal));
        Assert.DoesNotContain(zeilen, z => z.Contains("0532013000", StringComparison.Ordinal));
    }

    [Fact]
    public void EinUnsicherGelesenerWertWirdZurPrüfungAusgewiesen()
    {
        DetectionEntry eintrag = Single(new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("RE-2026-0815", DetectionConfidence.Medium),
        }, "Rechnungsnummer");

        Assert.Equal(DetectionEntryKind.Uncertain, eintrag.Kind);
        Assert.Equal("Rechnungsnummer erkannt: RE-2026-0815 – bitte prüfen", eintrag.Text);
    }

    [Fact]
    public void EinSicherGelesenerWertWirdNichtZurPrüfungAusgewiesen()
    {
        DetectionEntry eintrag = Single(Complete(), "Rechnungsnummer");

        Assert.Equal(DetectionEntryKind.Found, eintrag.Kind);
        Assert.DoesNotContain("prüfen", eintrag.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein Wert unterhalb der Übernahmeschwelle steht nicht im Formular. Ihn
    /// trotzdem als erkannt anzuzeigen wäre irreführend.
    /// </summary>
    [Fact]
    public void EinZuUnsichererWertGiltAlsNichtGefunden()
    {
        DetectionEntry eintrag = Single(new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("RE-2026-0815", DetectionConfidence.Low),
        }, "Rechnungsnummer");

        Assert.Equal(DetectionEntryKind.Missing, eintrag.Kind);
        Assert.DoesNotContain("RE-2026-0815", eintrag.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void OhneVerwertbarenTextStehtDortNurEineZeile()
    {
        DetectionEntry eintrag = Assert.Single(
            DetectionOverview.Describe(InvoiceDetectionResult.WithoutText));

        Assert.Equal(DetectionEntryKind.Missing, eintrag.Kind);
        Assert.Contains("von Hand", eintrag.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Positionserkennung gibt es nicht, und die Übersicht sagt das. Sonst
    /// wartet jemand auf eine Automatik, die nie kommt.
    /// </summary>
    [Fact]
    public void DieFehlendePositionserkennungWirdBenannt()
        => Assert.Contains(
            Describe(Complete()),
            z => z.Contains("Rechnungspositionen", StringComparison.Ordinal)
                 && z.Contains("von Hand", StringComparison.Ordinal));

    [Fact]
    public void EineFehlendeAngabeWirdAlsFehlendGemeldet()
    {
        DetectionEntry eintrag = Single(
            new InvoiceDetectionResult { HasUsableText = true }, "Empfänger");

        Assert.Equal(DetectionEntryKind.Missing, eintrag.Kind);
        Assert.Equal("Empfänger nicht gefunden", eintrag.Text);
    }

    private static DetectionEntry Single(InvoiceDetectionResult detection, string label)
        => Assert.Single(
            DetectionOverview.Describe(detection),
            e => e.Text.StartsWith(label + " ", StringComparison.Ordinal));

    private static string[] Describe(InvoiceDetectionResult detection)
        => [.. DetectionOverview.Describe(detection).Select(e => e.Text)];

    /// <summary>Die Daten der Testrechnung, alle zweifelsfrei gelesen.</summary>
    private static InvoiceDetectionResult Complete() => new()
    {
        HasUsableText = true,
        PageCount = 1,
        InvoiceNumber = new DetectedValue<string>("RE-2026-0815", DetectionConfidence.High),
        IssueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 9), DetectionConfidence.High),
        DeliveryDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 8), DetectionConfidence.High),
        DueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 23), DetectionConfidence.High),
        Currency = new DetectedValue<string>("EUR", DetectionConfidence.High),
        Seller = new DetectedParty
        {
            Name = new DetectedValue<string>("Muster IT GmbH", DetectionConfidence.High),
        },
        Buyer = new DetectedParty
        {
            Name = new DetectedValue<string>("Nordlicht Handel GmbH", DetectionConfidence.High),
            City = new DetectedValue<string>("Hamburg", DetectionConfidence.High),
        },
        Iban = new DetectedValue<string>("DE89370400440532013000", DetectionConfidence.High),
        Bic = new DetectedValue<string>("COBADEFFXXX", DetectionConfidence.High),
        Totals = new DetectedTotals
        {
            Net = new DetectedValue<decimal>(600.00m, DetectionConfidence.High),
            Tax = new DetectedValue<decimal>(114.00m, DetectionConfidence.High),
            Gross = new DetectedValue<decimal>(714.00m, DetectionConfidence.High),
            Payable = new DetectedValue<decimal>(714.00m, DetectionConfidence.High),
            VatRates = [new DetectedValue<decimal>(19m, DetectionConfidence.High)],
        },
    };
}

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
        Assert.Contains("Land des Empfängers erkannt: AT", zeilen);
        Assert.Contains("USt-IdNr. des Empfängers erkannt: ATU12345678", zeilen);
        Assert.Contains("E-Mail des Empfängers erkannt: einkauf@nordlicht.example", zeilen);
        Assert.Contains("Währung erkannt: EUR", zeilen);
        Assert.Contains("Rechnungsdatum erkannt: 09.08.2026", zeilen);
        Assert.Contains("Leistungszeitraum von erkannt: 01.08.2026", zeilen);
        Assert.Contains("Leistungszeitraum bis erkannt: 08.08.2026", zeilen);
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
    /// Ohne sichere Tabelle bleibt der manuelle Hinweis. Wer nichts erkannt
    /// bekommt, soll nicht auf eine Automatik warten, die hier nicht greift.
    /// </summary>
    [Fact]
    public void OhneSicherePositionenBleibtDerManuelleHinweis()
        => Assert.Contains(
            Describe(Complete()),
            z => z.Contains("Rechnungspositionen", StringComparison.Ordinal)
                 && z.Contains("von Hand", StringComparison.Ordinal));

    /// <summary>
    /// Mit sicherer Tabelle meldet Schritt 1 **nur die Anzahl**.
    ///
    /// Beschreibungen, Mengen und Preise gehören nicht in die Übersicht: Sie
    /// steht offen im Fenster, oft während einer Bildschirmübertragung, und
    /// zum Prüfen genügt die Zahl. Die Werte selbst sieht der Anwender in
    /// Schritt 2, wo er sie ohnehin bestätigen muss.
    /// </summary>
    [Fact]
    public void SicherePositionenWerdenNurGezählt()
    {
        string zeile = Assert.Single(
            Describe(Complete() with { Lines = [Line(1, "Beratung"), Line(2, "Schulung")] }),
            z => z.Contains("Rechnungspositionen", StringComparison.Ordinal));

        Assert.Contains("2", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("von Hand", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("Beratung", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("Schulung", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("100", zeile, StringComparison.Ordinal);
    }

    /// <summary>Eine erkannte Tabelle ist ein Fund, keine Lücke.</summary>
    [Fact]
    public void SicherePositionenGeltenAlsGefunden()
    {
        DetectionEntry eintrag = Assert.Single(
            DetectionOverview.Describe(Complete() with { Lines = [Line(1, "Beratung")] }),
            e => e.Text.Contains("Rechnungspositionen", StringComparison.Ordinal));

        Assert.Equal(DetectionEntryKind.Found, eintrag.Kind);
    }

    /// <summary>
    /// Nennt die Rechnung keine Mengeneinheit, sagt Schritt 1 das ausdrücklich
    /// – mit Anzahl und mit dem, was zu tun ist.
    ///
    /// **Warum das nicht schweigen darf:** Die Positionen sind erkannt und
    /// werden übernommen, aber die Einheit bleibt leer. Ohne diesen Satz
    /// stünde der Anwender in Schritt 2 vor leeren Feldern und hielte das für
    /// einen Fehler der Anwendung. Es ist keiner – die Rechnung sagt es
    /// schlicht nicht.
    /// </summary>
    [Fact]
    public void FehlendeMengeneinheitenWerdenBenannt()
    {
        string zeile = LineItemLine(
            Line(1, "Beratung", unitCode: null), Line(2, "Schulung", unitCode: null));

        Assert.Contains("erkannt: 2", zeile, StringComparison.Ordinal);
        Assert.Contains("bei 2 Positionen ist keine Mengeneinheit angegeben", zeile, StringComparison.Ordinal);
        Assert.Contains("ergänzen", zeile, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gemischt gezählt wird nur, was tatsächlich fehlt – nicht die ganze
    /// Tabelle.
    /// </summary>
    [Fact]
    public void NurDieTatsächlichFehlendenEinheitenWerdenGezählt()
    {
        string zeile = LineItemLine(
            Line(1, "Beratung"),
            Line(2, "Schulung", unitCode: null),
            Line(3, "Material", unitCode: null),
            Line(4, "Kabel"));

        Assert.Contains("erkannt: 4", zeile, StringComparison.Ordinal);
        Assert.Contains("bei 2 Positionen", zeile, StringComparison.Ordinal);
    }

    [Fact]
    public void EineEinzelneFehlendeEinheitStehtInDerEinzahl()
    {
        string zeile = LineItemLine(Line(1, "Beratung", unitCode: null));

        Assert.Contains("bei 1 Position ist", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("bei 1 Positionen", zeile, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sind alle Einheiten da, bleibt der Satz so kurz wie bisher. Ein Hinweis
    /// auf eine Lücke, die es nicht gibt, wäre Lärm.
    /// </summary>
    [Fact]
    public void MitVollständigenEinheitenBleibtDerHinweisAus()
    {
        string zeile = LineItemLine(Line(1, "Beratung"), Line(2, "Schulung"));

        Assert.Contains("erkannt: 2", zeile, StringComparison.Ordinal);
        Assert.DoesNotContain("Mengeneinheit", zeile, StringComparison.Ordinal);
    }

    private static string LineItemLine(params DetectedInvoiceLine[] lines)
        => Assert.Single(
            Describe(Complete() with { Lines = lines }),
            z => z.Contains("Rechnungspositionen", StringComparison.Ordinal));

    private static DetectedInvoiceLine Line(
        int number, string name, string? unitCode = "HUR") => new(
        number, name, null, 1m, unitCode, 100m, 100m,
        EInvoiceSender.Core.Models.VatCategory.StandardRate, 19m, []);

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
        BillingPeriodStart = new DetectedValue<DateOnly>(
            new DateOnly(2026, 8, 1), DetectionConfidence.High),
        BillingPeriodEnd = new DetectedValue<DateOnly>(
            new DateOnly(2026, 8, 8), DetectionConfidence.High),
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
            Country = new DetectedValue<string>("AT", DetectionConfidence.High),
            VatId = new DetectedValue<string>("ATU12345678", DetectionConfidence.High),
            Email = new DetectedValue<string>(
                "einkauf@nordlicht.example", DetectionConfidence.High),
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

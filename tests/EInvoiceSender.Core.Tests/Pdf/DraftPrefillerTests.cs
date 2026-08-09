using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Prüft den Weg vom Erkennungsergebnis ins Formular.
///
/// Hier entscheidet sich, ob die Zusage hält, dass unsichere Werte **nicht**
/// stillschweigend übernommen werden. Das ist die eigentliche Schutzlinie der
/// ganzen Erkennung.
/// </summary>
public sealed class DraftPrefillerTests
{
    [Fact]
    public void SichereWerteFüllenDasFormularUndWerdenGekennzeichnet()
    {
        var draft = new InvoiceDraft();

        var detection = new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("RE-2026-0815", DetectionConfidence.High),
            IssueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 9), DetectionConfidence.High),
        };

        PrefillSummary summary = DraftPrefiller.Apply(draft, detection);

        Assert.Equal("RE-2026-0815", draft.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 8, 9), draft.IssueDate);
        Assert.Equal(2, summary.FilledFields);
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.InvoiceNumber)));
    }

    /// <summary>
    /// Der wichtigste Test dieser Datei: Ein unsicherer Wert darf das Formular
    /// nicht anfassen. Er wird gezählt, damit die Oberfläche darauf hinweisen
    /// kann - eingetragen wird er nicht.
    /// </summary>
    [Fact]
    public void UnsichereWerteWerdenNichtÜbernommen()
    {
        var draft = new InvoiceDraft();

        var detection = new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("0381 1234567", DetectionConfidence.Low),
        };

        PrefillSummary summary = DraftPrefiller.Apply(draft, detection);

        Assert.Equal(string.Empty, draft.InvoiceNumber);
        Assert.Equal(0, summary.FilledFields);
        Assert.Contains("Rechnungsnummer", summary.SkippedLowConfidence);
    }

    [Fact]
    public void MittlereSicherheitWirdÜbernommenAberZurPrüfungGemeldet()
    {
        var draft = new InvoiceDraft();

        var detection = new InvoiceDetectionResult
        {
            HasUsableText = true,
            DeliveryDate = new DetectedValue<DateOnly>(
                new DateOnly(2026, 8, 8), DetectionConfidence.Medium),
        };

        PrefillSummary summary = DraftPrefiller.Apply(draft, detection);

        Assert.Equal(new DateOnly(2026, 8, 8), draft.DeliveryDate);
        Assert.Contains("Leistungsdatum", summary.UncertainFields);
        Assert.Equal(FieldOrigin.DetectedUncertain, draft.OriginOf(nameof(draft.DeliveryDate)));
    }

    /// <summary>
    /// Sobald der Anwender ein Feld anfasst, gilt es als von Hand erfasst und
    /// die Kennzeichnung verschwindet. Genau dann hat sie ihren Zweck erfüllt.
    /// </summary>
    [Fact]
    public void NachManuellerÄnderungGiltEinFeldNichtMehrAlsErkannt()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("RE-2026-0815", DetectionConfidence.High),
        });

        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.InvoiceNumber)));

        draft.InvoiceNumber = "RE-2026-0816";

        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.InvoiceNumber)));
    }

    /// <summary>
    /// Werte, die wortgleich in der Vorlage stehen, werden als "aus Vorlage"
    /// ausgewiesen. Das ist ehrlicher als "aus PDF erkannt" - die PDF hat sie
    /// nur bestätigt.
    /// </summary>
    [Fact]
    public void WerteAusDerVorlageWerdenAlsSolcheAusgewiesen()
    {
        var draft = new InvoiceDraft();
        var template = new CompanyTemplate { SellerName = "Muster IT GmbH" };

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Seller = new DetectedParty
            {
                Name = new DetectedValue<string>("Muster IT GmbH", DetectionConfidence.High),
            },
        }, template);

        Assert.Equal(FieldOrigin.Template, draft.OriginOf(nameof(draft.SellerName)));
    }

}

/// <summary>
/// Prüft den Abgleich zwischen dem aus der PDF gelesenen und dem berechneten
/// Betrag.
/// </summary>
public sealed class TotalsCrossCheckTests
{
    [Fact]
    public void GleicheBeträgeWerdenBestätigt()
    {
        TotalsComparison comparison = TotalsCrossCheck.Compare(
            new DetectedTotals { Gross = new DetectedValue<decimal>(1190.00m, DetectionConfidence.High) },
            Totals(grandTotal: 1190.00m));

        Assert.True(comparison.WasPerformed);
        Assert.True(comparison.Matches);
    }

    [Fact]
    public void AbweichungWirdMitBeidenBeträgenGemeldet()
    {
        TotalsComparison comparison = TotalsCrossCheck.Compare(
            new DetectedTotals { Gross = new DetectedValue<decimal>(1190.00m, DetectionConfidence.High) },
            Totals(grandTotal: 1090.00m));

        Assert.True(comparison.WasPerformed);
        Assert.False(comparison.Matches);
        Assert.Contains("1.190,00", comparison.Message, StringComparison.Ordinal);
        Assert.Contains("1.090,00", comparison.Message, StringComparison.Ordinal);
    }

    /// <summary>Ein Cent Unterschied ist eine Rundung, kein Fehler.</summary>
    [Fact]
    public void KleineRundungsunterschiedeGeltenAlsÜbereinstimmung()
    {
        TotalsComparison comparison = TotalsCrossCheck.Compare(
            new DetectedTotals { Gross = new DetectedValue<decimal>(1190.01m, DetectionConfidence.High) },
            Totals(grandTotal: 1190.00m));

        Assert.True(comparison.Matches);
    }

    [Fact]
    public void OhneErkanntenBetragWirdNichtVerglichen()
    {
        TotalsComparison comparison = TotalsCrossCheck.Compare(new DetectedTotals(), Totals(1190.00m));

        Assert.False(comparison.WasPerformed);
    }

    private static InvoiceTotals Totals(decimal grandTotal) => new(
        LineTotal: grandTotal,
        AllowanceTotal: 0m,
        ChargeTotal: 0m,
        TaxBasisTotal: grandTotal,
        TaxTotal: 0m,
        GrandTotal: grandTotal,
        PaidAmount: 0m,
        RoundingAmount: 0m,
        DuePayableAmount: grandTotal,
        LineNetAmounts: [],
        VatBreakdown: []);
}

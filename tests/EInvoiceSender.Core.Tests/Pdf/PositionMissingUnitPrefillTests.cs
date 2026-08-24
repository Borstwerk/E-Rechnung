using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C4 – eine fehlende Einheit darf nicht zu einer erfundenen werden.
///
/// **Der gefährlichste Standard der ganzen Anwendung** steht in
/// <see cref="InvoiceLineDraft"/>: <c>Unit</c> beginnt mit <c>"C62"</c> –
/// Stück. Übernimmt die Vorbefüllung eine Position ohne Einheit und lässt
/// diese Property unberührt, steht im Formular „Stück“, obwohl die Rechnung
/// nichts dergleichen sagt. Bei einer Stundenrechnung fällt das niemandem auf,
/// und der Kunde bekommt eine falsche E-Rechnung.
///
/// Deshalb wird die Einheit **ausdrücklich geleert**. Die bestehende
/// Entwurfsprüfung hält die Rechnung danach von selbst auf, bis der Anwender
/// eine Einheit ergänzt. Ein leeres Feld kostet einen Handgriff; ein still
/// gefülltes falsches Feld kostet eine fehlerhafte Rechnung.
/// </summary>
public sealed class PositionMissingUnitPrefillTests
{
    [Fact]
    public void OhneErkannteEinheitBleibtDieEinheitImEntwurfLeer()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, Detection(unitCode: null));

        InvoiceLineDraft line = Assert.Single(draft.Lines);

        Assert.Equal(string.Empty, line.Unit);
        Assert.NotEqual("C62", line.Unit);
    }

    /// <summary>Eine erkannte Einheit steht unverändert im Entwurf.</summary>
    [Theory]
    [InlineData("HUR")]
    [InlineData("C62")]
    [InlineData("KGM")]
    [InlineData("MTR")]
    public void EineErkannteEinheitStehtImEntwurf(string unit)
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, Detection(unit));

        Assert.Equal(unit, Assert.Single(draft.Lines).Unit);
    }

    /// <summary>
    /// Die übrigen Werte werden trotz fehlender Einheit vollständig
    /// übernommen. Die Lücke betrifft eine Angabe, nicht die Position.
    /// </summary>
    [Fact]
    public void DieÜbrigenWerteWerdenTrotzdemVollständigÜbernommen()
    {
        var draft = new InvoiceDraft();

        PrefillSummary summary = DraftPrefiller.Apply(draft, Detection(unitCode: null));

        InvoiceLineDraft line = Assert.Single(draft.Lines);

        Assert.Equal(1, line.Number);
        Assert.Equal("Beratung", line.Name);
        Assert.Equal("3", line.Quantity);
        Assert.Equal("100,00", line.NetUnitPrice);
        Assert.Equal("19", line.VatRate);
        Assert.Equal(1, summary.FilledLines);
    }

    /// <summary>
    /// **Die Probe aufs Exempel.** Ohne ergänzte Einheit entsteht keine
    /// Rechnung. Das leistet die bestehende Entwurfsprüfung, unverändert –
    /// die Vorbefüllung muss ihr nur die Wahrheit übergeben.
    /// </summary>
    [Fact]
    public void OhneErgänzteEinheitEntstehtKeineRechnung()
    {
        var draft = Complete(unitCode: null);

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.Null(invoice);
        Assert.True(report.HasErrors);
        Assert.Contains(
            report.Findings,
            f => f.FieldPath.Contains("Unit", StringComparison.Ordinal));
    }

    /// <summary>
    /// Und nach der Ergänzung durch den Anwender läuft der gewöhnliche Weg
    /// weiter. Die fehlende Einheit hält auf, sie sperrt nicht.
    /// </summary>
    [Fact]
    public void NachDemErgänzenDerEinheitEntstehtDieRechnung()
    {
        var draft = Complete(unitCode: null);

        draft.Lines[0].Unit = "HUR";

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);
        Assert.Equal("HUR", Assert.Single(invoice.Lines).Unit.Value);
    }

    /// <summary>
    /// Die Zusammenfassung zählt die fehlenden Einheiten der übernommenen
    /// Positionen.
    /// </summary>
    [Fact]
    public void DieZusammenfassungZähltDieFehlendenEinheiten()
    {
        var draft = new InvoiceDraft();

        PrefillSummary summary = DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(1, null), Line(2, "HUR"), Line(3, null)],
        });

        Assert.Equal(3, summary.FilledLines);
        Assert.Equal(2, summary.LinesMissingUnit);
    }

    /// <summary>
    /// **Die feine, aber wichtige Unterscheidung.** Wird wegen bereits
    /// erfasster Benutzerpositionen gar nichts übernommen, fehlt im Entwurf
    /// auch keine Einheit. Diese Positionen als „Einheit fehlt“ zu melden
    /// schickte den Anwender in seine eigene, vollständig ausgefüllte Tabelle,
    /// um dort etwas zu suchen, das nicht fehlt.
    /// </summary>
    [Fact]
    public void NichtÜbernommenePositionenMeldenKeineFehlendeEinheit()
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft manual = draft.AddLine();
        manual.Name = "Von Hand erfasst";

        PrefillSummary summary = DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(1, null), Line(2, null)],
        });

        Assert.Equal(0, summary.FilledLines);
        Assert.Equal(2, summary.SkippedExistingLines);
        Assert.Equal(0, summary.LinesMissingUnit);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private static DetectedInvoiceLine Line(int number, string? unitCode) => new(
        number, $"Position {number}", null, 1m, unitCode, 100.00m, 100.00m,
        VatCategory.StandardRate, 19m, []);

    private static InvoiceDetectionResult Detection(string? unitCode) => new()
    {
        HasUsableText = true,
        Lines =
        [
            new DetectedInvoiceLine(
                1, "Beratung", null, 3m, unitCode, 100.00m, 300.00m,
                VatCategory.StandardRate, 19m, []),
        ],
    };

    /// <summary>
    /// Der vorbefüllte Entwurf plus die Kopfdaten, die keine Positionstabelle
    /// liefert. Geprüft wird der Weg der Einheit, nicht der der Kopfdaten.
    /// </summary>
    private static InvoiceDraft Complete(string? unitCode)
    {
        var draft = new InvoiceDraft();
        DraftPrefiller.Apply(draft, Detection(unitCode));

        draft.InvoiceNumber = "RE-2026-0815";
        draft.IssueDate = new DateOnly(2026, 8, 9);

        draft.SellerName = "Muster IT GmbH";
        draft.SellerStreet = "Musterstraße 10";
        draft.SellerPostalCode = "18055";
        draft.SellerCity = "Rostock";
        draft.SellerCountry = "DE";
        draft.SellerVatId = "DE123456789";

        draft.BuyerName = "Nordlicht Handel GmbH";
        draft.BuyerStreet = "Hafenweg 3";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerCountry = "DE";

        draft.BankAccountHolder = "Muster IT GmbH";
        draft.BankIban = "DE89370400440532013000";
        draft.PaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.";
        draft.DueDate = new DateOnly(2026, 8, 23);

        return draft;
    }

    private static string Describe(ValidationReport report)
        => string.Join(
            Environment.NewLine,
            report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));
}

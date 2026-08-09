using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Prüft, womit eine neue Rechnungsposition beginnt und was daran fehlen darf.
///
/// **Was auffiel:** Eine neue Position stand mit „1“, „0,00“ und „19“ da. Das
/// sieht nach ausgefüllt aus, ist aber geraten. Schlimmer: Eine Null lässt
/// sich nicht von einer vergessenen Eingabe unterscheiden – aus einem
/// übersehenen Feld wird eine Position über null Euro, und niemand meldet
/// etwas.
///
/// Die Felder beginnen deshalb leer. Was fachlich fehlt, meldet die Prüfung;
/// was sich technisch ableiten lässt – die Positionsnummer – vergibt die
/// Anwendung selbst.
/// </summary>
public sealed class InvoiceLineDraftTests
{
    [Theory]
    [InlineData(nameof(InvoiceLineDraft.Quantity))]
    [InlineData(nameof(InvoiceLineDraft.NetUnitPrice))]
    [InlineData(nameof(InvoiceLineDraft.VatRate))]
    [InlineData(nameof(InvoiceLineDraft.AllowanceAmount))]
    public void EineNeuePositionBeginntOhneVorgegebeneZahlen(string property)
    {
        InvoiceLineDraft line = new InvoiceDraft().AddLine();

        string wert = (string)typeof(InvoiceLineDraft).GetProperty(property)!.GetValue(line)!;

        Assert.True(
            wert.Length == 0,
            $"{property} beginnt mit '{wert}'. Ein vorgegebener Zahlenwert lässt sich nicht von "
            + "einer bewussten Eingabe unterscheiden.");
    }

    /// <summary>
    /// Die Preisbasismenge ist die Ausnahme: Sie steht nicht im Formular und
    /// ist nach EN 16931 mit 1 belegt, wenn nichts angegeben ist. Ein
    /// ableitbarer technischer Wert darf vorgegeben sein.
    /// </summary>
    [Fact]
    public void DiePreisbasismengeBleibtVorgegeben()
        => Assert.Equal("1", new InvoiceDraft().AddLine().PriceBaseQuantity);

    [Theory]
    [InlineData(nameof(InvoiceLineDraft.Quantity), "APP-EDT-021")]
    [InlineData(nameof(InvoiceLineDraft.NetUnitPrice), "APP-EDT-022")]
    [InlineData(nameof(InvoiceLineDraft.VatRate), "APP-EDT-024")]
    public void EineFehlendePflichtangabeWirdGemeldet(string property, string ruleId)
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft line = draft.AddLine();

        line.Name = "Systemadministration";
        line.Quantity = "4";
        line.NetUnitPrice = "85,00";
        line.VatRate = "19";

        typeof(InvoiceLineDraft).GetProperty(property)!.SetValue(line, string.Empty);

        ValidationReport report = draft.TryBuildInvoice(out _);

        Assert.Contains(report.Findings, f => f.RuleId == ruleId && f.Severity == FindingSeverity.Error);
    }

    /// <summary>
    /// Der Gegenbeweis: Eine ausgefüllte Position erzeugt keine dieser
    /// Meldungen. Sonst prüfte der Test oben nur, dass überhaupt etwas
    /// beanstandet wird.
    /// </summary>
    [Fact]
    public void EineAusgefülltePositionWirdNichtBeanstandet()
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft line = draft.AddLine();

        line.Name = "Systemadministration";
        line.Quantity = "4";
        line.NetUnitPrice = "85,00";
        line.VatRate = "19";

        ValidationReport report = draft.TryBuildInvoice(out _);

        Assert.DoesNotContain(
            report.Findings,
            f => f.RuleId is "APP-EDT-021" or "APP-EDT-022" or "APP-EDT-024");
    }

    /// <summary>Ein fehlender Rabatt ist kein Fehler, sondern kein Rabatt.</summary>
    [Fact]
    public void EinFehlenderRabattWirdNichtBeanstandet()
    {
        var draft = new InvoiceDraft();
        InvoiceLineDraft line = draft.AddLine();

        line.Name = "Systemadministration";
        line.Quantity = "4";
        line.NetUnitPrice = "85,00";
        line.VatRate = "19";
        line.AllowanceAmount = string.Empty;

        ValidationReport report = draft.TryBuildInvoice(out _);

        Assert.DoesNotContain(report.Findings, f => f.RuleId == "APP-EDT-023");
    }

    [Fact]
    public void PositionenWerdenFortlaufendNummeriert()
    {
        var draft = new InvoiceDraft();

        draft.AddLine();
        draft.AddLine();
        draft.AddLine();

        Assert.Equal([1, 2, 3], draft.Lines.Select(l => l.Number));
    }

    [Fact]
    public void NachDemEntfernenBleibtDieNummerierungLückenlos()
    {
        var draft = new InvoiceDraft();

        draft.AddLine();
        InvoiceLineDraft zweite = draft.AddLine();
        draft.AddLine();

        draft.Lines.Remove(zweite);
        draft.RenumberLines();

        Assert.Equal([1, 2], draft.Lines.Select(l => l.Number));
    }

    /// <summary>
    /// Und die nächste Position zählt danach richtig weiter – nicht bei der
    /// alten Höchstnummer.
    /// </summary>
    [Fact]
    public void NachDemEntfernenZähltDieNächstePositionRichtigWeiter()
    {
        var draft = new InvoiceDraft();

        draft.AddLine();
        InvoiceLineDraft zweite = draft.AddLine();
        draft.AddLine();

        draft.Lines.Remove(zweite);
        draft.RenumberLines();

        Assert.Equal(3, draft.AddLine().Number);
    }
}

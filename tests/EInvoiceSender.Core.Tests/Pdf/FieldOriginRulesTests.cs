using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Prüft die eine Regel, die entscheidet, ob ein vorgeschlagener Wert einen
/// vorhandenen ersetzen darf.
/// </summary>
public sealed class FieldOriginRulesTests
{
    [Theory]
    // Ein Programmstandard weicht jeder Quelle.
    [InlineData(FieldOrigin.Default, FieldOrigin.TemplateDefault, true)]
    [InlineData(FieldOrigin.Default, FieldOrigin.Template, true)]
    [InlineData(FieldOrigin.Default, FieldOrigin.DetectedReliably, true)]
    [InlineData(FieldOrigin.Default, FieldOrigin.DetectedUncertain, true)]
    [InlineData(FieldOrigin.Default, FieldOrigin.Manual, true)]
    // Die Firmenvorlage steht über der Erkennung.
    [InlineData(FieldOrigin.Template, FieldOrigin.DetectedReliably, false)]
    [InlineData(FieldOrigin.Template, FieldOrigin.DetectedUncertain, false)]
    [InlineData(FieldOrigin.Template, FieldOrigin.TemplateDefault, false)]
    [InlineData(FieldOrigin.Template, FieldOrigin.Manual, true)]
    // Ein Komfort-Default darf neu berechnet und von jeder stärkeren Quelle ersetzt werden.
    [InlineData(FieldOrigin.TemplateDefault, FieldOrigin.TemplateDefault, true)]
    [InlineData(FieldOrigin.TemplateDefault, FieldOrigin.DetectedUncertain, true)]
    [InlineData(FieldOrigin.TemplateDefault, FieldOrigin.DetectedReliably, true)]
    [InlineData(FieldOrigin.TemplateDefault, FieldOrigin.Manual, true)]
    // Eine sichere Erkennung darf eine unsichere ersetzen, nicht umgekehrt.
    [InlineData(FieldOrigin.DetectedUncertain, FieldOrigin.DetectedReliably, true)]
    [InlineData(FieldOrigin.DetectedUncertain, FieldOrigin.TemplateDefault, false)]
    [InlineData(FieldOrigin.DetectedReliably, FieldOrigin.DetectedUncertain, false)]
    [InlineData(FieldOrigin.DetectedReliably, FieldOrigin.TemplateDefault, false)]
    // Eine Benutzereingabe bleibt unangetastet.
    [InlineData(FieldOrigin.Manual, FieldOrigin.TemplateDefault, false)]
    [InlineData(FieldOrigin.Manual, FieldOrigin.Template, false)]
    [InlineData(FieldOrigin.Manual, FieldOrigin.DetectedReliably, false)]
    [InlineData(FieldOrigin.Manual, FieldOrigin.DetectedUncertain, false)]
    public void DieVorrangregelGiltFürJedeKombination(
        FieldOrigin current, FieldOrigin proposed, bool expected)
        => Assert.Equal(expected, FieldOriginRules.CanReplace(current, proposed));

    /// <summary>
    /// Der Fall, der früher falsch war: Ein nie angefasstes Feld galt als
    /// Benutzereingabe und war damit unüberschreibbar.
    /// </summary>
    [Fact]
    public void EinNieAngefasstesFeldGiltAlsProgrammstandard()
        => Assert.Equal(FieldOrigin.Default, new InvoiceDraft().OriginOf(nameof(InvoiceDraft.Currency)));

    [Fact]
    public void EinVomAnwenderGeändertesFeldGiltAlsManuell()
    {
        var draft = new InvoiceDraft();

        draft.Currency = "CHF";

        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.Currency)));
    }
}

/// <summary>
/// Prüft die Vorrangregel am echten Formular, Feld für Feld.
///
/// Diese Tests sind der Grund für die zentrale Regel: Früher stand die
/// Überschreibbedingung als Sonderfall an einem einzigen Feld – mit dem
/// Ergebnis, dass sie für alle anderen gar nicht galt.
/// </summary>
public sealed class DraftOverwriteRulesTests
{
    public static TheoryData<string> ProtectedFields() =>
    [
        nameof(InvoiceDraft.InvoiceNumber),
        nameof(InvoiceDraft.Currency),
        nameof(InvoiceDraft.SellerName),
        nameof(InvoiceDraft.SellerCountry),
        nameof(InvoiceDraft.BuyerName),
        nameof(InvoiceDraft.BuyerCountry),
        nameof(InvoiceDraft.BankIban),
    ];

    /// <summary>Was der Anwender getippt hat, bleibt stehen – bei jedem Feld.</summary>
    [Theory]
    [MemberData(nameof(ProtectedFields))]
    public void EineBenutzereingabeWirdVonKeinerErkennungÜberschrieben(string field)
    {
        var draft = new InvoiceDraft();
        SetText(draft, field, "Von Hand");

        PrefillSummary summary = DraftPrefiller.Apply(draft, FullDetection(DetectionConfidence.High));

        Assert.Equal("Von Hand", TextOf(draft, field));
        Assert.NotEmpty(summary.SkippedProtected);
    }

    /// <summary>Ein Programmstandard darf einer Erkennung weichen.</summary>
    [Fact]
    public void ProgrammstandardWirdVonSichererErkennungErsetzt()
    {
        var draft = new InvoiceDraft();

        Assert.Equal("EUR", draft.Currency);

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Currency = new DetectedValue<string>("CHF", DetectionConfidence.High),
        });

        Assert.Equal("CHF", draft.Currency);
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.Currency)));
    }

    /// <summary>
    /// Auch das Rechnungsdatum ist ein Programmstandard (heute) und darf
    /// deshalb ersetzt werden – früher blieb es stehen.
    /// </summary>
    [Fact]
    public void DasVorbelegteRechnungsdatumWirdVonDerPdfErsetzt()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            IssueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 9), DetectionConfidence.High),
        });

        Assert.Equal(new DateOnly(2026, 8, 9), draft.IssueDate);
    }

    /// <summary>Ein Wert aus der Firmenvorlage weicht der PDF-Erkennung nicht.</summary>
    [Fact]
    public void VorlagenwertWirdNichtVonDerErkennungÜberschrieben()
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

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Seller = new DetectedParty
            {
                Name = new DetectedValue<string>("Fremde AG", DetectionConfidence.High),
            },
        });

        Assert.Equal("Muster IT GmbH", draft.SellerName);
    }

    /// <summary>Unsichere Werte füllen kein Feld – unabhängig davon, welches.</summary>
    [Theory]
    [MemberData(nameof(ProtectedFields))]
    public void UnsichereWerteFüllenNieEinFeld(string field)
    {
        var draft = new InvoiceDraft();
        string before = TextOf(draft, field);

        DraftPrefiller.Apply(draft, FullDetection(DetectionConfidence.Low));

        Assert.Equal(before, TextOf(draft, field));
    }

    private static InvoiceDetectionResult FullDetection(DetectionConfidence confidence) => new()
    {
        HasUsableText = true,
        InvoiceNumber = new DetectedValue<string>("RE-AUS-PDF", confidence),
        Currency = new DetectedValue<string>("CHF", confidence),
        IssueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 9), confidence),
        Seller = new DetectedParty
        {
            Name = new DetectedValue<string>("Verkäufer aus PDF", confidence),
            Country = new DetectedValue<string>("AT", confidence),
        },
        Buyer = new DetectedParty
        {
            Name = new DetectedValue<string>("Käufer aus PDF", confidence),
            Country = new DetectedValue<string>("NL", confidence),
        },
        Iban = new DetectedValue<string>("DE89370400440532013000", confidence),
    };

    private static void SetText(InvoiceDraft draft, string field, string value)
        => typeof(InvoiceDraft).GetProperty(field)!.SetValue(draft, value);

    private static string TextOf(InvoiceDraft draft, string field)
        => typeof(InvoiceDraft).GetProperty(field)!.GetValue(draft)?.ToString() ?? string.Empty;
}

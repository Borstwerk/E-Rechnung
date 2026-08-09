using System.Text.RegularExpressions;
using System.Xml.Linq;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Sichert die Datumsfelder der Oberfläche ab.
///
/// **Warum es diese Tests gibt:** Im manuellen Test blieben Leistungs- und
/// Fälligkeitsdatum in Schritt 2 leer, obwohl beide in Schritt 1 erkannt
/// worden waren. Der Entwurf trug damals drei Brückeneigenschaften
/// (<c>DueDateAsDateTime</c> und Geschwister), an die der DatePicker gebunden
/// war. Eine Änderung an <c>DueDate</c> meldete aber nur <c>DueDate</c> –
/// die Bindung erfuhr davon nie.
///
/// Die Ursache ist beseitigt, indem die Brücken entfallen sind: Der
/// DatePicker bindet unmittelbar an das Datum und rechnet über einen
/// Konverter um. Diese Tests halten beides fest – dass die Meldung ankommt
/// und dass die Bindung keinen Umweg mehr nimmt.
/// </summary>
public sealed class DateBindingTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Theory]
    [InlineData(nameof(InvoiceDraft.IssueDate))]
    [InlineData(nameof(InvoiceDraft.DueDate))]
    [InlineData(nameof(InvoiceDraft.DeliveryDate))]
    public void EineDatumsänderungWirdGemeldet(string property)
    {
        var draft = new InvoiceDraft();
        var gemeldet = new List<string>();

        draft.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName ?? string.Empty);

        typeof(InvoiceDraft).GetProperty(property)!
            .SetValue(draft, new DateOnly(2026, 8, 23));

        Assert.Contains(property, gemeldet);
    }

    /// <summary>
    /// Der eigentliche Fall aus dem Fehlerbericht: Was Schritt 1 erkennt, muss
    /// in Schritt 2 im Feld stehen.
    /// </summary>
    [Fact]
    public void ErkannteDatumsangabenStehenAnschliessendImEntwurf()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, ErkannteDaten());

        Assert.Equal(ErkanntesRechnungsdatum, draft.IssueDate);
        Assert.Equal(ErkanntesLeistungsdatum, draft.DeliveryDate);
        Assert.Equal(ErkanntesFälligkeitsdatum, draft.DueDate);
    }

    /// <summary>Die Vorbefüllung muss die Oberfläche auch benachrichtigen.</summary>
    [Fact]
    public void DieVorbefüllungMeldetJedesGeänderteDatum()
    {
        var draft = new InvoiceDraft();
        var gemeldet = new List<string>();

        draft.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName ?? string.Empty);

        DraftPrefiller.Apply(draft, ErkannteDaten());

        Assert.Contains(nameof(draft.IssueDate), gemeldet);
        Assert.Contains(nameof(draft.DeliveryDate), gemeldet);
        Assert.Contains(nameof(draft.DueDate), gemeldet);
    }

    /// <summary>
    /// Das Rechnungsdatum ist bewusst **kein** fester Wert im Quelltext.
    ///
    /// Der Entwurf trägt als Programmstandard das heutige Datum. Ein fest
    /// eingetragenes Datum kann diesem Standard zufällig entsprechen – bei der
    /// Testrechnung vom 09.08.2026 ist genau das an einem einzigen Tag im Jahr
    /// der Fall. An diesem Tag sähe der Test auch dann grün aus, wenn gar
    /// nichts übernommen würde. Ein Datum in der Vergangenheit schliesst diesen
    /// Zufall an jedem Tag aus.
    /// </summary>
    private static DateOnly ErkanntesRechnungsdatum => DateOnly.FromDateTime(DateTime.Today).AddDays(-30);

    private static DateOnly ErkanntesLeistungsdatum => ErkanntesRechnungsdatum.AddDays(-1);

    private static DateOnly ErkanntesFälligkeitsdatum => ErkanntesRechnungsdatum.AddDays(14);

    private static InvoiceDetectionResult ErkannteDaten() => new()
    {
        HasUsableText = true,
        IssueDate = new DetectedValue<DateOnly>(ErkanntesRechnungsdatum, DetectionConfidence.High),
        DeliveryDate = new DetectedValue<DateOnly>(ErkanntesLeistungsdatum, DetectionConfidence.High),
        DueDate = new DetectedValue<DateOnly>(ErkanntesFälligkeitsdatum, DetectionConfidence.High),
    };

    /// <summary>
    /// Der Wächter gegen einen Rückfall: Ein DatePicker darf nur an eine
    /// Eigenschaft binden, die auch eine Änderungsmeldung auslöst – also an ein
    /// <c>[ObservableProperty]</c> des Entwurfs. Genau das war beim
    /// ursprünglichen Fehler nicht der Fall.
    /// </summary>
    [Fact]
    public void JederDatePickerBindetAnEineMeldendeEigenschaft()
    {
        string[] beobachtbar = [.. Regex
            .Matches(
                File.ReadAllText(Path.Combine(
                    TestPaths.RepositoryRoot, "src", "EInvoiceSender.Core", "Models", "InvoiceDraft.cs")),
                @"^\s+private [\w<>?\.]+ _(\w+)\s*(=|;)", RegexOptions.Multiline)
            .Select(m => char.ToUpperInvariant(m.Groups[1].Value[0]) + m.Groups[1].Value[1..])];

        string[] gebunden = [.. XDocument
            .Load(Path.Combine(
                TestPaths.RepositoryRoot, "src", "EInvoiceSender.App",
                "Views", "Steps", "InvoiceDataView.xaml"))
            .Descendants(Presentation + "DatePicker")
            .Select(d => d.Attribute("SelectedDate")?.Value ?? string.Empty)
            .Select(v => Regex.Match(v, @"\{Binding\s+Draft\.(?<pfad>\w+)").Groups["pfad"].Value)
            .Where(p => p.Length > 0)];

        Assert.NotEmpty(gebunden);

        string[] stumm = [.. gebunden.Where(p => !beobachtbar.Contains(p, StringComparer.Ordinal))];

        Assert.True(
            stumm.Length == 0,
            $"Diese DatePicker binden an {string.Join(", ", stumm)}. Solche Eigenschaften melden "
            + "keine Änderung, das Bedienelement bleibt deshalb leer, auch wenn der Wert erkannt "
            + "wurde. Binden Sie unmittelbar an das Datum und rechnen Sie über einen Konverter um.");
    }
}

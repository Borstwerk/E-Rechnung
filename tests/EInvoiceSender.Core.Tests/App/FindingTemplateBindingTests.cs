using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft die Anzeigevorlage für Befunde gegen das ViewModel, das sie anzeigt.
///
/// **Warum dieser Test existiert:** Zwei Fehler dieser Art sind im laufenden
/// Programm aufgetreten, und beide meldet WPF nicht:
///
/// 1. Fehlt die Vorlage ganz, zeigt WPF das Ergebnis von <c>ToString()</c>, also
///    den Klassennamen. Genau das stand in der Befundliste im Fenster.
/// 2. Ist ein Bindungspfad falsch geschrieben, bleibt das Feld leer. WPF
///    schreibt eine Meldung ins Ausgabefenster des Debuggers und macht sonst
///    weiter. Ohne angehängten Debugger bemerkt das niemand.
///
/// Der Test liest XAML und ViewModel als Text und braucht deshalb weder WPF
/// noch einen Windows-Rechner.
/// </summary>
public sealed class FindingTemplateBindingTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>Erfasst <c>{Binding Pfad}</c> und <c>{Binding Path=Pfad}</c>.</summary>
    private static readonly Regex BindingPattern = new(
        @"\{Binding\s+(?:Path\s*=\s*)?(?<pfad>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Erfasst die öffentlichen Eigenschaften des ViewModels im Quelltext.</summary>
    private static readonly Regex PropertyPattern = new(
        @"public\s+[\w<>?\.\[\]]+\s+(?<name>\w+)\s*(?:=>|\{\s*get)",
        RegexOptions.Compiled);

    [Fact]
    public void FürBefundeGibtEsEineAnzeigevorlage()
        => Assert.True(
            FindingTemplate() is not null,
            "In App.xaml fehlt eine DataTemplate für FindingViewModel. Ohne sie zeigt WPF in "
            + "jeder Befundliste den Klassennamen statt der Meldung.");

    [Fact]
    public void AlleGebundenenPfadeDerVorlageExistierenAmViewModel()
    {
        string vorlage = Template();

        string[] eigenschaften = [.. PropertyPattern
            .Matches(FindingViewModelSource())
            .Select(m => m.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)];

        // Zwei Schreibweisen kommen vor: die Kurzform {Binding Pfad} in einem
        // Attribut und das ausgeschriebene <Binding Path="Pfad" /> in einem
        // MultiBinding. Beide müssen geprüft werden.
        string[] gebunden = [.. BindingPattern
            .Matches(vorlage)
            .Select(m => m.Groups["pfad"].Value)
            .Concat(XElement.Parse(vorlage)
                .Descendants(Presentation + "Binding")
                .Select(b => b.Attribute("Path")?.Value)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!))
            .Distinct(StringComparer.Ordinal)];

        Assert.True(gebunden.Length > 0, "Die Vorlage bindet überhaupt nichts – das kann nicht stimmen.");

        string[] unbekannt = [.. gebunden.Where(p => !eigenschaften.Contains(p, StringComparer.Ordinal))];

        Assert.True(
            unbekannt.Length == 0,
            $"Die Vorlage bindet an {string.Join(", ", unbekannt)}. Diese Eigenschaft(en) gibt es "
            + $"an FindingViewModel nicht (vorhanden: {string.Join(", ", eigenschaften)}). WPF "
            + "meldet so einen Fehler nur im Ausgabefenster des Debuggers; im Fenster bleibt das "
            + "Feld stillschweigend leer.");
    }

    /// <summary>
    /// Der Schweregrad muss als Wort **und** als Zeichen erscheinen. Farbe
    /// allein genügt nicht – für farbfehlsichtige Anwender wäre die Liste
    /// sonst nicht lesbar.
    /// </summary>
    [Fact]
    public void DerSchweregradStehtAlsWortUndAlsZeichenInDerVorlage()
    {
        string vorlage = Template();

        Assert.Contains("SeverityLabel", vorlage, StringComparison.Ordinal);
        Assert.Contains("SeverityGlyph", vorlage, StringComparison.Ordinal);
    }

    private static string Template()
        => (FindingTemplate() ?? throw new InvalidOperationException(
            "In App.xaml fehlt die DataTemplate für FindingViewModel.")).ToString();

    private static XElement? FindingTemplate()
        => XDocument
            .Load(Path.Combine(TestPaths.RepositoryRoot, "src", "EInvoiceSender.App", "App.xaml"))
            .Descendants(Presentation + "DataTemplate")
            .FirstOrDefault(t =>
                t.Attribute("DataType")?.Value.Contains("FindingViewModel", StringComparison.Ordinal) == true);

    private static string FindingViewModelSource()
    {
        string path = Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.App", "ViewModels", "DisplayViewModels.cs");

        string quelle = File.ReadAllText(path);
        int start = quelle.IndexOf("class FindingViewModel", StringComparison.Ordinal);

        Assert.True(start >= 0, $"FindingViewModel nicht gefunden in {path}.");

        int end = quelle.IndexOf("class StepProgressViewModel", StringComparison.Ordinal);

        return end > start ? quelle[start..end] : quelle[start..];
    }
}

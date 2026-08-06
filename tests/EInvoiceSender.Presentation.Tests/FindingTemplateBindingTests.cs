using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EInvoiceSender.Presentation.ViewModels;
using Xunit;

namespace EInvoiceSender.Presentation.Tests;

/// <summary>
/// Prueft die Anzeigevorlage fuer Befunde gegen das ViewModel, das sie anzeigt.
///
/// **Warum dieser Test existiert:** Zwei Fehler dieser Art sind beim ersten
/// Durchlauf sichtbar geworden, und beide meldet WPF nicht:
///
/// 1. Fehlt die Vorlage ganz, zeigt WPF den Klassennamen
///    ("EInvoiceSender.Presentation.ViewModels.FindingViewModel") – genau so
///    stand es im Fenster.
/// 2. Ist ein Bindungspfad falsch geschrieben, bleibt das Feld einfach leer.
///    WPF schreibt eine Meldung ins Ausgabefenster des Debuggers und macht
///    ansonsten weiter. Ohne angehaengten Debugger sieht das niemand.
///
/// Der Test liest deshalb die tatsaechliche XAML-Datei und prueft, dass es die
/// Vorlage gibt und dass jeder darin gebundene Pfad auf
/// <see cref="FindingViewModel"/> auch wirklich existiert. Er laeuft auf jedem
/// Agenten, weil er die Datei als Text liest und kein WPF benoetigt.
/// </summary>
public sealed class FindingTemplateBindingTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>
    /// Erfasst <c>{Binding Pfad}</c> und <c>{Binding Path=Pfad}</c>. Weitere
    /// Bestandteile wie <c>StringFormat</c> werden am Komma abgeschnitten.
    /// </summary>
    private static readonly Regex BindingPattern = new(
        @"\{Binding\s+(?:Path\s*=\s*)?(?<pfad>[A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void FuerBefundeGibtEsEineAnzeigevorlage()
    {
        XElement? template = FindingTemplate();

        Assert.True(
            template is not null,
            "In App.xaml fehlt eine DataTemplate fuer FindingViewModel. Ohne sie zeigt WPF "
            + "in jeder Befundliste den Klassennamen statt der Meldung.");
    }

    [Fact]
    public void AlleGebundenenPfadeDerVorlageExistierenAmViewModel()
    {
        XElement template = FindingTemplate()
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.");

        string[] eigenschaften = [.. typeof(FindingViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)];

        // Zwei Schreibweisen kommen vor: die Kurzform {Binding Pfad} in einem
        // Attribut und das ausgeschriebene <Binding Path="Pfad" /> innerhalb
        // eines MultiBinding. Beide muessen geprueft werden.
        string[] gebunden = [.. BindingPattern
            .Matches(template.ToString())
            .Select(m => m.Groups["pfad"].Value)
            .Concat(template
                .Descendants(Presentation + "Binding")
                .Select(b => b.Attribute("Path")?.Value)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!))
            .Distinct(StringComparer.Ordinal)];

        Assert.True(gebunden.Length > 0, "Die Vorlage bindet ueberhaupt nichts – das kann nicht stimmen.");

        string[] unbekannt = [.. gebunden.Where(p => !eigenschaften.Contains(p, StringComparer.Ordinal))];

        Assert.True(
            unbekannt.Length == 0,
            $"Die Vorlage bindet an {string.Join(", ", unbekannt)}. Diese Eigenschaft(en) gibt "
            + $"es an FindingViewModel nicht (vorhanden: {string.Join(", ", eigenschaften)}). "
            + "WPF meldet so einen Fehler nur im Debugger-Ausgabefenster; im Fenster bleibt "
            + "das Feld stillschweigend leer.");
    }

    /// <summary>
    /// Die Vorlage muss den Schweregrad als Wort **und** als Zeichen zeigen.
    /// Farbe allein genuegt nicht – das ist eine Zusage aus
    /// <c>docs/SPECIFICATION.md</c> zur Barrierefreiheit.
    /// </summary>
    [Fact]
    public void DerSchweregradStehtAlsWortUndAlsZeichenInDerVorlage()
    {
        string vorlage = (FindingTemplate()
            ?? throw new InvalidOperationException("Vorlage nicht gefunden.")).ToString();

        Assert.Contains("SeverityLabel", vorlage, StringComparison.Ordinal);
        Assert.Contains("SeverityGlyph", vorlage, StringComparison.Ordinal);
    }

    private static XElement? FindingTemplate()
    {
        XDocument app = XDocument.Load(Path.Combine(RepositoryRoot, "src", "EInvoiceSender.Desktop", "App.xaml"));

        return app.Descendants(Presentation + "DataTemplate")
            .FirstOrDefault(t =>
                t.Attribute("DataType")?.Value.Contains("FindingViewModel", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Sucht aufwaerts nach der Solutiondatei, damit der Pfad unabhaengig von
    /// der Build-Konfiguration stimmt.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException(
                       "Repository-Wurzel nicht gefunden – EInvoiceSender.slnx fehlt oberhalb von "
                       + AppContext.BaseDirectory);
        }
    }
}

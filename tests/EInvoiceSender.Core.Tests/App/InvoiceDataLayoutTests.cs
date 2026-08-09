using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EInvoiceSender.Core.Models;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft den Aufbau des Eingabeformulars aus Schritt 2.
///
/// **Warum es diese Tests gibt:** Im manuellen Test stand der Herkunftshinweis
/// in einer eigenen Rasterzeile unter mehreren Feldern nebeneinander. Welches
/// „✓ aus PDF erkannt“ zu welchem Feld gehörte, war nicht zu erkennen. Beim
/// Umbau zu Feldeinheiten kann so etwas wieder entstehen – WPF meldet weder
/// einen verrutschten Hinweis noch ein Bedienelement außerhalb seines Rasters.
///
/// Die Tests lesen die XAML als Text. Sie brauchen deshalb weder WPF noch einen
/// Windows-Rechner und laufen im selben Durchlauf wie alle übrigen Tests.
/// </summary>
public sealed class InvoiceDataLayoutTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>Alles, was ein Anwender ausfüllt.</summary>
    private static readonly XName[] InputElements =
    [
        Presentation + "TextBox",
        Presentation + "ComboBox",
        Presentation + "DatePicker",
    ];

    private const string HintStyle = "{StaticResource Herkunftshinweis}";

    [Fact]
    public void JederHerkunftshinweisNenntInTextUndSichtbarkeitDasselbeFeld()
    {
        string[] abweichend =
        [
            .. Hints()
                .Select(h => (Text: FieldOf(h, "Text"), Sichtbar: FieldOf(h, "Visibility")))
                .Where(p => !string.Equals(p.Text, p.Sichtbar, StringComparison.Ordinal))
                .Select(p => $"{p.Text}/{p.Sichtbar}"),
        ];

        Assert.True(
            abweichend.Length == 0,
            $"Diese Hinweise nennen zwei verschiedene Felder: {string.Join(", ", abweichend)}. Der "
            + "Hinweis zeigt dann den Text des einen Feldes und erscheint nach der Herkunft eines "
            + "anderen.");
    }

    [Fact]
    public void JederHerkunftshinweisNenntEinFeldDesEntwurfs()
    {
        string[] unbekannt =
        [
            .. Hints()
                .Select(h => FieldOf(h, "Text"))
                .Distinct(StringComparer.Ordinal)
                .Where(f => typeof(InvoiceDraft).GetProperty(f) is null),
        ];

        Assert.True(
            unbekannt.Length == 0,
            $"Diese Hinweise nennen Felder, die es am Entwurf nicht gibt: {string.Join(", ", unbekannt)}. "
            + "Der Hinweis bleibt dann dauerhaft unsichtbar, ohne dass es auffällt.");
    }

    /// <summary>
    /// Der Kern der Beanstandung: Ein Hinweis muss zu **einem** Feld gehören.
    ///
    /// Geprüft wird die räumliche Zuordnung, so wie WPF sie auswertet: Im
    /// Raster steht der Hinweis in der Spalte seines Feldes, unmittelbar eine
    /// Zeile darunter. Außerhalb eines Rasters steht er als Nachbar in
    /// derselben Feldeinheit.
    /// </summary>
    [Fact]
    public void JederHerkunftshinweisStehtBeiSeinemEingabefeld()
    {
        string[] verwaist =
        [
            .. Hints().Where(h => !StandsWithItsField(h)).Select(h => FieldOf(h, "Text")),
        ];

        Assert.True(
            verwaist.Length == 0,
            $"Zu diesen Hinweisen steht kein Eingabefeld: {string.Join(", ", verwaist)}. Ein Hinweis "
            + "ohne erkennbares Feld ist schlimmer als keiner – der Anwender bezieht ihn auf das "
            + "falsche Feld.");
    }

    /// <summary>
    /// Jedes Feld, das die Vorbefüllung setzen kann, muss seine Herkunft auch
    /// zeigen. Sonst erschiene ein erkannter Wert als eigene Eingabe.
    /// </summary>
    [Fact]
    public void JedesVorbefüllbareFeldZeigtSeineHerkunft()
    {
        string[] gezeigt = [.. Hints().Select(h => FieldOf(h, "Text")).Distinct(StringComparer.Ordinal)];

        string[] ohneAnzeige = [.. PrefillableFields().Where(f => !gezeigt.Contains(f, StringComparer.Ordinal))];

        Assert.True(
            ohneAnzeige.Length == 0,
            $"Die Vorbefüllung setzt {string.Join(", ", ohneAnzeige)}, das Formular zeigt dazu aber "
            + "keine Herkunft. Ein aus der PDF gelesener Wert sähe dann wie eine eigene Eingabe aus.");
    }

    /// <summary>
    /// Ein Bedienelement in einer Zeile, die es gar nicht gibt, landet bei WPF
    /// stillschweigend in der letzten vorhandenen – mehrere Felder liegen dann
    /// übereinander. Genau das passiert beim Einfügen eines Feldes leicht.
    /// </summary>
    [Fact]
    public void KeinBedienelementLiegtAußerhalbSeinesRasters()
    {
        var zuGroß = new List<string>();

        foreach (XElement grid in View().Descendants(Presentation + "Grid"))
        {
            int rows = Count(grid, "Grid.RowDefinitions");
            int columns = Count(grid, "Grid.ColumnDefinitions");

            Assert.True(
                rows > 0 || columns > 0,
                "Dieses Raster hat weder Zeilen- noch Spaltendefinitionen. Dann prüft der Test "
                + "nichts – vermutlich heißen die Elemente anders als erwartet.");

            foreach (XElement child in grid.Elements().Where(e => !e.Name.LocalName.Contains('.')))
            {
                zuGroß.AddRange(TooFarOut(child, "Grid.Row", "Grid.RowSpan", rows, "Zeile"));
                zuGroß.AddRange(TooFarOut(child, "Grid.Column", "Grid.ColumnSpan", columns, "Spalte"));
            }
        }

        Assert.True(
            zuGroß.Count == 0,
            "Diese Bedienelemente liegen außerhalb ihres Rasters: "
            + $"{string.Join("; ", zuGroß)}. WPF schiebt sie stillschweigend in die letzte "
            + "vorhandene Zeile beziehungsweise Spalte, wo sie andere Felder überdecken.");
    }

    /// <summary>
    /// Meldet, wenn ein Element über die letzte vorhandene Zeile oder Spalte
    /// hinausragt. Ohne Rasterangaben gilt nichts als zu weit außen.
    /// </summary>
    private static IEnumerable<string> TooFarOut(
        XElement child, string index, string span, int defined, string what)
    {
        if (defined == 0)
        {
            yield break;
        }

        int start = Attached(child, index);
        int occupied = start + Attached(child, span, 1);

        if (occupied > defined)
        {
            yield return $"{child.Name.LocalName} in {what} {start} (definiert sind {defined})";
        }
    }

    /// <summary>
    /// Steht der Hinweis bei dem Feld, dessen Herkunft er nennt? Geprüft wird
    /// beides: die Lage **und** die gebundene Eigenschaft. Nur die Lage zu
    /// prüfen ließe einen Hinweis durchgehen, der neben dem falschen Feld
    /// steht.
    /// </summary>
    private static bool StandsWithItsField(XElement hint)
    {
        if (hint.Parent is not { } parent)
        {
            return false;
        }

        string field = FieldOf(hint, "Text");

        IEnumerable<XElement> nachbarn = parent.Elements()
            .Where(e => InputElements.Contains(e.Name) && BoundField(e) == field);

        if (hint.Attribute("Grid.Row") is null)
        {
            // Eine Feldeinheit ohne Raster: Beschriftung, Feld und Hinweis
            // stehen als Geschwister untereinander.
            return nachbarn.Any();
        }

        int row = Attached(hint, "Grid.Row");
        int column = Attached(hint, "Grid.Column");

        return nachbarn.Any(e =>
            Attached(e, "Grid.Row") == row - 1 && Attached(e, "Grid.Column") == column);
    }

    /// <summary>Die Entwurfseigenschaft, an die ein Bedienelement bindet.</summary>
    private static string BoundField(XElement input)
        => input.Attributes()
            .Select(a => Regex.Match(a.Value, @"\{Binding\s+Draft\.(\w+)"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .FirstOrDefault(string.Empty);

    private static int Count(XElement grid, string definitions)
        => grid.Element(Presentation + definitions)?.Elements().Count() ?? 0;

    private static int Attached(XElement element, string name, int fallback = 0)
        => element.Attribute(name) is { Value: { Length: > 0 } value }
           && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            ? number
            : fallback;

    private static string FieldOf(XElement hint, string attribute)
        => Regex.Match(hint.Attribute(attribute)?.Value ?? string.Empty, @"ConverterParameter=(\w+)")
            .Groups[1].Value;

    private static IEnumerable<XElement> Hints()
        => View().Descendants().Where(e => e.Attribute("Style")?.Value == HintStyle);

    /// <summary>
    /// Die Felder, die <c>DraftPrefiller</c> setzen kann – aus dessen Quelltext
    /// gelesen, damit ein neu vorbefülltes Feld hier von selbst auftaucht.
    /// </summary>
    private static string[] PrefillableFields()
    {
        string quelle = File.ReadAllText(Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.Core",
            "Pdf", "Detection", "DraftPrefiller.cs"));

        string[] felder = [.. Regex.Matches(quelle, @"nameof\(d\.(\w+)\)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)];

        Assert.NotEmpty(felder);

        return felder;
    }

    private static XDocument View()
        => XDocument.Load(Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.App",
            "Views", "Steps", "InvoiceDataView.xaml"));
}

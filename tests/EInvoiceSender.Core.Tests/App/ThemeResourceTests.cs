using System.Text.RegularExpressions;
using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft die Gestaltungsbausteine gegen ihre Verwendung.
///
/// **Warum es diese Tests braucht.** WPF löst einen StaticResource-Verweis
/// erst beim Laden auf. Ein Tippfehler im Schlüssel übersetzt sich
/// anstandslos und wirft die Anwendung beim Start mit einer
/// <c>ResourceReferenceKeyNotFoundException</c> aus dem Fenster – nicht an
/// der Stelle, an der die Ansicht gebraucht wird, sondern sofort. Weder der
/// Übersetzer noch ein Prüfer auf einem Rechner ohne Windows bemerkt das.
///
/// Diese Tests lesen deshalb XAML als Text und rechnen nach, was WPF sonst
/// erst zur Laufzeit prüft. Sie ersetzen keinen Blick auf das laufende
/// Fenster, aber sie fangen die Art von Fehler ab, die dort als Absturz
/// erscheint.
/// </summary>
public sealed class ThemeResourceTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Erfasst <c>{StaticResource Schlüssel}</c> in einem Attribut.</summary>
    private static readonly Regex StaticResourceReference = new(
        @"\{StaticResource\s+(?<key>[^}\s,]+)\s*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void JederVerwendeteGestaltungsschlüsselIstAuchDefiniert()
    {
        HashSet<string> defined = DefinedKeys();

        string[] unbekannt =
        [
            .. from path in XamlFiles()
               let text = File.ReadAllText(path)
               from match in StaticResourceReference.Matches(text).Cast<Match>()
               let key = match.Groups["key"].Value
               where !defined.Contains(key)
               select $"{ProjectFiles.Relative(path)}: {key}",
        ];

        Assert.True(
            unbekannt.Length == 0,
            "Diese Verweise zeigen auf einen Schlüssel, den es nirgends gibt. WPF wirft dafür "
            + "beim Start eine Ausnahme:\n" + string.Join("\n", unbekannt.Distinct()));
    }

    /// <summary>
    /// Ohne diese Prüfung wäre der Test oben wertlos: Findet die Suche keine
    /// Verweise, meldet sie zufrieden, dass alles stimmt.
    /// </summary>
    [Fact]
    public void DieSucheFindetÜberhauptVerweise()
    {
        int verweise = XamlFiles()
            .Sum(p => StaticResourceReference.Count(File.ReadAllText(p)));

        Assert.True(verweise > 50, $"Nur {verweise} Verweise gefunden – das kann nicht stimmen.");
    }

    /// <summary>
    /// In den Ansichten steht kein Farbwert mehr. Genau dafür gibt es die
    /// Bausteine; eine einzelne durchgerutschte Farbe fällt sonst erst auf,
    /// wenn jemand die Gestaltung ändert und diese eine Stelle stehen bleibt.
    /// </summary>
    [Fact]
    public void AußerhalbDerBausteineStehtKeinFarbwert()
    {
        var farbwert = new Regex(@"#[0-9A-Fa-f]{3,8}\b", RegexOptions.Compiled);

        string[] treffer =
        [
            .. from path in XamlFiles()
               where !IsThemeFile(path)
               from match in farbwert.Matches(File.ReadAllText(path)).Cast<Match>()
               select $"{ProjectFiles.Relative(path)}: {match.Value}",
        ];

        Assert.True(
            treffer.Length == 0,
            "Diese Stellen schreiben eine Farbe aus, statt einen Baustein zu verwenden:\n"
            + string.Join("\n", treffer));
    }

    /// <summary>
    /// Die Produktfarbe der E-Rechnung ist verbindlich vorgegeben. Sie steht
    /// genau einmal da – als Farbwert in Colors.xaml.
    /// </summary>
    [Fact]
    public void DieProduktfarbeIstDieVorgegebene()
    {
        XElement farbe = Assert.Single(
            Theme("Colors.xaml").Root!.Elements(Presentation + "Color"),
            c => c.Attribute(Xaml + "Key")?.Value == "BorstWerk.Color.ProductAccent");

        Assert.Equal("#176B87", farbe.Value.Trim(), ignoreCase: true);
    }

    /// <summary>
    /// Ein sichtbarer Tastaturfokus ist Pflicht. WPF zeichnet von sich aus
    /// einen dünnen gepunkteten Rahmen innerhalb des Elements; auf einer
    /// eingefärbten Schaltfläche ist er nicht zu sehen.
    /// </summary>
    [Fact]
    public void SchaltflächenHabenEinenEigenenSichtbarenFokus()
    {
        string buttons = File.ReadAllText(ThemePath("Controls.Buttons.xaml"));

        Assert.Contains("BorstWerk.FocusVisual", buttons, StringComparison.Ordinal);
        Assert.Contains("FocusVisualStyle", buttons, StringComparison.Ordinal);
    }

    /// <summary>
    /// Zugriffstasten wie Alt+W für „_Weiter“ funktionieren nur, wenn die
    /// Vorlage sie auswertet. Ohne <c>RecognizesAccessKey</c> erscheint statt
    /// der Taste ein sichtbarer Unterstrich – und die Tastaturbedienung wäre
    /// kaputt, ohne dass es jemand meldet.
    /// </summary>
    [Fact]
    public void DieSchaltflächenvorlageWertetZugriffstastenAus()
    {
        string buttons = File.ReadAllText(ThemePath("Controls.Buttons.xaml"));

        Assert.Contains("RecognizesAccessKey=\"True\"", buttons, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein Eingabefeld muss sich vom Hintergrund abheben. WCAG 2.1,
    /// Erfolgskriterium 1.4.11 verlangt für die Umrandung eines
    /// Bedienelements 3:1.
    /// </summary>
    [Theory]
    [InlineData("BorstWerk.Color.Border.Input", 3.0)]
    [InlineData("BorstWerk.Color.Text.Primary", 4.5)]
    [InlineData("BorstWerk.Color.Text.Secondary", 4.5)]
    [InlineData("BorstWerk.Color.Text.Muted", 4.5)]
    public void DieFarbenHaltenDenGefordertenKontrastGegenWeiß(string key, double required)
    {
        double contrast = Contrast(ColorValue(key), "#FFFFFF");

        Assert.True(
            contrast >= required,
            $"{key} erreicht gegen Weiß nur {contrast:F2}:1, gefordert sind {required}:1.");
    }

    /// <summary>Schrift auf der Produktfarbe muss lesbar bleiben.</summary>
    [Fact]
    public void SchriftAufDerProduktfarbeIstLesbar()
    {
        double contrast = Contrast(
            ColorValue("BorstWerk.Color.Text.OnAccent"),
            ColorValue("BorstWerk.Color.ProductAccent"));

        Assert.True(contrast >= 4.5, $"Nur {contrast:F2}:1 auf der Primärschaltfläche.");
    }

    /// <summary>
    /// Jede Statusfarbe muss auf ihrer eigenen hellen Fläche als Fließtext
    /// lesbar sein – sonst ist die Meldung zwar bunt, aber nicht zu lesen.
    /// </summary>
    [Theory]
    [InlineData("Success")]
    [InlineData("Info")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Neutral")]
    public void JedeStatusfarbeIstAufIhrerFlächeLesbar(string status)
    {
        double contrast = Contrast(
            ColorValue($"BorstWerk.Color.Status.{status}"),
            ColorValue($"BorstWerk.Color.Status.{status}.Surface"));

        Assert.True(contrast >= 4.5, $"Status {status} erreicht nur {contrast:F2}:1.");
    }

    /// <summary>
    /// Kontrastverhältnis nach WCAG 2.1. Die Formel steht in der Norm; sie
    /// hier auszurechnen ist ehrlicher, als eine Zahl in einen Kommentar zu
    /// schreiben und zu hoffen, dass sie stimmt.
    /// </summary>
    private static double Contrast(string first, string second)
    {
        double a = RelativeLuminance(first);
        double b = RelativeLuminance(second);

        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        string value = hex.TrimStart('#');

        double[] channel =
        [
            .. new[] { 0, 2, 4 }.Select(i =>
            {
                double raw = Convert.ToInt32(value.Substring(i, 2), 16) / 255.0;

                return raw <= 0.03928 ? raw / 12.92 : Math.Pow((raw + 0.055) / 1.055, 2.4);
            }),
        ];

        return (0.2126 * channel[0]) + (0.7152 * channel[1]) + (0.0722 * channel[2]);
    }

    private static string ColorValue(string key)
        => Theme("Colors.xaml").Root!
            .Elements(Presentation + "Color")
            .Single(c => c.Attribute(Xaml + "Key")?.Value == key)
            .Value.Trim();

    /// <summary>
    /// Alle Schlüssel, die irgendwo vergeben werden – aus den Bausteinen und
    /// aus App.xaml. Verweise auf Windows-eigene Schlüssel gibt es in diesem
    /// Programm nicht; käme einer hinzu, meldete der Test ihn, und dann ist
    /// eine Ausnahme hier die richtige Antwort.
    /// </summary>
    private static HashSet<string> DefinedKeys()
        => [.. XamlFiles()
            .SelectMany(p => XDocument.Load(p).Descendants())
            .Select(e => e.Attribute(Xaml + "Key")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => k!)];

    private static IEnumerable<string> XamlFiles()
        => Directory.EnumerateFiles(AppDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal);

    private static bool IsThemeFile(string path)
        => path.Contains(Path.Combine("UI", "Themes"), StringComparison.Ordinal);

    private static XDocument Theme(string file) => XDocument.Load(ThemePath(file));

    private static string ThemePath(string file)
        => Path.Combine(AppDirectory, "UI", "Themes", file);

    private static string AppDirectory
        => Path.Combine(TestPaths.RepositoryRoot, "src", "EInvoiceSender.App");
}

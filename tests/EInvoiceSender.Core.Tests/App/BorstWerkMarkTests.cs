using System.Text.RegularExpressions;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält die beiden Fassungen des BorstWerk-Zeichens zusammen.
///
/// **Das Zeichen steht zweimal im Repository**, und das lässt sich nicht
/// vermeiden:
///
/// * <c>build/icon/BorstWerkMark.cs</c> zeichnet damit die Windows-Symboldatei,
/// * <c>UI/Themes/BorstWerkLogo.xaml</c> zeichnet damit das Zeichen im Fenster.
///
/// Die Pfade selbst sind aus dem Markenblatt vermessen und nicht entworfen;
/// wie das geschah, steht in <c>build/icon/BorstWerkMark.cs</c>.
///
/// Zusammenlegen ginge nur, indem eine Seite die andere zur Bauzeit erzeugt.
/// Das Werkzeug gehört aber bewusst nicht zur Projektmappe – sein Ergebnis
/// ist eingecheckt, und die CI soll es nicht übersetzen müssen.
///
/// Bleibt die Gefahr, dass jemand eine Seite ändert und die andere vergisst.
/// Auffallen würde das spät und beiläufig: Das Fenster zeigte ein anderes
/// Zeichen als die Taskleiste. Dieser Test vergleicht die Pfaddaten und
/// meldet den Unterschied sofort.
/// </summary>
public sealed class BorstWerkMarkTests
{
    /// <summary>
    /// Erfasst eine Pfadangabe im Erzeuger. Die Pfade sind lang und über
    /// mehrere aneinandergehängte Zeichenketten verteilt; <c>Unquote</c>
    /// setzt sie wieder zusammen.
    /// </summary>
    private static readonly Regex GeneratorPath = new(
        @"public const string (?<name>\w+Path)\s*=\s*(?<value>""[^;]*?"");",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Erfasst eine Pfadangabe im XAML.</summary>
    private static readonly Regex XamlGeometry = new(
        @"Geometry=""(?<value>[^""]+)""",
        RegexOptions.Compiled);

    [Theory]
    [InlineData("BodyPath")]
    [InlineData("RingPath")]
    public void DasXamlZeichnetDenselbenPfadWieDerSymbolerzeuger(string name)
    {
        string expected = GeneratorPaths()[name];

        Assert.True(
            XamlPaths().Contains(expected),
            $"Der Pfad '{name}' aus build/icon/BorstWerkMark.cs kommt in "
            + "UI/Themes/BorstWerkLogo.xaml nicht vor. Eine der beiden Seiten wurde geändert, "
            + $"die andere nicht.\n\nErwartet:\n  {expected}\n\nIm XAML steht:\n  "
            + string.Join("\n  ", XamlPaths()));
    }

    /// <summary>
    /// Beide Fassungen des Zeichens – hell und dunkel – zeichnen dieselben
    /// zwei Flächen. Fehlte in einer eine, wäre das Zeichen auf dunklem
    /// Grund ein anderes als auf hellem.
    /// </summary>
    [Fact]
    public void BeideFassungenZeichnenZweiFlächen()
        => Assert.Equal(4, XamlPaths().Count);

    /// <summary>
    /// Ohne diese Prüfung wäre der Vergleich oben wertlos: Findet die Suche
    /// keine Pfade, meldet sie zufrieden, dass alles übereinstimmt.
    /// </summary>
    [Fact]
    public void DieSucheFindetInBeidenDateienPfade()
    {
        Assert.Equal(2, GeneratorPaths().Count);
        Assert.NotEmpty(XamlPaths());
    }

    /// <summary>Die Symboldatei ist vorhanden und trägt alle Größen.</summary>
    [Fact]
    public void DieSymboldateiEnthältAlleBenötigtenGrößen()
    {
        byte[] icon = File.ReadAllBytes(Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.App", "Assets", "BorstWerkEInvoice.ico"));

        Assert.True(icon.Length > 1000, "Die Symboldatei ist verdächtig klein.");

        // Kopf einer ICO-Datei: zwei Byte reserviert (0), zwei Byte Typ (1),
        // zwei Byte Anzahl der Bilder.
        Assert.Equal(0, BitConverter.ToUInt16(icon, 0));
        Assert.Equal(1, BitConverter.ToUInt16(icon, 2));

        int count = BitConverter.ToUInt16(icon, 4);

        // Die Kantenlänge steht in einem einzelnen Byte; 256 wird als 0
        // eingetragen, weil sie dort nicht hineinpasst.
        int[] sizes = [.. Enumerable.Range(0, count)
            .Select(i => icon[6 + (16 * i)] == 0 ? 256 : icon[6 + (16 * i)])];

        // 16 für die Taskleiste, 32 für den Explorer, 256 für die große
        // Kacheldarstellung. Ohne diese drei sucht sich Windows eine Größe
        // zum Hochrechnen, und das Ergebnis ist unscharf.
        Assert.Contains(16, sizes);
        Assert.Contains(32, sizes);
        Assert.Contains(256, sizes);
    }

    private static Dictionary<string, string> GeneratorPaths()
        => GeneratorPath
            .Matches(File.ReadAllText(Path.Combine(
                TestPaths.RepositoryRoot, "build", "icon", "BorstWerkMark.cs")))
            .ToDictionary(
                m => m.Groups["name"].Value,
                m => Unquote(m.Groups["value"].Value),
                StringComparer.Ordinal);

    /// <summary>
    /// Setzt eine womöglich über mehrere Zeilen verteilte Zeichenkette
    /// zusammen und nimmt die Anführungszeichen weg.
    /// </summary>
    private static string Unquote(string literal)
        => string.Concat(Regex.Matches(literal, @"""(?<text>[^""]*)""")
            .Select(m => m.Groups["text"].Value));

    /// <summary>
    /// Die Pfade aus dem XAML, ohne das vorangestellte <c>F0</c>. Das Kürzel
    /// ist die WPF-Schreibweise für die Füllregel EvenOdd; im Erzeuger wird
    /// sie getrennt vom Pfad gesetzt, weil Skia sie als eigene Eigenschaft
    /// führt. Der Vergleich gilt der Geometrie, nicht der Schreibweise.
    /// </summary>
    private static List<string> XamlPaths()
        => [.. XamlGeometry
            .Matches(File.ReadAllText(Path.Combine(
                TestPaths.RepositoryRoot, "src", "EInvoiceSender.App",
                "UI", "Themes", "BorstWerkLogo.xaml")))
            .Select(m => m.Groups["value"].Value)
            .Select(v => v.StartsWith("F0 ", StringComparison.Ordinal) ? v[3..] : v)];
}

using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft den Abschlusszustand des Ablaufs.
///
/// **Der Befund aus dem manuellen Testlauf:** In Schritt 5 war alles
/// erledigt – die Datei erzeugt, die Prüfungen bestanden, der E-Mail-Entwurf
/// bereit –, und trotzdem stand dort eine abgeblendete Schaltfläche
/// „Weiter“. Sie liest sich als „hier fehlt noch etwas“ oder „eine Prüfung
/// hält dich auf“. Beides war falsch.
///
/// Abgeblendet ist nicht dasselbe wie abwesend: Eine abgeblendete
/// Schaltfläche behauptet, es gäbe hier etwas zu tun, das gerade nicht geht.
/// Im letzten Schritt gibt es nichts mehr – also weg damit.
///
/// **Was bleiben muss:** „Zurück“, damit sich nach dem Erzeugen noch etwas
/// korrigieren lässt, und „Neue Rechnung“, das dort die naheliegende
/// nächste Handlung ist und deshalb die Primärform bekommt.
///
/// Geprüft wird am Quelltext, weil das Testprojekt die WPF-Anwendung nicht
/// referenzieren kann – sie ist auf Windows festgelegt.
/// </summary>
public sealed class WizardFinishTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>
    /// Das ViewModel beantwortet die Frage nach dem Ende – nicht die
    /// Ansicht. Stünde der Vergleich im XAML, wäre die Regel an der Stelle,
    /// an der sie am schlechtesten zu prüfen ist.
    /// </summary>
    [Theory]
    [InlineData("IsFinished")]
    [InlineData("ShowsForwardButton")]
    public void DasViewModelKenntDenAbschlusszustand(string eigenschaft)
        => Assert.Contains($"public bool {eigenschaft}", MainViewModel(), StringComparison.Ordinal);

    /// <summary>
    /// Beide Angaben hängen an <c>CurrentStep</c>. Fehlt die Benachrichtigung,
    /// bleibt die Schaltfläche im zuletzt bewerteten Zustand stehen – und
    /// genau dann ist der Fehler wieder da, nur schwerer zu finden.
    /// </summary>
    [Theory]
    [InlineData("IsFinished")]
    [InlineData("ShowsForwardButton")]
    public void DerSchrittwechselMeldetDenAbschlusszustand(string eigenschaft)
    {
        string quelle = MainViewModel();
        int schritt = quelle.IndexOf("private WizardStep _currentStep", StringComparison.Ordinal);

        Assert.True(schritt > 0, "Das Feld _currentStep wurde nicht gefunden.");

        // Die Kennzeichnungen stehen unmittelbar über dem Feld.
        string kennzeichnungen = quelle[..schritt];
        int beginn = kennzeichnungen.LastIndexOf("[ObservableProperty]", StringComparison.Ordinal);

        Assert.Contains(
            $"NotifyPropertyChangedFor(nameof({eigenschaft}))",
            kennzeichnungen[beginn..],
            StringComparison.Ordinal);
    }

    /// <summary>
    /// „Weiter“ wird im letzten Schritt ausgeblendet, nicht nur abgeblendet.
    /// </summary>
    [Fact]
    public void WeiterWirdImLetztenSchrittAusgeblendet()
    {
        XElement weiter = Assert.Single(
            Buttons(), b => (b.Attribute("Content")?.Value ?? string.Empty).Contains("Weiter",
                                                                                     StringComparison.Ordinal));

        Assert.Contains(
            "ShowsForwardButton",
            weiter.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// „Zurück“ bleibt in jedem Schritt sichtbar. Ob es benutzbar ist,
    /// entscheidet weiterhin die Freigabeprüfung des Befehls – im ersten
    /// Schritt gibt es nichts, wohin zurückzugehen wäre.
    /// </summary>
    [Fact]
    public void ZurückBleibtSichtbar()
    {
        XElement zurück = Assert.Single(
            Buttons(), b => (b.Attribute("Content")?.Value ?? string.Empty).Contains("Zurück",
                                                                                     StringComparison.Ordinal));

        Assert.Null(zurück.Attribute("Visibility"));
        Assert.Contains("GoBackCommand", zurück.Attribute("Command")?.Value ?? string.Empty,
                        StringComparison.Ordinal);
    }

    /// <summary>
    /// Im letzten Schritt ist „Neue Rechnung“ die Primäraktion; davor ist es
    /// eine Nebenhandlung, die den Stand verwirft. Deshalb zwei
    /// Schaltflächen, von denen immer genau eine sichtbar ist.
    /// </summary>
    [Fact]
    public void NeueRechnungIstImLetztenSchrittDiePrimäraktion()
    {
        XElement[] neueRechnung =
        [
            .. Buttons().Where(b => (b.Attribute("Content")?.Value ?? string.Empty)
                .Contains("Neue", StringComparison.Ordinal)),
        ];

        Assert.Equal(2, neueRechnung.Length);

        XElement abschluss = Assert.Single(
            neueRechnung,
            b => (b.Attribute("Visibility")?.Value ?? string.Empty)
                .Contains("IsFinished", StringComparison.Ordinal));

        Assert.Contains("BorstWerk.Button.Primary", abschluss.Attribute("Style")?.Value ?? string.Empty,
                        StringComparison.Ordinal);

        XElement währenddessen = Assert.Single(
            neueRechnung,
            b => (b.Attribute("Visibility")?.Value ?? string.Empty)
                .Contains("ShowsForwardButton", StringComparison.Ordinal));

        Assert.Null(währenddessen.Attribute("Style"));

        // Beide lösen denselben Befehl aus – es ist dieselbe Handlung.
        Assert.All(neueRechnung, b => Assert.Contains(
            "StartOverCommand", b.Attribute("Command")?.Value ?? string.Empty, StringComparison.Ordinal));
    }

    /// <summary>
    /// Jede Schaltfläche der Navigation behält ihre Zugriffstaste. Ein
    /// verschwundener Unterstrich fällt beim Sehen nicht auf, beim Bedienen
    /// mit der Tastatur sofort.
    /// </summary>
    [Fact]
    public void DieNavigationBehältIhreZugriffstasten()
        => Assert.All(
            Buttons(),
            b => Assert.Contains("_", b.Attribute("Content")?.Value ?? string.Empty,
                                 StringComparison.Ordinal));

    /// <summary>
    /// Der Abschluss steht als Text da, mit Zeichen und Wort – nicht nur als
    /// grüne Fläche. Und er trägt einen Namen für die Sprachausgabe.
    /// </summary>
    [Fact]
    public void DerAbschlussStehtAlsTextUndZeichenImErgebnis()
    {
        XDocument ergebnis = XDocument.Load(Path.Combine(
            AppDirectory, "Views", "Steps", "ResultView.xaml"));

        XElement abschluss = Assert.Single(
            ergebnis.Descendants(Presentation + "Border"),
            b => (b.Attribute("Style")?.Value ?? string.Empty)
                .Contains("BorstWerk.Status.Success", StringComparison.Ordinal));

        string[] texte =
        [
            .. abschluss.Descendants(Presentation + "TextBlock")
                .Select(t => t.Attribute("Text")?.Value ?? string.Empty),
        ];

        Assert.Contains(texte, t => t == "✓");
        Assert.Contains(texte, t => t.Contains("Fertig", StringComparison.Ordinal));
        Assert.Contains(texte, t => t.Contains("erzeugt und geprüft", StringComparison.Ordinal));

        // Angehängte Eigenschaften stehen im XAML als ein Attribut mit Punkt
        // im Namen; für XLinq heißt es schlicht "AutomationProperties.Name".
        string name = abschluss.Attributes()
            .FirstOrDefault(a => a.Name.LocalName == "AutomationProperties.Name")
            ?.Value ?? string.Empty;

        Assert.Contains("Fertig", name, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Freigabeprüfung von „Weiter“ bleibt unverändert: Sie entscheidet
    /// weiterhin je Schritt und nicht anhand der Sichtbarkeit.
    /// </summary>
    [Fact]
    public void DieFreigabeprüfungVonWeiterBleibtSchrittweise()
    {
        string quelle = MainViewModel();

        Assert.Contains("WizardStep.SelectPdf => PdfSelection.IsSuitable", quelle, StringComparison.Ordinal);
        Assert.Contains("WizardStep.Review => Review.ContentMatchConfirmed", quelle, StringComparison.Ordinal);
    }

    private static IEnumerable<XElement> Buttons()
        => XDocument
            .Load(Path.Combine(AppDirectory, "Views", "MainWindow.xaml"))
            .Descendants(Presentation + "Button")
            .Where(b => b.Attribute("Content") is not null)
            .Where(b => (b.Attribute("Click")?.Value ?? string.Empty) != "OnAboutClicked");

    private static string MainViewModel()
        => File.ReadAllText(Path.Combine(AppDirectory, "ViewModels", "MainViewModel.cs"));

    private static string AppDirectory
        => Path.Combine(TestPaths.RepositoryRoot, "src", "EInvoiceSender.App");
}

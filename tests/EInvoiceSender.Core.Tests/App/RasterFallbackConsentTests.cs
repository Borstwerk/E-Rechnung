using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Sichert die Zustimmung zur sichtbaren Kopie in der Oberfläche.
///
/// **Warum das eigens geprüft wird.** Die fachliche Sperre sitzt im Kern und
/// ist dort mit Ende-zu-Ende-Prüfungen belegt. Was der Kern aber nicht
/// verhindern kann, ist eine Oberfläche, die die Frage gar nicht erst stellt
/// oder sie so stellt, dass man versehentlich zustimmt. Genau das prüfen die
/// Fälle hier: dass gefragt wird, dass die Antwort eine Handlung ist und kein
/// vorbelegtes Kästchen, und dass eine neue Datei die Frage neu stellt.
///
/// Geprüft wird am Quelltext, weil das Prüfprojekt die WPF-Anwendung nicht
/// referenzieren kann – sie ist auf Windows festgelegt.
/// </summary>
public sealed class RasterFallbackConsentTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    // ------------------------------------------------------------- ViewModel

    /// <summary>
    /// „Weiter“ bleibt gesperrt, solange die Zustimmung fehlt. Ohne diese
    /// Bedingung liefe der Benutzer in die Sperre des Kerns – und bekäme dort
    /// eine Fehlermeldung statt einer Frage.
    /// </summary>
    [Fact]
    public void WeiterVerlangtDieZustimmungZurSichtbarenKopie()
        => Assert.Contains(
            "Report?.CanProceed == true && (!RequiresRasterFallback || RasterFallbackConfirmed)",
            PdfSelection(),
            StringComparison.Ordinal);

    /// <summary>
    /// Die Zustimmung beginnt bei „nein“ – als Feld ohne Vorbelegung. Ein
    /// <c>= true</c> stünde hier für eine Zustimmung, die niemand gegeben hat.
    /// </summary>
    [Fact]
    public void DieZustimmungIstNichtVorbelegt()
    {
        string quelle = PdfSelection();

        Assert.Contains("private bool _rasterFallbackConfirmed;", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("_rasterFallbackConfirmed = true;", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Jede neu geprüfte Datei stellt die Frage neu, und das Zurücksetzen des
    /// Ablaufs ebenfalls. Eine stehen gebliebene Zustimmung wäre eine
    /// Zustimmung zur falschen Datei.
    /// </summary>
    [Fact]
    public void JedeNeueDateiStelltDieFrageNeu()
    {
        string quelle = PdfSelection();

        // Einmal in InspectAsync, einmal in Reset.
        Assert.Equal(
            2,
            quelle.Split("RasterFallbackConfirmed = false;", StringSplitOptions.None).Length - 1);

        int inspect = quelle.IndexOf("public async Task InspectAsync", StringComparison.Ordinal);
        int reset = quelle.IndexOf("public void Reset()", StringComparison.Ordinal);

        Assert.True(inspect > 0 && reset > inspect, "Die erwarteten Methoden fehlen.");

        Assert.Contains(
            "RasterFallbackConfirmed = false;",
            quelle[inspect..reset],
            StringComparison.Ordinal);

        Assert.Contains(
            "RasterFallbackConfirmed = false;",
            quelle[reset..],
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Zustimmung erreicht den Kern. Bliebe sie in der Oberfläche stehen,
    /// bräche der Vorgang in Schritt 4 ab – nach der vollständigen Erfassung
    /// aller Rechnungsdaten.
    /// </summary>
    [Fact]
    public void DieZustimmungWirdAnDenKernWeitergereicht()
        => Assert.Contains(
            "RasterFallbackConfirmed: PdfSelection.RasterFallbackConfirmed",
            MainViewModel(),
            StringComparison.Ordinal);

    /// <summary>
    /// Die Zustimmung gilt dem einzelnen Vorgang und wird nirgends aufbewahrt.
    /// Stünde sie in den Einstellungen, wäre aus einer Entscheidung eine
    /// Voreinstellung geworden.
    /// </summary>
    [Fact]
    public void DieZustimmungWirdNichtGespeichert()
    {
        string einstellungen = File.ReadAllText(Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.Core", "Services", "SettingsPorts.cs"));

        Assert.DoesNotContain("Raster", einstellungen, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Die Datenerkennung läuft weiterhin auf dem Original – auch dann, wenn
    /// nur der Rasterweg bleibt.
    ///
    /// Der Text steckt im Original; dass seine Schriften nicht eingebettet
    /// sind, ändert daran nichts. Aus der gerasterten Fassung wird nie gelesen:
    /// Dort gibt es keinen Text mehr, nur ein Bild davon.
    /// </summary>
    [Fact]
    public void DieDatenerkennungLiestDasOriginal()
    {
        string quelle = PdfSelection();

        // Die Bedingung ist CanProceed – und das schließt den Rasterweg ein.
        Assert.Contains("if (Report.CanProceed)", quelle, StringComparison.Ordinal);

        // Erkannt wird der Pfad, der geprüft wurde, nicht ein Ergebnis.
        Assert.Contains("await DetectAsync(path, cancellationToken)", quelle, StringComparison.Ordinal);
        Assert.Contains(
            "_detector.DetectAsync(path, template, cancellationToken)",
            quelle,
            StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------- Ansicht

    /// <summary>
    /// Das Angebot erscheint, solange die Frage offen ist, und verschwindet
    /// danach. Es ist an <c>ShowsRasterFallbackOffer</c> gebunden und nicht an
    /// den Befund allein.
    /// </summary>
    [Fact]
    public void DasAngebotHängtAmOffenenZustand()
    {
        XElement angebot = OfferBorder();

        Assert.Contains(
            "ShowsRasterFallbackOffer",
            angebot.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Kein roter Fehlerzustand, solange ein erprobter Weg offensteht. Ein
    /// Fehler wäre hier die falsche Auskunft: Es geht ja etwas, es muss nur
    /// jemand entscheiden.
    /// </summary>
    [Fact]
    public void DasAngebotIstKeinFehlerzustand()
    {
        string stil = OfferBorder().Attribute("Style")?.Value ?? string.Empty;

        Assert.Contains("BorstWerk.Status.Warning", stil, StringComparison.Ordinal);
        Assert.DoesNotContain("Status.Error", stil, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Zustimmung ist ein eigener Klick. Ein Kästchen – erst recht ein
    /// vorbelegtes – wäre etwas, das man übersieht.
    /// </summary>
    [Fact]
    public void DieZustimmungIstEineHandlungUndKeinKästchen()
    {
        XElement angebot = OfferBorder();

        Assert.Empty(angebot.Descendants(Presentation + "CheckBox"));

        XElement zustimmen = Assert.Single(
            angebot.Descendants(Presentation + "Button"),
            b => (b.Attribute("Command")?.Value ?? string.Empty)
                .Contains("ConfirmRasterFallbackCommand", StringComparison.Ordinal));

        Assert.Contains(
            "BorstWerk.Button.Primary",
            zustimmen.Attribute("Style")?.Value ?? string.Empty,
            StringComparison.Ordinal);

        // Mit Zugriffstaste, wie jede Schaltfläche im Ablauf.
        Assert.Contains("_", zustimmen.Attribute("Content")?.Value ?? string.Empty,
                        StringComparison.Ordinal);
    }

    /// <summary>
    /// Was der Weg kostet, steht vor der Schaltfläche und nicht danach. Ein
    /// Hinweis nach der Zustimmung wäre eine Ausrede, keine Aufklärung.
    /// </summary>
    [Fact]
    public void DieNachteileStehenVorDerSchaltfläche()
    {
        XElement angebot = OfferBorder();

        string[] texte =
        [
            .. angebot.Descendants(Presentation + "TextBlock")
                .Select(t => t.Attribute("Text")?.Value ?? string.Empty),
        ];

        Assert.Contains(texte, t => t.Contains("nicht mehr durchsuchbar", StringComparison.Ordinal));
        Assert.Contains(texte, t => t.Contains("Original bleibt unverändert", StringComparison.Ordinal));
        Assert.Contains(texte, t => t.Contains("maschinenlesbar", StringComparison.Ordinal));

        // Die Aufklärung steht im Baum vor der Schaltflächenzeile.
        int letzterText = angebot.Descendants()
            .ToList()
            .FindLastIndex(e => e.Name == Presentation + "TextBlock");

        int erstesKnopf = angebot.Descendants()
            .ToList()
            .FindIndex(e => e.Name == Presentation + "Button");

        Assert.True(letzterText < erstesKnopf,
                    "Die Nachteile stehen im Markup nach der Schaltfläche.");
    }

    /// <summary>
    /// Zeichen, Wort und Farbe – in dieser Reihenfolge, in beiden Zuständen.
    /// Fiele die Farbe weg, bliebe die Aussage vollständig lesbar. Und beide
    /// Meldungen tragen einen Namen für die Sprachausgabe.
    /// </summary>
    [Theory]
    [InlineData("ShowsRasterFallbackOffer", "⚠")]
    [InlineData("RasterFallbackAccepted", "✓")]
    public void BeideZuständeTragenZeichenWortUndNamen(string bindung, string zeichen)
    {
        XElement bereich = Assert.Single(
            PdfSelectionView().Descendants(Presentation + "Border"),
            b => (b.Attribute("Visibility")?.Value ?? string.Empty)
                .Contains(bindung, StringComparison.Ordinal));

        string[] texte =
        [
            .. bereich.Descendants(Presentation + "TextBlock")
                .Select(t => t.Attribute("Text")?.Value ?? string.Empty),
        ];

        Assert.Contains(texte, t => t == zeichen);
        Assert.Contains(texte, t => t.Length > 10);

        // Angehängte Eigenschaften stehen im XAML als ein Attribut mit Punkt
        // im Namen; für XLinq heißt es schlicht "AutomationProperties.Name".
        Assert.Contains(
            bereich.Attributes(),
            a => a.Name.LocalName == "AutomationProperties.Name");

        Assert.Contains(
            bereich.Attributes(),
            a => a.Name.LocalName == "AutomationProperties.LiveSetting");
    }

    /// <summary>
    /// Ohne diese Prüfung wären die Suchen oben wertlos: Findet die Suche keine
    /// Bereiche, meldet sie zufrieden, dass alles stimmt.
    /// </summary>
    [Fact]
    public void DieSucheFindetBeideBereiche()
    {
        string[] bindungen = [.. PdfSelectionView()
            .Descendants(Presentation + "Border")
            .Select(b => b.Attribute("Visibility")?.Value ?? string.Empty)];

        Assert.Contains(bindungen, v => v.Contains("ShowsRasterFallbackOffer", StringComparison.Ordinal));
        Assert.Contains(bindungen, v => v.Contains("RasterFallbackAccepted", StringComparison.Ordinal));
    }

    private static XElement OfferBorder()
        => Assert.Single(
            PdfSelectionView().Descendants(Presentation + "Border"),
            b => (b.Attribute("Visibility")?.Value ?? string.Empty)
                .Contains("ShowsRasterFallbackOffer", StringComparison.Ordinal));

    private static XDocument PdfSelectionView()
        => XDocument.Load(Path.Combine(AppDirectory, "Views", "Steps", "PdfSelectionView.xaml"));

    private static string PdfSelection()
        => File.ReadAllText(Path.Combine(AppDirectory, "ViewModels", "PdfSelectionViewModel.cs"));

    private static string MainViewModel()
        => File.ReadAllText(Path.Combine(AppDirectory, "ViewModels", "MainViewModel.cs"));

    private static string AppDirectory
        => Path.Combine(TestPaths.RepositoryRoot, "src", "EInvoiceSender.App");
}

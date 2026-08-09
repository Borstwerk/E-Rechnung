using System.Text;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.Text;

/// <summary>
/// Wacht darüber, dass deutscher Text im Projekt echte Umlaute trägt.
///
/// **Warum:** Die Anwendung richtet sich an deutsche Kleinunternehmer. Eine
/// Oberfläche, die „Waehrung“ und „Faellig am“ schreibt, wirkt wie aus einer
/// Zeit, in der ein Zeichensatz noch keine Umlaute kannte. Der Eindruck
/// überträgt sich auf die Sorgfalt der Rechnung, die sie erzeugt.
///
/// Geprüft werden Wortstämme, die es im Deutschen nur mit Umlaut gibt und die
/// in englischen Bezeichnern nicht vorkommen. Ausdrücklich **nicht** geprüft
/// wird alles, was eine Kennung ist: Klassennamen, Dateinamen, Codes aus
/// Codelisten, XML-Elemente, WiX-Ids. Die stehen weiterhin in ASCII, und das
/// ist richtig so.
/// </summary>
public sealed class GermanSpellingTests
{
    /// <summary>
    /// Umschreibungen, die in aktivem Text nichts zu suchen haben.
    ///
    /// Jeder Eintrag ist geprüft: Er kommt in keinem englischen Bezeichner des
    /// Projekts vor. „gross“ steht bewusst nicht hier – das ist zugleich der
    /// englische Name der Bruttosumme.
    /// </summary>
    private static readonly string[] Transliterations =
    [
        "fuer", "ueber", "pruef", "waehr", "kaeufer", "empfaenger", "muess",
        "koenn", "moegl", "vollstaend", "zurueck", "gemaess", "naechst",
        "aender", "strasse", "groess", "laesst", "enthaelt", "gueltig",
        "oeffn", "zusaetz", "beschaedig", "bestaetig", "tatsaechlich",
        "unabhaeng", "oberflaech", "schliess", "faellig", "waehl",
        "erklaer", "schluessel", "zulaess", "verstaend", "geprueft",

        // Nachgetragen: Diese Stämme standen noch im Quelltext, als die Liste
        // oben schon grün war. Der erste – „fuell“ – blieb übrig, weil die
        // Umstellung ein ue vor ll pauschal stehen liess, um „manuell“ und
        // „aktuell“ nicht zu zerstören. Die Sperre gehört an die
        // Fremdwortstämme, nicht an die Buchstabenfolge.
        "fuell", "fuss", "hiess", "aussen", "draussen", "bloss", "massgeb",
        "fuehr", "fuegt", "duerf", "waere", "wuerde", "traegt", "haelt",
        "faengt", "zaehl", "geraet", "gebaeude", "betraeg", "spaeter",
        "haeufig", "gaengig", "aehnlich", "erhaelt", "laeuft", "ausloes",
        "aufloes", "hoech", "noetig", "uebernahme", "uebernimmt",
        "veroeffent", "verknuepf",
    ];

    /// <summary>
    /// Werte von <c>Id</c>-Attributen. Im Installationspaket heissen zwei
    /// Bestandteile <c>StartmenuVerknuepfung</c> und
    /// <c>DesktopVerknuepfung</c>. Das sind Kennungen: Sie stehen in der
    /// MSI-Datenbank, werden innerhalb der Datei gegenseitig referenziert und
    /// bekommen deshalb keine Umlaute. Angezeigt werden <c>Title</c>,
    /// <c>Description</c> und <c>Name</c> – die werden geprüft.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex IdAttribute =
        new("\\bId=\"[^\"]*\"", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly string[] Extensions =
    [
        ".cs", ".xaml", ".md", ".ps1", ".sh", ".wxs", ".csproj", ".props", ".yml",
    ];

    [Fact]
    public void KeinAngezeigterTextVerwendetUmschreibungenStattUmlaute()
    {
        string[] funde =
        [
            .. from path in ActiveFiles()
               let text = IdAttribute.Replace(File.ReadAllText(path), string.Empty)
               from marker in Transliterations
               where text.Contains(marker, StringComparison.OrdinalIgnoreCase)
               select $"{Relative(path)}: {marker}",
        ];

        Assert.True(
            funde.Length == 0,
            "Diese Stellen schreiben deutsche Wörter noch mit ae, oe, ue oder ss:\n"
            + string.Join("\n", funde)
            + "\n\nBezeichner, Dateinamen und Codes bleiben in ASCII – angezeigter "
            + "Text, Kommentare und Testnamen tragen Umlaute.");
    }

    /// <summary>
    /// Ohne diese Prüfung wäre der Wächter oben wertlos: Findet die Suche keine
    /// Dateien, meldet sie fröhlich, dass alles in Ordnung ist.
    /// </summary>
    [Fact]
    public void DieSucheFindetÜberhauptDateien()
    {
        string[] dateien = [.. ActiveFiles()];

        Assert.True(dateien.Length > 100, $"Nur {dateien.Length} Dateien gefunden.");
        Assert.Contains(dateien, p => p.EndsWith("InvoiceDataView.xaml", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ein PowerShell-Skript mit Umlauten braucht eine Byte-Reihenfolge-Marke.
    ///
    /// Windows PowerShell 5.1 liest eine Datei ohne Marke als ANSI. Aus
    /// „Prüfung“ wird dann „PrÃ¼fung“ – ausgerechnet in der Ausgabe, die dem
    /// Anwender den Baulauf erklärt.
    /// </summary>
    [Fact]
    public void JedesSkriptMitUmlautenTrägtEineByteReihenfolgeMarke()
    {
        string[] ohneMarke =
        [
            .. from path in ActiveFiles()
               where path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
               let bytes = File.ReadAllBytes(path)
               where bytes.Any(b => b > 127) && !StartsWithBom(bytes)
               select Relative(path),
        ];

        Assert.True(
            ohneMarke.Length == 0,
            "Diese Skripte enthalten Umlaute, aber keine Byte-Reihenfolge-Marke: "
            + string.Join(", ", ohneMarke)
            + ". Windows PowerShell 5.1 liest sie dann als ANSI und gibt Buchstabensalat aus.");
    }

    private static bool StartsWithBom(byte[] bytes)
        => bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static IEnumerable<string> ActiveFiles()
        => ProjectFiles
            .With(Extensions)
            // Diese Datei führt die Umschreibungen als Suchmuster auf.
            .Where(p => !p.EndsWith(nameof(GermanSpellingTests) + ".cs", StringComparison.Ordinal));

    private static string Relative(string path) => ProjectFiles.Relative(path);
}

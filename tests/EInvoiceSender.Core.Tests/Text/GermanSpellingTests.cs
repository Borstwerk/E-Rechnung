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

        // Nachgetragen beim Rasterversuch: Diese beiden standen noch im aktiven
        // Quelltext, weil kein Stamm sie traf. „hiess“ war erfasst, „heiss“
        // nicht – ein Buchstabe Unterschied, zwei übersehene Stellen.
        "heiss", "stroem",
    ];

    /// <summary>
    /// Was hier bewusst **nicht** steht – und warum das keine Nachlässigkeit ist.
    ///
    /// „weiss“ wäre der nächstliegende Eintrag, und er ist nicht brauchbar:
    /// Der Testname <c>JederHerkunftshinweisStehtBeiSeinemEingabefeld</c>
    /// enthält die Folge in <c>…hinweisSteht…</c>, ohne dass ein falsch
    /// geschriebenes Wort im Spiel wäre. Ein Wächter, der berechtigten Text
    /// anmahnt, wird abgeschaltet – und dann meldet er auch die echten Fälle
    /// nicht mehr.
    ///
    /// Dasselbe gilt für „gross“: Es ist zugleich der englische Name der
    /// Bruttosumme (siehe oben).
    /// </summary>
    private static readonly string[] NotUsable = ["weiss", "gross"];

    /// <summary>
    /// Hält die Begründung oben fest: Wer eines dieser Muster nachträgt, macht
    /// den Wächter unbrauchbar, und der Test sagt ihm, warum.
    /// </summary>
    [Fact]
    public void DieUnbrauchbarenMusterBleibenDraußen()
        => Assert.All(
            NotUsable,
            marker => Assert.DoesNotContain(marker, Transliterations, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Werte von <c>Id</c>-Attributen. Im Installationspaket heißen zwei
    /// Bestandteile <c>StartmenuVerknuepfung</c> und
    /// <c>DesktopVerknuepfung</c>. Das sind Kennungen: Sie stehen in der
    /// MSI-Datenbank, werden innerhalb der Datei gegenseitig referenziert und
    /// bekommen deshalb keine Umlaute. Angezeigt werden <c>Title</c>,
    /// <c>Description</c> und <c>Name</c> – die werden geprüft.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex IdAttribute =
        new("\\bId=\"[^\"]*\"", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Dateinamen, die in Markdown als Code ausgezeichnet sind.
    ///
    /// Der Windows-Testkoffer enthält eine Datei namens
    /// <c>08_Oeffnungspasswort_test123.pdf</c>. Der Name ist eine Kennung –
    /// er steht so auf der Platte, und ein Ablaufplan, der ihn stillschweigend
    /// zu <c>08_Öffnungspasswort…</c> verbessert, schickt den Prüfer zu einer
    /// Datei, die es nicht gibt.
    ///
    /// Das Muster ist eng gefasst: Es greift nur zwischen Rückwärtsakzenten,
    /// nur ohne Leerzeichen und nur mit einer kurzen Endung. Fließtext bleibt
    /// vollständig geprüft, auch in derselben Zeile.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex FileNameInCode =
        new("`[^`\\s]+\\.[A-Za-z0-9]{2,4}`", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Was geprüft wird.
    ///
    /// Nachgetragen: <c>.wixproj</c>, <c>.editorconfig</c> und
    /// <c>.gitattributes</c>. In allen dreien standen deutsche Kommentare, und
    /// keine der drei Endungen war erfasst – der Wächter hat sie nie gesehen.
    ///
    /// **Nicht erfasst ist <c>.sln</c>, und das bleibt so.** Eine Projektmappe
    /// besteht überwiegend aus GUIDs, und deren Hex-Ziffern ergeben Folgen wie
    /// <c>FAE04EC0</c> oder <c>BAEE</c>. Jeder Stamm mit „ae“ schlüge dort an,
    /// ohne dass ein einziges deutsches Wort im Spiel wäre. Deutscher Text
    /// steht in einer Projektmappe nicht.
    /// </summary>
    private static readonly string[] Extensions =
    [
        ".cs", ".xaml", ".md", ".ps1", ".sh", ".wxs", ".wixproj", ".csproj",
        ".props", ".yml", ".editorconfig", ".gitattributes",
    ];

    [Fact]
    public void KeinAngezeigterTextVerwendetUmschreibungenStattUmlaute()
    {
        string[] funde =
        [
            .. from path in ActiveFiles()
               let roh = File.ReadAllText(path)
               let text = FileNameInCode.Replace(IdAttribute.Replace(roh, string.Empty), string.Empty)
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

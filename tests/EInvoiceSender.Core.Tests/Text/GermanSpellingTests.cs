using System.Text;
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
        "aender", "strasse", "groesse", "laesst", "enthaelt", "gueltig",
        "oeffn", "zusaetz", "beschaedig", "bestaetig", "tatsaechlich",
        "unabhaeng", "oberflaech", "ausschliess", "faellig", "waehl",
        "erklaer", "schluessel", "zulaess", "verstaend", "geprueft",
    ];

    private static readonly string[] Extensions =
    [
        ".cs", ".xaml", ".md", ".ps1", ".sh", ".wxs", ".csproj", ".props", ".yml",
    ];

    /// <summary>
    /// Ausgeschlossen: Baustände, Fremdwerkzeuge und der abgelegte alte Stand.
    /// <c>docs/legacy</c> beschreibt eine Fassung, die es nicht mehr gibt; sie
    /// nachträglich umzuschreiben verfälschte nur die Aufzeichnung.
    /// </summary>
    private static readonly string[] ExcludedFolders =
    [
        "bin", "obj", ".git", "legacy", "artifacts", "tools", "packages",
    ];

    [Fact]
    public void KeinAngezeigterTextVerwendetUmschreibungenStattUmlaute()
    {
        string[] funde =
        [
            .. from path in ActiveFiles()
               let text = File.ReadAllText(path)
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
        => Directory
            .EnumerateFiles(TestPaths.RepositoryRoot, "*", SearchOption.AllDirectories)
            .Where(p => Extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Where(p => !IsExcluded(p))
            // Diese Datei führt die Umschreibungen als Suchmuster auf.
            .Where(p => !p.EndsWith(nameof(GermanSpellingTests) + ".cs", StringComparison.Ordinal));

    private static bool IsExcluded(string path)
        => Relative(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => ExcludedFolders.Contains(part, StringComparer.OrdinalIgnoreCase));

    private static string Relative(string path)
        => Path.GetRelativePath(TestPaths.RepositoryRoot, path);
}

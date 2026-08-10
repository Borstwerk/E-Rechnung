using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft, dass das Installationspaket als dual-purpose-Paket verfasst bleibt.
///
/// **Der Fehler:** Das Paket war <c>Scope="perUser"</c> und legte
/// INSTALLFOLDER fest unter <c>LocalAppDataFolder</c> ab. Damit landeten
/// sämtliche Programmdateien im Benutzerprofil – und genau das beanstandet
/// ICE38: Eine Komponente, die in das Benutzerprofil installiert, braucht
/// einen Registrierungswert als Schlüsselpfad, keine Datei. Bei rund 580
/// eingelesenen Laufzeitdateien ergab das 580 Fehler, und der Bau brach nach
/// der MSI-Prüfung ab.
///
/// Der Harvester kann keine Registrierungsschlüssel erfinden, eine von Hand
/// gepflegte Dateiliste über hunderte Einträge wäre eine Zumutung, und ICE38
/// zu unterdrücken hieße nur, die Prüfung zum Schweigen zu bringen. Die
/// Lösung liegt beim Ziel, nicht bei den Dateien.
///
/// **Zwei weitere Befunde aus dem Windows-Bau** stecken ebenfalls hier:
/// ICE57 am Schlüsselpfad der Verknüpfungen und ICE61 an der
/// Aktualisierungsregel. Beide sind an der Ursache behoben, keiner ist
/// unterdrückt.
///
/// **Warum als Quelltextprüfung:** Ein MSI entsteht nur unter Windows;
/// <c>wix.exe</c> bricht auf jeder anderen Plattform sofort ab. Dieser Test
/// hält deshalb die Merkmale fest, an denen die Entscheidung hängt.
/// </summary>
public sealed class InstallerScopeTests
{
    private static readonly XNamespace Wxs = "http://wixtoolset.org/schemas/v4/wxs";

    [Fact]
    public void DasPaketIstNichtMehrReinBenutzerbezogen()
    {
        string scope = Scope();

        Assert.False(
            string.Equals(scope, "perUser", StringComparison.Ordinal),
            "Ein reines perUser-Paket installiert alle Programmdateien in das Benutzerprofil. "
            + "ICE38 verlangt dort einen Registrierungswert als Schlüsselpfad – für rund 580 "
            + "eingelesene Dateien nicht zu leisten.");
    }

    [Fact]
    public void DasPaketTaugtFürBeideInstallationsarten()
        => Assert.Equal("perUserOrMachine", Scope());

    /// <summary>
    /// Die Vorgabe „nur für mich“ kommt aus dem Scope, nicht aus einer eigenen
    /// Eigenschaft: WiX setzt für <c>perUserOrMachine</c> selbst
    /// <c>ALLUSERS=2</c> und <c>MSIINSTALLPERUSER=1</c>. Beide hier noch
    /// einmal festzulegen wäre überflüssig und je nach Verarbeitung eine
    /// doppelte Definition.
    /// </summary>
    [Theory]
    [InlineData("ALLUSERS")]
    [InlineData("MSIINSTALLPERUSER")]
    public void DieVorgabeKommtAusDemScopeUndNichtAusEinerEigenenEigenschaft(string property)
    {
        Assert.DoesNotContain(
            Package().Elements(Wxs + "Property"),
            p => string.Equals(p.Attribute("Id")?.Value, property, StringComparison.Ordinal));
    }

    [Fact]
    public void DieAnwendungLiegtNichtMehrFestImBenutzerprofil()
    {
        string[] verzeichnisse = [.. StandardDirectories()];

        Assert.DoesNotContain("LocalAppDataFolder", verzeichnisse);
    }

    /// <summary>
    /// Windows Installer soll den Zielordner je nach Installationsart selbst
    /// auflösen – benutzerbezogen unter %LOCALAPPDATA%\Programs, sonst unter
    /// C:\Program Files.
    /// </summary>
    [Fact]
    public void DerZielordnerWirdVonWindowsInstallerAufgelöst()
    {
        XElement programFiles = Assert.Single(
            Package().Elements(Wxs + "StandardDirectory"),
            d => d.Attribute("Id")?.Value == "ProgramFiles64Folder");

        Assert.Contains(
            programFiles.Descendants(Wxs + "Directory"),
            d => d.Attribute("Id")?.Value == "INSTALLFOLDER");
    }

    /// <summary>
    /// Der Schlüsselpfad der Verknüpfungskomponenten ist eindeutig
    /// benutzerbezogen.
    ///
    /// **Der Fehler:** Vorher stand dort HKMU – ein Pfad, der erst zur
    /// Installationszeit zu HKCU oder HKLM wird. ICE57 beanstandet genau
    /// das: „has both per-user data and a keypath that can be either
    /// per-user or per-machine.“ Eine Verknüpfung im Startmenü ist immer
    /// benutzerbezogene Angabe; ihr Schlüsselpfad darf deshalb nicht offen
    /// lassen, für wen er gilt.
    ///
    /// HKCU erfüllt zugleich ICE38, das für alles, was in ein Benutzerprofil
    /// installiert, einen Schlüsselpfad unter HKCU verlangt.
    /// </summary>
    [Fact]
    public void DerSchlüsselpfadDerVerknüpfungenIstEindeutigBenutzerbezogen()
    {
        string[] wurzeln =
        [
            .. Package()
                .Descendants(Wxs + "RegistryValue")
                .Select(r => r.Attribute("Root")?.Value ?? string.Empty),
        ];

        Assert.NotEmpty(wurzeln);
        Assert.All(wurzeln, root => Assert.Equal("HKCU", root));
    }

    /// <summary>
    /// Das Paket ersetzt nur ältere Fassungen seiner selbst.
    ///
    /// <c>AllowSameVersionUpgrades</c> nimmt die eigene Fassung in den
    /// Bereich der zu ersetzenden Fassungen auf. Die Obergrenze ist dann
    /// nicht mehr kleiner als die eigene Version – ICE61 meldet „This
    /// product should remove only older versions of itself“. Ein Paket, das
    /// sich selbst für seinen Vorgänger hält, kann sich bei einer
    /// Neuinstallation selbst wieder entfernen.
    /// </summary>
    [Fact]
    public void DasPaketErsetztNurÄltereFassungen()
    {
        XElement upgrade = Assert.Single(Package().Elements(Wxs + "MajorUpgrade"));

        Assert.Null(upgrade.Attribute("AllowSameVersionUpgrades"));
        Assert.NotNull(upgrade.Attribute("DowngradeErrorMessage"));
    }

    /// <summary>
    /// Der UpgradeCode ist die Kennung, an der Windows eine spätere Fassung
    /// als Aktualisierung erkennt. Ändert er sich, steht die neue Fassung
    /// neben der alten statt an ihrer Stelle – und der Anwender hat die
    /// Anwendung zweimal installiert.
    /// </summary>
    [Fact]
    public void DerUpgradeCodeBleibtUnverändert()
        => Assert.Equal(
            "7f3c1d92-4b6a-4d21-9d0e-2a5c8b1e7f40",
            Package().Attribute("UpgradeCode")?.Value,
            ignoreCase: true);

    /// <summary>
    /// Der Startmenüeintrag gehört zur Hauptfunktion, die Desktopverknüpfung
    /// zu einer eigenen Funktion mit <c>Level="2"</c> – damit ist sie
    /// abwählbar. Läge sie in der Hauptfunktion, bekäme sie jeder.
    /// </summary>
    [Fact]
    public void DerStartmenüeintragIstFestUndDerDesktopeintragWählbar()
    {
        XElement[] features = [.. Package().Elements(Wxs + "Feature")];

        XElement haupt = Assert.Single(features, f => f.Attribute("Level")?.Value == "1");
        XElement desktop = Assert.Single(features, f => f.Attribute("Level")?.Value == "2");

        // Gesucht wird über den Zielordner, nicht über die Kennung: Welche
        // Verknüpfung wohin gehört, ist die Aussage – wie die Komponente
        // heißt, ist Nebensache und darf sich ändern.
        Assert.Contains(KomponentenIn(haupt), id => id == KomponenteFür("StartmenuOrdner"));
        Assert.Contains(KomponentenIn(desktop), id => id == KomponenteFür("DesktopFolder"));
    }

    /// <summary>Kennung der Komponente, die in diesen Ordner installiert.</summary>
    private static string KomponenteFür(string ordner)
        => Assert.Single(
            Package().Elements(Wxs + "Component"),
            c => c.Attribute("Directory")?.Value == ordner)
            .Attribute("Id")!.Value;

    private static IEnumerable<string> KomponentenIn(XElement feature)
        => feature.Elements(Wxs + "ComponentRef")
            .Select(r => r.Attribute("Id")?.Value ?? string.Empty);

    /// <summary>
    /// Beide Verknüpfungen tragen das BorstWerk-Symbol, und die Symboldatei
    /// ist im Paket angemeldet. Fehlte der Eintrag, zeigte „Apps und
    /// Features“ das Ersatzsymbol von Windows.
    /// </summary>
    [Fact]
    public void DieVerknüpfungenTragenDasBorstWerkSymbol()
    {
        XElement icon = Assert.Single(Package().Elements(Wxs + "Icon"));
        string id = icon.Attribute("Id")?.Value ?? string.Empty;

        Assert.Contains("BorstWerk", icon.Attribute("SourceFile")?.Value ?? string.Empty,
                        StringComparison.Ordinal);

        Assert.Contains(
            Package().Elements(Wxs + "Property"),
            p => p.Attribute("Id")?.Value == "ARPPRODUCTICON"
                 && p.Attribute("Value")?.Value == id);

        XElement[] verknüpfungen = [.. Package().Descendants(Wxs + "Shortcut")];

        Assert.Equal(2, verknüpfungen.Length);
        Assert.All(verknüpfungen, s => Assert.Equal(id, s.Attribute("Icon")?.Value));
    }

    /// <summary>
    /// Die Dateien werden weiterhin eingelesen. Eine von Hand gepflegte Liste
    /// über hunderte Laufzeitdateien vergisst früher oder später eine.
    /// </summary>
    [Fact]
    public void DieDateienWerdenWeiterhinEingelesen()
    {
        XElement files = Assert.Single(
            Document("Files.wxs").Descendants(Wxs + "Files"));

        Assert.Contains("$(PublishDir)", files.Attribute("Include")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Möglichkeiten, eine MSI-Prüfung stumm zu schalten.
    ///
    /// Gesucht wird nach dem Abschalten, nicht nach der Erwähnung: In den
    /// Kommentaren steht durchaus „ICE38“, und das soll auch so bleiben – dort
    /// ist erklärt, warum das Paket aussieht, wie es aussieht.
    /// </summary>
    private static readonly string[] Suppressions =
    [
        "SuppressIces", "SuppressValidation", "-sice", "-sval", "SuppressSpecificWarnings",
    ];

    /// <summary>
    /// Keine MSI-Prüfung wird unterdrückt. Eine stummgeschaltete Prüfung
    /// meldet den nächsten Fehler derselben Art auch nicht mehr.
    /// </summary>
    [Fact]
    public void KeineMsiPrüfungWirdUnterdrückt()
    {
        string[] treffer =
        [
            .. from path in ProjectFiles.With(".wxs", ".wixproj", ".ps1", ".yml")
               let text = File.ReadAllText(path)
               from suppression in Suppressions
               where text.Contains(suppression, StringComparison.OrdinalIgnoreCase)
               select $"{ProjectFiles.Relative(path)}: {suppression}",
        ];

        Assert.True(
            treffer.Length == 0,
            $"Diese Stellen schalten eine MSI-Prüfung stumm: {string.Join(", ", treffer)}.");
    }

    private static string Scope()
        => Package().Attribute("Scope")?.Value ?? string.Empty;

    private static IEnumerable<string> StandardDirectories()
        => Package()
            .Elements(Wxs + "StandardDirectory")
            .Select(d => d.Attribute("Id")?.Value ?? string.Empty);

    private static XElement Package()
        => Document("Package.wxs").Root!.Element(Wxs + "Package")!;

    private static XDocument Document(string file)
        => XDocument.Load(ProjectFiles.With(".wxs").Single(p => Path.GetFileName(p) == file));
}

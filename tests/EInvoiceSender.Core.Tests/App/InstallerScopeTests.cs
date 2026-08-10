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
    /// HKCU wäre bei einer Installation für alle Benutzer die falsche Stelle.
    /// HKMU übersetzt WiX je nach Installationsart.
    /// </summary>
    [Fact]
    public void DieVerknüpfungenSchreibenInDieKontextabhängigeRegistrierung()
    {
        string[] wurzeln =
        [
            .. Package()
                .Descendants(Wxs + "RegistryValue")
                .Select(r => r.Attribute("Root")?.Value ?? string.Empty),
        ];

        Assert.NotEmpty(wurzeln);
        Assert.All(wurzeln, root => Assert.Equal("HKMU", root));
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

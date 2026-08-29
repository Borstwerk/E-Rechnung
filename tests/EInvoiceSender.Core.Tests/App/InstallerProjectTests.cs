using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält die Bau-Einstellungen des Installerprojekts fest.
///
/// **Warum als Quelltextprüfung:** Ein MSI entsteht nur unter Windows –
/// <c>wix.exe</c> läuft nirgends sonst. Der Fehler, um den es hier geht, zeigt
/// sich also erst im Windows-Job der Pipeline. Diese Prüfung läuft überall und
/// meldet ihn vorher.
/// </summary>
public sealed class InstallerProjectTests
{
    /// <summary>
    /// WiX kennt keine portablen Symboldateien.
    ///
    /// <c>Directory.Build.props</c> setzt <c>DebugType=portable</c> für alle
    /// Projekte unterhalb des Wurzelverzeichnisses – das Installerprojekt
    /// eingeschlossen. Die WiX-SDK reicht <c>$(DebugType)</c> unverändert als
    /// <c>PdbType</c> an <c>wix.exe</c> weiter, und der bricht mit
    /// <c>WIX0268</c> ab. Das Installerprojekt muss den Wert deshalb selbst
    /// überschreiben.
    /// </summary>
    [Fact]
    public void DasInstallerprojektSetztDebugTypeAufNone()
    {
        string? debugType = Properties(SetupProject)
            .Elements()
            .Where(e => e.Name.LocalName == "DebugType")
            .Select(e => e.Value.Trim())
            .LastOrDefault();

        Assert.True(
            string.Equals(debugType, "none", StringComparison.OrdinalIgnoreCase),
            $"Das Installerprojekt setzt DebugType auf '{debugType ?? "nichts"}'. WiX kennt nur "
            + "'full' und 'none'; der aus Directory.Build.props geerbte Wert 'portable' lässt "
            + "wix.exe mit WIX0268 abbrechen.");
    }

    /// <summary>
    /// Die Gegenrichtung: Der Fehler darf **nicht** dadurch behoben werden,
    /// dass die zentralen Bau-Einstellungen aufgeweicht werden. Portable
    /// Symboldateien gehören zu den reproduzierbaren Builds der
    /// .NET-Projekte; sie wegen des Installers aufzugeben wäre der falsche
    /// Handel.
    /// </summary>
    [Fact]
    public void DieZentralenEinstellungenBehaltenPortableSymboldateien()
    {
        string? debugType = Properties(Path.Combine(TestPaths.RepositoryRoot, "Directory.Build.props"))
            .Elements()
            .Where(e => e.Name.LocalName == "DebugType")
            .Select(e => e.Value.Trim())
            .FirstOrDefault();

        Assert.Equal("portable", debugType);
    }

    /// <summary>
    /// Die Quelltextprüfungen allein belegen nicht, was WiX tatsächlich in
    /// die MSI-Datenbank schreibt. Deshalb müssen sowohl der örtliche
    /// Installerbau als auch der Windows-Job die gebaute Datei prüfen, bevor
    /// sie als Release-Artefakt weitergereicht wird.
    /// </summary>
    [Fact]
    public void JederInstallerbauPrüftDieGebautenMsiMetadaten()
    {
        string scriptName = "Test-InstallerMetadata.ps1";
        string installerBuild = File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "build", "Build-Installer.ps1"));
        string releaseBuild = File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "build", "Build-Release.ps1"));
        string continuousIntegration = File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, ".github", "workflows", "ci.yml"));

        Assert.Contains(scriptName, installerBuild, StringComparison.Ordinal);
        Assert.Contains("-ApplicationPath", installerBuild, StringComparison.Ordinal);
        Assert.Contains("Build-Installer.ps1", releaseBuild, StringComparison.Ordinal);
        Assert.Contains("Build-Release.ps1", continuousIntegration, StringComparison.Ordinal);
        Assert.True(
            File.Exists(Path.Combine(TestPaths.RepositoryRoot, "build", scriptName)),
            $"build/{scriptName} fehlt.");
    }

    /// <summary>
    /// **Der Installerbau veröffentlicht bedingungslos neu.**
    ///
    /// Am 25.08.2026 entstand ein MSI mit der Produktversion 0.2.0, dessen
    /// Programmdateien aus einem älteren Quellstand stammten. Möglich war das,
    /// weil der Installerbau einen vorhandenen Veröffentlichungsbestand
    /// wiederverwenden durfte. Keine Versionsprüfung konnte das bemerken: Auch
    /// der alte Bestand trug schon <c>VersionPrefix</c> 0.2.0.
    ///
    /// Der Wächter im WiX-Projekt schützt davor nur halb. Er erzwingt, dass
    /// der Bau über <c>Build-Installer.ps1</c> läuft – nicht, dass dieses
    /// Skript vorher tatsächlich veröffentlicht hat. Käme der Übersprung
    /// zurück, wäre der alte Fehler wieder da, und zwar auf dem offiziellen
    /// Weg.
    ///
    /// **Warum als Quelltextprüfung:** Die Skripte laufen nur unter Windows
    /// mit PowerShell. Diese Prüfung läuft überall.
    /// </summary>
    [Fact]
    public void DerInstallerbauVeröffentlichtBeiJedemAufrufNeu()
    {
        string[] zeilen = File.ReadAllLines(
            Path.Combine(TestPaths.RepositoryRoot, "build", "Build-Installer.ps1"));
        string skript = string.Join('\n', zeilen);

        Assert.DoesNotContain("SkipPublish", skript, StringComparison.OrdinalIgnoreCase);

        // Der Aufruf, nicht die Erwähnung: Von Publish.ps1 ist auch im
        // Kopfkommentar die Rede.
        string? aufruf = zeilen.FirstOrDefault(
            z => z.TrimStart().StartsWith('&') && z.Contains("Publish.ps1", StringComparison.Ordinal));

        Assert.True(
            aufruf is not null,
            "Build-Installer.ps1 ruft Publish.ps1 nicht mehr auf. Ohne frische "
            + "Veröffentlichung kann der Installerbau alten Programmbestand paketieren.");

        // Eingerückt heißt: in einem Block, also unter einer Bedingung. Genau
        // das war der alte Fehler.
        Assert.False(
            aufruf!.StartsWith(' ') || aufruf.StartsWith('\t'),
            "Der Aufruf von Publish.ps1 steht in Build-Installer.ps1 eingerückt und damit "
            + $"vermutlich in einem Bedingungsblock: '{aufruf.Trim()}'. Der Installerbau muss "
            + "bei jedem Aufruf veröffentlichen.");

        // Die Absprache zwischen Skript und WiX-Projekt: ohne diese
        // Eigenschaft weist der Wächter auch den offiziellen Weg ab.
        Assert.Contains("BorstWerkInstallerBuild=true", skript, StringComparison.Ordinal);
        Assert.Contains("-p:PublishDir=", skript, StringComparison.Ordinal);
    }

    /// <summary>
    /// Das WiX-Projekt bricht ab, wenn es nicht über den vorgesehenen Weg
    /// gebaut wird, und kennt keinen Vorgabewert für <c>PublishDir</c>.
    ///
    /// Das Verhalten selbst prüft <c>build/test-installer-build-guard.sh</c>
    /// mit einem echten Buildversuch. Hier steht nur die Struktur, die dort
    /// vorausgesetzt wird – ein versehentlich wieder eingeführter
    /// Vorgabewert fällt damit schon in der Solution auf.
    /// </summary>
    [Fact]
    public void DasInstallerprojektKenntKeinenVorgabewertFürPublishDir()
    {
        XElement wurzel = XDocument.Load(SetupProject).Root!;

        Assert.Equal(
            "EnsureAuthorizedInstallerBuild",
            wurzel.Attribute("InitialTargets")?.Value);

        Assert.DoesNotContain(
            Properties(SetupProject).Elements(),
            e => e.Name.LocalName == "PublishDir");
    }

    private static string SetupProject { get; } = Path.Combine(
        TestPaths.RepositoryRoot, "installer", "EInvoiceSender.Setup", "EInvoiceSender.Setup.wixproj");

    private static IEnumerable<XElement> Properties(string projectFile)
    {
        Assert.True(File.Exists(projectFile), $"{ProjectFiles.Relative(projectFile)} fehlt.");

        return XDocument
            .Load(projectFile)
            .Root!
            .Elements()
            .Where(e => e.Name.LocalName == "PropertyGroup");
    }
}

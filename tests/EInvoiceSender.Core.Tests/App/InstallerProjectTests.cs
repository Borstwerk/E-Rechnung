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

using System.Text.RegularExpressions;
using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält die Produktversion als eine einzige, gemeinsam verwendete Quelle fest.
///
/// Die Versionsnummer gehört in <c>Directory.Build.props</c>. Projekte,
/// Skripte und CI dürfen keine zweite aktive Produktversion einführen. Die
/// veröffentlichte Anwendung zeigt den aus ihrer Assembly gelesenen Wert.
/// </summary>
public sealed class VersioningTests
{
    private static readonly string[] ProductVersionProperties =
    [
        "VersionPrefix", "Version", "AssemblyVersion", "FileVersion",
        "InformationalVersion", "ProductVersion",
    ];

    [Fact]
    public void VersionPrefixIstDieEinzigeAktiveProduktversionsquelle()
    {
        var definitions =
            from path in ProjectFiles.With(".props", ".csproj", ".wixproj")
            from element in XDocument.Load(path).Descendants()
            where ProductVersionProperties.Contains(element.Name.LocalName, StringComparer.Ordinal)
            select new
            {
                Path = ProjectFiles.Relative(path),
                Name = element.Name.LocalName,
                Value = element.Value.Trim(),
            };

        var definition = Assert.Single(definitions);

        Assert.Equal("Directory.Build.props", definition.Path);
        Assert.Equal("VersionPrefix", definition.Name);
        Assert.Matches(@"^\d+\.\d+\.\d+$", definition.Value);
    }

    [Fact]
    public void InstallerÜbernimmtVersionPrefixOhneEigeneProductVersion()
    {
        string projectPath = Path.Combine(
            TestPaths.RepositoryRoot, "installer", "EInvoiceSender.Setup",
            "EInvoiceSender.Setup.wixproj");
        XDocument project = XDocument.Load(projectPath);
        string constants = Assert.Single(
            project.Descendants(), e => e.Name.LocalName == "DefineConstants").Value;

        Assert.Contains("ProductVersion=$(VersionPrefix);", constants, StringComparison.Ordinal);
        Assert.DoesNotContain(
            project.Descendants(), e => e.Name.LocalName == "ProductVersion");
    }

    [Fact]
    public void BuildUndCiSetzenKeineUnabhängigeProductVersion()
    {
        string[] paths =
        [
            Path.Combine(TestPaths.RepositoryRoot, ".github", "workflows", "ci.yml"),
            Path.Combine(TestPaths.RepositoryRoot, "build", "Build.ps1"),
            Path.Combine(TestPaths.RepositoryRoot, "build", "Publish.ps1"),
            Path.Combine(TestPaths.RepositoryRoot, "build", "Build-Installer.ps1"),
            Path.Combine(TestPaths.RepositoryRoot, "build", "Build-Release.ps1"),
        ];

        foreach (string path in paths)
        {
            string text = File.ReadAllText(path);

            Assert.DoesNotContain("-p:ProductVersion", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ÜberAnzeigeLiestDieInformationsversionOhneBuildmetadaten()
    {
        string path = Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.App", "Views", "Dialogs",
            "AboutWindow.xaml.cs");
        string source = File.ReadAllText(path);

        Assert.Contains("AssemblyInformationalVersionAttribute", source, StringComparison.Ordinal);
        Assert.Contains("IndexOf('+', StringComparison.Ordinal)", source, StringComparison.Ordinal);
        Assert.False(
            Regex.IsMatch(source, @"\b\d+\.\d+\.\d+\b", RegexOptions.CultureInvariant),
            "Die Über-Anzeige darf keine Produktversion fest eintragen.");
    }
}

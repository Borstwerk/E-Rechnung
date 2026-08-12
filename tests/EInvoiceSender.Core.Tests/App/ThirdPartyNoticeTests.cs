using System.Xml.Linq;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält Paketdefinition, technische Hauptquelle und Anwenderhinweise
/// zusammen. Die Prüfung bewertet keine Lizenz; sie verhindert, dass eine
/// bereits geprüfte Zuordnung unbemerkt vom technischen Bestand abweicht.
/// </summary>
public sealed class ThirdPartyNoticeTests
{
    private const string ProductName = "BorstWerk E-Rechnung";

    private static readonly string[] ObsoleteRuntimeNames =
    [
        "Serilog",
        "Microsoft.Extensions.Hosting",
    ];

    [Fact]
    public void DirekteRuntimepaketeEntsprechenDerTechnischenHauptquelle()
    {
        Dictionary<string, RuntimePackage> documented = ReadRuntimePackages();
        Dictionary<string, string> centralVersions = ReadCentralPackageVersions();
        string[] actual = ProductProjects()
            .SelectMany(ReadPackageReferences)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] declaredDirect = documented.Values
            .Where(p => p.Kind.StartsWith("direkt", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AssertPackageSetsEqual(actual, declaredDirect);

        foreach (string package in actual)
        {
            Assert.True(centralVersions.TryGetValue(package, out string? expectedVersion),
                $"{package} besitzt keine zentrale Paketfassung.");
            Assert.Equal(expectedVersion, documented[package].Version);
        }
    }

    [Fact]
    public void HauptquelleBenenntDenKuratiertenTransitivUndNativenBestand()
    {
        Dictionary<string, RuntimePackage> documented = ReadRuntimePackages();
        string[] required =
        [
            "BouncyCastle.Cryptography",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Options",
            "Microsoft.Extensions.Primitives",
            "bblanchon.PDFium.Win32",
            "SkiaSharp",
            "SkiaSharp.NativeAssets.Win32",
        ];

        foreach (string package in required)
        {
            Assert.True(documented.ContainsKey(package),
                $"Der bekannte transitive/native Bestandteil {package} fehlt in der Hauptquelle.");
        }
    }

    [Fact]
    public void AnwenderdarstellungenEnthaltenDieVorgesehenenRuntimefamilien()
    {
        string markdown = Read("installer", "Drittanbieterhinweise", "README.md");
        string rtf = Read("installer", "EInvoiceSender.Setup", "Lizenzhinweise.rtf");

        RequireTerms(markdown,
            "CommunityToolkit.Mvvm",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Options",
            "Microsoft.Extensions.Primitives",
            "PDFsharp",
            "PdfPig",
            "PDFtoImage",
            "PDFium",
            "SkiaSharp",
            "MimeKit",
            "BouncyCastle.Cryptography",
            "System.Security.Cryptography.ProtectedData",
            "Microsoft.NETCore.App",
            "Microsoft.WindowsDesktop.App");

        RequireTerms(rtf,
            "CommunityToolkit.Mvvm",
            "Microsoft.Extensions.DependencyInjection",
            "Logging",
            "Options",
            "Primitives",
            "PDFsharp",
            "PdfPig",
            "PDFtoImage",
            "PDFium",
            "SkiaSharp",
            "MimeKit",
            "BouncyCastle.Cryptography",
            "System.Security.Cryptography.ProtectedData",
            "Microsoft.NETCore.App",
            "Microsoft.WindowsDesktop.App");
    }

    [Fact]
    public void AktuelleRuntimehinweiseEnthaltenKeineVeraltetenPakete()
    {
        foreach (string path in CurrentNoticeFiles())
        {
            ForbidTerms(File.ReadAllText(path), ObsoleteRuntimeNames);
        }
    }

    [Fact]
    public void AlleSichtbarenHinweiseVerwendenDenProduktnamen()
    {
        foreach (string path in CurrentNoticeFiles())
        {
            Assert.Contains(ProductName, File.ReadAllText(path), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RtfBleibtEineGültigeKleineAnwenderzusammenfassung()
    {
        string rtf = Read("installer", "EInvoiceSender.Setup", "Lizenzhinweise.rtf");

        Assert.StartsWith(@"{\rtf1\ansi", rtf, StringComparison.Ordinal);
        Assert.Equal(rtf.Count(c => c == '{'), rtf.Count(c => c == '}'));
        Assert.Contains("Zusammenfassung", rtf, StringComparison.Ordinal);
        Assert.Contains("Drittanbieterhinweise.md", rtf, StringComparison.Ordinal);
        Assert.DoesNotContain("vollst\\'e4ndigen Lizenztexte", rtf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenaueFremdpaketfassungenStehenNurInDerTechnischenHauptquelle()
    {
        string technical = Read("docs", "THIRD-PARTY-NOTICES.md");
        string userMarkdown = Read("installer", "Drittanbieterhinweise", "README.md");
        string rtf = Read("installer", "EInvoiceSender.Setup", "Lizenzhinweise.rtf");

        foreach (RuntimePackage package in ReadRuntimePackages().Values)
        {
            Assert.Contains(package.Version, technical, StringComparison.Ordinal);
            Assert.DoesNotContain(package.Version, userMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain(package.Version, rtf, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GeprüfteLizenzUndNoticeTexteSindVollständigAbgelegt()
    {
        string root = Path.Combine(TestPaths.RepositoryRoot,
            "installer", "Drittanbieterhinweise", "Lizenzen");
        string runtimeVersion = ReadProductRuntimeFrameworkVersion();
        string[] runtimeLicenseNames =
        [
            $"Microsoft.NETCore.App.Runtime.win-x64-{runtimeVersion}-LICENSE.txt",
            $"Microsoft.NETCore.App.Runtime.win-x64-{runtimeVersion}-THIRD-PARTY-NOTICES.txt",
            $"Microsoft.WindowsDesktop.App.Runtime.win-x64-{runtimeVersion}-LICENSE.txt",
        ];
        string[] required =
        [
            "README.md",
            "Apache-2.0.txt",
            "BouncyCastle.Cryptography-2.6.2-LICENSE.md",
            "CommunityToolkit.Mvvm-8.4.2-LICENSE.md",
            "CommunityToolkit.Mvvm-8.4.2-THIRD-PARTY-NOTICES.txt",
            "Microsoft-.NET-Library-License.txt",
            .. runtimeLicenseNames,
            "MimeKit-4.17.0-LICENSE.txt",
            "PDFsharp-6.2.4-LICENSE.txt",
            "PDFtoImage-5.3.0-LICENSE.txt",
            "SkiaSharp-4.150.1-LICENSE.txt",
            "SkiaSharp-4.150.1-THIRD-PARTY-NOTICES.txt",
            Path.Combine("PDFium-152.0.7961", "LICENSE"),
        ];

        foreach (string relative in required)
        {
            string path = Path.Combine(root, relative);
            Assert.True(File.Exists(path), $"Der geprüfte Primärtext fehlt: {relative}");
            Assert.True(new FileInfo(path).Length > 500, $"Der Primärtext ist unerwartet leer: {relative}");
        }

        string pdfiumLicenses = Path.Combine(root, "PDFium-152.0.7961", "licenses");
        Assert.Equal(15, Directory.EnumerateFiles(pdfiumLicenses).Count());

        string[] actualRuntimeLicenseNames = Directory
            .EnumerateFiles(root, "Microsoft.*.Runtime.win-x64-*", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(runtimeLicenseNames.Order(StringComparer.Ordinal), actualRuntimeLicenseNames);
    }

    [Fact]
    public void SelfContainedRuntimepacksFolgenDerBewusstenPublishkonfiguration()
    {
        string expectedVersion = ReadProductRuntimeFrameworkVersion();
        Dictionary<string, string> documented = ReadRuntimePacks();
        string origins = Read("installer", "Drittanbieterhinweise", "Lizenzen", "README.md");
        string[] expectedIds =
        [
            "runtimepack.Microsoft.NETCore.App.Runtime.win-x64",
            "runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64",
        ];

        AssertPackageSetsEqual(expectedIds, documented.Keys);
        foreach (string id in expectedIds)
        {
            Assert.Equal(expectedVersion, documented[id]);
        }

        RequireTerms(origins,
            $".NET-{expectedVersion}-Distribution",
            $"Microsoft.NETCore.App.Runtime.win-x64-{expectedVersion}-*",
            $"Microsoft.WindowsDesktop.App.Runtime.win-x64-{expectedVersion}-LICENSE.txt");
    }

    [Fact]
    public void InstallerUndCiNehmenDieselbenHinweiseAuf()
    {
        string files = Read("installer", "EInvoiceSender.Setup", "Files.wxs");
        string package = Read("installer", "EInvoiceSender.Setup", "Package.wxs");
        string workflow = Read(".github", "workflows", "ci.yml");

        RequireTerms(files,
            @"..\Drittanbieterhinweise\README.md",
            "Name=\"Drittanbieterhinweise.md\"",
            @"..\Drittanbieterhinweise\Lizenzen\**",
            "DrittanbieterLizenzordner");
        RequireTerms(package, "DrittanbieterHinweisordner", "DrittanbieterLizenzordner");
        RequireTerms(workflow,
            "installer/Drittanbieterhinweise/README.md",
            "installer/Drittanbieterhinweise/Lizenzen",
            "artifacts/publish/win-x64/Drittanbieterhinweise.md");

        int notices = workflow.IndexOf("Drittanbieterhinweise beilegen", StringComparison.Ordinal);
        int archive = workflow.IndexOf("Portable ZIP erzeugen", StringComparison.Ordinal);
        Assert.True(notices >= 0 && archive > notices,
            "Die CI muss die Hinweise vor dem portablen ZIP beilegen.");
    }

    [Fact]
    public void NeueRuntimeabhängigkeitOhneHinweisWirdAbgelehnt()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            AssertPackageSetsEqual(["Vorhanden", "Neu.Runtime"], ["Vorhanden"]));

        Assert.Contains("undokumentiert: Neu.Runtime", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EntfernteRuntimeabhängigkeitImHinweisWirdAbgelehnt()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            AssertPackageSetsEqual(["Vorhanden"], ["Vorhanden", "Alt.Runtime"]));

        Assert.Contains("nicht ausgeliefert: Alt.Runtime", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FehlendesPdfPigInAnwenderhinweisenWirdAbgelehnt()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            RequireTerms("PDFsharp und PDFtoImage", "PdfPig"));

        Assert.Contains("PdfPig", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VeralteteRuntimeangabeWirdAbgelehnt()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ForbidTerms("Aktueller Bestandteil: Serilog", ObsoleteRuntimeNames));

        Assert.Contains("Serilog", error.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> ProductProjects()
    {
        yield return Path.Combine(TestPaths.RepositoryRoot,
            "src", "EInvoiceSender.App", "EInvoiceSender.App.csproj");
        yield return Path.Combine(TestPaths.RepositoryRoot,
            "src", "EInvoiceSender.Core", "EInvoiceSender.Core.csproj");
    }

    private static IEnumerable<string> ReadPackageReferences(string project)
        => XDocument.Load(project)
            .Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))!;

    private static Dictionary<string, string> ReadCentralPackageVersions()
        => XDocument.Load(Path.Combine(TestPaths.RepositoryRoot, "Directory.Packages.props"))
            .Descendants()
            .Where(e => e.Name.LocalName == "PackageVersion")
            .ToDictionary(
                e => e.Attribute("Include")!.Value,
                e => e.Attribute("Version")!.Value,
                StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, RuntimePackage> ReadRuntimePackages()
    {
        string document = Read("docs", "THIRD-PARTY-NOTICES.md");
        string table = Between(document,
            "<!-- runtime-packages:start -->",
            "<!-- runtime-packages:end -->");

        return table
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.TrimStart().StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .Select(cells => new RuntimePackage(
                cells[0].Trim('`'),
                cells[1],
                cells[2],
                cells[4].Equals("ja", StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ReadRuntimePacks()
    {
        string document = Read("docs", "THIRD-PARTY-NOTICES.md");
        string table = Between(document,
            "<!-- runtime-packs:start -->",
            "<!-- runtime-packs:end -->");

        return table
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.TrimStart().StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .ToDictionary(
                cells => cells[0].Trim('`'),
                cells => cells[1],
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadProductRuntimeFrameworkVersion()
    {
        XDocument project = XDocument.Load(Path.Combine(TestPaths.RepositoryRoot,
            "src", "EInvoiceSender.App", "EInvoiceSender.App.csproj"));
        XElement runtimeVersion = Assert.Single(project.Descendants(),
            element => element.Name.LocalName == "RuntimeFrameworkVersion");
        string version = runtimeVersion.Value.Trim();
        string condition = runtimeVersion.Parent?.Attribute("Condition")?.Value ?? string.Empty;

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        RequireTerms(condition,
            "$(Configuration)",
            "Release",
            "$(RuntimeIdentifier)",
            "win-x64",
            "$(SelfContained)",
            "true");
        return version;
    }

    private static IEnumerable<string> CurrentNoticeFiles()
    {
        yield return Path.Combine(TestPaths.RepositoryRoot, "docs", "THIRD-PARTY-NOTICES.md");
        yield return Path.Combine(TestPaths.RepositoryRoot,
            "installer", "Drittanbieterhinweise", "README.md");
        yield return Path.Combine(TestPaths.RepositoryRoot,
            "installer", "EInvoiceSender.Setup", "Lizenzhinweise.rtf");
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine([TestPaths.RepositoryRoot, .. parts]));

    private static string Between(string value, string start, string end)
    {
        int startIndex = value.IndexOf(start, StringComparison.Ordinal);
        int endIndex = value.IndexOf(end, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex,
            $"Die technischen Tabellenmarker {start} und {end} fehlen oder stehen in falscher Reihenfolge.");
        return value[(startIndex + start.Length)..endIndex];
    }

    private static void AssertPackageSetsEqual(
        IEnumerable<string> actualPackages,
        IEnumerable<string> documentedPackages)
    {
        HashSet<string> actual = new(actualPackages, StringComparer.OrdinalIgnoreCase);
        HashSet<string> documented = new(documentedPackages, StringComparer.OrdinalIgnoreCase);
        string[] missing = actual.Except(documented).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] stale = documented.Except(actual).Order(StringComparer.OrdinalIgnoreCase).ToArray();

        if (missing.Length > 0 || stale.Length > 0)
        {
            throw new InvalidOperationException(
                $"Runtimepakete weichen ab; undokumentiert: {string.Join(", ", missing)}; "
                + $"nicht ausgeliefert: {string.Join(", ", stale)}.");
        }
    }

    private static void RequireTerms(string content, params string[] required)
    {
        string[] missing = required
            .Where(term => !content.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Vorgesehene Hinweise fehlen: {string.Join(", ", missing)}.");
        }
    }

    private static void ForbidTerms(string content, params string[] forbidden)
    {
        string[] present = forbidden
            .Where(term => content.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (present.Length > 0)
        {
            throw new InvalidOperationException(
                $"Veraltete Runtimeangaben sind vorhanden: {string.Join(", ", present)}.");
        }
    }

    private sealed record RuntimePackage(string Id, string Kind, string Version, bool InDepsJson);
}

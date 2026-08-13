using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Verhindert, dass neben dem gemeinsamen Release-Skript erneut eine zweite
/// Paketierungslogik in der CI entsteht.
/// </summary>
public sealed class ReleasePackagingTests
{
    [Fact]
    public void CiVerwendetDenGemeinsamenReleasewegOhneEigenePaketierung()
    {
        string workflow = Read(".github", "workflows", "ci.yml");

        Assert.Contains("./build/Test-ReleasePackaging.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("./build/Build-Release.ps1", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Compress-Archive", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-FileHash", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SHA256SUMS.txt", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Drittanbieterhinweise beilegen", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-ChildItem -Recurse -Filter *.msi", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Test-InstallerMetadata.ps1", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleasewegOrchestriertPublishInstallerUndGeprüftePromotion()
    {
        string release = Read("build", "Build-Release.ps1");

        Assert.Contains("Publish.ps1", release, StringComparison.Ordinal);
        Assert.Contains("Build-Installer.ps1", release, StringComparison.Ordinal);
        Assert.Contains("release-staging", release, StringComparison.Ordinal);
        Assert.Contains("Assert-PortableArchive", release, StringComparison.Ordinal);
        Assert.Contains("Write-ReleaseChecksums", release, StringComparison.Ordinal);
        Assert.Contains("Assert-ReleaseChecksums", release, StringComparison.Ordinal);
        Assert.Contains("Move-Item", release, StringComparison.Ordinal);
    }

    [Fact]
    public void PflichtartefaktnamenSindImReleasewegEindeutigDefiniert()
    {
        string release = Read("build", "Build-Release.ps1");

        Assert.Equal(1, Count(release, "'BorstWerk-E-Rechnung-Setup.msi'"));
        Assert.Equal(1, Count(release, "'BorstWerk-E-Rechnung-portable-win-x64.zip'"));
        Assert.Contains("$releaseArtifacts = @($msiName, $zipName)", release, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerSkriptEnthältKeineReleaseZipOderShaLogikMehr()
    {
        string installer = Read("build", "Build-Installer.ps1");

        Assert.Contains("Test-InstallerMetadata.ps1", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("Compress-Archive", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SHA256SUMS", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifacts' 'release", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-ChildItem", installer, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine([TestPaths.RepositoryRoot, .. parts]));

    private static int Count(string text, string value)
        => text.Split(value, StringSplitOptions.None).Length - 1;
}

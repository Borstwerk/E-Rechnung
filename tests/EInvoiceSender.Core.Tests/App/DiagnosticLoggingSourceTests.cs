using System.Text.RegularExpressions;
using EInvoiceSender.Core.Diagnostics;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Zusätzliche Leitplanken für neue Logevents. Der maßgebliche
/// Datenschutzbeleg bleibt der Test der tatsächlich geschriebenen Dateien.
/// </summary>
public sealed partial class DiagnosticLoggingSourceTests
{
    private static readonly HashSet<string> ForbiddenPlaceholderNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "InvoiceNumber", "FileName", "FilePath", "Path", "Target", "Checksum",
        "Hash", "Email", "Iban", "Bic", "VatId", "TaxNumber", "Content",
        "Xml", "PdfText", "Message", "Data", "JavaExecutable",
    };

    [Fact]
    public void KeinLoggerTemplateBesitztEinVerdächtigesNutzdatenfeld()
    {
        var violations = new List<string>();

        foreach (string path in ProjectFiles.With(".cs"))
        {
            string source = File.ReadAllText(path);

            foreach (Match loggerMessage in LoggerMessageAttribute().Matches(source))
            {
                foreach (Match placeholder in TemplatePlaceholder().Matches(loggerMessage.Value))
                {
                    string name = placeholder.Groups[1].Value;

                    if (ForbiddenPlaceholderNames.Contains(name))
                    {
                        violations.Add($"{ProjectFiles.Relative(path)}: {{{name}}}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Verdächtige Loggerfelder gefunden:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void PersistenterProviderLiestKeineRohenExceptiontexteOderQuelldateien()
    {
        string provider = Source("src", "EInvoiceSender.Core", "Diagnostics",
            "LocalFileLoggerProvider.cs");
        string formatter = Source("src", "EInvoiceSender.Core", "Diagnostics",
            "DiagnosticExceptionFormatter.cs");

        Assert.DoesNotContain("exception.Message", provider, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.ToString", provider, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.Data", provider, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.Message", formatter, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.ToString", formatter, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.Data", formatter, StringComparison.Ordinal);
        Assert.Contains("new StackTrace(exception, false)", formatter, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFileName", formatter, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFileLineNumber", formatter, StringComparison.Ordinal);
    }

    [Fact]
    public void KeinProduktiverLogaufrufErhältRohenExceptiontextOderNutzdaten()
    {
        string productSource = string.Join('\n',
            ProjectFiles.With(".cs")
                .Where(path => ProjectFiles.Relative(path).StartsWith(
                    "src" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotMatch(RawExceptionLogArgument(), productSource);
        Assert.DoesNotMatch(ForbiddenDataLogArgument(), productSource);
    }

    [Fact]
    public void DiagnosepfadGrenzenUndUiVerwendenDieZentralenDefinitionen()
    {
        Assert.Equal(10, DiagnosticLogOptions.DefaultMaxCompletedFiles);
        Assert.Equal(1024L * 1024L, DiagnosticLogOptions.DefaultMaxBytesPerSession);

        string app = Source("src", "EInvoiceSender.App", "App.xaml.cs");
        string about = Source("src", "EInvoiceSender.App", "Views", "Dialogs",
            "AboutWindow.xaml.cs");
        string path = Source("src", "EInvoiceSender.Core", "Diagnostics",
            "DiagnosticLogDirectory.cs");

        Assert.Contains("DiagnosticLogDirectory.CreateDefault()", app, StringComparison.Ordinal);
        Assert.Contains("DiagnosticLogDirectory diagnosticLogDirectory", about,
            StringComparison.Ordinal);
        Assert.Contains("_diagnosticLogDirectory.DirectoryPath", about, StringComparison.Ordinal);
        Assert.Contains("Environment.SpecialFolder.LocalApplicationData", path,
            StringComparison.Ordinal);
        Assert.Contains("ApplicationDirectoryName = \"EInvoiceSender\"", path,
            StringComparison.Ordinal);
        Assert.Contains("DiagnosisDirectoryName = \"Diagnose\"", path,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnoseimplementierungBesitztKeineNetzwerkOderUploadlogik()
    {
        string diagnosticsDirectory = Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.Core", "Diagnostics");
        string source = string.Join('\n',
            Directory.EnumerateFiles(diagnosticsDirectory, "*.cs")
                .Select(File.ReadAllText));

        string[] forbidden =
        [
            "System.Net", "HttpClient", "Socket", "WebRequest", "Upload", "Telemetry",
            "ApplicationInsights",
        ];

        foreach (string value in forbidden)
        {
            Assert.DoesNotContain(value, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Source(params string[] parts)
        => File.ReadAllText(Path.Combine([TestPaths.RepositoryRoot, .. parts]));

    [GeneratedRegex(@"\[LoggerMessage\([\s\S]*?\)\]", RegexOptions.CultureInvariant)]
    private static partial Regex LoggerMessageAttribute();

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TemplatePlaceholder();

    [GeneratedRegex(
        @"Log[A-Za-z0-9_]*\([^;]*(?:exception|ex)\.(?:Message|Data|ToString\s*\()",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RawExceptionLogArgument();

    [GeneratedRegex(
        @"Log[A-Za-z0-9_]*\([^;]*(?:InvoiceNumber|Checksum|FileName|FilePath|JavaExecutable)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenDataLogArgument();
}

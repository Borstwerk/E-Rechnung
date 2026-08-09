using System.Text.RegularExpressions;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests;

/// <summary>
/// Prüft, dass Verweise auf Dokumentation ins Leere zeigen – oder eben nicht.
///
/// **Warum es diesen Test gibt:** Beim Umbau wurden mehrere Dokumente
/// zusammengelegt oder nach <c>docs/legacy</c> verschoben. Die Verweise in den
/// Klassenkommentaren blieben stehen. <c>EInvoiceService</c> nannte noch
/// „docs/SPECIFICATION.md, Abschnitt 8“, <c>CodeValues</c> eine
/// <c>docs/STANDARDS.md</c>, die es nie unter diesem Namen gab. Ein Verweis
/// auf eine Datei, die es nicht gibt, ist schlimmer als keiner: Er schickt den
/// Leser los und lässt ihn glauben, er habe etwas übersehen.
///
/// Der Übersetzer merkt so etwas nie – ein Kommentar ist für ihn Text.
/// </summary>
public sealed class DocumentationReferenceTests
{
    /// <summary>
    /// Erfasst <c>docs/NAME.md</c> und <c>docs/ordner/NAME.md</c>, in
    /// Kommentaren wie in Markdown-Verweisen.
    /// </summary>
    private static readonly Regex Reference = new(
        @"docs/[A-Za-z0-9._/-]+\.md", RegexOptions.Compiled);

    private static readonly string[] Extensions =
    [
        ".cs", ".xaml", ".md", ".ps1", ".sh", ".wxs", ".csproj", ".props", ".yml",
    ];

    [Fact]
    public void JederVerweisAufEinDokumentZeigtAufEineVorhandeneDatei()
    {
        string[] tot =
        [
            .. from path in Files()
               from match in Reference.Matches(File.ReadAllText(path)).Distinct()
               where !File.Exists(Path.Combine(TestPaths.RepositoryRoot, match.Value))
               select $"{ProjectFiles.Relative(path)} → {match.Value}",
        ];

        Assert.True(
            tot.Length == 0,
            "Diese Verweise zeigen auf Dateien, die es nicht gibt:\n" + string.Join("\n", tot));
    }

    /// <summary>
    /// Der Quelltext soll auf die geltende Dokumentation zeigen, nicht auf den
    /// abgelegten alten Stand. In den Dokumenten selbst ist ein ausdrücklicher
    /// Verweis nach <c>docs/legacy</c> dagegen in Ordnung – dort steht dann
    /// auch dabei, dass es sich um eine frühere Fassung handelt.
    /// </summary>
    [Fact]
    public void DerQuelltextVerweistNichtAufDenAbgelegtenStand()
    {
        string[] verweise =
        [
            .. from path in Files()
               where Path.GetExtension(path) is not ".md"
               from match in Reference.Matches(File.ReadAllText(path)).Distinct()
               where match.Value.StartsWith("docs/legacy/", StringComparison.Ordinal)
               select $"{ProjectFiles.Relative(path)} → {match.Value}",
        ];

        Assert.True(
            verweise.Length == 0,
            "Diese Stellen verweisen auf docs/legacy:\n" + string.Join("\n", verweise)
            + "\n\ndocs/legacy beschreibt eine Fassung, die es nicht mehr gibt.");
    }

    /// <summary>
    /// Ohne diese Prüfung wäre der Wächter oben wertlos: Findet die Suche keine
    /// Verweise, meldet sie, dass alles in Ordnung ist.
    /// </summary>
    [Fact]
    public void EsGibtÜberhauptVerweiseZuPrüfen()
    {
        int gefunden = Files().Sum(p => Reference.Count(File.ReadAllText(p)));

        Assert.True(gefunden > 10, $"Nur {gefunden} Verweise gefunden.");
    }

    /// <summary>
    /// Der Test selbst nennt tote Verweise als Beispiel im Kommentar. Er würde
    /// sich sonst selbst beanstanden.
    /// </summary>
    private static IEnumerable<string> Files()
        => ProjectFiles
            .With(Extensions)
            .Where(p => !p.EndsWith(nameof(DocumentationReferenceTests) + ".cs", StringComparison.Ordinal));
}

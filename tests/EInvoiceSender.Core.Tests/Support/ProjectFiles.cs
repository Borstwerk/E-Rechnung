namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Findet die Dateien des aktiven Projekts.
///
/// Mehrere Prüfungen lesen den Quelltext als Text – die Schreibweise deutscher
/// Wörter, die Verweise auf Dokumentation. Alle brauchen dieselbe Antwort auf
/// die Frage, was „aktiv“ heißt; sie steht deshalb an einer Stelle.
/// </summary>
public static class ProjectFiles
{
    /// <summary>
    /// Ausgeschlossen: Baustände, Fremdwerkzeuge und der abgelegte alte Stand.
    /// <c>docs/legacy</c> beschreibt eine Fassung, die es nicht mehr gibt; sie
    /// nachträglich umzuschreiben verfälschte nur die Aufzeichnung.
    /// </summary>
    private static readonly string[] ExcludedFolders =
    [
        "bin", "obj", ".git", "legacy", "artifacts", "tools", "packages", "TestResults",
    ];

    /// <summary>Alle aktiven Dateien mit einer der genannten Endungen.</summary>
    public static IEnumerable<string> With(params string[] extensions)
        => Directory
            .EnumerateFiles(TestPaths.RepositoryRoot, "*", SearchOption.AllDirectories)
            .Where(p => extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Where(p => !IsExcluded(p));

    /// <summary>Der Pfad ab dem Wurzelverzeichnis – so steht er in Meldungen.</summary>
    public static string Relative(string path)
        => Path.GetRelativePath(TestPaths.RepositoryRoot, path);

    private static bool IsExcluded(string path)
        => Relative(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => ExcludedFolders.Contains(part, StringComparer.OrdinalIgnoreCase));
}

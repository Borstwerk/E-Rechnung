namespace EInvoiceSender.Core.Tests;

/// <summary>
/// Findet die Repository-Wurzel, damit Tests auf abgelegte Dateien zugreifen
/// können, ohne einen absoluten Pfad zu kennen.
/// </summary>
public static class TestPaths
{
    /// <summary>
    /// Sucht aufwärts nach der Solutiondatei. Der Pfad stimmt damit
    /// unabhängig von Build-Konfiguration und Arbeitsverzeichnis.
    /// </summary>
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException(
                       "Repository-Wurzel nicht gefunden – EInvoiceSender.sln fehlt oberhalb von "
                       + AppContext.BaseDirectory);
        }
    }
}

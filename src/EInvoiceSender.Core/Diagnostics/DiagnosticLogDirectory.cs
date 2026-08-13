namespace EInvoiceSender.Core.Diagnostics;

/// <summary>
/// Berechnet den einen Diagnosepfad, den Provider und Oberfläche gemeinsam
/// verwenden. Bestehende Einstellungen und Firmendaten bleiben unangetastet.
/// </summary>
public sealed class DiagnosticLogDirectory
{
    private const string ApplicationDirectoryName = "EInvoiceSender";
    private const string DiagnosisDirectoryName = "Diagnose";

    /// <summary>Erstellt eine Pfadangabe für den übergebenen Ordner.</summary>
    public DiagnosticLogDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        DirectoryPath = directoryPath;
    }

    /// <summary>Der vollständige lokale Diagnosepfad.</summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Berechnet <c>%LOCALAPPDATA%\EInvoiceSender\Diagnose</c> über die
    /// Windows-/Laufzeit-API und nicht über eine eigene Umgebungsvariable.
    /// </summary>
    public static DiagnosticLogDirectory CreateDefault()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Das lokale Anwendungsverzeichnis ist auf diesem System nicht verfügbar.");
        }

        return new DiagnosticLogDirectory(Path.Combine(
            localApplicationData,
            ApplicationDirectoryName,
            DiagnosisDirectoryName));
    }

    /// <summary>
    /// Legt den Ordner bei Bedarf an. Ein Fehler bleibt lokal und darf weder
    /// die Anwendung noch den Aufruf des Infofensters beenden.
    /// </summary>
    public bool TryEnsureExists()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
                                          and not StackOverflowException)
        {
            return false;
        }
    }
}

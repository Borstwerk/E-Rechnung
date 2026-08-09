namespace EInvoiceSender.Core.Services;

/// <summary>
/// Zeitquelle. Über einen Port, damit Berichte und Dateinamen in Tests
/// reproduzierbar sind und kein statischer Zugriff auf <c>DateTime.Now</c> nötig ist.
/// </summary>
public interface IClock
{
    /// <summary>Aktueller Zeitpunkt einschließlich Zeitzonenversatz.</summary>
    DateTimeOffset Now { get; }
}

/// <summary>
/// Ergebnis eines Speichervorgangs.
/// </summary>
/// <param name="FullPath">Tatsächlich verwendeter Pfad.</param>
/// <param name="Sha256">Prüfsumme der geschriebenen Datei als Hexdarstellung.</param>
/// <param name="SizeInBytes">Größe der geschriebenen Datei.</param>
public sealed record StoredFile(string FullPath, string Sha256, long SizeInBytes);

/// <summary>
/// Schreibt Dateien in das Ausgabeverzeichnis.
///
/// Alle Schreibvorgänge sind atomar: erst in eine temporäre Datei im selben
/// Verzeichnis, dann verschieben. Eine vorhandene Datei wird nie stillschweigend
/// überschrieben.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Schreibt Daten in das Zielverzeichnis.
    /// </summary>
    /// <param name="directory">Zielverzeichnis. Muss vorhanden oder anlegbar sein.</param>
    /// <param name="fileName">Gewünschter Dateiname; wird sicherheitshalber bereinigt.</param>
    /// <param name="content">Zu schreibender Inhalt.</param>
    /// <param name="overwriteBehavior">Verhalten, wenn die Datei bereits existiert.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    Task<StoredFile> WriteAsync(
        string directory,
        string fileName,
        ReadOnlyMemory<byte> content,
        OverwriteBehavior overwriteBehavior = OverwriteBehavior.CreateNumberedCopy,
        CancellationToken cancellationToken = default);

    /// <summary>Berechnet die SHA-256-Prüfsumme einer vorhandenen Datei.</summary>
    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>Verhalten beim Schreiben, wenn die Zieldatei bereits existiert.</summary>
public enum OverwriteBehavior
{
    /// <summary>Bricht mit einem Fehler ab. Sicherste Einstellung.</summary>
    Fail,

    /// <summary>Legt eine nummerierte Kopie an: <c>Rechnung (2).pdf</c>.</summary>
    CreateNumberedCopy,

    /// <summary>
    /// Überschreibt. Nur zulässig, wenn der Benutzer das ausdrücklich
    /// bestätigt hat – niemals als Vorgabe.
    /// </summary>
    Overwrite,
}

/// <summary>Ergebnis eines externen Prozessaufrufs.</summary>
/// <param name="ExitCode">Rückgabewert des Prozesses.</param>
/// <param name="StandardOutput">Erfasste Standardausgabe.</param>
/// <param name="StandardError">Erfasste Fehlerausgabe.</param>
/// <param name="TimedOut">Wurde der Prozess wegen Zeitüberschreitung beendet?</param>
/// <param name="Duration">Laufzeit des Prozesses.</param>
public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    TimeSpan Duration);

/// <summary>
/// Führt externe Programme aus.
///
/// Argumente werden ausschließlich als Liste übergeben; es wird nie eine
/// Kommandozeile aus Zeichenketten zusammengesetzt. Jeder Aufruf hat ein
/// Zeitlimit, und stdout sowie stderr werden vollständig erfasst.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Führt ein Programm aus und wartet auf sein Ende.</summary>
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Öffnet Dateien und Ordner mit den Bordmitteln des Betriebssystems.
/// Nur die Oberfläche verwendet diesen Port; die Fachlogik nie.
/// </summary>
public interface IShellService
{
    /// <summary>Öffnet ein Verzeichnis im Explorer.</summary>
    Task OpenFolderAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>Öffnet eine Datei mit dem zugeordneten Programm.</summary>
    Task OpenFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Öffnet eine URI, etwa einen <c>mailto:</c>-Verweis.</summary>
    Task OpenUriAsync(Uri uri, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ein Arbeitsverzeichnis für genau einen Erzeugungsvorgang.
///
/// Alle Zwischendateien liegen darunter. Beim Verwerfen wird das gesamte
/// Verzeichnis gelöscht – auch bei einem Fehler oder einem Abbruch durch den
/// Benutzer.
/// </summary>
public interface ITemporaryWorkspace : IDisposable
{
    /// <summary>Vollständiger Pfad des Arbeitsverzeichnisses.</summary>
    string Path { get; }

    /// <summary>Schreibt eine Zwischendatei und liefert ihren Pfad.</summary>
    Task<string> WriteAsync(
        string fileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
}

/// <summary>Erzeugt Arbeitsverzeichnisse.</summary>
public interface ITemporaryWorkspaceFactory
{
    /// <summary>Legt ein neues, leeres Arbeitsverzeichnis an.</summary>
    ITemporaryWorkspace Create();
}

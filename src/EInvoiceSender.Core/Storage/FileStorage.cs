using System.Globalization;
using System.Security.Cryptography;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Storage;

/// <summary>
/// Schreibt Ergebnisdateien in das Ausgabeverzeichnis.
///
/// Sicherheitsvorgaben (docs/SECURITY.md, S3 und S4):
/// * **Atomar.** Geschrieben wird zuerst in eine temporaere Datei im
///   Zielverzeichnis, danach wird verschoben. Bricht der Vorgang ab, bleibt
///   entweder die alte Datei oder gar keine zurueck – niemals eine halb
///   geschriebene.
/// * **Kein stillschweigendes Ueberschreiben.** Vorgabe ist eine nummerierte
///   Kopie. Ueberschreiben nur, wenn der Aufrufer es ausdruecklich verlangt.
/// * **Kein Ausbrechen aus dem Zielverzeichnis.** Der Dateiname wird bereinigt
///   und der zusammengesetzte Pfad danach geprueft. Selbst eine Rechnungsnummer
///   wie "../../etc/passwd" kann so nichts ausserhalb des Ziels anlegen.
/// </summary>
public sealed partial class FileStorage : IFileStorage
{
    private readonly ILogger<FileStorage> _logger;

    /// <summary>Hoechstzahl der Versuche fuer eine nummerierte Kopie.</summary>
    private const int MaxNumberedAttempts = 999;

    public FileStorage(ILogger<FileStorage> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<StoredFile> WriteAsync(
        string directory,
        string fileName,
        ReadOnlyMemory<byte> content,
        OverwriteBehavior overwriteBehavior = OverwriteBehavior.CreateNumberedCopy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string targetDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(targetDirectory);

        string safeName = SanitizeFileName(fileName);
        string targetPath = ResolveTargetPath(targetDirectory, safeName, overwriteBehavior);

        // Die temporaere Datei liegt im selben Verzeichnis. Nur dann ist das
        // abschliessende Verschieben ein Umbenennen innerhalb desselben
        // Dateisystems und damit atomar.
        string temporaryPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileNameWithoutExtension(safeName)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, targetPath, overwrite: overwriteBehavior == OverwriteBehavior.Overwrite);
        }
        catch
        {
            DeleteQuietly(temporaryPath);
            throw;
        }

        string checksum = ComputeSha256(content.Span);
        string writtenName = Path.GetFileName(targetPath);

        LogFileWritten(_logger, writtenName, content.Length);

        return new StoredFile(targetPath, checksum, content.Length);
    }

    /// <inheritdoc />
    public async Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Berechnet die Pruefsumme eines Puffers.</summary>
    public static string ComputeSha256(ReadOnlySpan<byte> content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>
    /// Bereinigt den Dateinamen. Der Aufrufer darf hier einen beliebigen
    /// Vorschlag uebergeben; was hier herauskommt, ist immer ein einzelner,
    /// unter Windows zulaessiger Dateiname ohne Pfadanteile.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);

        string safeStem = SafeFileName.Sanitize(stem, SafeFileName.MaxSegmentLength);
        string safeExtension = string.IsNullOrEmpty(extension)
            ? string.Empty
            : "." + SafeFileName.Sanitize(extension.TrimStart('.'), 10);

        return safeStem + safeExtension;
    }

    /// <summary>
    /// Ermittelt den endgueltigen Zielpfad und prueft, dass er das
    /// Zielverzeichnis nicht verlaesst.
    /// </summary>
    private static string ResolveTargetPath(
        string targetDirectory, string safeName, OverwriteBehavior behavior)
    {
        string candidate = Path.GetFullPath(Path.Combine(targetDirectory, safeName));

        // Guertel und Hosentraeger: Nach der Bereinigung darf kein Pfadanteil
        // mehr enthalten sein, trotzdem wird das Ergebnis geprueft.
        if (!candidate.StartsWith(
                targetDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Der ermittelte Ausgabepfad liegt ausserhalb des Zielverzeichnisses.");
        }

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        switch (behavior)
        {
            case OverwriteBehavior.Overwrite:
                return candidate;

            case OverwriteBehavior.Fail:
                throw new IOException(
                    $"Die Datei '{safeName}' existiert bereits im Ausgabeverzeichnis.");

            case OverwriteBehavior.CreateNumberedCopy:
                for (int counter = 2; counter <= MaxNumberedAttempts; counter++)
                {
                    string numbered = Path.Combine(
                        targetDirectory, SafeFileName.AppendCounter(safeName, counter));

                    if (!File.Exists(numbered))
                    {
                        return numbered;
                    }
                }

                throw new IOException(
                    $"Es konnte kein freier Dateiname fuer '{safeName}' gefunden werden.");

            default:
                throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Die temporaere Datei bleibt liegen. Das ist unschoen, aber kein
            // Grund, den eigentlichen Fehler zu ueberdecken.
        }
        catch (UnauthorizedAccessException)
        {
            // Ebenso.
        }
    }

    [LoggerMessage(
        EventId = 5001, Level = LogLevel.Information,
        Message = "Datei geschrieben: {FileName}, {ByteCount} Bytes")]
    private static partial void LogFileWritten(ILogger logger, string fileName, int byteCount);
}

/// <summary>
/// Verwaltet ein Arbeitsverzeichnis fuer einen einzelnen Vorgang.
///
/// Alle temporaeren Dateien eines Erzeugungslaufs liegen darunter und werden
/// beim Verwerfen geloescht – auch bei einem Fehler oder einem Abbruch durch
/// den Benutzer (docs/SECURITY.md, S8).
/// </summary>
public sealed class TemporaryWorkspace : ITemporaryWorkspace
{
    private TemporaryWorkspace(string path) => Path = path;

    /// <summary>Vollstaendiger Pfad des Arbeitsverzeichnisses.</summary>
    public string Path { get; }

    /// <summary>Legt ein neues, leeres Arbeitsverzeichnis an.</summary>
    public static TemporaryWorkspace Create()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            string.Create(CultureInfo.InvariantCulture, $"EInvoiceSender-{Guid.NewGuid():N}"));

        Directory.CreateDirectory(path);

        return new TemporaryWorkspace(path);
    }

    /// <summary>Liefert einen Pfad innerhalb des Arbeitsverzeichnisses.</summary>
    public string GetFilePath(string fileName)
        => System.IO.Path.Combine(Path, SafeFileName.Sanitize(
            System.IO.Path.GetFileNameWithoutExtension(fileName))
            + System.IO.Path.GetExtension(fileName));

    /// <inheritdoc />
    public async Task<string> WriteAsync(
        string fileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        string path = GetFilePath(fileName);

        await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);

        return path;
    }

    /// <summary>Loescht das Arbeitsverzeichnis samt Inhalt.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Beim Aufraeumen darf nichts mehr schiefgehen koennen.
        }
        catch (UnauthorizedAccessException)
        {
            // Ebenso.
        }
    }
}

/// <summary>Erzeugt Arbeitsverzeichnisse im temporaeren Verzeichnis des Systems.</summary>
public sealed class TemporaryWorkspaceFactory : ITemporaryWorkspaceFactory
{
    /// <inheritdoc />
    public ITemporaryWorkspace Create() => TemporaryWorkspace.Create();
}

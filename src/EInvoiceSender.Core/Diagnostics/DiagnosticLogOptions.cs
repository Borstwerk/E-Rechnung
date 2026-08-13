using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Diagnostics;

/// <summary>
/// Die bewusst kleinen, festen Grenzen des lokalen Diagnoselogs.
/// </summary>
public sealed class DiagnosticLogOptions
{
    /// <summary>Vorgesehene Zahl abgeschlossener Sitzungslogs.</summary>
    public const int DefaultMaxCompletedFiles = 10;

    /// <summary>Vorgesehene Höchstgröße einer Sitzung: ein MiB.</summary>
    public const long DefaultMaxBytesPerSession = 1024L * 1024L;

    /// <summary>
    /// Erstellt die Optionen. Abweichende Grenzen sind ausschließlich für
    /// kleine, deterministische Tests vorgesehen.
    /// </summary>
    public DiagnosticLogOptions(
        string directoryPath,
        int maxCompletedFiles = DefaultMaxCompletedFiles,
        long maxBytesPerSession = DefaultMaxBytesPerSession,
        LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCompletedFiles, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytesPerSession, 1);

        DirectoryPath = directoryPath;
        MaxCompletedFiles = maxCompletedFiles;
        MaxBytesPerSession = maxBytesPerSession;
        MinimumLevel = minimumLevel;
    }

    /// <summary>Lokaler, benutzerbezogener Diagnoseordner.</summary>
    public string DirectoryPath { get; }

    /// <summary>Zahl der aufzubewahrenden abgeschlossenen Sitzungen.</summary>
    public int MaxCompletedFiles { get; }

    /// <summary>Maximale Zahl UTF-8-Bytes je Programmlauf.</summary>
    public long MaxBytesPerSession { get; }

    /// <summary>Kleinste persistierte Protokollstufe.</summary>
    public LogLevel MinimumLevel { get; }
}

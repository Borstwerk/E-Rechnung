using System.Diagnostics;
using System.IO;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.App.Services;

/// <summary>
/// Öffnet Dateien, Ordner und Verweise mit den Bordmitteln von Windows.
///
/// Der einzige Ort im Programm, an dem <c>UseShellExecute = true</c> steht.
/// Übergeben werden ausschließlich Pfade, die die Anwendung selbst erzeugt
/// hat, beziehungsweise vom Benutzer ausgewählte Dateien – nie eine Zeichenkette
/// aus einer Rechnung oder aus fremdem XML.
/// </summary>
public sealed partial class WindowsShellService : IShellService
{
    private readonly ILogger<WindowsShellService> _logger;

    public WindowsShellService(ILogger<WindowsShellService> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public Task OpenFolderAsync(string directory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            LogTargetMissing(_logger, directory);

            return Task.CompletedTask;
        }

        return StartAsync(directory);
    }

    /// <inheritdoc />
    public Task OpenFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            LogTargetMissing(_logger, filePath);

            return Task.CompletedTask;
        }

        return StartAsync(filePath);
    }

    /// <inheritdoc />
    public Task OpenUriAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // Nur Schemata, die hier vorkommen dürfen. Damit kann ein präparierter
        // Wert kein beliebiges Programm starten.
        if (uri.Scheme is not ("mailto" or "http" or "https" or "file"))
        {
            LogSchemeRejected(_logger, uri.Scheme);

            return Task.CompletedTask;
        }

        return StartAsync(uri.ToString());
    }

    private Task StartAsync(string target)
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Kein zugeordnetes Programm oder Start verweigert. Das darf die
            // Anwendung nicht beenden – die Datei liegt ja bereits vor.
            LogStartFailed(_logger, ex.GetType().Name);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Warning, Message = "Ziel nicht gefunden: {Target}")]
    private static partial void LogTargetMissing(ILogger logger, string target);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning, Message = "Schema abgelehnt: {Scheme}")]
    private static partial void LogSchemeRejected(ILogger logger, string scheme);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Warning, Message = "Öffnen fehlgeschlagen ({Reason}).")]
    private static partial void LogStartFailed(ILogger logger, string reason);
}

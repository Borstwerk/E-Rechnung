using System.Diagnostics;
using System.Text;
using EInvoiceSender.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Infrastructure.Process;

/// <summary>
/// Fuehrt externe Programme aus.
///
/// Sicherheitsvorgaben (docs/SECURITY.md, S5), die hier eingehalten werden:
/// * Argumente ausschliesslich ueber <see cref="ProcessStartInfo.ArgumentList"/>.
///   Es wird nie eine Kommandozeile aus Zeichenketten zusammengesetzt, damit
///   Dateinamen mit Leerzeichen oder Anfuehrungszeichen nichts ausloesen koennen.
/// * <c>UseShellExecute = false</c> – keine Shell, also keine Shell-Sonderzeichen.
/// * Jeder Aufruf hat ein Zeitlimit. Laeuft es ab, wird der Prozessbaum beendet.
/// * stdout und stderr werden vollstaendig und nebenlaeufig gelesen. Wuerde nur
///   eine der beiden Leitungen gelesen, koennte der Kindprozess blockieren,
///   sobald die andere ihren Puffer fuellt.
/// * Ein Abbruch durch den Benutzer beendet den Prozess ebenfalls.
/// </summary>
public sealed partial class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout), timeout, "Ein Zeitlimit von null oder weniger ist nicht zulaessig.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        var stopwatch = Stopwatch.StartNew();

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                standardOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                standardError.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Der Prozess '{fileName}' konnte nicht gestartet werden.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Das Zeitlimit und der Benutzerabbruch werden zusammengefuehrt, damit
        // beide Wege denselben Aufraeumpfad nehmen.
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);

        bool timedOut = false;

        try
        {
            await process.WaitForExitAsync(combined.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutSource.IsCancellationRequested;

            KillQuietly(process);

            if (!timedOut)
            {
                // Der Benutzer hat abgebrochen – das ist kein Fehlerfall des
                // Werkzeugs und wird nach oben durchgereicht.
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // Nach WaitForExitAsync kann noch Ausgabe unterwegs sein.
        // WaitForExit ohne Argument wartet auf das Ende der Umleitungen.
        if (!timedOut)
        {
            process.WaitForExit();
        }

        stopwatch.Stop();

        int exitCode = timedOut ? -1 : process.ExitCode;

        LogProcessFinished(_logger, fileName, exitCode, stopwatch.ElapsedMilliseconds, timedOut);

        return new ProcessResult(
            ExitCode: exitCode,
            StandardOutput: standardOutput.ToString(),
            StandardError: standardError.ToString(),
            TimedOut: timedOut,
            Duration: stopwatch.Elapsed);
    }

    /// <summary>
    /// Beendet den Prozess samt Kindprozessen. Fehler dabei werden bewusst
    /// geschluckt: Der Prozess kann in der Zwischenzeit von selbst geendet sein,
    /// und dann ist das Ziel ohnehin erreicht.
    /// </summary>
    private static void KillQuietly(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Prozess bereits beendet.
        }
        catch (NotSupportedException)
        {
            // Auf dieser Plattform nicht unterstuetzt.
        }
        catch (SystemException)
        {
            // Win32Exception und Verwandte: Zugriff verweigert oder Prozess weg.
        }
    }

    [LoggerMessage(
        EventId = 3001, Level = LogLevel.Information,
        Message = "Externes Werkzeug {FileName} beendet: Exitcode {ExitCode}, {Milliseconds} ms, Zeitueberschreitung: {TimedOut}")]
    private static partial void LogProcessFinished(
        ILogger logger, string fileName, int exitCode, long milliseconds, bool timedOut);
}

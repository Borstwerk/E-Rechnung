using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Diagnostics;

/// <summary>
/// Kleiner lokaler Provider für ein begrenztes Sitzungslog. Er verwendet
/// keine Scopes, keine Hintergrundwarteschlange und keine Netzwerkfunktion.
/// Jeder interne Fehler schaltet ausschließlich das Logging ab.
/// </summary>
public sealed class LocalFileLoggerProvider : ILoggerProvider
{
    private const string FilePrefix = "diagnose-";
    private const string FilePattern = FilePrefix + "*.log";
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly object _sync = new();
    private readonly DiagnosticLogOptions _options;
    private readonly string _sessionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
    private Stream? _stream;
    private long _bytesWritten;
    private bool _disabled;

    /// <summary>Öffnet eine neue, zufällig benannte Sitzungsdatei.</summary>
    public LocalFileLoggerProvider(DiagnosticLogOptions options)
        : this(options, OpenSessionStream)
    {
    }

    internal LocalFileLoggerProvider(
        DiagnosticLogOptions options,
        Func<string, Stream> streamFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(streamFactory);

        TryInitialize(streamFactory);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => new LocalFileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            DisableWriterNoThrow();
        }
    }

    private static Stream OpenSessionStream(string path)
        => new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.WriteThrough);

    private void TryInitialize(Func<string, Stream> streamFactory)
    {
        try
        {
            Directory.CreateDirectory(_options.DirectoryPath);
            RotateCompletedFiles();

            string timestamp = DateTimeOffset.UtcNow.ToString(
                "yyyyMMdd-HHmmssfff",
                CultureInfo.InvariantCulture);
            string path = Path.Combine(
                _options.DirectoryPath,
                $"{FilePrefix}{timestamp}-{_sessionId}.log");

            Stream stream = streamFactory(path);

            if (!stream.CanWrite)
            {
                stream.Dispose();
                _disabled = true;
                return;
            }

            _stream = stream;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
                                          and not StackOverflowException)
        {
            _disabled = true;
            DisableWriterNoThrow();
        }
    }

    private void RotateCompletedFiles()
    {
        var completed = new List<FileInfo>();

        foreach (string path in Directory.EnumerateFiles(
                     _options.DirectoryPath,
                     FilePattern,
                     SearchOption.TopDirectoryOnly))
        {
            if (CanExclusivelyOpen(path))
            {
                completed.Add(new FileInfo(path));
            }
        }

        foreach (FileInfo stale in completed
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .Skip(_options.MaxCompletedFiles))
        {
            try
            {
                stale.Delete();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                                              and not StackOverflowException)
            {
                // Gesperrte, schreibgeschützte oder zwischenzeitlich entfernte
                // Dateien werden bewusst übersprungen. Rotation ist niemals
                // wichtiger als der normale Anwendungspfad.
            }
        }
    }

    private static bool CanExclusivelyOpen(string path)
    {
        try
        {
            using var candidate = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
                                          and not StackOverflowException)
        {
            return false;
        }
    }

    private bool IsEnabled(LogLevel level)
        => !_disabled
           && level != LogLevel.None
           && level >= _options.MinimumLevel;

    private void Write<TState>(
        string categoryName,
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        string message;

        try
        {
            message = SingleLine(formatter(state, exception));
        }
        catch (Exception formatterException) when (formatterException is not OutOfMemoryException
                                                    and not StackOverflowException)
        {
            return;
        }

        string exceptionPart = exception is null
            ? string.Empty
            : " | " + DiagnosticExceptionFormatter.Format(exception);
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:O} | {LevelCode(level)} | event={eventId.Id} | category={SingleLine(categoryName)} | session={_sessionId} | {message}{exceptionPart}\n");
        byte[] bytes = Utf8WithoutBom.GetBytes(line);

        lock (_sync)
        {
            if (_disabled || _stream is null)
            {
                return;
            }

            if (_bytesWritten + bytes.LongLength > _options.MaxBytesPerSession)
            {
                DisableWriterNoThrow();
                return;
            }

            try
            {
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
                _bytesWritten += bytes.LongLength;
            }
            catch (Exception writeException) when (writeException is not OutOfMemoryException
                                                   and not StackOverflowException)
            {
                DisableWriterNoThrow();
            }
        }
    }

    private void DisableWriterNoThrow()
    {
        _disabled = true;
        Stream? stream = _stream;
        _stream = null;

        if (stream is null)
        {
            return;
        }

        try
        {
            stream.Dispose();
        }
        catch (Exception disposeException) when (disposeException is not OutOfMemoryException
                                                 and not StackOverflowException)
        {
            // Auch beim Beenden bleibt ein Loggerfehler folgenlos.
        }
    }

    private static string SingleLine(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ');

    private static string LevelCode(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "NON",
    };

    private sealed class LocalFileLogger(
        LocalFileLoggerProvider provider,
        string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => EmptyScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            provider.Write(categoryName, logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

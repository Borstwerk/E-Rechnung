using System.Text;
using EInvoiceSender.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EInvoiceSender.Core.Tests.Diagnostics;

/// <summary>
/// Prüft Dateiformat, harte Grenzen und vor allem die Fehlerfreiheit des
/// Diagnoseproviders. Ein Logger darf niemals zum neuen Anwendungsfehler
/// werden.
/// </summary>
public sealed class LocalFileLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"diagnostic-log-tests-{Guid.NewGuid():N}");

    [Fact]
    public void ErlaubteTechnischeDatenWerdenAlsEinzeiligesSitzungslogGeschrieben()
    {
        using var provider = Provider();
        ILogger logger = provider.CreateLogger("Synthetic.Workflow");

        Write(logger, LogLevel.Information, 6123, "Route Direct, Dauer 42 ms");

        string text = ReadOnlyLog();

        Assert.Contains(" | INF | event=6123 | category=Synthetic.Workflow | session=", text,
            StringComparison.Ordinal);
        Assert.Contains("Route Direct, Dauer 42 ms", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void ExceptionEnthältNurTypketteUndMethodenstack()
    {
        const string secretMessage = "PRIVACY-EXCEPTION-MESSAGE-9F3";
        const string secretData = "PRIVACY-EXCEPTION-DATA-9F3";
        const string secretPath = @"C:\Users\PRIVACY-USER-9F3\PRIVACY-INVOICE-9F3.pdf";

        using var provider = Provider();
        ILogger logger = provider.CreateLogger("Synthetic.Exception");
        Exception exception = CaptureException(secretMessage, secretData, secretPath);

        Write(logger, LogLevel.Error, 6999, "Technischer Vorgang fehlgeschlagen", exception);

        string text = ReadOnlyLog();

        Assert.Contains("System.InvalidOperationException>System.IO.IOException", text,
            StringComparison.Ordinal);
        Assert.Contains(nameof(ThrowNested), text, StringComparison.Ordinal);
        Assert.DoesNotContain(secretMessage, text, StringComparison.Ordinal);
        Assert.DoesNotContain(secretData, text, StringComparison.Ordinal);
        Assert.DoesNotContain(secretPath, text, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(LocalFileLoggerTests) + ".cs", text, StringComparison.Ordinal);
        Assert.DoesNotContain(":line ", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Zeile ", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GrößenlimitBeendetNurDasLogging()
    {
        const long maximumBytes = 320;
        using var provider = Provider(maxBytesPerSession: maximumBytes);
        ILogger logger = provider.CreateLogger("Synthetic.Size");

        for (int i = 0; i < 100; i++)
        {
            Write(logger, LogLevel.Information, 6200,
                $"Technisches Ereignis {i} mit fester Testlänge");
        }

        var file = new FileInfo(Assert.Single(LogFiles()));
        Assert.InRange(file.Length, 1, maximumBytes);

        // Auch nach Erreichen der Grenze bleibt der Aufrufer unbeeinträchtigt.
        Exception? failure = Record.Exception(
            () => Write(logger, LogLevel.Error, 6201,
                "Weiteres technisches Ereignis nach dem Limit"));
        Assert.Null(failure);
    }

    [Fact]
    public void RotationBehältNurDieVorgeseheneZahlAbgeschlossenerLogs()
    {
        Directory.CreateDirectory(_directory);

        for (int i = 0; i < 5; i++)
        {
            string path = Path.Combine(_directory, $"diagnose-2026010{i}-000000-old{i}.log");
            File.WriteAllText(path, "alt", Encoding.UTF8);
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, i + 1, 0, 0, 0, DateTimeKind.Utc));
        }

        using var provider = Provider(maxCompletedFiles: 2);
        Write(provider.CreateLogger("Synthetic.Rotation"), LogLevel.Information, 6300,
            "Neue Sitzung");

        string[] files = LogFiles();
        Assert.Equal(3, files.Length); // zwei abgeschlossene plus die aktive Sitzung
        Assert.Contains(files, path => path.Contains("old4", StringComparison.Ordinal));
        Assert.Contains(files, path => path.Contains("old3", StringComparison.Ordinal));
    }

    [Fact]
    public void GesperrteAktiveDateiWirdBeiRotationÜbersprungen()
    {
        Directory.CreateDirectory(_directory);
        string activePath = Path.Combine(_directory, "diagnose-20260101-000000-active.log");

        using var active = new FileStream(
            activePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);

        using var provider = Provider(maxCompletedFiles: 1);
        Write(provider.CreateLogger("Synthetic.Rotation"), LogLevel.Information, 6301,
            "Parallele Sitzung");

        Assert.True(File.Exists(activePath));
        Assert.Equal(2, LogFiles().Length);
    }

    [Fact]
    public void NichtBeschreibbaresDiagnosezielBeeinträchtigtDenAufruferNicht()
    {
        Directory.CreateDirectory(_directory);
        string regularFile = Path.Combine(_directory, "kein-ordner");
        File.WriteAllText(regularFile, "Datei statt Verzeichnis", Encoding.UTF8);

        Exception? failure = Record.Exception(() =>
        {
            using var provider = new LocalFileLoggerProvider(new DiagnosticLogOptions(regularFile));
            Write(provider.CreateLogger("Synthetic.Unavailable"), LogLevel.Error, 6400,
                "Testereignis");
        });

        Assert.Null(failure);
        Assert.Equal("Datei statt Verzeichnis", File.ReadAllText(regularFile, Encoding.UTF8));
    }

    [Fact]
    public void WriterfehlerSchaltetDenProviderOhneRekursionAb()
    {
        Directory.CreateDirectory(_directory);
        var stream = new ThrowingWriteStream();

        using var provider = new LocalFileLoggerProvider(
            new DiagnosticLogOptions(_directory),
            _ => stream);
        ILogger logger = provider.CreateLogger("Synthetic.WriterFailure");

        Exception? failure = Record.Exception(() =>
        {
            Write(logger, LogLevel.Error, 6500, "Erster Schreibversuch");
            Write(logger, LogLevel.Error, 6501, "Zweiter Schreibversuch");
        });

        Assert.Null(failure);
        Assert.Equal(1, stream.WriteAttempts);
    }

    [Fact]
    public void ParalleleProviderVerwendenUnabhängigeSitzungsdateien()
    {
        Directory.CreateDirectory(_directory);

        Exception? failure = Record.Exception(() => Parallel.For(0, 8, index =>
        {
            using var provider = Provider();
            Write(
                provider.CreateLogger("Synthetic.Parallel"),
                LogLevel.Information,
                6600,
                $"Parallele technische Sitzung {index}");
        }));

        Assert.Null(failure);
        Assert.Equal(8, LogFiles().Length);
        Assert.All(LogFiles(), path => Assert.NotEmpty(File.ReadAllText(path, Encoding.UTF8)));
    }

    private LocalFileLoggerProvider Provider(
        int maxCompletedFiles = DiagnosticLogOptions.DefaultMaxCompletedFiles,
        long maxBytesPerSession = DiagnosticLogOptions.DefaultMaxBytesPerSession)
        => new(new DiagnosticLogOptions(_directory, maxCompletedFiles, maxBytesPerSession));

    private static void Write(
        ILogger logger,
        LogLevel level,
        int eventId,
        string message,
        Exception? exception = null)
        => logger.Log(
            level,
            new EventId(eventId),
            message,
            exception,
            static (state, _) => state);

    private string ReadOnlyLog()
    {
        using var stream = new FileStream(
            Assert.Single(LogFiles()),
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private string[] LogFiles()
        => Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "diagnose-*.log")
            : [];

    private static Exception CaptureException(string message, string data, string path)
    {
        try
        {
            ThrowNested(message, data, path);
            throw new InvalidOperationException("Unerreichbarer Testpfad");
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void ThrowNested(string message, string data, string path)
    {
        var inner = new IOException($"{message} in {path}");
        inner.Data["sensitive"] = data;
        throw new InvalidOperationException($"{message}: {path}", inner);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufräumen darf einen Testlauf nicht scheitern lassen.
        }
    }

    private sealed class ThrowingWriteStream : Stream
    {
        public int WriteAttempts { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAttempts++;
            throw new IOException("Absichtlicher Writerfehler");
        }
    }
}

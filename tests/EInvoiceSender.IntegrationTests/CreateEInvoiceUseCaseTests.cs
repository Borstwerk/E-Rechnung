using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.UseCases;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Formats.Cii;
using EInvoiceSender.Infrastructure.PdfA;
using EInvoiceSender.Infrastructure.Storage;
using EInvoiceSender.TestSupport;
using EInvoiceSender.Validation.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Ende-zu-Ende-Tests des Gesamtablaufs.
///
/// Abgedeckt sind Erfolg, Warnung, Validierungsfehler, beschaedigte PDF,
/// nicht eingebettete Schrift, Zeitueberschreitung eines externen Werkzeugs und
/// Abbruch durch den Benutzer.
///
/// In jedem Fehlerfall wird geprueft, dass **keine Ausgabedatei** entsteht und
/// das **Original unveraendert** bleibt. Eine halb fertige Datei waere schlimmer
/// als gar keine, weil der Anwender sie fuer gueltig halten koennte.
/// </summary>
public sealed class CreateEInvoiceUseCaseTests : IDisposable
{
    private readonly List<string> _temporaryPaths = [];
    private readonly CiiInvoiceWriter _writer = new();
    private readonly CiiInvoiceReader _reader = new();
    private readonly PdfAnalyzer _analyzer;
    private readonly string _outputDirectory;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 4, 1, 10, 0, 0, TimeSpan.FromHours(2));

    public CreateEInvoiceUseCaseTests()
    {
        _analyzer = new PdfAnalyzer(_reader, NullLogger<PdfAnalyzer>.Instance);
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"einvoice-out-{Guid.NewGuid():N}");
        _temporaryPaths.Add(_outputDirectory);
    }

    [Fact]
    public async Task ErfolgreicherDurchlaufErzeugtDateiBerichtUndPruefsumme()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        byte[] originalBytes = await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken);

        var progressMessages = new List<PipelineProgress>();
        var progress = new ImmediateProgress<PipelineProgress>(progressMessages.Add);

        CreateEInvoiceResult result = await BuildUseCase().ExecuteAsync(
            Request(source), progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.NotNull(result.OutputFile);
        Assert.True(File.Exists(result.OutputFile.FullPath));

        // Pruefsumme, Zeitpunkt, Standard und Profil sind gesetzt.
        Assert.Equal(64, result.OutputFile.Sha256.Length);
        Assert.Equal(FixedNow, result.CreatedAt);
        Assert.Equal(CiiConstants.ProfileEn16931, result.ProfileId);
        Assert.Contains("EN 16931", result.StandardDescription, StringComparison.Ordinal);

        // Die Pruefsumme muss zur tatsaechlich geschriebenen Datei passen.
        byte[] written = await File.ReadAllBytesAsync(
            result.OutputFile.FullPath, TestContext.Current.CancellationToken);
        Assert.Equal(FileStorage.ComputeSha256(written), result.OutputFile.Sha256);

        // Beide Berichte liegen vor.
        Assert.NotNull(result.ReportJsonFile);
        Assert.NotNull(result.ReportTextFile);

        string json = await File.ReadAllTextAsync(
            result.ReportJsonFile.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains(result.OutputFile.Sha256, json, StringComparison.Ordinal);
        Assert.Contains("\"profil\"", json, StringComparison.Ordinal);

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains("SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("Pruefwerkzeuge", text, StringComparison.Ordinal);

        // Das Original bleibt unveraendert.
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));

        // Alle neun Schritte wurden gemeldet.
        Assert.Equal(9, result.CompletedSteps.Count);
        Assert.Contains(progressMessages, m => m.State == StepState.Running);
        Assert.All(result.CompletedSteps, s => Assert.NotEqual(StepState.Failed, s.State));
    }

    [Fact]
    public async Task OhneBestaetigungWirdNichtsErzeugt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase().ExecuteAsync(
            Request(source) with { ContentMatchConfirmed = false },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-001");

        // Es darf nicht einmal ein Ausgabeverzeichnis mit Resten entstehen.
        Assert.False(
            Directory.Exists(_outputDirectory) && Directory.GetFiles(_outputDirectory).Length > 0);
    }

    [Fact]
    public async Task FehlerhafteRechnungsdatenVerhindernDieErzeugung()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        Invoice invalid = BaseInvoice() with { InvoiceNumber = "  " };

        CreateEInvoiceResult result = await BuildUseCase().ExecuteAsync(
            Request(source) with { Invoice = invalid },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-DOC-001");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.ValidateInvoiceData, failed.Step);
    }

    [Fact]
    public async Task NichtEingebetteteSchriftFuehrtZumAbbruchImPreflight()
    {
        string source = TempPdf(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        CreateEInvoiceResult result = await BuildUseCase().ExecuteAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-PRE-011");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.Preflight, failed.Step);
    }

    [Fact]
    public async Task BeschaedigtePdfFuehrtZumAbbruch()
    {
        string source = TempPdf(TestPdfFactory.CreateDamagedPdf());

        CreateEInvoiceResult result = await BuildUseCase().ExecuteAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.True(result.Report.HasErrors);
    }

    [Fact]
    public async Task BereitsHybridePdfBrauchtEineAusdrueckicheBestaetigung()
    {
        // Zuerst eine fertige E-Rechnung erzeugen ...
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        CreateEInvoiceResult first = await BuildUseCase().ExecuteAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, Describe(first));

        // ... und diese ohne Bestaetigung erneut verarbeiten.
        CreateEInvoiceResult second = await BuildUseCase().ExecuteAsync(
            Request(first.OutputFile!.FullPath),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(second.Succeeded);
        Assert.Contains(second.Report.Findings, f => f.RuleId == "APP-USE-002");

        // Mit Bestaetigung geht es durch.
        CreateEInvoiceResult third = await BuildUseCase().ExecuteAsync(
            Request(first.OutputFile.FullPath) with { ExistingInvoiceReplacementConfirmed = true },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.Succeeded, Describe(third));
    }

    [Fact]
    public async Task ZeitueberschreitungDesExternenWerkzeugsVerhindertDieAusgabe()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.TimesOut))
            .ExecuteAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-EXT-003");

        // Kein Rest im Ausgabeverzeichnis.
        Assert.True(
            !Directory.Exists(_outputDirectory) || Directory.GetFiles(_outputDirectory).Length == 0);
    }

    [Fact]
    public async Task BeanstandungDurchDenExternenValidatorVerhindertDieAusgabe()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.ReportsError))
            .ExecuteAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.ExternalValidation, failed.Step);
    }

    [Fact]
    public async Task FehlenderExternerValidatorWirdImBerichtAusgewiesen()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.NotAvailable))
            .ExecuteAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        // Die Datei entsteht, aber der Bericht sagt deutlich, dass die
        // Gegenpruefung nicht stattgefunden hat.
        Assert.True(result.Succeeded, Describe(result));
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-030");

        ValidatorInfo info = Assert.Single(result.Validators);
        Assert.False(info.WasExecuted);

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains("NICHT AUSGEFUEHRT", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BenutzerabbruchHinterlaesstKeineDatei()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        using var cts = new CancellationTokenSource();

        // Beim ersten Fortschrittsereignis abbrechen.
        var progress = new ImmediateProgress<PipelineProgress>(_ => cts.Cancel());

        CreateEInvoiceResult result = await BuildUseCase()
            .ExecuteAsync(Request(source), progress, cts.Token);

        Assert.False(result.Succeeded);
        Assert.True(result.Canceled);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-090");

        Assert.True(
            !Directory.Exists(_outputDirectory) || Directory.GetFiles(_outputDirectory).Length == 0);
    }

    [Fact]
    public async Task VorhandeneDateiWirdNichtStillUeberschrieben()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult first = await BuildUseCase().ExecuteAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, Describe(first));

        CreateEInvoiceResult second = await BuildUseCase().ExecuteAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(second.Succeeded, Describe(second));

        Assert.NotEqual(first.OutputFile!.FullPath, second.OutputFile!.FullPath);
        Assert.Contains("(2)", second.OutputFile.FullPath, StringComparison.Ordinal);
        Assert.True(File.Exists(first.OutputFile.FullPath));
    }

    [Fact]
    public async Task TemporaereDateienWerdenAuchImFehlerfallEntfernt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        string[] before = Directory.GetDirectories(Path.GetTempPath(), "EInvoiceSender-*");

        await BuildUseCase(new StubValidator("Testvalidator", StubBehavior.ReportsError))
            .ExecuteAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        string[] after = Directory.GetDirectories(Path.GetTempPath(), "EInvoiceSender-*");

        Assert.Equal(before.Length, after.Length);
    }

    // ------------------------------------------------------------------ Aufbau

    private CreateEInvoiceUseCase BuildUseCase(params IExternalDocumentValidator[] validators)
        => new(
            new PdfPreflightService(_analyzer, NullLogger<PdfPreflightService>.Instance),
            new En16931RuleValidator(new FixedClock(FixedNow)),
            _writer,
            _reader,
            new PdfAInvoiceComposer(_analyzer, NullLogger<PdfAInvoiceComposer>.Instance),
            _analyzer,
            new FileStorage(NullLogger<FileStorage>.Instance),
            new TemporaryWorkspaceFactory(),
            new StubClock(FixedNow),
            validators,
            NullLogger<CreateEInvoiceUseCase>.Instance);

    private CreateEInvoiceRequest Request(string sourcePath)
        => new(
            SourcePdfPath: sourcePath,
            Invoice: BaseInvoice(),
            ContentMatchConfirmed: true,
            OutputDirectory: _outputDirectory);

    private static Invoice BaseInvoice()
        => InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice;

    private static string Describe(CreateEInvoiceResult result)
        => string.Join(
            " | ",
            result.Report.Findings
                .Where(f => f.Severity == FindingSeverity.Error)
                .Select(f => $"{f.RuleId}: {f.Message} [{f.TechnicalDetail}]"));

    private string TempPdf(byte[] content)
    {
        string path = TestPdfFactory.WriteToTempFile(content);
        _temporaryPaths.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (string path in _temporaryPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Aufraeumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

/// <summary>Zeitquelle mit festem Wert.</summary>
internal sealed class StubClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now => now;
}

/// <summary>Zeitquelle mit festem Wert fuer den Regelvalidator.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Verhalten des Ersatzvalidators im Test.</summary>
internal enum StubBehavior
{
    /// <summary>Meldet ein bestandenes Ergebnis.</summary>
    Passes,

    /// <summary>Beanstandet die Datei.</summary>
    ReportsError,

    /// <summary>Meldet eine Zeitueberschreitung.</summary>
    TimesOut,

    /// <summary>Ist auf diesem System nicht eingerichtet.</summary>
    NotAvailable,
}

/// <summary>
/// Ersatzvalidator fuer die Ablauftests. Bildet die vier Ausgaenge nach, die
/// ein echter externer Validator haben kann, ohne dass Java noetig ist.
/// </summary>
internal sealed class StubValidator(string name, StubBehavior behavior) : IExternalDocumentValidator
{
    public string Name => name;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(behavior != StubBehavior.NotAvailable);

    public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>("1.0-test");

    public Task<ValidationReport> ValidateAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        var report = new ValidationReportBuilder();

        switch (behavior)
        {
            case StubBehavior.Passes:
                report.Information("APP-EXT-000", "Die externe Pruefung wurde bestanden.");
                break;

            case StubBehavior.ReportsError:
                report.Error(
                    "APP-EXT-010",
                    "Die erzeugte Datei entspricht nicht dem Standard PDF/A-3.",
                    technicalDetail: "Testfall");
                break;

            case StubBehavior.TimesOut:
                report.Error(
                    "APP-EXT-003",
                    "Die externe Pruefung wurde abgebrochen, weil sie zu lange gedauert hat. "
                    + "Die Datei gilt damit als ungeprueft.",
                    technicalDetail: "Testfall");
                break;

            case StubBehavior.NotAvailable:
            default:
                break;
        }

        return Task.FromResult(report.Build());
    }
}

/// <summary>
/// Liefert Fortschrittsmeldungen sofort und im meldenden Thread aus.
///
/// <see cref="Progress{T}"/> ist hier untauglich: Es stellt jede Meldung ueber
/// den Synchronisierungskontext zu – im Test also ueber den Threadpool. Die
/// Meldung trifft damit irgendwann ein, moeglicherweise erst nach dem Ende des
/// Tests. Zwei Folgefehler wurden dadurch beobachtet:
///
/// - Eine Zusicherung prueft die Meldungsliste, bevor sie gefuellt ist. Der
///   Test schlaegt dann sporadisch fehl, ohne dass sich am Programm etwas
///   geaendert haette.
/// - Ein Rueckruf ruft <c>Cancel</c> auf einer <c>CancellationTokenSource</c>
///   auf, die das <c>using</c> des Tests bereits entsorgt hat. Das Ergebnis
///   ist eine unbeobachtete <c>ObjectDisposedException</c> auf einem
///   Threadpool-Thread, die xunit als "Catastrophic failure" meldet und die
///   den gesamten Testlauf rot faerbt.
///
/// Im Test ist die sofortige Zustellung ausserdem das, was gemeint ist:
/// "beim ersten Fortschrittsereignis abbrechen" soll genau dann geschehen und
/// nicht irgendwann spaeter.
///
/// In der Oberflaeche bleibt <see cref="Progress{T}"/> richtig – dort ist das
/// Zustellen ueber den Kontext genau der Zweck, weil die Meldung auf dem
/// Oberflaechen-Thread ankommen muss.
/// </summary>
internal sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

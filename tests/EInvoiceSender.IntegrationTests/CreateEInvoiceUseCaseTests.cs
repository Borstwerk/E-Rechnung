using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Ende-zu-Ende-Tests des Gesamtablaufs.
///
/// Abgedeckt sind Erfolg, Warnung, Validierungsfehler, beschädigte PDF,
/// nicht eingebettete Schrift, Zeitüberschreitung eines externen Werkzeugs und
/// Abbruch durch den Benutzer.
///
/// In jedem Fehlerfall wird geprüft, dass **keine Ausgabedatei** entsteht und
/// das **Original unverändert** bleibt. Eine halb fertige Datei wäre schlimmer
/// als gar keine, weil der Anwender sie für gültig halten könnte.
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
    public async Task ErfolgreicherDurchlaufErzeugtDateiBerichtUndPrüfsumme()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        byte[] originalBytes = await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken);

        var progressMessages = new List<PipelineProgress>();
        var progress = new ImmediateProgress<PipelineProgress>(progressMessages.Add);

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.NotNull(result.OutputFile);
        Assert.True(File.Exists(result.OutputFile.FullPath));

        // Prüfsumme, Zeitpunkt, Standard und Profil sind gesetzt.
        Assert.Equal(64, result.OutputFile.Sha256.Length);
        Assert.Equal(FixedNow, result.CreatedAt);
        Assert.Equal(CiiConstants.ProfileEn16931, result.ProfileId);
        Assert.Contains("EN 16931", result.StandardDescription, StringComparison.Ordinal);

        // Die Prüfsumme muss zur tatsächlich geschriebenen Datei passen.
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
        Assert.Contains("Prüfwerkzeuge", text, StringComparison.Ordinal);

        // Das Original bleibt unverändert.
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));

        // Alle neun Schritte wurden gemeldet.
        Assert.Equal(9, result.CompletedSteps.Count);
        Assert.Contains(progressMessages, m => m.State == StepState.Running);
        Assert.All(result.CompletedSteps, s => Assert.NotEqual(StepState.Failed, s.State));
    }

    [Fact]
    public async Task OhneBestätigungWirdNichtsErzeugt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
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

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
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
    public async Task NichtEingebetteteSchriftFührtZumAbbruchImPreflight()
    {
        string source = TempPdf(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-PRE-011");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.Preflight, failed.Step);
    }

    [Fact]
    public async Task BeschädigtePdfFührtZumAbbruch()
    {
        string source = TempPdf(TestPdfFactory.CreateDamagedPdf());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.True(result.Report.HasErrors);
    }

    [Fact]
    public async Task BereitsHybridePdfBrauchtEineAusdrückicheBestätigung()
    {
        // Zuerst eine fertige E-Rechnung erzeugen ...
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        CreateEInvoiceResult first = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, Describe(first));

        // ... und diese ohne Bestätigung erneut verarbeiten.
        CreateEInvoiceResult second = await BuildUseCase().CreateAsync(
            Request(first.OutputFile!.FullPath),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(second.Succeeded);
        Assert.Contains(second.Report.Findings, f => f.RuleId == "APP-USE-002");

        // Mit Bestätigung geht es durch.
        CreateEInvoiceResult third = await BuildUseCase().CreateAsync(
            Request(first.OutputFile.FullPath) with { ExistingInvoiceReplacementConfirmed = true },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.Succeeded, Describe(third));
    }

    [Fact]
    public async Task ZeitüberschreitungDesExternenWerkzeugsVerhindertDieAusgabe()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.TimesOut))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

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
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.ExternalValidation, failed.Step);
    }

    /// <summary>
    /// Die ausgelieferte Anwendung richtet gar keinen externen Validator ein –
    /// sie soll ohne Java-Laufzeit arbeiten. Dieser Test fährt genau diese
    /// Zusammenstellung: kein Validator, vollständiger Ablauf.
    ///
    /// Er ist das automatisierte Gegenstück zur Abnahme auf einem sauberen
    /// Windows ohne Java: PDF laden, Rechnung erzeugen, speichern.
    /// </summary>
    [Fact]
    public async Task OhneJedenExternenValidatorEntstehtEineVollständigeDatei()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase()
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.NotNull(result.OutputFile);
        Assert.True(File.Exists(result.OutputFile!.FullPath));
        Assert.Empty(result.Validators);

        // Der Bericht behauptet nicht, es habe eine externe Prüfung gegeben.
        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);

        Assert.Contains("kein externer Validator", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FehlenderExternerValidatorWirdImBerichtAusgewiesen()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.NotAvailable))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        // Die Datei entsteht, aber der Bericht sagt deutlich, dass die
        // Gegenprüfung nicht stattgefunden hat.
        Assert.True(result.Succeeded, Describe(result));
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-030");

        ValidatorInfo info = Assert.Single(result.Validators);
        Assert.False(info.WasExecuted);

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains("NICHT AUSGEFÜHRT", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BenutzerabbruchHinterlässtKeineDatei()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        using var cts = new CancellationTokenSource();

        // Beim ersten Fortschrittsereignis abbrechen.
        var progress = new ImmediateProgress<PipelineProgress>(_ => cts.Cancel());

        CreateEInvoiceResult result = await BuildUseCase()
            .CreateAsync(Request(source), progress, cts.Token);

        Assert.False(result.Succeeded);
        Assert.True(result.Canceled);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-090");

        Assert.True(
            !Directory.Exists(_outputDirectory) || Directory.GetFiles(_outputDirectory).Length == 0);
    }

    [Fact]
    public async Task VorhandeneDateiWirdNichtStillÜberschrieben()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult first = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, Describe(first));

        CreateEInvoiceResult second = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(second.Succeeded, Describe(second));

        Assert.NotEqual(first.OutputFile!.FullPath, second.OutputFile!.FullPath);
        Assert.Contains("(2)", second.OutputFile.FullPath, StringComparison.Ordinal);
        Assert.True(File.Exists(first.OutputFile.FullPath));
    }

    [Fact]
    public async Task TemporäreDateienWerdenAuchImFehlerfallEntfernt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        string[] before = Directory.GetDirectories(Path.GetTempPath(), "EInvoiceSender-*");

        await BuildUseCase(new StubValidator("Testvalidator", StubBehavior.ReportsError))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        string[] after = Directory.GetDirectories(Path.GetTempPath(), "EInvoiceSender-*");

        Assert.Equal(before.Length, after.Length);
    }

    // ------------------------------------------------------------------ Aufbau

    private EInvoiceService BuildUseCase(params IExternalDocumentValidator[] validators)
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
            NullLogger<EInvoiceService>.Instance);

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
                // Aufräumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

/// <summary>Zeitquelle mit festem Wert.</summary>
internal sealed class StubClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now => now;
}

/// <summary>Zeitquelle mit festem Wert für den Regelvalidator.</summary>
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

    /// <summary>Meldet eine Zeitüberschreitung.</summary>
    TimesOut,

    /// <summary>Ist auf diesem System nicht eingerichtet.</summary>
    NotAvailable,
}

/// <summary>
/// Ersatzvalidator für die Ablauftests. Bildet die vier Ausgänge nach, die
/// ein echter externer Validator haben kann, ohne dass Java nötig ist.
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
                report.Information("APP-EXT-000", "Die externe Prüfung wurde bestanden.");
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
                    "Die externe Prüfung wurde abgebrochen, weil sie zu lange gedauert hat. "
                    + "Die Datei gilt damit als ungeprüft.",
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
/// <see cref="Progress{T}"/> ist hier untauglich: Es stellt jede Meldung über
/// den Synchronisierungskontext zu – im Test also über den Threadpool. Die
/// Meldung trifft damit irgendwann ein, möglicherweise erst nach dem Ende des
/// Tests. Zwei Folgefehler wurden dadurch beobachtet:
///
/// - Eine Zusicherung prüft die Meldungsliste, bevor sie gefüllt ist. Der
///   Test schlägt dann sporadisch fehl, ohne dass sich am Programm etwas
///   geändert hätte.
/// - Ein Rückruf ruft <c>Cancel</c> auf einer <c>CancellationTokenSource</c>
///   auf, die das <c>using</c> des Tests bereits entsorgt hat. Das Ergebnis
///   ist eine unbeobachtete <c>ObjectDisposedException</c> auf einem
///   Threadpool-Thread, die xunit als "Catastrophic failure" meldet und die
///   den gesamten Testlauf rot färbt.
///
/// Im Test ist die sofortige Zustellung außerdem das, was gemeint ist:
/// "beim ersten Fortschrittsereignis abbrechen" soll genau dann geschehen und
/// nicht irgendwann später.
///
/// In der Oberfläche bleibt <see cref="Progress{T}"/> richtig – dort ist das
/// Zustellen über den Kontext genau der Zweck, weil die Meldung auf dem
/// Oberflächen-Thread ankommen muss.
/// </summary>
internal sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

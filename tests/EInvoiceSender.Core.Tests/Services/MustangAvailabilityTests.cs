using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Services;

/// <summary>
/// Prüft, wann sich der externe Validator als einsatzbereit meldet.
///
/// **Der Fehler:** Die Prüfung sah nur nach, ob die JAR-Datei existiert. Auf
/// einem Rechner mit JAR, aber ohne Java galt das Werkzeug damit als
/// verfügbar. Der anschließende Startversuch scheiterte und meldete die
/// erzeugte Datei als ungeprüft – mit einem Fehler, der die Erzeugung der
/// Rechnung abgebrochen hätte.
///
/// Für die ausgelieferte Anwendung ist der Fall inzwischen gar nicht mehr
/// erreichbar: Sie trägt keinen externen Validator ein (siehe
/// <c>ProductionWithoutJavaTests</c>). In Entwicklung und Pipeline zählt die
/// Antwort aber weiterhin, und sie muss stimmen.
/// </summary>
public sealed class MustangAvailabilityTests : IDisposable
{
    private readonly string _jar = Path.Combine(
        Path.GetTempPath(), $"mustang-test-{Guid.NewGuid():N}.jar");

    public MustangAvailabilityTests() => File.WriteAllText(_jar, "kein echtes Archiv");

    public void Dispose()
    {
        if (File.Exists(_jar))
        {
            File.Delete(_jar);
        }
    }

    [Fact]
    public async Task OhneJavaGiltDasWerkzeugNichtAlsVerfügbar()
    {
        var runner = new StubProcessRunner(ProcessOutcome.ExecutableMissing);

        bool verfügbar = await Validator(runner).IsAvailableAsync(TestContext.Current.CancellationToken);

        Assert.False(
            verfügbar,
            "Eine vorhandene JAR ohne startbare Java-Laufzeit darf nicht als einsatzbereit gelten.");
    }

    [Fact]
    public async Task EinFehlgeschlagenerJavaAufrufGiltNichtAlsVerfügbar()
    {
        var runner = new StubProcessRunner(ProcessOutcome.NonZeroExitCode);

        Assert.False(await Validator(runner).IsAvailableAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OhneJarWirdJavaGarNichtErstGestartet()
    {
        var runner = new StubProcessRunner(ProcessOutcome.Succeeds);

        var validator = new MustangValidator(
            runner,
            MustangOptions.ForJar(Path.Combine(Path.GetTempPath(), "gibt-es-nicht.jar")),
            NullLogger<MustangValidator>.Instance);

        Assert.False(await validator.IsAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task MitJarUndJavaIstDasWerkzeugVerfügbar()
    {
        var runner = new StubProcessRunner(ProcessOutcome.Succeeds);

        Assert.True(await Validator(runner).IsAvailableAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Die Frage nach Java startet einen Prozess. Sie darf nicht bei jedem
    /// Aufruf erneut gestellt werden.
    /// </summary>
    [Fact]
    public async Task DieJavaPrüfungLäuftNurEinmal()
    {
        var runner = new StubProcessRunner(ProcessOutcome.Succeeds);
        MustangValidator validator = Validator(runner);

        await validator.IsAvailableAsync(TestContext.Current.CancellationToken);
        await validator.IsAvailableAsync(TestContext.Current.CancellationToken);
        await validator.IsAvailableAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, runner.Calls);
    }

    /// <summary>Geprüft wird die Laufzeit, nicht das Archiv.</summary>
    [Fact]
    public async Task GeprüftWirdMitEinemHarmlosenAufruf()
    {
        var runner = new StubProcessRunner(ProcessOutcome.Succeeds);

        await Validator(runner).IsAvailableAsync(TestContext.Current.CancellationToken);

        Assert.Equal("java", runner.LastExecutable);
        Assert.Equal(["-version"], runner.LastArguments);
    }

    private MustangValidator Validator(IProcessRunner runner)
        => new(runner, MustangOptions.ForJar(_jar), NullLogger<MustangValidator>.Instance);

    private enum ProcessOutcome
    {
        /// <summary>Java antwortet wie erwartet.</summary>
        Succeeds,

        /// <summary>Java gibt es, meldet aber einen Fehler.</summary>
        NonZeroExitCode,

        /// <summary>Es gibt kein java – der Start wirft.</summary>
        ExecutableMissing,
    }

    private sealed class StubProcessRunner(ProcessOutcome outcome) : IProcessRunner
    {
        public int Calls { get; private set; }

        public string? LastExecutable { get; private set; }

        public IReadOnlyList<string> LastArguments { get; private set; } = [];

        public Task<ProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastExecutable = executable;
            LastArguments = arguments;

            return outcome switch
            {
                ProcessOutcome.ExecutableMissing =>
                    throw new System.ComponentModel.Win32Exception(2, "Datei nicht gefunden"),
                ProcessOutcome.NonZeroExitCode =>
                    Task.FromResult(new ProcessResult(1, string.Empty, "kaputt", false, TimeSpan.Zero)),
                _ =>
                    Task.FromResult(new ProcessResult(
                        0, string.Empty, "openjdk version \"21\"", false, TimeSpan.Zero)),
            };
        }
    }
}

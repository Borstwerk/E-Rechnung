using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.UseCases;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Presentation.Editing;
using EInvoiceSender.Presentation.ViewModels;
using EInvoiceSender.Validation.Rules;
using Xunit;

namespace EInvoiceSender.Presentation.Tests;

/// <summary>
/// Sichert zu, dass das ViewModel nach einem <c>await</c> auf dem
/// Oberflaechen-Thread weiterarbeitet.
///
/// **Warum dieser Test existiert:** In WPF gehoeren alle Bedienelemente dem
/// Oberflaechen-Thread. Ein <c>ConfigureAwait(false)</c> im ViewModel fuehrt
/// dazu, dass die Fortsetzung nach dem <c>await</c> auf einem Thread des
/// Threadpools laeuft. Jede anschliessende Zuweisung an eine gebundene
/// Eigenschaft – und erst recht jede Aenderung an einer
/// <c>ObservableCollection</c> – meldet dann aus dem falschen Thread an die
/// Oberflaeche und wirft:
///
///   "Der aufrufende Thread kann nicht auf dieses Objekt zugreifen, da sich das
///    Objekt im Besitz eines anderen Threads befindet."
///
/// Genau dieser Fehler ist beim ersten Start der Anwendung aufgetreten.
///
/// Die uebrigen ViewModel-Tests koennen ihn nicht finden: Ein gewoehnlicher
/// Testlauf hat keinen Synchronisierungskontext, und ohne Kontext verhalten sich
/// <c>ConfigureAwait(true)</c> und <c>ConfigureAwait(false)</c> gleich. Deshalb
/// stellt dieser Test einen echten Ein-Thread-Kontext bereit – den einfachsten
/// Nachbau dessen, was WPF tut – und prueft, auf welchem Thread die
/// Aenderungsmeldungen des ViewModels ankommen.
///
/// Abgedeckt sind alle vier Befehle des ViewModels, die etwas abwarten. Jeder
/// von ihnen braucht einen eigenen Vorbereitungsschritt, sonst kehrt er an
/// seiner Eingangspruefung sofort zurueck und der Test liefe ins Leere.
/// </summary>
public sealed class UiThreadAffinityTests
{
    [Fact]
    public Task SelectPdfMeldetAenderungenAufDemOberflaechenThread()
        => AssertNotificationsStayOnUiThreadAsync(
            action: viewModel => viewModel.SelectPdfAsync("/tmp/beispiel.pdf"));

    [Fact]
    public Task VorlageLadenMeldetAenderungenAufDemOberflaechenThread()
        => AssertNotificationsStayOnUiThreadAsync(
            action: viewModel => viewModel.LoadTemplateAsync(CancellationToken.None));

    [Fact]
    public Task ErzeugenMeldetAenderungenAufDemOberflaechenThread()
        => AssertNotificationsStayOnUiThreadAsync(
            setup: viewModel =>
            {
                FillValidDraft(viewModel.Draft);
                viewModel.PreflightReport = SuitableReport("/tmp/beispiel.pdf");
                viewModel.ContentMatchConfirmed = true;
            },
            action: viewModel => viewModel.GenerateAsync());

    [Fact]
    public async Task MailentwurfMeldetAenderungenAufDemOberflaechenThread()
    {
        // Der Befehl liest die erzeugte Datei ein, es muss also wirklich eine geben.
        string outputFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(outputFile, [1, 2, 3]);

        try
        {
            await AssertNotificationsStayOnUiThreadAsync(
                setup: viewModel => viewModel.Result = ResultWithOutputFile(outputFile),
                action: viewModel => viewModel.CreateEmailDraftAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    /// <summary>
    /// Fuehrt die uebergebene Aktion auf einem eigenen Oberflaechen-Thread aus
    /// und prueft, dass jede Aenderungsmeldung des ViewModels von genau diesem
    /// Thread kommt.
    /// </summary>
    private static async Task AssertNotificationsStayOnUiThreadAsync(
        Func<ShellViewModel, Task> action,
        Action<ShellViewModel>? setup = null)
    {
        using var uiThread = new SingleThreadedSynchronizationContext();

        IReadOnlyCollection<int> observedThreads = await uiThread.RunAsync(async () =>
        {
            var threads = new ConcurrentQueue<int>();

            using ShellViewModel viewModel = BuildViewModel();

            // Erst vorbereiten, dann zuhoeren: Die Vorbereitung laeuft ohnehin
            // auf dem Oberflaechen-Thread und wuerde das Ergebnis nur verwaessern.
            setup?.Invoke(viewModel);

            viewModel.PropertyChanged += OnPropertyChanged;
            viewModel.Draft.PropertyChanged += OnPropertyChanged;
            viewModel.Findings.CollectionChanged += OnCollectionChanged;
            viewModel.Progress.CollectionChanged += OnCollectionChanged;

            await action(viewModel);

            return (IReadOnlyCollection<int>)threads;

            void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
                => threads.Enqueue(Environment.CurrentManagedThreadId);

            void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
                => threads.Enqueue(Environment.CurrentManagedThreadId);
        });

        Assert.True(
            observedThreads.Count > 0,
            "Der Befehl hat ueberhaupt nichts an die Oberflaeche gemeldet. Dann prueft "
            + "dieser Test nichts. Vermutlich ist er an einer Eingangspruefung sofort "
            + "zurueckgekehrt – die Vorbereitung im Test muss ergaenzt werden.");

        int[] fremdeThreads = observedThreads.Where(id => id != uiThread.ThreadId).Distinct().ToArray();

        Assert.True(
            fremdeThreads.Length == 0,
            $"Das ViewModel hat von fremden Thread(s) aus an die Oberflaeche gemeldet "
            + $"(Oberflaechen-Thread: {uiThread.ThreadId}, fremd: "
            + $"{string.Join(", ", fremdeThreads)}). In WPF wirft das eine "
            + "InvalidOperationException. Ursache ist in aller Regel ein "
            + "ConfigureAwait(false) im ViewModel: Damit kehrt die Fortsetzung nach dem "
            + "await nicht auf den Oberflaechen-Thread zurueck.");
    }

    private static ShellViewModel BuildViewModel()
        => new(
            new AsyncStubPreflight(),
            new En16931RuleValidator(),
            new AsyncStubUseCase(),
            new AsyncStubEmailDraftService(),
            new AsyncStubSettingsStore());

    internal static PdfPreflightReport SuitableReport(string filePath)
        => new(
            PreflightVerdict.Suitable, filePath, Path.GetFileName(filePath), 1024,
            true, false, false, false, true, false, "1.7", 1, [], null, false, null,
            ValidationReport.Empty);

    private static CreateEInvoiceResult ResultWithOutputFile(string fullPath)
        => new(
            true, new StoredFile(fullPath, new string('a', 64), 3), null, null,
            ValidationReport.Empty, [], DateTimeOffset.UnixEpoch, "Test",
            "urn:cen.eu:en16931:2017", []);

    private static void FillValidDraft(InvoiceDraft draft)
    {
        draft.InvoiceNumber = "RE-2026-0001";
        draft.IssueDate = new DateOnly(2026, 3, 15);
        draft.DueDate = new DateOnly(2026, 3, 29);
        draft.SellerName = "Musterbetrieb Beispiel GmbH";
        draft.SellerStreet = "Beispielweg 1";
        draft.SellerPostalCode = "10115";
        draft.SellerCity = "Berlin";
        draft.SellerVatId = "DE123456789";
        draft.SellerEmail = "rechnung@example.invalid";
        draft.BuyerName = "Beispielkunde AG";
        draft.BuyerStreet = "Kundenstrasse 7";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerEmail = "einkauf@example.invalid";
        draft.BankAccountHolder = "Musterbetrieb Beispiel GmbH";
        draft.BankIban = "DE89370400440532013000";
        draft.PaymentTerms = "Zahlbar innerhalb von 14 Tagen.";

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Beratungsleistung";
        line.Quantity = "10";
        line.Unit = "HUR";
        line.NetUnitPrice = "100,00";
        line.VatRate = "19";
    }
}

/// <summary>
/// Ein Synchronisierungskontext mit genau einem Thread und einer
/// Nachrichtenschleife – der kleinstmoegliche Nachbau des WPF-Dispatchers.
/// Anders als ein Kontext, der Fortsetzungen einfach sofort ausfuehrt, macht er
/// den Unterschied zwischen "auf dem Oberflaechen-Thread" und "irgendwo"
/// ueberhaupt erst sichtbar.
/// </summary>
internal sealed class SingleThreadedSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
    private readonly Thread _thread;

    public SingleThreadedSynchronizationContext()
    {
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "Test-Oberflaechen-Thread",
        };

        _thread.Start();
    }

    /// <summary>Der Thread, dem in diesem Test alle Bedienelemente gehoeren.</summary>
    public int ThreadId => _thread.ManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        if (!_queue.IsAddingCompleted)
        {
            _queue.Add((d, state));
        }
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        if (Environment.CurrentManagedThreadId == ThreadId)
        {
            d(state);

            return;
        }

        using var done = new ManualResetEventSlim();

        Post(
            inner =>
            {
                try
                {
                    d(inner);
                }
                finally
                {
                    done.Set();
                }
            },
            state);

        done.Wait();
    }

    /// <summary>
    /// Startet die Arbeit auf dem Oberflaechen-Thread und liefert ihr Ergebnis.
    /// </summary>
    public Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Post(_ => _ = ExecuteAsync(), null);

        return completion.Task;

        async Task ExecuteAsync()
        {
            try
            {
                completion.SetResult(await work());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }
    }

    private void RunMessageLoop()
    {
        SetSynchronizationContext(this);

        foreach ((SendOrPostCallback callback, object? state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
        _queue.Dispose();
    }
}

/// <summary>
/// Eingangspruefung, die tatsaechlich auf einem fremden Thread fertig wird.
///
/// Wichtig: Eine bereits abgeschlossene Aufgabe (<c>Task.FromResult</c>) laeuft
/// nach dem <c>await</c> einfach synchron weiter und bliebe damit auf dem
/// Oberflaechen-Thread – der Test waere wirkungslos. Auch <c>Task.Yield</c>
/// genuegt nicht, weil es selbst wieder ueber den Kontext zurueckkehrt. Erst das
/// <c>ConfigureAwait(false)</c> im Stub sorgt dafuer, dass die Aufgabe auf einem
/// Threadpool-Thread abschliesst – so wie es echte Datei- und Prozessarbeit tut.
/// </summary>
internal sealed class AsyncStubPreflight : IPdfPreflightService
{
    public async Task<PdfPreflightReport> InspectAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        return UiThreadAffinityTests.SuitableReport(filePath);
    }
}

/// <summary>Anwendungsfall, der auf einem fremden Thread fertig wird.</summary>
internal sealed class AsyncStubUseCase : ICreateEInvoiceUseCase
{
    public async Task<CreateEInvoiceResult> ExecuteAsync(
        CreateEInvoiceRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        return new CreateEInvoiceResult(
            false, null, null, null, ValidationReport.Empty, [],
            DateTimeOffset.UnixEpoch, "Test", "urn:cen.eu:en16931:2017", []);
    }
}

/// <summary>Mailentwurf, der auf einem fremden Thread fertig wird.</summary>
internal sealed class AsyncStubEmailDraftService : IEmailDraftService
{
    public string Name => "Test";

    public async Task<EmailDraftResult> CreateDraftAsync(
        EmailDraft draft, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        return new EmailDraftResult(true, "/tmp/test.eml", null, "ok");
    }

    public Uri BuildMailtoUri(EmailDraft draft) => new("mailto:test@example.invalid");
}

/// <summary>
/// Einstellungen, die auf einem fremden Thread fertig werden.
///
/// Die Vorlage ist absichtlich gefuellt: Eine leere Vorlage aendert im Formular
/// nichts, und ohne Aenderung gibt es keine Meldung an die Oberflaeche – der
/// Test haette dann nichts zu pruefen.
/// </summary>
internal sealed class AsyncStubSettingsStore : ISettingsStore
{
    public bool SupportsProtectedStorage => false;

    public async Task<CompanyTemplate> LoadTemplateAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        return new CompanyTemplate
        {
            SellerName = "Musterbetrieb Beispiel GmbH",
            SellerStreet = "Beispielweg 1",
            SellerPostalCode = "10115",
            SellerCity = "Berlin",
            DefaultEmailSubject = "Ihre Rechnung",
            DefaultEmailBody = "Guten Tag,\n\nanbei Ihre Rechnung.",
            LastOutputDirectory = "/tmp/ausgabe",
        };
    }

    public Task SaveTemplateAsync(
        CompanyTemplate companyTemplate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task<ApplicationSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        return new ApplicationSettings();
    }

    public Task SaveSettingsAsync(
        ApplicationSettings settings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using EInvoiceSender.App.Services;
using EInvoiceSender.App.ViewModels;
using EInvoiceSender.App.Views;
using EInvoiceSender.App.Views.Dialogs;
using EInvoiceSender.Core.Checking;
using EInvoiceSender.Core.Diagnostics;
using EInvoiceSender.Core.Mail;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using EInvoiceSender.Core.Storage;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EInvoiceSender.App;

/// <summary>
/// Der Einstiegspunkt und zugleich die einzige Stelle, an der die Bausteine
/// zusammengesetzt werden.
///
/// Bewusst ohne Generic Host: Eine Desktopanwendung braucht weder
/// Hintergrunddienste noch Konfigurationsanbieter. Eine
/// <see cref="ServiceCollection"/> genügt und bleibt überschaubar.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _logger;
    private Stopwatch? _sessionStopwatch;

    /// <summary>
    /// Die zusammengesetzten Dienste. Wird außerhalb dieser Klasse nur von
    /// Fenstern verwendet, die WPF selbst erzeugt.
    /// </summary>
    public static IServiceProvider Services =>
        ((App)Current)._services
        ?? throw new InvalidOperationException("Die Anwendung ist noch nicht gestartet.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _sessionStopwatch = Stopwatch.StartNew();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();

        // Eine unerwartete Ausnahme darf die Anwendung nicht wortlos beenden.
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledDomainException;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            Version? version = typeof(App).Assembly.GetName().Version;
            string versionText = version?.ToString(3) ?? "unbekannt";
            string architecture = RuntimeInformation.ProcessArchitecture.ToString();
            LogStarted(
                _logger,
                versionText,
                RuntimeInformation.FrameworkDescription,
                architecture);
        }

        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _sessionStopwatch?.Stop();

        if (_logger is not null)
        {
            LogStopped(_logger, _sessionStopwatch?.ElapsedMilliseconds ?? 0, e.ApplicationExitCode);
        }

        DispatcherUnhandledException -= OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledDomainException;
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        DiagnosticLogDirectory diagnosticDirectory = DiagnosticLogDirectory.CreateDefault();
        var diagnosticOptions = new DiagnosticLogOptions(diagnosticDirectory.DirectoryPath);

        services.AddSingleton(diagnosticDirectory);
        services.AddLogging(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new LocalFileLoggerProvider(diagnosticOptions)));

        // --- Kern -----------------------------------------------------------
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileStorage, FileStorage>();
        services.AddSingleton<ITemporaryWorkspaceFactory, TemporaryWorkspaceFactory>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IInvoiceXmlWriter, CiiInvoiceWriter>();
        services.AddSingleton<IInvoiceXmlReader, CiiInvoiceReader>();
        services.AddSingleton<ICiiInvoiceInspector>(provider =>
            (ICiiInvoiceInspector)provider.GetRequiredService<IInvoiceXmlReader>());
        services.AddSingleton<IBusinessRuleValidator, En16931RuleValidator>();
        services.AddSingleton<IPdfAnalyzer, PdfAnalyzer>();
        services.AddSingleton<IPdfAttachmentReader>(provider =>
            (IPdfAttachmentReader)provider.GetRequiredService<IPdfAnalyzer>());
        services.AddSingleton<IPdfRenderProbe, PdfiumRenderProbe>();
        services.AddSingleton<IPdfPreflightService, PdfPreflightService>();
        services.AddSingleton<IPdfAInvoiceComposer, PdfAInvoiceComposer>();

        // Der Rasterweg für PDF-Dateien ohne eingebettete Schriften. Er wird nur
        // beschritten, wenn die Eingangsprüfung ihn freigibt **und** der
        // Benutzer ihm ausdrücklich zugestimmt hat; beides prüft der Kern.
        services.AddSingleton<RasterizedPdfBuilder>();
        services.AddSingleton<IPdfARasterFallbackComposer, RasterFallbackComposer>();
        services.AddSingleton<IEmailDraftService, EmlDraftService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IEInvoiceService, EInvoiceService>();
        services.AddSingleton<IEInvoiceCheckService, EInvoiceCheckService>();

        // Lokale Datenerkennung: liest nur bereits vorhandenen PDF-Text aus.
        // Kein OCR, keine externen Dienste, nichts verlässt das Gerät.
        services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IInvoiceDataDetector, InvoiceDataDetector>();

        // **Hier wird bewusst kein externer Validator eingetragen.**
        //
        // Mustangproject, das CEN-Schematron und veraPDF sind Werkzeuge der
        // Entwicklung und der Freigabe: Sie belegen, dass diese Anwendung
        // normgerechte Dateien erzeugt, und laufen in der Pipeline
        // (build/Validate-Reference.ps1, docs/TESTING.md). Sie brauchen eine
        // Java-Laufzeit.
        //
        // Auf dem Rechner des Anwenders hat das nichts zu suchen. Das
        // Installationspaket bringt kein Java mit, und niemand soll eines
        // nachinstallieren müssen, um eine Rechnung zu schreiben. Ohne Eintrag
        // sucht die Anwendung erst gar nicht danach.
        //
        // Der Bericht bleibt dabei ehrlich: Ist kein Validator eingerichtet,
        // schreibt er "Es war kein externer Validator eingerichtet." Er
        // behauptet nie, eine externe Prüfung habe stattgefunden.

        // --- Oberfläche ----------------------------------------------------
        services.AddSingleton<IShellService, WindowsShellService>();
        services.AddSingleton<IPdfPreviewService, PdfPreviewService>();

        services.AddSingleton<PdfSelectionViewModel>();
        services.AddSingleton<InvoiceDataViewModel>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<GenerationViewModel>();
        services.AddSingleton<ResultViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<EInvoiceCheckViewModel>();
        services.AddTransient<AboutWindow>();
        services.AddTransient<EInvoiceCheckWindow>();
        services.AddSingleton<MainWindow>();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_logger is not null)
        {
            LogUnhandledDispatcherException(_logger, e.Exception);
        }

        MessageBox.Show(
            "Es ist eine unerwartete Störung aufgetreten. Die bisher erzeugten Dateien "
            + "bleiben erhalten.\n\nTechnische Angabe:\n" + e.Exception.Message,
            "BorstWerk E-Rechnung",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnUnhandledDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (_logger is null)
        {
            return;
        }

        if (e.ExceptionObject is Exception exception)
        {
            LogUnhandledDomainException(_logger, e.IsTerminating, exception);
            return;
        }

        LogUnhandledDomainObject(
            _logger,
            e.IsTerminating,
            e.ExceptionObject?.GetType().Name ?? "unbekannt");
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Anwendung gestartet: Version {Version}, Runtime {Runtime}, Architektur {Architecture}")]
    private static partial void LogStarted(
        ILogger logger,
        string version,
        string runtime,
        string architecture);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Anwendung regulär beendet: {Milliseconds} ms, Exitcode {ExitCode}")]
    private static partial void LogStopped(ILogger logger, long milliseconds, int exitCode);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Unerwartete Ausnahme im Oberflächenthread")]
    private static partial void LogUnhandledDispatcherException(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Critical,
        Message = "Unbehandelte Laufzeitausnahme; Programmende: {IsTerminating}")]
    private static partial void LogUnhandledDomainException(
        ILogger logger,
        bool isTerminating,
        Exception exception);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Critical,
        Message = "Unbehandeltes Laufzeitobjekt vom Typ {ExceptionObjectType}; Programmende: {IsTerminating}")]
    private static partial void LogUnhandledDomainObject(
        ILogger logger,
        bool isTerminating,
        string exceptionObjectType);
}

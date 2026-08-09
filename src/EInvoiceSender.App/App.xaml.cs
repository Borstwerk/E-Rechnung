using System.Windows;
using System.Windows.Threading;
using EInvoiceSender.App.Services;
using EInvoiceSender.App.ViewModels;
using EInvoiceSender.App.Views;
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
/// <see cref="ServiceCollection"/> genuegt und bleibt ueberschaubar.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    /// <summary>
    /// Die zusammengesetzten Dienste. Wird ausserhalb dieser Klasse nur von
    /// Fenstern verwendet, die WPF selbst erzeugt.
    /// </summary>
    public static IServiceProvider Services =>
        ((App)Current)._services
        ?? throw new InvalidOperationException("Die Anwendung ist noch nicht gestartet.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        // Eine unerwartete Ausnahme darf die Anwendung nicht wortlos beenden.
        DispatcherUnhandledException += OnUnhandledException;

        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));

        // --- Kern -----------------------------------------------------------
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileStorage, FileStorage>();
        services.AddSingleton<ITemporaryWorkspaceFactory, TemporaryWorkspaceFactory>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IInvoiceXmlWriter, CiiInvoiceWriter>();
        services.AddSingleton<IInvoiceXmlReader, CiiInvoiceReader>();
        services.AddSingleton<IBusinessRuleValidator, En16931RuleValidator>();
        services.AddSingleton<IPdfAnalyzer, PdfAnalyzer>();
        services.AddSingleton<IPdfPreflightService, PdfPreflightService>();
        services.AddSingleton<IPdfAInvoiceComposer, PdfAInvoiceComposer>();
        services.AddSingleton<IEmailDraftService, EmlDraftService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IEInvoiceService, EInvoiceService>();

        // Lokale Datenerkennung: liest nur bereits vorhandenen PDF-Text aus.
        // Kein OCR, keine externen Dienste, nichts verlaesst das Geraet.
        services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IInvoiceDataDetector, InvoiceDataDetector>();

        // Referenzvalidatoren sind optional: Ohne Java oder ohne die
        // Mustang-JAR startet die Anwendung normal weiter. Der Validator meldet
        // sich dann als nicht verfuegbar, und der Bericht weist die Pruefung
        // ausdruecklich als NICHT AUSGEFUEHRT aus.
        services.AddSingleton(MustangOptions.Discover());
        services.AddSingleton<IExternalDocumentValidator, MustangValidator>();

        // --- Oberflaeche ----------------------------------------------------
        services.AddSingleton<IShellService, WindowsShellService>();
        services.AddSingleton<IPdfPreviewService, PdfPreviewService>();

        services.AddSingleton<PdfSelectionViewModel>();
        services.AddSingleton<InvoiceDataViewModel>();
        services.AddSingleton<ReviewViewModel>();
        services.AddSingleton<GenerationViewModel>();
        services.AddSingleton<ResultViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Es ist eine unerwartete Stoerung aufgetreten. Die bisher erzeugten Dateien "
            + "bleiben erhalten.\n\nTechnische Angabe:\n" + e.Exception.Message,
            "EInvoiceSender",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}

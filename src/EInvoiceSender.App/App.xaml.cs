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
/// <see cref="ServiceCollection"/> genügt und bleibt überschaubar.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

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
        services.AddSingleton<MainWindow>();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Es ist eine unerwartete Störung aufgetreten. Die bisher erzeugten Dateien "
            + "bleiben erhalten.\n\nTechnische Angabe:\n" + e.Exception.Message,
            "EInvoiceSender",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}

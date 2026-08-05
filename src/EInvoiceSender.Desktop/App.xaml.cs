using System.IO;
using System.Windows;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.UseCases;
using EInvoiceSender.Desktop.Services;
using EInvoiceSender.Desktop.Views;
using EInvoiceSender.Formats.Cii;
using EInvoiceSender.Infrastructure.PdfA;
using EInvoiceSender.Infrastructure.Process;
using EInvoiceSender.Infrastructure.Settings;
using EInvoiceSender.Infrastructure.Storage;
using EInvoiceSender.Presentation.ViewModels;
using EInvoiceSender.Validation.External;
using EInvoiceSender.Validation.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EInvoiceSender.Desktop;

/// <summary>
/// Einstiegspunkt der Anwendung. Baut den Dienstbehaelter auf und verdrahtet
/// die Ports mit ihren Umsetzungen.
///
/// Hier steht die einzige Stelle, an der konkrete Klassen den Schnittstellen
/// zugeordnet werden. Alles darunter kennt nur noch die Ports.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EInvoiceSender");

        Directory.CreateDirectory(dataDirectory);

        // Strukturierte lokale Protokollierung als JSON-Zeilen, tagesweise
        // rollierend. Es wird ausschliesslich lokal geschrieben.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                new Serilog.Formatting.Compact.CompactJsonFormatter(),
                Path.Combine(dataDirectory, "protokoll", "einvoicesender-.jsonl"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync().ConfigureAwait(true);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // --- Formate ---
        services.AddSingleton<IInvoiceXmlWriter, CiiInvoiceWriter>();
        services.AddSingleton<IInvoiceXmlReader, CiiInvoiceReader>();

        // --- Validierung ---
        services.AddSingleton<IBusinessRuleValidator>(_ => new En16931RuleValidator());

        // Der externe Validator ist optional. Fehlt er, laeuft die Anwendung
        // weiter und weist im Bericht darauf hin.
        services.AddSingleton<IExternalDocumentValidator>(provider =>
            new MustangValidator(
                provider.GetRequiredService<IProcessRunner>(),
                MustangOptions.ForJar(Path.Combine(
                    AppContext.BaseDirectory, "tools", "mustang", "Mustang-CLI.jar")),
                provider.GetRequiredService<ILogger<MustangValidator>>()));

        // --- Infrastruktur ---
        services.AddSingleton<IPdfAnalyzer, PdfAnalyzer>();
        services.AddSingleton<IPdfAInvoiceComposer, PdfAInvoiceComposer>();
        services.AddSingleton<IPdfPreflightService, PdfPreflightService>();
        services.AddSingleton<IFileStorage, FileStorage>();
        services.AddSingleton<ITemporaryWorkspaceFactory, TemporaryWorkspaceFactory>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IClock, SystemClock>();

        // --- Windows-Dienste ---
        services.AddSingleton<IShellService, WindowsShellService>();

        // --- E-Mail ---
        services.AddSingleton<IEmailDraftService, Mail.EmlDraftService>();

        // --- Anwendungsfaelle und Oberflaeche ---
        services.AddSingleton<ICreateEInvoiceUseCase, CreateEInvoiceUseCase>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync().ConfigureAwait(true);

        base.OnExit(e);
    }
}

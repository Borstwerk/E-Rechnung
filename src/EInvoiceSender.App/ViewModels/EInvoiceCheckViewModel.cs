using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.App.Presentation;
using EInvoiceSender.Core.Checking;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Flüchtiger Oberflächenzustand für die technische Bestandsaufnahme einer
/// fertigen E-Rechnung. Er gehört ausdrücklich nicht zum Erzeugungswizard.
/// </summary>
public sealed partial class EInvoiceCheckViewModel(
    IEInvoiceCheckService service,
    ILogger<EInvoiceCheckViewModel> logger) : StepViewModel
{
    private readonly IEInvoiceCheckService _service =
        service ?? throw new ArgumentNullException(nameof(service));
    private readonly ILogger<EInvoiceCheckViewModel> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Läuft gerade eine Bestandsaufnahme?</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isChecking;

    /// <summary>Kann eine andere Datei gewählt werden?</summary>
    public bool IsIdle => !IsChecking;

    /// <summary>Nur der Dateiname, nie der vollständige Pfad.</summary>
    [ObservableProperty]
    private string _selectedFileName = string.Empty;

    /// <summary>Das unverändert dargestellte Ergebnis des Core-Dienstes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(HasDocumentInfo))]
    [NotifyPropertyChangedFor(nameof(HasInvoiceSummary))]
    [NotifyPropertyChangedFor(nameof(IssueDateText))]
    [NotifyPropertyChangedFor(nameof(LineTotalText))]
    [NotifyPropertyChangedFor(nameof(TaxBasisTotalText))]
    [NotifyPropertyChangedFor(nameof(TaxTotalText))]
    [NotifyPropertyChangedFor(nameof(GrandTotalText))]
    [NotifyPropertyChangedFor(nameof(DuePayableAmountText))]
    private CheckEInvoiceResult? _result;

    /// <summary>Liegt ein Ergebnis vor, auch wenn es vorzeitig endete?</summary>
    public bool HasResult => Result is not null;

    /// <summary>Konnten technische PDF-Angaben gelesen werden?</summary>
    public bool HasDocumentInfo => Result?.DocumentInfo is not null;

    /// <summary>Konnte eine CII-Rechnungsübersicht gelesen werden?</summary>
    public bool HasInvoiceSummary => Result?.InvoiceSummary is not null;

    /// <summary>BT-2 als deutsches Datum ohne Uhrzeit.</summary>
    public string IssueDateText =>
        EInvoiceCheckDisplayFormatter.FormatDate(Result?.InvoiceSummary?.IssueDate);

    /// <summary>BT-106 mit der gelesenen Rechnungswährung.</summary>
    public string LineTotalText => FormatMoney(Result?.InvoiceSummary?.LineTotal);

    /// <summary>BT-109 mit der gelesenen Rechnungswährung.</summary>
    public string TaxBasisTotalText => FormatMoney(Result?.InvoiceSummary?.TaxBasisTotal);

    /// <summary>BT-110 mit der gelesenen Rechnungswährung.</summary>
    public string TaxTotalText => FormatMoney(Result?.InvoiceSummary?.TaxTotal);

    /// <summary>BT-112 mit der gelesenen Rechnungswährung.</summary>
    public string GrandTotalText => FormatMoney(Result?.InvoiceSummary?.GrandTotal);

    /// <summary>BT-115 mit der gelesenen Rechnungswährung.</summary>
    public string DuePayableAmountText => FormatMoney(Result?.InvoiceSummary?.DuePayableAmount);

    /// <summary>Zusammenfassung ohne Aussage über Normkonformität.</summary>
    [ObservableProperty]
    private string _summaryText =
        "Wählen Sie eine fertige PDF-Hybridrechnung für die technische Bestandsaufnahme aus.";

    /// <summary>
    /// Prüft eine Datei. Jede Auswahl beginnt mit einem leeren Anzeigestand,
    /// damit kein Wert der vorigen Rechnung neben neuen Befunden stehenbleibt.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    public async Task InspectAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || IsChecking)
        {
            return;
        }

        BeginInspection(path);

        try
        {
            CheckEInvoiceResult ergebnis = await _service
                .CheckAsync(new CheckEInvoiceRequest(path), cancellationToken)
                .ConfigureAwait(true);

            Result = ergebnis;
            ShowFindings(ergebnis.Report);
            SummaryText = Describe(ergebnis);
        }
        catch (Exception exception)
        {
            // Der Core bildet unbrauchbare Eingaben als Befunde ab. Dieser
            // Schutz bleibt für echte Laufzeitstörungen wie einen während des
            // Lesens entfernten Datenträger. Das sichere Diagnoselog schreibt
            // ausschließlich Exceptiontypen und Methodennamen.
            LogInspectionFailed(_logger, exception);
            Result = null;
            ClearFindings();
            SummaryText = "Die Datei konnte wegen einer technischen Störung nicht gelesen werden. "
                          + "Sie wurde nicht verändert.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void BeginInspection(string path)
    {
        Result = null;
        ClearFindings();
        SelectedFileName = Path.GetFileName(path);
        SummaryText = "Die technische Bestandsaufnahme läuft …";
        IsChecking = true;
    }

    private static string Describe(CheckEInvoiceResult ergebnis)
    {
        if (ergebnis.Canceled)
        {
            return "Die technische Bestandsaufnahme wurde abgebrochen. Die Datei wurde nicht verändert.";
        }

        return ergebnis.Completed
            ? "Die technische Bestandsaufnahme wurde vollständig durchgeführt. "
              + "Sie ist keine vollständige EN-16931- oder PDF/A-Konformitätsprüfung."
            : "Die technische Bestandsaufnahme konnte nicht vollständig durchgeführt werden. "
              + "Die Befunde nennen den Grund; die Datei wurde nicht verändert.";
    }

    private string FormatMoney(decimal? amount)
        => EInvoiceCheckDisplayFormatter.FormatMoney(amount, Result?.InvoiceSummary?.Currency);

    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Error,
        Message = "Technische Bestandsaufnahme einer E-Rechnung unerwartet abgebrochen.")]
    private static partial void LogInspectionFailed(ILogger logger, Exception exception);
}

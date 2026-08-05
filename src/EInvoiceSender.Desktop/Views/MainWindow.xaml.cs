using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.UseCases;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Presentation.ViewModels;

namespace EInvoiceSender.Desktop.Views;

/// <summary>
/// Das Hauptfenster.
///
/// Enthaelt bewusst **keine Fachlogik**. Der Code-behind erledigt nur, was ohne
/// WPF nicht geht: Dateidialoge, Drag-and-drop, Sichtbarkeit der Schritte und
/// das Rendern der PDF-Vorschau. Alles Weitere liegt im
/// <see cref="ShellViewModel"/> und darunter.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly IShellService _shellService;

    public MainWindow(ShellViewModel viewModel, IShellService shellService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _shellService = shellService ?? throw new ArgumentNullException(nameof(shellService));

        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateStepVisibility();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.CurrentStep):
                UpdateStepVisibility();
                break;

            case nameof(ShellViewModel.Totals):
                UpdateTotals();
                break;

            case nameof(ShellViewModel.PreflightReport):
                UpdatePreview();
                break;

            case nameof(ShellViewModel.Result):
                UpdateResult();
                break;

            default:
                break;
        }
    }

    /// <summary>Blendet genau den Bereich des aktuellen Schrittes ein.</summary>
    private void UpdateStepVisibility()
    {
        SchrittPdf.Visibility = Visible(WizardStep.SelectPdf);
        SchrittDaten.Visibility = Visible(WizardStep.EnterData);
        SchrittKontrolle.Visibility = Visible(WizardStep.Review);
        SchrittErzeugen.Visibility = Visible(WizardStep.Generate);
        SchrittAbschluss.Visibility = Visible(WizardStep.Finish);

        if (_viewModel.CurrentStep == WizardStep.Review)
        {
            UpdateReviewSummary();
        }

        Visibility Visible(WizardStep step)
            => _viewModel.CurrentStep == step ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateTotals()
    {
        if (_viewModel.Totals is not { } totals)
        {
            SummenAnzeige.Text = "Die Summen koennen noch nicht berechnet werden.";

            return;
        }

        var german = CultureInfo.GetCultureInfo("de-DE");
        var text = new StringBuilder();

        text.Append(CultureInfo.CurrentCulture, $"Summe der Positionen: {totals.LineTotal.ToString("N2", german)}");
        text.Append(CultureInfo.CurrentCulture, $"   Netto: {totals.TaxBasisTotal.ToString("N2", german)}");
        text.Append(CultureInfo.CurrentCulture, $"   Umsatzsteuer: {totals.TaxTotal.ToString("N2", german)}");
        text.Append(CultureInfo.CurrentCulture, $"   Brutto: {totals.GrandTotal.ToString("N2", german)}");
        text.Append(CultureInfo.CurrentCulture, $"   Zahlbetrag: {totals.DuePayableAmount.ToString("N2", german)}");

        SummenAnzeige.Text = text.ToString();
    }

    /// <summary>Stellt die Kerndaten neben der PDF dar.</summary>
    private void UpdateReviewSummary()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");
        var text = new StringBuilder();

        text.AppendLine(CultureInfo.CurrentCulture, $"Verkaeufer:      {_viewModel.Draft.SellerName}");
        text.AppendLine(CultureInfo.CurrentCulture, $"Kaeufer:         {_viewModel.Draft.BuyerName}");
        text.AppendLine(CultureInfo.CurrentCulture, $"Rechnungsnummer: {_viewModel.Draft.InvoiceNumber}");
        text.AppendLine(CultureInfo.CurrentCulture,
            $"Rechnungsdatum:  {_viewModel.Draft.IssueDate?.ToString("dd.MM.yyyy", german) ?? "-"}");

        if (_viewModel.Totals is { } totals)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.CurrentCulture, $"Netto:           {totals.TaxBasisTotal.ToString("N2", german)}");
            text.AppendLine(CultureInfo.CurrentCulture, $"Umsatzsteuer:    {totals.TaxTotal.ToString("N2", german)}");
            text.AppendLine(CultureInfo.CurrentCulture, $"Brutto:          {totals.GrandTotal.ToString("N2", german)}");
            text.AppendLine(CultureInfo.CurrentCulture, $"Zahlbetrag:      {totals.DuePayableAmount.ToString("N2", german)}");
        }

        KontrollAnzeige.Text = text.ToString();
    }

    /// <summary>
    /// Rendert die erste Seite der PDF als Vorschau.
    ///
    /// PDFtoImage ist nicht threadsicher; deshalb laeuft das Rendern
    /// ausschliesslich hier im UI-Thread und nie nebenlaeufig.
    /// </summary>
    private void UpdatePreview()
    {
        PdfVorschau.Source = null;

        if (_viewModel.PreflightReport is not { CanProceed: true } report)
        {
            return;
        }

        try
        {
            byte[] pdf = File.ReadAllBytes(report.FilePath);
            using var stream = new MemoryStream();

            PDFtoImage.Conversion.SavePng(stream, pdf, page: 0);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            PdfVorschau.Source = image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
        {
            // Eine fehlende Vorschau ist kein Grund, den Ablauf zu stoppen –
            // die Datei wurde bereits als verarbeitbar geprueft.
            PdfVorschau.Source = null;
        }
    }

    private void UpdateResult()
    {
        if (_viewModel.Result is not { Succeeded: true } result || result.OutputFile is null)
        {
            ErgebnisAnzeige.Text = string.Empty;

            return;
        }

        var german = CultureInfo.GetCultureInfo("de-DE");
        var text = new StringBuilder();

        text.AppendLine("Die E-Rechnung wurde erzeugt und geprueft.");
        text.AppendLine();
        text.AppendLine(CultureInfo.CurrentCulture, $"Datei:      {result.OutputFile.FullPath}");
        text.AppendLine(CultureInfo.CurrentCulture,
            $"Groesse:    {result.OutputFile.SizeInBytes.ToString("N0", german)} Bytes");
        text.AppendLine(CultureInfo.CurrentCulture, $"SHA-256:    {result.OutputFile.Sha256}");
        text.AppendLine(CultureInfo.CurrentCulture, $"Standard:   {result.StandardDescription}");
        text.AppendLine(CultureInfo.CurrentCulture,
            $"Erzeugt am: {result.CreatedAt.ToString("dd.MM.yyyy HH:mm", german)}");

        if (result.ReportTextFile is not null)
        {
            text.AppendLine(CultureInfo.CurrentCulture, $"Bericht:    {result.ReportTextFile.FullPath}");
        }

        text.AppendLine();
        text.AppendLine("Verwendete Pruefwerkzeuge:");

        foreach (ValidatorInfo validator in result.Validators)
        {
            text.AppendLine(CultureInfo.CurrentCulture,
                $"  - {validator.Name}: {(validator.WasExecuted ? validator.Version ?? "Version unbekannt" : "NICHT AUSGEFUEHRT")}");
        }

        ErgebnisAnzeige.Text = text.ToString();
    }

    // --- Ereignisse --------------------------------------------------------

    private async void OnSelectPdfClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "PDF-Rechnung auswaehlen",
            Filter = "PDF-Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.SelectPdfAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private async void OnFileDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files)
        {
            return;
        }

        await _viewModel.SelectPdfAsync(files[0]).ConfigureAwait(true);
    }

    private void OnAddLineClicked(object sender, RoutedEventArgs e)
        => _viewModel.Draft.AddLine();

    private async void OnCreateDraftClicked(object sender, RoutedEventArgs e)
    {
        await _viewModel.CreateEmailDraftAsync().ConfigureAwait(true);

        if (!string.IsNullOrEmpty(_viewModel.EmailDraftPath))
        {
            await _shellService.OpenFileAsync(_viewModel.EmailDraftPath).ConfigureAwait(true);
        }
    }

    private async void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        string? directory = _viewModel.Result?.OutputFile is { } file
            ? Path.GetDirectoryName(file.FullPath)
            : _viewModel.OutputDirectory;

        if (!string.IsNullOrWhiteSpace(directory))
        {
            await _shellService.OpenFolderAsync(directory).ConfigureAwait(true);
        }
    }
}

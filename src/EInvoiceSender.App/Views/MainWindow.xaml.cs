using System.Windows;
using System.Windows.Controls;
using EInvoiceSender.App.ViewModels;
using EInvoiceSender.App.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace EInvoiceSender.App.Views;

/// <summary>
/// Das Hauptfenster.
///
/// Der Code hier beschränkt sich auf echte WPF-Aufgaben: Drag-and-drop,
/// Dateidialog, Unterfenster und das Ein- und Ausblenden des aktuellen
/// Schrittes. Alles Weitere steht im <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.CurrentStep))
            {
                ShowCurrentStep();
            }
        };

        Loaded += OnLoadedAsync;
        ShowCurrentStep();
    }

    /// <summary>
    /// Beim Anzeigen die gespeicherte Firmenvorlage laden.
    ///
    /// <c>async void</c> ist hier unvermeidbar – WPF-Ereignisse haben keine
    /// andere Signatur. Deshalb fängt die Methode ihre Fehler selbst ab.
    /// </summary>
    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadTemplateAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _viewModel.ErrorMessage =
                "Die gespeicherten Vorgaben konnten nicht geladen werden. Sie können "
                + $"trotzdem weiterarbeiten. Technische Angabe: {exception.Message}";
        }
    }

    private void ShowCurrentStep()
    {
        UserControl[] views = [ViewSchritt1, ViewSchritt2, ViewSchritt3, ViewSchritt4, ViewSchritt5];

        for (int i = 0; i < views.Length; i++)
        {
            views[i].Visibility = (int)_viewModel.CurrentStep == i + 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsSinglePdf(e, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Eine hereingezogene PDF prüfen. <c>async void</c> ist auch hier durch
    /// das WPF-Ereignis vorgegeben.
    /// </summary>
    private async void OnFileDropped(object sender, DragEventArgs e)
    {
        if (!IsSinglePdf(e, out string? path) || path is null)
        {
            return;
        }

        await InspectAsync(path).ConfigureAwait(true);
    }

    private async Task InspectAsync(string path)
    {
        try
        {
            await _viewModel.PdfSelection.InspectAsync(path).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _viewModel.ErrorMessage =
                $"Die Datei konnte nicht geprüft werden. Technische Angabe: {exception.Message}";
        }
    }

    private static bool IsSinglePdf(DragEventArgs e, out string? path)
    {
        path = null;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
        {
            return false;
        }

        if (!files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = files[0];

        return true;
    }

    /// <summary>Öffnet den Info-Bereich.</summary>
    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        AboutWindow dialog = App.Services.GetRequiredService<AboutWindow>();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    /// <summary>
    /// Öffnet den getrennten read-only Prüfmodus. Das modale Fenster besitzt
    /// einen eigenen flüchtigen Zustand; der laufende Erzeugungsvorgang bleibt
    /// im <see cref="MainViewModel"/> unverändert erhalten.
    /// </summary>
    private void OnCheckEInvoiceClicked(object sender, RoutedEventArgs e)
    {
        EInvoiceCheckWindow dialog = App.Services.GetRequiredService<EInvoiceCheckWindow>();
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    /// <summary>
    /// Öffnet die Einstellungen und übernimmt danach die geänderten Vorgaben.
    ///
    /// <c>async void</c> ist durch das WPF-Ereignis vorgegeben.
    /// </summary>
    private async void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(App.Services.GetRequiredService<SettingsViewModel>())
        {
            Owner = this,
        };

        dialog.ShowDialog();

        try
        {
            // Ob überhaupt etwas gespeichert wurde, entscheidet das ViewModel
            // anhand des Vorgangsstands – nicht dieses Fenster.
            await _viewModel.ApplyChangedTemplateAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _viewModel.ErrorMessage =
                "Die geänderten Vorgaben konnten nicht übernommen werden. Sie gelten ab dem "
                + $"nächsten Start. Technische Angabe: {exception.Message}";
        }
    }
}

using System.Windows;
using EInvoiceSender.App.ViewModels;
using Microsoft.Win32;

namespace EInvoiceSender.App.Views.Dialogs;

/// <summary>Das getrennte Fenster für die read-only Bestandsaufnahme.</summary>
public partial class EInvoiceCheckWindow : Window
{
    private readonly EInvoiceCheckViewModel _viewModel;

    public EInvoiceCheckWindow(EInvoiceCheckViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Wählt genau eine vorhandene PDF-Datei zur Prüfung aus.</summary>
    private async void OnSelectClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "E-Rechnung prüfen",
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await _viewModel.InspectCommand.ExecuteAsync(dialog.FileName).ConfigureAwait(true);
    }

    /// <summary>Eine noch laufende Prüfung beim Schließen kontrolliert abbrechen.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.InspectCancelCommand.Execute(null);
        base.OnClosed(e);
    }
}

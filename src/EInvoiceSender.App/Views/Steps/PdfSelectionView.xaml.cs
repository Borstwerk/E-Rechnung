using System.Windows;
using System.Windows.Controls;
using EInvoiceSender.App.ViewModels;
using Microsoft.Win32;

namespace EInvoiceSender.App.Views.Steps;

/// <summary>
/// Schritt 1. Der Code-behind öffnet nur den Windows-Dateidialog – eine
/// Aufgabe, die ohne WPF-Bezug nicht lösbar ist.
/// </summary>
public partial class PdfSelectionView : UserControl
{
    public PdfSelectionView() => InitializeComponent();

    /// <summary>
    /// <c>async void</c> ist durch das WPF-Ereignis vorgegeben; die Methode
    /// fängt ihre Fehler deshalb selbst ab.
    /// </summary>
    private async void OnSelectClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PdfSelectionViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "PDF-Rechnung auswählen",
            Filter = "PDF-Dateien (*.pdf)|*.pdf",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            await viewModel.InspectAsync(dialog.FileName).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Die Datei konnte nicht geprüft werden.\n\nTechnische Angabe:\n" + exception.Message,
                "EInvoiceSender", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

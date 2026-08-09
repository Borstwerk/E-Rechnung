using System.Windows;
using System.Windows.Controls;
using EInvoiceSender.App.ViewModels;
using Microsoft.Win32;

namespace EInvoiceSender.App.Views.Steps;

/// <summary>
/// Schritt 3. Der Code-behind oeffnet nur den Windows-Ordnerdialog – eine
/// Aufgabe, die ohne WPF-Bezug nicht loesbar ist.
/// </summary>
public partial class ReviewView : UserControl
{
    public ReviewView() => InitializeComponent();

    private void OnChooseFolderClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReviewViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Ordner fuer die erzeugte E-Rechnung waehlen",
            InitialDirectory = viewModel.OutputDirectory,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            viewModel.OutputDirectory = dialog.FolderName;
        }
    }
}

using System.Windows;
using EInvoiceSender.App.ViewModels;

namespace EInvoiceSender.App.Views.Dialogs;

/// <summary>
/// Die Einstellungen. Gespeichert wird ausschliesslich auf Knopfdruck – es gibt
/// bewusst kein stilles Uebernehmen beim Schliessen.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;
        Loaded += OnLoadedAsync;
    }

    /// <summary><c>async void</c> ist durch das WPF-Ereignis vorgegeben.</summary>
    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage =
                $"Die Vorgaben konnten nicht geladen werden. Technische Angabe: {exception.Message}";
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}

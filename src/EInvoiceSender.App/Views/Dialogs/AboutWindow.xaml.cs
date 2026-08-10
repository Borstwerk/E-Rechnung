using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace EInvoiceSender.App.Views.Dialogs;

/// <summary>
/// Der Info-Bereich: Fassung, Verarbeitung, Lizenz, Entstehung und Grenzen.
///
/// Ohne ViewModel. Das Fenster zeigt eine einzige veränderliche Angabe – die
/// Fassungsnummer –, und die steht in der Assembly. Ein ViewModel mit
/// Abhängigkeitsauflösung dafür wäre eine Schicht ohne Aufgabe.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Fassung.Text = $"Fassung {ReadVersion()}";
    }

    /// <summary>
    /// Liest die Fassungsnummer aus der Programmdatei.
    ///
    /// Bevorzugt wird die Informationsfassung, weil dort bei einem Bau aus
    /// der Versionsverwaltung auch der Zusatz steht. Fehlt sie – etwa in
    /// einem Testlauf ohne gesetzte Fassung –, wird die Dateifassung
    /// genommen; fehlt auch die, bleibt eine ehrliche Auskunft statt einer
    /// erfundenen Nummer.
    /// </summary>
    private static string ReadVersion()
    {
        Assembly assembly = typeof(AboutWindow).Assembly;

        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Bei einem Bau aus der Versionsverwaltung hängt .NET den
            // Commit-Hash mit einem Pluszeichen an. Für den Anwender ist er
            // ohne Wert.
            int plus = informational.IndexOf('+', StringComparison.Ordinal);

            return plus > 0 ? informational[..plus] : informational;
        }

        string? file = FileVersionInfo
            .GetVersionInfo(assembly.Location).FileVersion;

        return string.IsNullOrWhiteSpace(file) ? "unbekannt" : file;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}

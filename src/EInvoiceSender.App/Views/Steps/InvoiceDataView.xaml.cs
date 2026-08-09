using System.Windows;
using System.Windows.Controls;

namespace EInvoiceSender.App.Views.Steps;

/// <summary>
/// Eingabemaske für die Rechnungsdaten. Die Fachlogik steht im zugehörigen
/// ViewModel; hier steht nur, was ohne WPF nicht geht.
/// </summary>
public partial class InvoiceDataView : UserControl
{
    public InvoiceDataView() => InitializeComponent();

    /// <summary>
    /// Schließt eine offene Zellenbearbeitung ab, bevor eine Schaltfläche der
    /// Positionsleiste ihre Arbeit tut.
    ///
    /// **Der Fall aus dem manuellen Test:** Der Anwender tippt den Steuersatz
    /// der letzten Position, klickt sofort auf „Summen neu berechnen“ – und die
    /// Summen bleiben leer. Solange die Zelle im Bearbeitungsmodus ist, steht
    /// der getippte Wert nur im Bedienelement, nicht im Entwurf. Der Befehl
    /// rechnet dann mit dem Stand von vorher.
    ///
    /// <see cref="DataGrid.CommitEdit(DataGridEditingUnit, bool)"/> auf der
    /// Zeilenebene schreibt Zelle und Zeile zurück. WPF löst
    /// <see cref="System.Windows.Controls.Primitives.ButtonBase.Click"/> aus,
    /// **bevor** es den gebundenen Befehl ausführt – der Befehl sieht also
    /// bereits die fertigen Werte.
    ///
    /// Kein Umweg über ein Neuladen der Ansicht: Die Bearbeitung wird
    /// abgeschlossen, nicht die Anzeige neu aufgebaut.
    /// </summary>
    private void OnLineActionClicked(object sender, RoutedEventArgs e)
        => Positionen.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
}

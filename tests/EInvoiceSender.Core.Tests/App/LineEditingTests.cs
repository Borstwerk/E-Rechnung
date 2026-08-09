using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Sichert das Zusammenspiel von Positionstabelle, Summen und Prüfung ab.
///
/// **Der Fehler aus dem manuellen Test:** Drei Positionen eingetragen, auf
/// „Summen neu berechnen“ geklickt – nichts. Erst „Weiter“ brachte die Zahlen.
///
/// Dahinter standen zwei Ursachen. Die eine liegt im Kern und ist dort
/// geprüft (<c>DraftTotalsTests</c>): Gerechnet wurde über den Bau einer
/// vollständigen Rechnung. Die andere liegt in der Oberfläche: Solange eine
/// Zelle im Bearbeitungsmodus steht, ist der getippte Wert noch nicht im
/// Entwurf. Diese Hälfte prüft der Test hier – am Quelltext, weil ein
/// laufendes DataGrid einen Windows-Rechner mit Bildschirm bräuchte. Der
/// manuelle Testfall steht in <c>docs/BACKLOG.md</c>.
/// </summary>
public sealed class LineEditingTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    /// <summary>
    /// Jede Schaltfläche, die mit den Positionen arbeitet, muss zuvor eine
    /// offene Zellenbearbeitung abschließen.
    /// </summary>
    [Theory]
    [InlineData("AddLineCommand")]
    [InlineData("RemoveLineCommand")]
    [InlineData("RecalculateTotalsCommand")]
    public void JedeSchaltflächeDerPositionsleisteSchließtDieBearbeitungAb(string command)
    {
        XElement button = Assert.Single(
            View().Descendants(Presentation + "Button"),
            b => b.Attribute("Command")?.Value.Contains(command, StringComparison.Ordinal) == true);

        Assert.True(
            button.Attribute("Click") is not null,
            $"Die Schaltfläche zu {command} schließt keine offene Zellenbearbeitung ab. Ein "
            + "gerade getippter Wert steht dann noch im Bedienelement und nicht im Entwurf; der "
            + "Befehl rechnet mit dem Stand von vorher.");
    }

    /// <summary>
    /// Abgeschlossen wird auf Zeilenebene. Nur die Zelle zu bestätigen ließe
    /// die Zeile offen.
    /// </summary>
    [Fact]
    public void DieBearbeitungWirdAufZeilenebeneAbgeschlossen()
    {
        string quelle = Source("InvoiceDataView.xaml.cs");

        Assert.Contains("CommitEdit(DataGridEditingUnit.Row", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kein Umweg über ein Neuladen: Die Bearbeitung wird abgeschlossen, nicht
    /// die Anzeige neu aufgebaut.
    /// </summary>
    [Fact]
    public void DieAnzeigeWirdNichtNeuAufgebaut()
    {
        string quelle = Source("InvoiceDataView.xaml.cs");

        Assert.DoesNotContain("DataContext =", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.Refresh", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Summen sollen von selbst stimmen: Eine bestätigte Änderung an einer
    /// Position rechnet sie neu, ohne dass jemand eine Schaltfläche drücken
    /// muss.
    /// </summary>
    [Fact]
    public void EineBestätigtePositionsänderungRechnetDieSummenNeu()
    {
        string quelle = Source("InvoiceDataViewModel.cs");

        Assert.Contains("Draft.Lines.CollectionChanged +=", quelle, StringComparison.Ordinal);
        Assert.Contains("line.PropertyChanged +=", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gerechnet wird über die Positionen, nicht über den Bau einer
    /// vollständigen Rechnung. Genau daran scheiterte die Anzeige.
    /// </summary>
    [Fact]
    public void DasBerechnenHängtNichtAmBauEinerVollständigenRechnung()
    {
        string body = MethodBody("InvoiceDataViewModel.cs", "public void RecalculateTotals(");

        Assert.Contains("TryCalculateTotals", body, StringComparison.Ordinal);
        Assert.DoesNotContain("TryBuildInvoice", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Schneidet eine Methode ab ihrer Vereinbarung bis zur nächsten Leerzeile
    /// heraus. Gesucht wird die Vereinbarung, nicht der erste Aufruf: Beide
    /// tragen denselben Namen, und der Aufruf steht weiter oben.
    /// </summary>
    private static string MethodBody(string file, string declaration)
    {
        string quelle = Source(file);
        int start = quelle.IndexOf(declaration, StringComparison.Ordinal);

        Assert.True(start >= 0, $"{declaration} nicht in {file} gefunden.");

        int end = quelle.IndexOf("\n\n", start, StringComparison.Ordinal);

        return end > start ? quelle[start..end] : quelle[start..];
    }

    private static XDocument View()
        => XDocument.Load(ProjectFiles
            .With(".xaml")
            .Single(p => Path.GetFileName(p) == "InvoiceDataView.xaml"));

    private static string Source(string file)
        => File.ReadAllText(ProjectFiles.With(".cs").Single(p => Path.GetFileName(p) == file));
}

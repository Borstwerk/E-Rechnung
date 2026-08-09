using System.Text.RegularExpressions;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft, dass „Neue Rechnung“ jeden Schritt zurücksetzt und die dauerhaften
/// Vorgaben des Anwenders wieder lädt.
///
/// **Der Fehler:** <c>StartOver</c> setzte <c>PdfSelection</c>, <c>Review</c>,
/// <c>Generation</c> und <c>Result</c> zurück – <c>InvoiceData</c> nicht. Die
/// Rechnungsdaten des vorigen Vorgangs blieben im Formular stehen.
///
/// **Warum als Quelltext:** Die ViewModels liegen in <c>EInvoiceSender.App</c>,
/// einem WPF-Projekt; ein Testprojekt mit Verweis darauf liefe nur unter
/// Windows. Die Liste der Schritte liest der Test aus dem ViewModel selbst –
/// ein neu hinzugefügter Schritt muss damit von selbst auch zurückgesetzt
/// werden, sonst schlägt der Test fehl. Was <c>Reset</c> im Entwurf
/// tatsächlich bewirkt, prüft <c>InvoiceDraftResetTests</c> zur Laufzeit.
/// </summary>
public sealed class NewInvoiceResetTests
{
    [Fact]
    public void NeueRechnungSetztJedenSchrittZurück()
    {
        string body = MethodBody("MainViewModel.cs", "StartOverAsync");

        string[] vergessen = [.. StepProperties().Where(step => !body.Contains($"{step}.Reset()", StringComparison.Ordinal))];

        Assert.True(
            vergessen.Length == 0,
            $"Diese Schritte setzt StartOverAsync nicht zurück: {string.Join(", ", vergessen)}. "
            + "Nach „Neue Rechnung“ stehen dort noch die Daten des vorigen Vorgangs.");
    }

    /// <summary>
    /// Ohne diese Prüfung wäre der Test oben wertlos: Findet er keine
    /// Schritte, ist die Liste der vergessenen leer.
    /// </summary>
    [Fact]
    public void EsGibtÜberhauptSchritteZuPrüfen()
    {
        string[] schritte = [.. StepProperties()];

        Assert.True(schritte.Length >= 5, $"Nur {schritte.Length} Schritte gefunden.");
        Assert.Contains("InvoiceData", schritte);
    }

    /// <summary>
    /// Nach dem Zurücksetzen müssen die dauerhaften Vorgaben wieder in das
    /// Formular – eigene Firma, Bankverbindung, Standardwährung,
    /// Zahlungsbedingungen, Ausgabeordner, E-Mail-Vorgaben. Sie kommen aus der
    /// gespeicherten Vorlage, und zwar frisch von der Festplatte.
    /// </summary>
    [Fact]
    public void NeueRechnungLädtDieGespeichertenVorgabenWieder()
    {
        string body = MethodBody("MainViewModel.cs", "StartOverAsync");

        Assert.Contains("LoadTemplateAsync", body, StringComparison.Ordinal);

        int reset = body.LastIndexOf(".Reset()", StringComparison.Ordinal);
        int laden = body.IndexOf("LoadTemplateAsync", StringComparison.Ordinal);

        Assert.True(
            laden > reset,
            "Die Vorlage wird geladen, bevor der letzte Schritt zurückgesetzt ist. Das "
            + "Zurücksetzen würde die gerade eingetragenen Vorgaben wieder löschen.");
    }

    /// <summary>
    /// Das Formular selbst muss alles ablegen, was zur alten Rechnung gehört:
    /// Inhalt, Vorbefüllungshinweis, ausgewählte Zeile, Summen und Befunde.
    /// </summary>
    [Theory]
    [InlineData("Draft.Reset()")]
    [InlineData("Prefill = null")]
    [InlineData("SelectedLine = null")]
    [InlineData("Totals = null")]
    [InlineData("ClearFindings()")]
    public void DasFormularLegtAllesAusDemVorigenVorgangAb(string erwartet)
    {
        string body = MethodBody("InvoiceDataViewModel.cs", "Reset");

        Assert.Contains(erwartet, body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Eine Änderung in den Einstellungen soll für den nächsten Vorgang
    /// gelten, ohne dass die Anwendung neu gestartet werden muss – aber sie
    /// darf keine bereits erfassten Rechnungsdaten überschreiben.
    /// </summary>
    [Fact]
    public void GeänderteEinstellungenGreifenNurVorDemErstenVorgang()
    {
        string body = MethodBody("MainViewModel.cs", "ApplyChangedTemplateAsync");

        Assert.Contains("_formWasOpened", body, StringComparison.Ordinal);
        Assert.Contains("LoadTemplateAsync", body, StringComparison.Ordinal);

        int sperre = body.IndexOf("_formWasOpened", StringComparison.Ordinal);
        int laden = body.IndexOf("LoadTemplateAsync", StringComparison.Ordinal);

        Assert.True(
            sperre < laden,
            "Die Vorlage wird geladen, bevor geprüft ist, ob das Formular schon offen war. "
            + "Damit überschriebe eine Einstellungsänderung eingetippte Rechnungsdaten.");
    }

    /// <summary>Das Fenster muss die Übernahme nach dem Dialog auch anstoßen.</summary>
    [Fact]
    public void DasFensterÜbernimmtDieVorgabenNachDemSchließenDesDialogs()
    {
        string quelle = Source("MainWindow.xaml.cs");

        Assert.Contains("ShowDialog()", quelle, StringComparison.Ordinal);
        Assert.Contains("ApplyChangedTemplateAsync", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Schritte des Assistenten, aus dem ViewModel gelesen: jede
    /// öffentliche Eigenschaft, deren Typ auf <c>ViewModel</c> endet.
    /// </summary>
    private static IEnumerable<string> StepProperties()
        => Regex
            .Matches(Source("MainViewModel.cs"), @"public\s+(\w+ViewModel)\s+(\w+)\s*\{\s*get")
            .Select(m => m.Groups[2].Value);

    /// <summary>
    /// Schneidet den Rumpf einer Methode heraus – von ihrer geschweiften
    /// Klammer bis zur zugehörigen schließenden.
    /// </summary>
    private static string MethodBody(string file, string method)
    {
        string quelle = Source(file);
        int start = quelle.IndexOf($" {method}(", StringComparison.Ordinal);

        Assert.True(start >= 0, $"{method} nicht in {file} gefunden.");

        int open = quelle.IndexOf('{', start);

        Assert.True(open >= 0, $"{method} in {file} hat keinen Rumpf.");

        int depth = 0;

        for (int i = open; i < quelle.Length; i++)
        {
            depth += quelle[i] switch { '{' => 1, '}' => -1, _ => 0 };

            if (depth == 0)
            {
                return quelle[open..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Der Rumpf von {method} endet nicht.");
    }

    private static string Source(string file)
        => File.ReadAllText(
            ProjectFiles.With(".cs").Single(p => Path.GetFileName(p) == file));
}

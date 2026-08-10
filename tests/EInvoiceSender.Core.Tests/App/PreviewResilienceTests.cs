using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält fest, dass die Seitenvorschau niemals wirft.
///
/// **Der Befund aus dem Windows-Testlauf.** Eine PDF mit Öffnungskennwort wurde
/// in Schritt 1 richtig erkannt und mit einem verständlichen deutschen Satz
/// abgelehnt. Unmittelbar danach erschien ein Störungsfenster mit dem Text
/// „Password required or incorrect password.“ – englisch, aus einer
/// Bibliothek, und direkt neben dem Befund, der dasselbe schon auf Deutsch
/// gesagt hatte.
///
/// Die Ursache war eine Aufzählung: Der Vorschaudienst fing
/// <c>IOException</c>, <c>InvalidOperationException</c> und
/// <c>NotSupportedException</c>. PDFium meldet eine kennwortgeschützte Datei
/// mit einer eigenen Ausnahme, die in keiner der drei steckt. Sie lief bis zum
/// letzten Auffangnetz der Anwendung durch, und das zeigt pflichtschuldig, was
/// es bekommt.
///
/// **Warum eine Aufzählung hier grundsätzlich falsch ist:** Was eine fremde PDF
/// in einer nativen Bibliothek auslöst, lässt sich nicht vorher wissen. Die
/// Vorschau ist eine Annehmlichkeit – sie hat kein Recht, den Ablauf
/// anzuhalten, gleich woran sie scheitert.
///
/// Geprüft wird am Quelltext, weil das Prüfprojekt die WPF-Anwendung nicht
/// referenzieren kann – sie ist auf Windows festgelegt.
/// </summary>
public sealed class PreviewResilienceTests
{
    /// <summary>
    /// Gefangen wird alles außer Abbruch und den Zuständen, in denen
    /// Weiterarbeiten ohnehin sinnlos wäre.
    /// </summary>
    [Fact]
    public void DieVorschauFängtJedenFehlerAb()
    {
        string quelle = PreviewService();

        Assert.Contains("is not OperationCanceledException", quelle, StringComparison.Ordinal);
        Assert.Contains("and not OutOfMemoryException", quelle, StringComparison.Ordinal);
        Assert.Contains("and not StackOverflowException", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Keine Aufzählung erwarteter Ausnahmen mehr. Sie sah vollständig aus und
    /// war es nicht – genau das ist die Art Fehler, die erst beim Anwender
    /// auffällt.
    /// </summary>
    [Fact]
    public void DieVorschauZähltKeineErwartetenFehlerMehrAuf()
        => Assert.DoesNotContain(
            "is IOException or InvalidOperationException",
            PreviewService(),
            StringComparison.Ordinal);

    /// <summary>
    /// Und sie liefert in diesem Fall <c>null</c>, statt etwas zu melden. Die
    /// Aussage über die Datei hat die Eingangsprüfung längst getroffen; eine
    /// zweite, technische Meldung daneben verwirrt nur.
    /// </summary>
    [Fact]
    public void DieVorschauMeldetNichtsUndLiefertNichts()
    {
        string quelle = PreviewService();
        int fang = quelle.IndexOf("catch (Exception exception)", StringComparison.Ordinal);

        Assert.True(fang > 0, "Der Auffangblock wurde nicht gefunden.");

        string rest = quelle[fang..];

        Assert.Contains("return null;", rest, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox", rest, StringComparison.Ordinal);
        Assert.DoesNotContain("throw", rest, StringComparison.Ordinal);
    }

    private static string PreviewService()
        => File.ReadAllText(Path.Combine(
            TestPaths.RepositoryRoot, "src", "EInvoiceSender.App", "Services", "PdfPreviewService.cs"));
}

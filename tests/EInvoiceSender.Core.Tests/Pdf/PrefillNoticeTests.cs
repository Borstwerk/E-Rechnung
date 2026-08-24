using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice 5 – der Satz, den Schritt 2 über die Vorbefüllung sagt.
///
/// **Warum das im Kern geprüft wird und nicht am Quelltext der Oberfläche:**
/// Die Meldung entscheidet, *was* gemeldet wird. Sie ist damit dieselbe Art
/// Aussage wie <see cref="DetectionOverview"/> und gehört wie diese in den
/// Kern, wo sie ohne WPF messbar ist. Die Oberfläche entscheidet nur noch,
/// wie der Satz aussieht.
///
/// Drei Dinge sind auseinanderzuhalten, und sie sind nicht dasselbe:
///
/// * übernommene Einzelfelder,
/// * übernommene Rechnungspositionen,
/// * erkannte Positionen, die wegen bereits erfasster Arbeit **nicht**
///   übernommen wurden.
///
/// Der letzte Fall ist der wichtigste: Schritt 1 meldet dann „2 Positionen
/// erkannt“, im Formular steht aber die eigene Zeile. Ohne diesen Satz sähe
/// das nach einem Fehler der Anwendung aus.
/// </summary>
public sealed class PrefillNoticeTests
{
    [Fact]
    public void OhneVorbefüllungBleibtEsStill()
        => Assert.Equal(string.Empty, PrefillNotice.Describe(null));

    [Fact]
    public void EineZusammenfassungOhneJedenTrefferBleibtStill()
        => Assert.Equal(string.Empty, PrefillNotice.Describe(Summary()));

    [Fact]
    public void ÜbernommeneFelderWerdenGenannt()
    {
        string text = PrefillNotice.Describe(Summary(filledFields: 3));

        Assert.Contains("3 Felder", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Position", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Eine Position ist eine Position.** Sie trägt sieben fachliche Werte;
    /// sie als sieben ausgefüllte Felder zu zählen behauptete eine
    /// Arbeitsersparnis, die es so nicht gab, und machte die Zahl der Felder
    /// unbrauchbar.
    /// </summary>
    [Fact]
    public void PositionenZählenNieAlsAusgefüllteFelder()
    {
        string text = PrefillNotice.Describe(Summary(filledFields: 3, filledLines: 2));

        Assert.Contains("3 Felder", text, StringComparison.Ordinal);
        Assert.Contains("2 Rechnungspositionen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("5 Felder", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Übernommene Positionen allein genügen für eine Meldung. Früher hing
    /// der ganze Satz an der Feldzahl; eine reine Tabellenübernahme wäre
    /// unkommentiert geblieben.
    /// </summary>
    [Fact]
    public void ÜbernommenePositionenAlleinGenügenFürEineMeldung()
    {
        string text = PrefillNotice.Describe(Summary(filledLines: 2));

        Assert.Contains("2 Rechnungspositionen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Feld", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nicht übernommene Positionen werden ausdrücklich mit ihrem Grund
    /// genannt. Stillschweigen wäre hier die schlechteste Lösung: Schritt 1
    /// hat die Tabelle gemeldet, im Formular steht sie nicht.
    /// </summary>
    [Fact]
    public void NichtÜbernommenePositionenWerdenMitGrundGenannt()
    {
        string text = PrefillNotice.Describe(Summary(skippedExistingLines: 2));

        Assert.Contains("2 erkannte Rechnungspositionen", text, StringComparison.Ordinal);
        Assert.Contains("nicht übernommen", text, StringComparison.Ordinal);
        Assert.Contains("bereits", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Übernommen und nicht übernommen schließen einander aus – die
    /// Vorbefüllung übernimmt geschlossen oder gar nicht. Der Satz darf
    /// deshalb nie beides gleichzeitig behaupten.
    /// </summary>
    [Fact]
    public void ÜbernahmeUndAblehnungStehenNieImSelbenSatz()
    {
        string übernommen = PrefillNotice.Describe(Summary(filledLines: 2));
        string abgelehnt = PrefillNotice.Describe(Summary(skippedExistingLines: 2));

        Assert.DoesNotContain("nicht übernommen", übernommen, StringComparison.Ordinal);
        Assert.DoesNotContain("wurden übernommen", abgelehnt, StringComparison.Ordinal);
    }

    [Fact]
    public void EinzahlUndMehrzahlStimmen()
    {
        string text = PrefillNotice.Describe(
            Summary(filledFields: 1, filledLines: 1));

        Assert.Contains("1 Feld ", text, StringComparison.Ordinal);
        Assert.Contains("1 Rechnungsposition ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1 Felder", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1 Rechnungspositionen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsichereFelderWerdenZurPrüfungBenannt()
    {
        string text = PrefillNotice.Describe(
            Summary(filledFields: 2, uncertain: ["Rechnungsdatum", "Empfänger"]));

        Assert.Contains("Rechnungsdatum, Empfänger", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Der Schlusssatz sagt nicht mehr „Jeder Wert lässt sich überschreiben“.
    /// Das stimmte nicht: Die Summen werden gerechnet, nicht getippt. Ein Satz,
    /// der mehr verspricht als die Anwendung hält, ist schlimmer als keiner.
    /// </summary>
    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, 2)]
    public void JedeMeldungEndetMitDerAufforderungZuPrüfen(
        int felder, int positionen, int übersprungen)
    {
        string text = PrefillNotice.Describe(Summary(felder, positionen, übersprungen));

        Assert.EndsWith(
            "Bitte prüfen Sie die vorausgefüllten Angaben. "
            + "Bearbeitbare Werte können Sie jederzeit ändern.",
            text,
            StringComparison.Ordinal);

        Assert.DoesNotContain("überschreiben", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fehlt bei übernommenen Positionen die Mengeneinheit, sagt der Satz das
    /// – mit Anzahl. Die leeren Felder im Formular sind sonst nicht erklärbar.
    /// </summary>
    [Fact]
    public void FehlendeMengeneinheitenWerdenGenannt()
    {
        string text = PrefillNotice.Describe(Summary(filledLines: 4, linesMissingUnit: 4));

        Assert.Contains("4 Rechnungspositionen", text, StringComparison.Ordinal);
        Assert.Contains("Bei 4 Positionen fehlt die Mengeneinheit", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EineEinzelneFehlendeMengeneinheitStehtInDerEinzahl()
    {
        string text = PrefillNotice.Describe(Summary(filledLines: 1, linesMissingUnit: 1));

        Assert.Contains("Bei 1 Position fehlt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bei 1 Positionen", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sind alle Einheiten da, entfällt der Satz. Ein Hinweis auf eine Lücke,
    /// die es nicht gibt, macht die übrigen Hinweise unglaubwürdig.
    /// </summary>
    [Fact]
    public void OhneFehlendeMengeneinheitEntfälltDerHinweis()
        => Assert.DoesNotContain(
            "Mengeneinheit",
            PrefillNotice.Describe(Summary(filledLines: 4)),
            StringComparison.Ordinal);

    private static PrefillSummary Summary(
        int filledFields = 0,
        int filledLines = 0,
        int skippedExistingLines = 0,
        int linesMissingUnit = 0,
        IReadOnlyList<string>? uncertain = null)
        => new(
            filledFields, uncertain ?? [], [], [],
            filledLines, skippedExistingLines, linesMissingUnit);
}

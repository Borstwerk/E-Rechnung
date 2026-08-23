using EInvoiceSender.Core.Text;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Formuliert, was die Vorbefüllung getan hat – der Satz über dem Formular in
/// Schritt 2.
///
/// Diese Klasse steht bewusst im Kern und nicht in der Oberfläche, aus
/// demselben Grund wie <see cref="DetectionOverview"/>: Sie entscheidet,
/// **was** gemeldet wird, und ist damit ohne WPF prüfbar. Wie der Satz
/// aussieht – Schriftgrad, Farbe, Platz – entscheidet die Oberfläche.
///
/// **Drei Aussagen, die nicht dasselbe sind:**
///
/// 1. Wie viele Einzelfelder wurden übernommen.
/// 2. Wie viele Rechnungspositionen wurden übernommen.
/// 3. Wie viele erkannte Positionen wurden **nicht** übernommen, weil im
///    Entwurf bereits Positionen standen.
///
/// Der dritte Fall ist der, der ohne Satz nach einem Fehler der Anwendung
/// aussieht: Schritt 1 meldet „Rechnungspositionen erkannt: 2“, im Formular
/// steht aber die selbst getippte Zeile. Beide Aussagen sind richtig, und
/// genau deshalb muss die zweite dazugesagt werden.
/// </summary>
public static class PrefillNotice
{
    /// <summary>
    /// Der Schlusssatz. Er verspricht ausdrücklich nicht mehr, dass sich
    /// „jeder Wert überschreiben“ lasse – das stimmte nicht, denn die Summen
    /// werden gerechnet und nicht getippt. Ein Satz, der mehr zusagt als die
    /// Anwendung hält, ist schlechter als gar keiner.
    /// </summary>
    private const string Closing =
        "Bitte prüfen Sie die vorausgefüllten Angaben. "
        + "Bearbeitbare Werte können Sie jederzeit ändern.";

    /// <summary>
    /// Beschreibt eine Vorbefüllung. Leer, wenn nichts zu berichten ist –
    /// dann zeigt die Oberfläche den Hinweis gar nicht erst an.
    /// </summary>
    public static string Describe(PrefillSummary? summary)
    {
        if (summary is null)
        {
            return string.Empty;
        }

        string[] parts =
        [
            .. Fields(summary),
            .. TakenLines(summary),
            .. SkippedLines(summary),
        ];

        if (parts.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(" ", [.. parts, .. Uncertain(summary), Closing]);
    }

    private static IEnumerable<string> Fields(PrefillSummary summary)
    {
        if (summary.FilledFields == 0)
        {
            yield break;
        }

        yield return $"{Plural.Count(summary.FilledFields, "Feld", "Felder")} "
            + $"{Plural.Word(summary.FilledFields, "wurde", "wurden")} aus der PDF vorausgefüllt.";
    }

    /// <summary>
    /// Positionen zählen als Positionen, nie als Felder. Eine Position trägt
    /// sieben fachliche Werte; sie als sieben ausgefüllte Felder zu zählen
    /// behauptete eine Arbeitsersparnis, die es so nicht gab, und machte die
    /// Zahl der Felder unbrauchbar.
    /// </summary>
    private static IEnumerable<string> TakenLines(PrefillSummary summary)
    {
        if (summary.FilledLines == 0)
        {
            yield break;
        }

        yield return Plural.Count(
            summary.FilledLines, "Rechnungsposition", "Rechnungspositionen")
            + $" {Plural.Word(summary.FilledLines, "wurde", "wurden")} übernommen.";
    }

    /// <summary>
    /// Der Grund gehört in den Satz. „Wurden nicht übernommen“ allein liest
    /// sich wie ein Fehlschlag der Erkennung; tatsächlich ist es eine
    /// Schutzregel, und der Anwender kann sie kennen.
    /// </summary>
    private static IEnumerable<string> SkippedLines(PrefillSummary summary)
    {
        if (summary.SkippedExistingLines == 0)
        {
            yield break;
        }

        yield return $"{summary.SkippedExistingLines} erkannte "
            + Plural.Word(summary.SkippedExistingLines, "Rechnungsposition", "Rechnungspositionen")
            + $" {Plural.Word(summary.SkippedExistingLines, "wurde", "wurden")} nicht übernommen, "
            + "weil bereits Positionen erfasst sind.";
    }

    private static IEnumerable<string> Uncertain(PrefillSummary summary)
    {
        if (summary.UncertainFields.Count == 0)
        {
            yield break;
        }

        yield return $"Bitte prüfen Sie besonders: {string.Join(", ", summary.UncertainFields)}.";
    }
}

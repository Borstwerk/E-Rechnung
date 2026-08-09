using System.Globalization;

namespace EInvoiceSender.Core.Text;

/// <summary>
/// Bildet Einzahl und Mehrzahl in angezeigten Sätzen.
///
/// **Warum es diese Klasse gibt:** In der Oberfläche stand „1 Seite(n)“ und
/// „1 Feld(er)“. Diese Klammerform ist Entwicklersprache; sie hat in einer
/// Anwendung nichts zu suchen, die ein Anwender bedient. Die beiden Formen
/// hier auszuschreiben ist die einfachste Lösung, die das behebt – und sie
/// braucht weder Sprachdateien noch eine Übersetzungsschicht.
///
/// Bewusst ohne Regelwerk: Deutsche Mehrzahlbildung ist unregelmässig
/// („Seite/Seiten“, „Feld/Felder“, „Angabe/Angaben“). Ein Automatismus würde
/// hier mehr falsch als richtig machen. Wer eine Meldung schreibt, kennt beide
/// Formen ohnehin.
/// </summary>
public static class Plural
{
    /// <summary>
    /// Zahl und passendes Hauptwort, etwa <c>2 Seiten</c> oder <c>1 Seite</c>.
    /// </summary>
    public static string Count(int count, string singular, string plural)
        => string.Create(
            CultureInfo.CurrentCulture, $"{count} {Word(count, singular, plural)}");

    /// <summary>
    /// Nur die passende Wortform – für Zeitwörter, die sich der Zahl anpassen
    /// müssen: <c>1 Angabe muss</c>, <c>2 Angaben müssen</c>.
    /// </summary>
    public static string Word(int count, string singular, string plural)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(singular);
        ArgumentException.ThrowIfNullOrWhiteSpace(plural);

        return count == 1 ? singular : plural;
    }
}

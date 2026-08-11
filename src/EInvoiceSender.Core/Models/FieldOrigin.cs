namespace EInvoiceSender.Core.Models;

/// <summary>
/// Woher der Inhalt eines Formularfeldes stammt.
///
/// **Die Reihenfolge der Werte ist bedeutungstragend.** Sie bildet den Vorrang
/// ab: Ein Wert darf einen anderen nur ersetzen, wenn er mindestens denselben
/// Rang hat. <see cref="FieldOriginRules.CanReplace"/> ist die einzige Stelle,
/// an der diese Entscheidung getroffen wird.
/// </summary>
public enum FieldOrigin
{
    /// <summary>
    /// Vom Programm gesetzter Ausgangswert, etwa die Währung EUR.
    ///
    /// Der Anwender hat das Feld nie angefasst. Ein solcher Wert darf von jeder
    /// Quelle ersetzt werden – er ist eine Bequemlichkeit, keine Aussage.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Aus einer gespeicherten Komfortvorgabe übernommen, etwa Standardwährung,
    /// Zahlungsbedingungen oder ein daraus berechnetes Fälligkeitsdatum.
    ///
    /// Diese Werte helfen beim Ausfüllen, sind aber keine Stammdaten der Firma
    /// und keine Aussage über die einzelne Rechnung. Eine brauchbare
    /// PDF-Erkennung darf sie deshalb ersetzen.
    /// </summary>
    TemplateDefault = 1,

    /// <summary>
    /// Aus dem PDF-Text gelesen, Zuordnung aber nicht eindeutig.
    /// Wird sichtbar zur Prüfung gekennzeichnet.
    /// </summary>
    DetectedUncertain = 2,

    /// <summary>Aus dem PDF-Text gelesen, Zuordnung eindeutig.</summary>
    DetectedReliably = 3,

    /// <summary>
    /// Aus der gespeicherten Firmenvorlage.
    ///
    /// Eigene Stammdaten wie Firmenname, Anschrift oder Bankverbindung stehen
    /// über der PDF-Erkennung: Der Anwender hat diese Werte einmal bewusst
    /// hinterlegt. Weicht die PDF davon ab, ist eine Fehlerkennung
    /// wahrscheinlicher als eine Korrektur – und die eigenen Stammdaten
    /// stillschweigend zu ändern wäre überraschend.
    /// </summary>
    Template = 4,

    /// <summary>
    /// Vom Anwender eingegeben oder geändert.
    ///
    /// Wird **nie** automatisch ersetzt. Was durch die Hand des Menschen
    /// gegangen ist, bleibt so stehen.
    /// </summary>
    Manual = 5,
}

/// <summary>
/// Die eine Stelle, an der entschieden wird, ob ein vorgeschlagener Wert einen
/// vorhandenen ersetzen darf.
///
/// Früher stand diese Entscheidung verstreut als Sonderfall an einzelnen
/// Feldern – mit dem Ergebnis, dass sie für die meisten Felder gar nicht
/// stattfand. Eine Regel an einer Stelle ist nicht nur kürzer, sie ist auch
/// prüfbar.
/// </summary>
public static class FieldOriginRules
{
    /// <summary>
    /// Darf ein Wert der Herkunft <paramref name="proposed"/> einen Wert der
    /// Herkunft <paramref name="current"/> ersetzen?
    /// </summary>
    public static bool CanReplace(FieldOrigin current, FieldOrigin proposed)
    {
        // Eine Eingabe des Anwenders wird nie automatisch überschrieben -
        // auch nicht von einer noch so sicheren Erkennung.
        if (current == FieldOrigin.Manual)
        {
            return false;
        }

        return proposed >= current;
    }
}

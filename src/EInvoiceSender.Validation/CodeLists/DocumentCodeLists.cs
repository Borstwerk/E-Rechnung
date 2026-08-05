using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace EInvoiceSender.Validation.CodeLists;

/// <summary>
/// Rechnungsartcodes nach UNTDID 1001 (BT-3), soweit diese Anwendung sie
/// fachlich beherrscht (siehe <c>docs/STANDARDS.md</c>, Abschnitt 5, und
/// <c>docs/AGENTS.md</c> zum Anwendungsumfang). Reine Nachschlagetabelle
/// ohne Geschaeftslogik.
/// </summary>
/// <remarks>
/// 875/876/877 (Teil- und Schlussrechnungen fuer Bauleistungen) sind mit
/// mittlerer Sicherheit belegt – die genaue Abgrenzung zwischen den drei
/// Codes sollte vor produktivem Einsatz gegen die aktuelle UNTDID-1001-Liste
/// gegengeprueft werden.
/// </remarks>
public static class InvoiceTypeCodes
{
    private static readonly FrozenDictionary<int, string> Names = new Dictionary<int, string>
    {
        [380] = "Handelsrechnung",
        [381] = "Gutschrift",
        [384] = "Rechnungskorrektur",
        [386] = "Vorauszahlungsrechnung",
        [389] = "Selbstfakturierte Rechnung",
        [875] = "Teilrechnung (Bauleistung)",
        [876] = "Teilschlussrechnung (Bauleistung)",
        [877] = "Schlussrechnung (Bauleistung)",
    }.ToFrozenDictionary();

    /// <summary>Prueft, ob <paramref name="code"/> ein bekannter Rechnungsartcode ist.</summary>
    public static bool IsValid(int code) => Names.ContainsKey(code);

    /// <summary>Liefert den deutschen Namen der Rechnungsart.</summary>
    public static bool TryGetName(int code, [MaybeNullWhen(false)] out string name)
        => Names.TryGetValue(code, out name);
}

/// <summary>
/// Zahlungsartcodes nach UNTDID 4461 (BT-81), Teilmenge der in dieser
/// Anwendung unterstuetzten Zahlungsarten. Reine Nachschlagetabelle ohne
/// Geschaeftslogik.
/// </summary>
public static class PaymentMeansCodes
{
    private static readonly FrozenDictionary<int, string> Names = new Dictionary<int, string>
    {
        [1] = "Nicht naeher bestimmt",
        [10] = "Barzahlung",
        [20] = "Scheck",
        [30] = "Ueberweisung",
        [42] = "Zahlung auf Bankkonto",
        [48] = "Kartenzahlung",
        [49] = "Lastschrift",
        [57] = "Dauerauftrag",
        [58] = "SEPA-Ueberweisung",
        [59] = "SEPA-Lastschrift",
        [68] = "Onlinezahlungsdienst",
        [97] = "Verrechnung",
    }.ToFrozenDictionary();

    /// <summary>Prueft, ob <paramref name="code"/> eine bekannte Zahlungsart ist.</summary>
    public static bool IsValid(int code) => Names.ContainsKey(code);

    /// <summary>Liefert den deutschen Namen der Zahlungsart.</summary>
    public static bool TryGetName(int code, [MaybeNullWhen(false)] out string name)
        => Names.TryGetValue(code, out name);
}

/// <summary>
/// Begruendungscodes fuer Steuerbefreiungen (BT-120/BT-121) nach der
/// CEF-/VATEX-Codeliste, wie sie u. a. in EN 16931 und Peppol BIS Billing
/// verwendet wird. Reine Nachschlagetabelle ohne Geschaeftslogik.
/// </summary>
/// <remarks>
/// Die Codes selbst sind mit hoher Sicherheit belegt, die deutschen
/// Kurzbeschreibungen sind jedoch eigene, sinngemaesse Zusammenfassungen der
/// jeweiligen Artikel der Richtlinie 2006/112/EG und <b>kein</b> verbindlicher
/// Rechtstext. Vor produktivem Einsatz sollte der genaue Wortlaut gegen die
/// offizielle CEF-VATEX-Liste bzw. die Richtlinie gegengeprueft werden – vgl.
/// die Vertrauensangaben in <c>docs/STANDARDS.md</c>.
/// </remarks>
public static class VatExemptionReasonCodes
{
    private static readonly FrozenDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["VATEX-EU-AE"] = "Steuerschuldnerschaft des Leistungsempfaengers (Reverse Charge)",
        ["VATEX-EU-O"] = "Nicht steuerbarer Umsatz (ausserhalb des Anwendungsbereichs der Mehrwertsteuer)",
        ["VATEX-EU-IC"] = "Innergemeinschaftliche Lieferung, steuerfrei",
        ["VATEX-EU-G"] = "Ausfuhrlieferung ausserhalb der EU, steuerfrei",
        ["VATEX-EU-D"] = "Innergemeinschaftlicher Erwerb von Gebrauchtfahrzeugen (Differenzbesteuerung)",
        ["VATEX-EU-F"] = "Innergemeinschaftlicher Erwerb von Gebrauchtgegenstaenden, Kunstgegenstaenden, Sammlungsstuecken und Antiquitaeten (Differenzbesteuerung)",
        ["VATEX-EU-I"] = "Erwerb von Kunstgegenstaenden, Sammlungsstuecken und Antiquitaeten (Differenzbesteuerung)",
        ["VATEX-EU-J"] = "Innergemeinschaftliche Lieferung eines neuen Fahrzeugs",
        ["VATEX-EU-79-C"] = "Steuerbefreiung nach Art. 79 Buchst. c der Richtlinie 2006/112/EG (Preisnachlaesse/durchlaufende Posten)",
        ["VATEX-EU-132"] = "Steuerbefreiung nach Art. 132 der Richtlinie 2006/112/EG (Taetigkeiten von allgemeinem Interesse)",
        ["VATEX-EU-143"] = "Steuerbefreiung nach Art. 143 der Richtlinie 2006/112/EG (Einfuhrbefreiungen)",
        ["VATEX-EU-148"] = "Steuerbefreiung nach Art. 148 der Richtlinie 2006/112/EG (grenzueberschreitender See- und Luftverkehr)",
        ["VATEX-EU-151"] = "Steuerbefreiung nach Art. 151 der Richtlinie 2006/112/EG (voelkerrechtliche Einrichtungen, diplomatische und konsularische Beziehungen)",
        ["VATEX-EU-309"] = "Steuerbefreiung fuer Reisebueros nach Art. 309 der Richtlinie 2006/112/EG (Margenbesteuerung)",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Prueft, ob der Code ein bekannter Basiscode ist **oder** ein Untercode
    /// eines bekannten Basiscodes.
    ///
    /// Hintergrund: Die VATEX-Liste kennt zu mehreren Artikeln feiner
    /// gegliederte Untercodes, etwa <c>VATEX-EU-132-1A</c> fuer
    /// Artikel 132 Absatz 1 Buchstabe a. Diese Untercodes hier einzeln
    /// aufzufuehren, wuerde bedeuten, sie aus dem Gedaechtnis zu erfinden – die
    /// offizielle Liste war bei der Erstellung nicht abrufbar. Stattdessen wird
    /// die Zugehoerigkeit zu einem bekannten Basiscode geprueft.
    ///
    /// Das ist bewusst grosszuegig: Ein unbekannter Untercode fuehrt ohnehin nur
    /// zu einer Warnung, und die verbindliche Pruefung uebernimmt das
    /// CEN-Schematron. Ein falscher Alarm bei einem gueltigen Code waere fuer
    /// den Anwender dagegen aergerlich und verwirrend.
    /// </summary>
    public static bool IsValidOrKnownSubcode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string trimmed = code.Trim();

        if (IsValid(trimmed))
        {
            return true;
        }

        foreach (string known in Names.Keys)
        {
            if (trimmed.Length > known.Length
                && trimmed.StartsWith(known, StringComparison.Ordinal)
                && trimmed[known.Length] == '-')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Prueft, ob <paramref name="code"/> ein bekannter VATEX-Code ist.
    /// Anders als bei den uebrigen Codelisten wird hier <b>fallsensitiv</b>
    /// geprueft, wie in der VATEX-Liste veroeffentlicht – nur umgebende
    /// Leerzeichen werden entfernt. Liefert <see langword="false"/> bei
    /// <see langword="null"/>, leerem oder reinem Leerraum-Text – wirft nie.
    /// </summary>
    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Names.ContainsKey(code.Trim());
    }

    /// <summary>Liefert die deutsche Kurzbeschreibung des Befreiungsgrunds.</summary>
    public static bool TryGetName(string? code, [MaybeNullWhen(false)] out string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            name = null;
            return false;
        }

        return Names.TryGetValue(code.Trim(), out name);
    }
}

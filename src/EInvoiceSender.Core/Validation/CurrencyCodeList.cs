using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Waehrungskennungen nach ISO 4217 (BT-5). Reine Nachschlagetabelle ohne
/// Geschaeftslogik – die Pruefung, ob eine Regel daraus eine Warnung oder
/// einen Fehler macht, gehoert in die Validierungsregeln, nicht hierher.
/// </summary>
/// <remarks>
/// Diese Liste ist eine <b>kuratierte Teilmenge</b> von ISO 4217: der
/// Euro-Raum, die uebrigen EU-/EWR-Waehrungen sowie die im internationalen
/// Zahlungsverkehr gaengigsten Waehrungen. Sie erhebt keinen Anspruch auf
/// Vollstaendigkeit gegenueber der vollen ISO-4217-Liste (rund 180 aktive
/// Waehrungen). Ein Code, der hier nicht enthalten ist, ist deshalb
/// <b>nicht automatisch ungueltig</b> – aufrufender Code sollte einen
/// unbekannten Code als Warnung behandeln, nicht als harten Fehler.
/// HRK (Kroatische Kuna) ist seit der Euro-Einfuehrung Kroatiens 2023 in
/// ISO 4217 zurueckgezogen; sie ist hier trotzdem als historischer Wert
/// gefuehrt, da Altbelege damit referenziert sein koennen.
/// </remarks>
public static class CurrencyCodeList
{
    private static readonly FrozenDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["EUR"] = "Euro",
        ["USD"] = "US-Dollar",
        ["GBP"] = "Britisches Pfund",
        ["CHF"] = "Schweizer Franken",
        ["JPY"] = "Japanischer Yen",
        ["CNY"] = "Renminbi Yuan",
        ["CAD"] = "Kanadischer Dollar",
        ["AUD"] = "Australischer Dollar",
        ["NZD"] = "Neuseeland-Dollar",
        ["SEK"] = "Schwedische Krone",
        ["NOK"] = "Norwegische Krone",
        ["DKK"] = "Daenische Krone",
        ["ISK"] = "Islaendische Krone",
        ["PLN"] = "Polnischer Zloty",
        ["CZK"] = "Tschechische Krone",
        ["HUF"] = "Ungarischer Forint",
        ["RON"] = "Rumaenischer Leu",
        ["BGN"] = "Bulgarischer Lew",
        ["HRK"] = "Kroatische Kuna (zurueckgezogen seit 2023)",
        ["TRY"] = "Tuerkische Lira",
        ["RUB"] = "Russischer Rubel",
        ["UAH"] = "Ukrainische Hrywnja",
        ["RSD"] = "Serbischer Dinar",
        ["ALL"] = "Albanischer Lek",
        ["MKD"] = "Mazedonischer Denar",
        ["BAM"] = "Konvertible Mark (Bosnien und Herzegowina)",
        ["MDL"] = "Moldau-Leu",
        ["GEL"] = "Georgischer Lari",
        ["ILS"] = "Israelischer Schekel",
        ["AED"] = "VAE-Dirham",
        ["SAR"] = "Saudi-Riyal",
        ["ZAR"] = "Suedafrikanischer Rand",
        ["BRL"] = "Brasilianischer Real",
        ["MXN"] = "Mexikanischer Peso",
        ["INR"] = "Indische Rupie",
        ["SGD"] = "Singapur-Dollar",
        ["HKD"] = "Hongkong-Dollar",
        ["KRW"] = "Suedkoreanischer Won",
        ["THB"] = "Thailaendischer Baht",
        ["MYR"] = "Malaysischer Ringgit",
        ["IDR"] = "Indonesische Rupiah",
        ["PHP"] = "Philippinischer Peso",
        ["VND"] = "Vietnamesischer Dong",
        ["TWD"] = "Neuer Taiwan-Dollar",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Alle Waehrungen als Auswahlliste, nach dem Code sortiert.
    /// Fuer die Auswahlfelder der Oberflaeche.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name)> All { get; } =
        [.. Names.Select(e => (Code: e.Key.ToUpperInvariant(), Name: e.Value))
                 .OrderBy(e => e.Code, StringComparer.Ordinal)];

    /// <summary>
    /// Prueft, ob <paramref name="code"/> in der kuratierten Teilmenge
    /// enthalten ist. Gross-/Kleinschreibung und umgebende Leerzeichen
    /// spielen keine Rolle. Liefert <see langword="false"/> bei
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

    /// <summary>
    /// Liefert den deutschen Namen der Waehrung, sofern sie in der Liste
    /// gefuehrt wird.
    /// </summary>
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

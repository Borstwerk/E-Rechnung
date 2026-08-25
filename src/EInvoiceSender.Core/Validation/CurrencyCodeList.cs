using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Währungskennungen nach ISO 4217 (BT-5). Reine Nachschlagetabelle ohne
/// Geschäftslogik – die Prüfung, ob eine Regel daraus eine Warnung oder
/// einen Fehler macht, gehört in die Validierungsregeln, nicht hierher.
/// </summary>
/// <remarks>
/// <para>
/// Diese Liste kennt <b>zwei verschiedene Aussagen</b>, und das ist ihr
/// eigentlicher Zweck:
/// </para>
/// <list type="bullet">
///   <item><see cref="IsOffered"/> – „diesen Code bietet BorstWerk zur
///   Erstellung an“. Eine <b>kuratierte Teilmenge</b> von ISO 4217: der
///   Euro-Raum, die übrigen EU-/EWR-Währungen und die im internationalen
///   Zahlungsverkehr gängigsten Währungen. Rund 180 Währungen sind nach
///   ISO 4217 aktiv; die volle Liste ist hier bewusst nicht abgebildet.</item>
///   <item><see cref="IsWithdrawnFromEn16931"/> – „dieser Code ist aus dem
///   gepinnten EN-16931-Codebestand entfernt“. Das ist eine Aussage über die
///   Norm und trifft nur auf die wenigen Codes zu, deren Rückzug hier belegt
///   ist.</item>
/// </list>
/// <para>
/// Ein Code, der <b>weder</b> angeboten <b>noch</b> als zurückgezogen geführt
/// wird, ist damit ausdrücklich <b>nicht</b> als normwidrig gekennzeichnet –
/// über ihn trifft diese Anwendung schlicht keine Aussage. Beides zu
/// vermengen wäre besonders im späteren Prüfmodus fatal: Eine fremde, gültige
/// E-Rechnung dürfte nie beanstandet werden, nur weil BorstWerk ihre Währung
/// selbst nicht zur Auswahl stellt.
/// </para>
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
        ["DKK"] = "Dänische Krone",
        ["ISK"] = "Isländische Krone",
        ["PLN"] = "Polnischer Zloty",
        ["CZK"] = "Tschechische Krone",
        ["HUF"] = "Ungarischer Forint",
        ["RON"] = "Rumänischer Leu",
        ["TRY"] = "Türkische Lira",
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
        ["ZAR"] = "Südafrikanischer Rand",
        ["BRL"] = "Brasilianischer Real",
        ["MXN"] = "Mexikanischer Peso",
        ["INR"] = "Indische Rupie",
        ["SGD"] = "Singapur-Dollar",
        ["HKD"] = "Hongkong-Dollar",
        ["KRW"] = "Südkoreanischer Won",
        ["THB"] = "Thailändischer Baht",
        ["MYR"] = "Malaysischer Ringgit",
        ["IDR"] = "Indonesische Rupiah",
        ["PHP"] = "Philippinischer Peso",
        ["VND"] = "Vietnamesischer Dong",
        ["TWD"] = "Neuer Taiwan-Dollar",
        ["XCG"] = "Karibischer Gulden",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Codes, die aus dem von BorstWerk gepinnten EN-16931-Codebestand
    /// entfernt sind. Anders als bei einem schlicht nicht angebotenen Code ist
    /// hier belegt, dass er nicht mehr gilt – deshalb darf das Regelwerk ihn
    /// als Normbefund behandeln.
    /// </summary>
    private static readonly FrozenDictionary<string, string> Withdrawn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ANG"] = "Niederländische-Antillen-Gulden, abgelöst durch XCG",
        ["BGN"] = "Bulgarischer Lew, mit dem Codebestand v17b entfernt",
        ["HRK"] = "Kroatische Kuna, seit der Euro-Einführung Kroatiens 2023 zurückgezogen",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Alle Währungen als Auswahlliste, nach dem Code sortiert.
    /// Für die Auswahlfelder der Oberfläche.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name)> All { get; } =
        [.. Names.Select(e => (Code: e.Key.ToUpperInvariant(), Name: e.Value))
                 .OrderBy(e => e.Code, StringComparer.Ordinal)];

    /// <summary>
    /// Prüft, ob BorstWerk <paramref name="code"/> zur Erstellung anbietet.
    ///
    /// **Das ist keine Aussage über die Norm.** Ein Code, für den dies
    /// <see langword="false"/> liefert, kann nach ISO 4217 vollkommen gültig
    /// sein – er steht nur nicht in der kuratierten Auswahl dieser Anwendung.
    /// Wer wissen will, ob ein Code nachweislich nicht mehr gilt, fragt
    /// <see cref="IsWithdrawnFromEn16931"/>.
    ///
    /// Gross-/Kleinschreibung und umgebende Leerzeichen spielen keine Rolle.
    /// Liefert <see langword="false"/> bei <see langword="null"/>, leerem oder
    /// reinem Leerraum-Text – wirft nie.
    /// </summary>
    public static bool IsOffered(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Names.ContainsKey(code.Trim());
    }

    /// <summary>
    /// Prüft, ob <paramref name="code"/> aus dem gepinnten
    /// EN-16931-Codebestand entfernt wurde.
    ///
    /// Nur für diese wenigen Codes ist der Rückzug belegt; für alles andere
    /// liefert die Methode <see langword="false"/> – auch für Codes, die diese
    /// Anwendung gar nicht kennt. <see langword="false"/> heißt hier also
    /// „nicht als zurückgezogen belegt“, nicht „gültig“.
    /// </summary>
    public static bool IsWithdrawnFromEn16931(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Withdrawn.ContainsKey(code.Trim());
    }

    /// <summary>
    /// Liefert die Begründung, weshalb ein Code nicht mehr gilt – für den
    /// technischen Teil eines Befunds.
    /// </summary>
    public static bool TryGetWithdrawalReason(string? code, [MaybeNullWhen(false)] out string reason)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            reason = null;
            return false;
        }

        return Withdrawn.TryGetValue(code.Trim(), out reason);
    }

    /// <summary>
    /// Liefert den deutschen Namen der Währung, sofern sie in der Liste
    /// geführt wird.
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

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
/// Diese Liste kennt <b>zwei voneinander unabhängige Aussagen</b>, und das
/// ist ihr eigentlicher Zweck:
/// </para>
/// <list type="bullet">
///   <item><see cref="IsValidPerEn16931"/> – „dieser Code steht im gepinnten
///   EN-16931-Codebestand v17b“. Das ist die <b>Aussage über die Norm</b> und
///   die einzige zulässige Grundlage für einen Normbefund. Sie beruht auf dem
///   vollständigen Bestand von <see cref="NormCodes"/>, nicht auf einer Liste
///   bekannter Ausfälle.</item>
///   <item><see cref="IsOffered"/> – „diesen Code bietet BorstWerk zur
///   Erstellung an“. Eine <b>kuratierte Teilmenge</b>: der Euro-Raum, die
///   übrigen EU-/EWR-Währungen und die im internationalen Zahlungsverkehr
///   gängigsten Währungen. Das ist eine Aussage über dieses Programm und über
///   die Norm ausdrücklich <b>keine</b>.</item>
/// </list>
/// <para>
/// Die beiden dürfen nie ineinander übersetzt werden. Ein normgültiger, aber
/// nicht angebotener Code wie <c>KZT</c> ist kein Normverstoß; ein erfundener
/// Code wie <c>XYZ</c> dagegen sehr wohl. Beides zu vermengen wäre besonders
/// im späteren Prüfmodus fatal: Eine fremde, gültige E-Rechnung dürfte nie
/// beanstandet werden, nur weil BorstWerk ihre Währung selbst nicht zur
/// Auswahl stellt.
/// </para>
/// </remarks>
public static class CurrencyCodeList
{
    /// <summary>
    /// Der vollständige Währungscodebestand des Stands, gegen den diese
    /// Anwendung erzeugt.
    ///
    /// <para><b>Quelle.</b> ZUGFeRD 2.5.2 / Factur-X 1.09.2, „EN16931 code
    /// lists values v17b“, Veröffentlichung 2026-04-16, anzuwenden ab
    /// 2026-05-15; Liste <c>Currency</c> mit 178 Codes. Der Bestand wurde aus
    /// zwei Stellen desselben Artefakts gelesen (Codelisten-Arbeitsmappe und
    /// <c>FACTUR-X_EN16931_codedb.xml</c>, Code list id 24) und stimmte
    /// überein.</para>
    ///
    /// <para><b>Abgesichert.</b> Alphabetisch sortiert und je Code mit
    /// Zeilenvorschub verkettet ergibt dieser Bestand die SHA-256-Prüfsumme
    /// <c>fb4f5fb74e80a59d37ace3a95b0ad1063db70a6e2fa582772e2c865e0f3610b2</c>.
    /// Ein Test rechnet sie nach – eine einzelne veränderte, ausgelassene oder
    /// hinzuerfundene Kennung fällt damit sofort auf und nicht erst dem
    /// Empfänger einer Rechnung.</para>
    ///
    /// <para>Der Bestand wird bewusst nicht zur Laufzeit geladen: Kein
    /// Netzzugriff im Erzeugungsweg. Er ist auch keine Kopie des
    /// FeRD-Artefakts, sondern allein dessen Codespalte.</para>
    /// </summary>
    public static IReadOnlySet<string> NormCodes { get; } = new[]
    {
        "AED", "AFN", "ALL", "AMD", "AOA", "ARS", "AUD", "AWG",
        "AZN", "BAM", "BBD", "BDT", "BHD", "BIF", "BMD", "BND",
        "BOB", "BOV", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD",
        "CAD", "CDF", "CHE", "CHF", "CHW", "CLF", "CLP", "CNH",
        "CNY", "COP", "COU", "CRC", "CUP", "CVE", "CZK", "DJF",
        "DKK", "DOP", "DZD", "EGP", "ERN", "ETB", "EUR", "FJD",
        "FKP", "GBP", "GEL", "GHS", "GIP", "GMD", "GNF", "GTQ",
        "GYD", "HKD", "HNL", "HTG", "HUF", "IDR", "ILS", "INR",
        "IQD", "IRR", "ISK", "JMD", "JOD", "JPY", "KES", "KGS",
        "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT", "LAK",
        "LBP", "LKR", "LRD", "LSL", "LYD", "MAD", "MDL", "MGA",
        "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK",
        "MXN", "MXV", "MYR", "MZN", "NAD", "NGN", "NIO", "NOK",
        "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR",
        "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR",
        "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SOS",
        "SRD", "SSP", "STN", "SVC", "SYP", "SZL", "THB", "TJS",
        "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS", "UAH",
        "UGX", "USD", "USN", "UYI", "UYU", "UYW", "UZS", "VED",
        "VES", "VND", "VUV", "WST", "XAF", "XAG", "XAU", "XBA",
        "XBB", "XBC", "XBD", "XCD", "XCG", "XDR", "XOF", "XPD",
        "XPF", "XPT", "XSU", "XTS", "XUA", "XXX", "YER", "ZAR",
        "ZMW", "ZWG",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Die Auswahl, die BorstWerk zur Erstellung anbietet – mit deutschem
    /// Namen für die Oberfläche.
    ///
    /// Jeder Eintrag hier muss auch in <see cref="NormCodes"/> stehen; ein
    /// Test wacht darüber. Umgekehrt gilt das nicht und soll es nicht: Der
    /// Normbestand führt 178 Kennungen, darunter Edelmetalle, Sonderziehungs-
    /// rechte und Rechnungseinheiten, die in einer Rechnungsanwendung nichts
    /// zur Auswahl zu suchen haben.
    /// </summary>
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
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Erläuterungen zu Kennungen, die dieser Anwendung schon einmal begegnet
    /// sind und im Bestand v17b nicht mehr stehen.
    ///
    /// <para><b>Das ist kein Prüfkriterium.</b> Ob ein Code gilt, entscheidet
    /// allein <see cref="NormCodes"/>. Diese Tabelle liefert nur den Satz, der
    /// im technischen Teil eines Befunds erklärt, warum ein früher üblicher
    /// Code jetzt abgelehnt wird – für einen Anwender, der ihn jahrelang
    /// verwendet hat, ist „steht nicht im Codebestand“ eine ärgerlich dünne
    /// Auskunft.</para>
    /// </summary>
    private static readonly FrozenDictionary<string, string> WithdrawalNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ANG"] = "Niederländische-Antillen-Gulden, im Codebestand v17b nicht mehr enthalten; an seine Stelle tritt XCG.",
        ["BGN"] = "Bulgarischer Lew, im Codebestand v17b nicht mehr enthalten.",
        ["HRK"] = "Kroatische Kuna, im Codebestand v17b nicht mehr enthalten.",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Die angebotenen Währungen als Auswahlliste, nach dem Code sortiert.
    /// Für die Auswahlfelder der Oberfläche.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name)> All { get; } =
        [.. Names.Select(e => (Code: e.Key.ToUpperInvariant(), Name: e.Value))
                 .OrderBy(e => e.Code, StringComparer.Ordinal)];

    /// <summary>
    /// Prüft, ob <paramref name="code"/> im gepinnten EN-16931-Codebestand
    /// v17b steht.
    ///
    /// **Das ist die einzige Methode dieser Klasse, die etwas über die Norm
    /// aussagt.** <see langword="false"/> heißt hier tatsächlich „nach dem
    /// abgeglichenen Stand ungültig“ – für einen zurückgezogenen Code wie
    /// <c>BGN</c> ebenso wie für einen erfundenen wie <c>XYZ</c>.
    ///
    /// Gross-/Kleinschreibung und umgebende Leerzeichen spielen keine Rolle.
    /// Liefert <see langword="false"/> bei <see langword="null"/>, leerem oder
    /// reinem Leerraum-Text – wirft nie.
    /// </summary>
    public static bool IsValidPerEn16931(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return NormCodes.Contains(code.Trim());
    }

    /// <summary>
    /// Prüft, ob BorstWerk <paramref name="code"/> zur Erstellung anbietet.
    ///
    /// **Das ist keine Aussage über die Norm.** Ein Code, für den dies
    /// <see langword="false"/> liefert, kann nach EN 16931 vollkommen gültig
    /// sein – er steht nur nicht in der kuratierten Auswahl dieser Anwendung.
    /// Wer wissen will, ob ein Code gilt, fragt
    /// <see cref="IsValidPerEn16931"/>.
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
    /// Liefert, sofern bekannt, die Erläuterung zu einer zurückgezogenen
    /// Kennung – für den technischen Teil eines Befunds.
    ///
    /// <see langword="false"/> heißt nur „dazu ist hier nichts hinterlegt“ und
    /// ist keine Aussage über die Gültigkeit. Die trifft
    /// <see cref="IsValidPerEn16931"/>.
    /// </summary>
    public static bool TryGetWithdrawalReason(string? code, [MaybeNullWhen(false)] out string reason)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            reason = null;
            return false;
        }

        return WithdrawalNotes.TryGetValue(code.Trim(), out reason);
    }

    /// <summary>
    /// Liefert den deutschen Namen der Währung, sofern sie zur Auswahl
    /// angeboten wird.
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

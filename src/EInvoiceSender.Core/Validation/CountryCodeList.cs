using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Länderkennungen nach ISO 3166-1 alpha-2 (BT-40 / BT-55). Reine
/// Nachschlagetabelle ohne Geschäftslogik. Anders als bei den übrigen
/// Codelisten dieses Namensraums ist diese Liste bewusst
/// <b>vollständig</b>: alle aktuell offiziell zugewiesenen
/// ISO-3166-1-alpha-2-Codes (rund 249 Stück), alphabetisch nach Code
/// sortiert. Zurückgezogene Codes (z. B. <c>AN</c>, <c>CS</c>, <c>TP</c>,
/// <c>ZR</c>) und nicht offiziell zugewiesene Codes (z. B. <c>XK</c> für
/// den Kosovo, der nur als Übergangscode einzelner Organisationen
/// verwendet wird) sind absichtlich nicht enthalten.
/// </summary>
public static class CountryCodeList
{
    private static readonly FrozenDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AD"] = "Andorra",
        ["AE"] = "Vereinigte Arabische Emirate",
        ["AF"] = "Afghanistan",
        ["AG"] = "Antigua und Barbuda",
        ["AI"] = "Anguilla",
        ["AL"] = "Albanien",
        ["AM"] = "Armenien",
        ["AO"] = "Angola",
        ["AQ"] = "Antarktis",
        ["AR"] = "Argentinien",
        ["AS"] = "Amerikanisch-Samoa",
        ["AT"] = "Österreich",
        ["AU"] = "Australien",
        ["AW"] = "Aruba",
        ["AX"] = "Aalandinseln",
        ["AZ"] = "Aserbaidschan",
        ["BA"] = "Bosnien und Herzegowina",
        ["BB"] = "Barbados",
        ["BD"] = "Bangladesch",
        ["BE"] = "Belgien",
        ["BF"] = "Burkina Faso",
        ["BG"] = "Bulgarien",
        ["BH"] = "Bahrain",
        ["BI"] = "Burundi",
        ["BJ"] = "Benin",
        ["BL"] = "Saint-Barthelemy",
        ["BM"] = "Bermuda",
        ["BN"] = "Brunei Darussalam",
        ["BO"] = "Bolivien",
        ["BQ"] = "Bonaire, Sint Eustatius und Saba",
        ["BR"] = "Brasilien",
        ["BS"] = "Bahamas",
        ["BT"] = "Bhutan",
        ["BV"] = "Bouvetinsel",
        ["BW"] = "Botsuana",
        ["BY"] = "Belarus",
        ["BZ"] = "Belize",
        ["CA"] = "Kanada",
        ["CC"] = "Kokosinseln",
        ["CD"] = "Kongo, Demokratische Republik",
        ["CF"] = "Zentralafrikanische Republik",
        ["CG"] = "Kongo",
        ["CH"] = "Schweiz",
        ["CI"] = "Cote d'Ivoire",
        ["CK"] = "Cookinseln",
        ["CL"] = "Chile",
        ["CM"] = "Kamerun",
        ["CN"] = "China",
        ["CO"] = "Kolumbien",
        ["CR"] = "Costa Rica",
        ["CU"] = "Kuba",
        ["CV"] = "Cabo Verde",
        ["CW"] = "Curacao",
        ["CX"] = "Weihnachtsinsel",
        ["CY"] = "Zypern",
        ["CZ"] = "Tschechien",
        ["DE"] = "Deutschland",
        ["DJ"] = "Dschibuti",
        ["DK"] = "Dänemark",
        ["DM"] = "Dominica",
        ["DO"] = "Dominikanische Republik",
        ["DZ"] = "Algerien",
        ["EC"] = "Ecuador",
        ["EE"] = "Estland",
        ["EG"] = "Ägypten",
        ["EH"] = "Westsahara",
        ["ER"] = "Eritrea",
        ["ES"] = "Spanien",
        ["ET"] = "Äthiopien",
        ["FI"] = "Finnland",
        ["FJ"] = "Fidschi",
        ["FK"] = "Falklandinseln",
        ["FM"] = "Mikronesien",
        ["FO"] = "Färöer",
        ["FR"] = "Frankreich",
        ["GA"] = "Gabun",
        ["GB"] = "Vereinigtes Königreich",
        ["GD"] = "Grenada",
        ["GE"] = "Georgien",
        ["GF"] = "Französisch-Guayana",
        ["GG"] = "Guernsey",
        ["GH"] = "Ghana",
        ["GI"] = "Gibraltar",
        ["GL"] = "Grönland",
        ["GM"] = "Gambia",
        ["GN"] = "Guinea",
        ["GP"] = "Guadeloupe",
        ["GQ"] = "Äquatorialguinea",
        ["GR"] = "Griechenland",
        ["GS"] = "Südgeorgien und die Südlichen Sandwichinseln",
        ["GT"] = "Guatemala",
        ["GU"] = "Guam",
        ["GW"] = "Guinea-Bissau",
        ["GY"] = "Guyana",
        ["HK"] = "Hongkong",
        ["HM"] = "Heard- und McDonald-Inseln",
        ["HN"] = "Honduras",
        ["HR"] = "Kroatien",
        ["HT"] = "Haiti",
        ["HU"] = "Ungarn",
        ["ID"] = "Indonesien",
        ["IE"] = "Irland",
        ["IL"] = "Israel",
        ["IM"] = "Insel Man",
        ["IN"] = "Indien",
        ["IO"] = "Britisches Territorium im Indischen Ozean",
        ["IQ"] = "Irak",
        ["IR"] = "Iran",
        ["IS"] = "Island",
        ["IT"] = "Italien",
        ["JE"] = "Jersey",
        ["JM"] = "Jamaika",
        ["JO"] = "Jordanien",
        ["JP"] = "Japan",
        ["KE"] = "Kenia",
        ["KG"] = "Kirgisistan",
        ["KH"] = "Kambodscha",
        ["KI"] = "Kiribati",
        ["KM"] = "Komoren",
        ["KN"] = "St. Kitts und Nevis",
        ["KP"] = "Nordkorea",
        ["KR"] = "Südkorea",
        ["KW"] = "Kuwait",
        ["KY"] = "Kaimaninseln",
        ["KZ"] = "Kasachstan",
        ["LA"] = "Laos",
        ["LB"] = "Libanon",
        ["LC"] = "St. Lucia",
        ["LI"] = "Liechtenstein",
        ["LK"] = "Sri Lanka",
        ["LR"] = "Liberia",
        ["LS"] = "Lesotho",
        ["LT"] = "Litauen",
        ["LU"] = "Luxemburg",
        ["LV"] = "Lettland",
        ["LY"] = "Libyen",
        ["MA"] = "Marokko",
        ["MC"] = "Monaco",
        ["MD"] = "Republik Moldau",
        ["ME"] = "Montenegro",
        ["MF"] = "Saint-Martin (französischer Teil)",
        ["MG"] = "Madagaskar",
        ["MH"] = "Marshallinseln",
        ["MK"] = "Nordmazedonien",
        ["ML"] = "Mali",
        ["MM"] = "Myanmar",
        ["MN"] = "Mongolei",
        ["MO"] = "Macau",
        ["MP"] = "Nördliche Marianen",
        ["MQ"] = "Martinique",
        ["MR"] = "Mauretanien",
        ["MS"] = "Montserrat",
        ["MT"] = "Malta",
        ["MU"] = "Mauritius",
        ["MV"] = "Malediven",
        ["MW"] = "Malawi",
        ["MX"] = "Mexiko",
        ["MY"] = "Malaysia",
        ["MZ"] = "Mosambik",
        ["NA"] = "Namibia",
        ["NC"] = "Neukaledonien",
        ["NE"] = "Niger",
        ["NF"] = "Norfolkinsel",
        ["NG"] = "Nigeria",
        ["NI"] = "Nicaragua",
        ["NL"] = "Niederlande",
        ["NO"] = "Norwegen",
        ["NP"] = "Nepal",
        ["NR"] = "Nauru",
        ["NU"] = "Niue",
        ["NZ"] = "Neuseeland",
        ["OM"] = "Oman",
        ["PA"] = "Panama",
        ["PE"] = "Peru",
        ["PF"] = "Französisch-Polynesien",
        ["PG"] = "Papua-Neuguinea",
        ["PH"] = "Philippinen",
        ["PK"] = "Pakistan",
        ["PL"] = "Polen",
        ["PM"] = "Saint-Pierre und Miquelon",
        ["PN"] = "Pitcairninseln",
        ["PR"] = "Puerto Rico",
        ["PS"] = "Staat Palästina",
        ["PT"] = "Portugal",
        ["PW"] = "Palau",
        ["PY"] = "Paraguay",
        ["QA"] = "Katar",
        ["RE"] = "Reunion",
        ["RO"] = "Rumänien",
        ["RS"] = "Serbien",
        ["RU"] = "Russische Föderation",
        ["RW"] = "Ruanda",
        ["SA"] = "Saudi-Arabien",
        ["SB"] = "Salomonen",
        ["SC"] = "Seychellen",
        ["SD"] = "Sudan",
        ["SE"] = "Schweden",
        ["SG"] = "Singapur",
        ["SH"] = "St. Helena, Ascension und Tristan da Cunha",
        ["SI"] = "Slowenien",
        ["SJ"] = "Svalbard und Jan Mayen",
        ["SK"] = "Slowakei",
        ["SL"] = "Sierra Leone",
        ["SM"] = "San Marino",
        ["SN"] = "Senegal",
        ["SO"] = "Somalia",
        ["SR"] = "Suriname",
        ["SS"] = "Südsudan",
        ["ST"] = "Sao Tome und Principe",
        ["SV"] = "El Salvador",
        ["SX"] = "Sint Maarten (niederländischer Teil)",
        ["SY"] = "Syrien",
        ["SZ"] = "Eswatini",
        ["TC"] = "Turks- und Caicosinseln",
        ["TD"] = "Tschad",
        ["TF"] = "Französische Süd- und Antarktisgebiete",
        ["TG"] = "Togo",
        ["TH"] = "Thailand",
        ["TJ"] = "Tadschikistan",
        ["TK"] = "Tokelau",
        ["TL"] = "Timor-Leste",
        ["TM"] = "Turkmenistan",
        ["TN"] = "Tunesien",
        ["TO"] = "Tonga",
        ["TR"] = "Türkei",
        ["TT"] = "Trinidad und Tobago",
        ["TV"] = "Tuvalu",
        ["TW"] = "Taiwan",
        ["TZ"] = "Tansania",
        ["UA"] = "Ukraine",
        ["UG"] = "Uganda",
        ["UM"] = "Amerikanisch-Ozeanien (United States Minor Outlying Islands)",
        ["US"] = "Vereinigte Staaten",
        ["UY"] = "Uruguay",
        ["UZ"] = "Usbekistan",
        ["VA"] = "Vatikanstadt",
        ["VC"] = "St. Vincent und die Grenadinen",
        ["VE"] = "Venezuela",
        ["VG"] = "Britische Jungferninseln",
        ["VI"] = "Amerikanische Jungferninseln",
        ["VN"] = "Vietnam",
        ["VU"] = "Vanuatu",
        ["WF"] = "Wallis und Futuna",
        ["WS"] = "Samoa",
        ["YE"] = "Jemen",
        ["YT"] = "Mayotte",
        ["ZA"] = "Südafrika",
        ["ZM"] = "Sambia",
        ["ZW"] = "Simbabwe",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> CodesByGermanName = Names
        .ToFrozenDictionary(entry => entry.Value, entry => entry.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Alle Länder als Auswahlliste, nach dem deutschen Namen sortiert.
    /// Für die Auswahlfelder der Oberfläche.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name)> All { get; } =
        [.. Names.Select(e => (Code: e.Key.ToUpperInvariant(), Name: e.Value))
                 .OrderBy(e => e.Name, StringComparer.CurrentCulture)];

    /// <summary>
    /// Prüft, ob <paramref name="code"/> ein offiziell zugewiesener
    /// ISO-3166-1-alpha-2-Code ist. Gross-/Kleinschreibung und umgebende
    /// Leerzeichen spielen keine Rolle. Liefert <see langword="false"/> bei
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
    /// Liefert den deutschen Namen des Landes zu einem gültigen Code.
    /// </summary>
    public static bool TryGetName(string? code, [MaybeNullWhen(false)] out string germanName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            germanName = null;
            return false;
        }

        return Names.TryGetValue(code.Trim(), out germanName);
    }

    /// <summary>
    /// Löst entweder einen ISO-3166-1-alpha-2-Code oder einen exakten deutschen
    /// Ländernamen aus derselben Codeliste in den normalisierten Code auf.
    ///
    /// Die Funktion ist bewusst kein unscharfer Übersetzer: Abkürzungen,
    /// fremdsprachige Namen und bloß ähnliche Schreibweisen werden nicht
    /// geraten.
    /// </summary>
    public static bool TryGetCode(string? codeOrGermanName, [MaybeNullWhen(false)] out string code)
    {
        if (string.IsNullOrWhiteSpace(codeOrGermanName))
        {
            code = null;
            return false;
        }

        string candidate = codeOrGermanName.Trim();

        if (Names.ContainsKey(candidate))
        {
            code = candidate.ToUpperInvariant();
            return true;
        }

        if (CodesByGermanName.TryGetValue(candidate, out string? found))
        {
            code = found.ToUpperInvariant();
            return true;
        }

        code = null;
        return false;
    }
}

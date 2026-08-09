namespace EInvoiceSender.Core.Zugferd;

/// <summary>
/// Feste Zeichenketten des Formats UN/CEFACT Cross Industry Invoice.
///
/// Diese Werte sind in docs/STANDARDS.md belegt und dort mit Quelle und
/// Vertrauensangabe dokumentiert. Sie duerfen nirgends im Code dupliziert
/// werden – bei einer Formatumstellung ist diese Datei die einzige Stelle,
/// die sich aendert.
/// </summary>
public static class CiiConstants
{
    /// <summary>Namensraum des Wurzelelements.</summary>
    public const string NsRsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";

    /// <summary>Namensraum der wiederverwendbaren Geschaeftsobjekte.</summary>
    public const string NsRam = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";

    /// <summary>Namensraum der unqualifizierten Datentypen.</summary>
    public const string NsUdt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

    /// <summary>Namensraum der qualifizierten Datentypen.</summary>
    public const string NsQdt = "urn:un:unece:uncefact:data:standard:QualifiedDataType:100";

    /// <summary>Praefix fuer <see cref="NsRsm"/>.</summary>
    public const string PrefixRsm = "rsm";

    /// <summary>Praefix fuer <see cref="NsRam"/>.</summary>
    public const string PrefixRam = "ram";

    /// <summary>Praefix fuer <see cref="NsUdt"/>.</summary>
    public const string PrefixUdt = "udt";

    /// <summary>Praefix fuer <see cref="NsQdt"/>.</summary>
    public const string PrefixQdt = "qdt";

    /// <summary>Name des Wurzelelements.</summary>
    public const string RootElement = "CrossIndustryInvoice";

    /// <summary>
    /// Profilkennung des Profils EN 16931 (COMFORT).
    /// Belegt aus zwei unabhaengigen Referenzimplementierungen, siehe
    /// docs/STANDARDS.md, Abschnitt 2.1.
    /// </summary>
    public const string ProfileEn16931 = "urn:cen.eu:en16931:2017";

    /// <summary>Datumsformat 102 nach UNTDID 2379: <c>JJJJMMTT</c>.</summary>
    public const string DateFormatCode = "102";

    /// <summary>Steuerart Umsatzsteuer nach UNTDID 5153.</summary>
    public const string TaxTypeVat = "VAT";

    /// <summary>Schemakennung fuer die Umsatzsteuer-Identifikationsnummer.</summary>
    public const string TaxSchemeVatId = "VA";

    /// <summary>Schemakennung fuer die Steuernummer des Finanzamts.</summary>
    public const string TaxSchemeTaxNumber = "FC";

    /// <summary>Schemakennung einer elektronischen Adresse vom Typ E-Mail (EAS-Liste).</summary>
    public const string ElectronicAddressSchemeEmail = "EM";

    /// <summary>Dateiname, unter dem die XML im PDF eingebettet wird.</summary>
    public const string EmbeddedFileName = "factur-x.xml";

    /// <summary>MIME-Typ des eingebetteten Anhangs.</summary>
    public const string EmbeddedMimeType = "text/xml";

    /// <summary>Beziehung des Anhangs zum Dokument nach PDF/A-3.</summary>
    public const string EmbeddedRelationship = "Alternative";

    /// <summary>Beschreibungstext des Anhangs im PDF.</summary>
    public const string EmbeddedDescription = "Factur-X/ZUGFeRD Rechnung";

    /// <summary>Menschenlesbare Formatbezeichnung fuer den Validierungsbericht.</summary>
    public const string FormatDescription =
        "ZUGFeRD 2.3 / Factur-X 1.07, Profil EN 16931 (CII D16B), PDF/A-3b";

    /// <summary>
    /// Bekannte Profilkennungen zur Erkennung eingehender Dateien.
    /// Wird nur gelesen, nie geschrieben – erzeugt wird ausschliesslich
    /// <see cref="ProfileEn16931"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> KnownProfiles { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["urn:factur-x.eu:1p0:minimum"] = "MINIMUM",
            ["urn:zugferd.de:2p0:minimum"] = "MINIMUM",
            ["urn:factur-x.eu:1p0:basicwl"] = "BASIC WL",
            ["urn:zugferd.de:2p0:basicwl"] = "BASIC WL",
            ["urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic"] = "BASIC",
            ["urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2p0:basic"] = "BASIC",
            ["urn:cen.eu:en16931:2017"] = "EN 16931 (COMFORT)",
            ["urn:cen.eu:en16931:2017#conformant#urn:factur-x.eu:1p0:extended"] = "EXTENDED",
            ["urn:cen.eu:en16931:2017#conformant#urn:zugferd.de:2p0:extended"] = "EXTENDED",
            ["urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0"] = "XRECHNUNG 3.0",
            ["urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_3.0"] = "XRECHNUNG 3.0",
            ["urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_2.3"] = "XRECHNUNG 2.3",
            ["urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_2.2"] = "XRECHNUNG 2.2",
        };

    /// <summary>
    /// Liefert die lesbare Profilbezeichnung zu einer Kennung, oder die Kennung
    /// selbst, wenn sie unbekannt ist.
    /// </summary>
    public static string DescribeProfile(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return "unbekannt";
        }

        return KnownProfiles.TryGetValue(profileId, out string? name) ? name : profileId;
    }
}

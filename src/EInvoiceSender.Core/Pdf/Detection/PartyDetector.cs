using System.Text.RegularExpressions;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Verkäufer und Käufer, so weit erkennbar.</summary>
public sealed record DetectedParties(DetectedParty Seller, DetectedParty Buyer)
{
    public static DetectedParties None { get; } = new(new DetectedParty(), new DetectedParty());
}

/// <summary>
/// Trennt Verkäufer und Käufer.
///
/// Das ist die schwierigste Zuordnung des ganzen Verfahrens, denn beide
/// Adressen sehen gleich aus. Genutzt werden zwei Signale:
///
/// 1. **Die eigene Firmenvorlage.** Steht der gespeicherte Firmenname oder die
///    gespeicherte USt-IdNr. im Dokument, ist das ein starkes Zeichen für den
///    Verkäufer - man stellt seine eigenen Rechnungen selbst aus.
/// 2. **Schlüsselwörter wie "Rechnung an".** Was darunter steht, ist der
///    Käufer.
///
/// Fehlt beides, wird **nichts** zugeordnet. Eine vertauschte Adresse wäre
/// schlimmer als ein leeres Feld: Sie ergibt eine formal gültige, inhaltlich
/// falsche Rechnung.
/// </summary>
internal static class PartyDetector
{
    private static readonly string[] BuyerKeywords =
    [
        "rechnung an", "rechnungsempfänger", "rechnungsempfänger", "kunde:", "empfänger",
        "empfänger", "bill to", "invoice to", "lieferanschrift", "rechnungsadresse",
    ];

    /// <summary>Mehr Zeilen sind erfahrungsgemäß schon der nächste Abschnitt.</summary>
    private const int AddressBlockLines = 5;

    /// <summary>
    /// Waagerechte Toleranz, innerhalb derer ein Abschnitt noch zur selben
    /// Spalte gehört.
    /// </summary>
    private const double SameColumnToleranceInPoints = 40;

    /// <summary>
    /// Begriffe, die eine Zeile als Rechnungsangabe ausweisen und damit als
    /// Firmenname ausschließen.
    ///
    /// Ohne diese Sperre wurde in einer zweispaltigen Rechnung „Währung: EUR“
    /// zum Käufernamen: Rechts neben dem Empfängerblock standen die
    /// Rechnungsdaten, und der Adressblock wurde rein nach Lesereihenfolge
    /// gelesen. Die räumliche Auswertung unten behebt die Ursache; diese Liste
    /// fängt zusätzlich die Fälle ab, in denen die Spaltenerkennung nicht
    /// greift.
    /// </summary>
    private static readonly string[] MetadataTerms =
    [
        "rechnungsnummer", "rechnungs-nr", "rechnungsnr", "rechnungsdatum", "rechnung vom",
        "leistungsdatum", "lieferdatum", "leistungszeitraum", "fällig", "fällig",
        "zahlbar", "zahlungsziel", "zahlungsbedingung", "währung", "währung", "currency",
        "netto", "brutto", "umsatzsteuer", "mehrwertsteuer", "mwst", "steuersatz",
        "gesamtbetrag", "rechnungsbetrag", "gesamtsumme", "zwischensumme", "zahlbetrag",
        "iban", "bic", "swift", "seite", "kundennummer", "kunden-nr", "bestellnummer",
        "auftragsnummer", "ust-id", "ust.-id", "steuernummer", "telefon", "telefax",
    ];

    public static DetectedParties Detect(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
        => new(DetectSeller(lines, ownCompany), DetectBuyer(lines, ownCompany));

    private static DetectedParty DetectSeller(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
    {
        if (ownCompany is null)
        {
            return new DetectedParty();
        }

        bool nameFound = Mentions(lines, ownCompany.SellerName);
        bool vatFound = MentionsCompact(lines, ownCompany.SellerVatId);

        if (!nameFound && !vatFound)
        {
            return new DetectedParty();
        }

        string reason = (nameFound, vatFound) switch
        {
            (true, true) => "Firmenname und USt-IdNr. der gespeicherten Vorlage stehen im Dokument.",
            (true, false) => "Der Firmenname der gespeicherten Vorlage steht im Dokument.",
            _ => "Die USt-IdNr. der gespeicherten Vorlage steht im Dokument.",
        };

        DetectedValue<string>? From(string? value) => string.IsNullOrWhiteSpace(value)
            ? null
            : new DetectedValue<string>(value, DetectionConfidence.High, null, reason);

        return new DetectedParty
        {
            Name = From(ownCompany.SellerName),
            Street = From(ownCompany.SellerStreet),
            PostalCode = From(ownCompany.SellerPostalCode),
            City = From(ownCompany.SellerCity),
            Country = From(ownCompany.SellerCountry),
            VatId = From(ownCompany.SellerVatId),
            TaxNumber = From(ownCompany.SellerTaxNumber),
            Email = From(ownCompany.SellerEmail),
        };
    }

    private static DetectedParty DetectBuyer(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            PdfTextSegment? keywordSegment = SegmentWithKeyword(lines[i], BuyerKeywords);

            if (keywordSegment is null)
            {
                continue;
            }

            // Der Adressblock steht **unter** dem Schlüsselwort, also in
            // derselben Spalte. Was rechts daneben steht, gehört zu einem
            // anderen Block und darf hier nicht hineinlaufen.
            List<string> block =
            [
                .. lines.Skip(i + 1).Take(AddressBlockLines)
                    .Select(l => TextInColumn(l, keywordSegment.Left))
                    .Where(t => t is { Length: > 0 })
                    .Select(t => t!),
            ];

            return BuildFromAddressBlock(block, keywordSegment.Text.Trim(), ownCompany);
        }

        return new DetectedParty();
    }

    private static PdfTextSegment? SegmentWithKeyword(PdfTextLine line, string[] keywords)
        => line.Segments.FirstOrDefault(s => DetectionParsers.FirstKeywordIn(s.Text, keywords) is not null);

    /// <summary>
    /// Liefert den Abschnitt einer Zeile, der waagerecht zur angegebenen Spalte
    /// gehört – oder <c>null</c>, wenn die Zeile dort nichts stehen hat.
    /// </summary>
    private static string? TextInColumn(PdfTextLine line, double columnLeft)
        => line.Segments
            .Where(s => Math.Abs(s.Left - columnLeft) <= SameColumnToleranceInPoints)
            .Select(s => s.Text)
            .FirstOrDefault();

    private static DetectedParty BuildFromAddressBlock(
        IReadOnlyList<string> block, string keywordLine, CompanyTemplate? ownCompany)
    {
        DetectedValue<string>? name = null;
        DetectedValue<string>? street = null;
        DetectedValue<string>? postalCode = null;
        DetectedValue<string>? city = null;

        string reason = $"Adressblock unter \"{keywordLine}\".";

        foreach (string line in block)
        {
            // Eine Rechnungsangabe ist nie ein Firmenname oder eine Anschrift.
            if (IsMetadata(line))
            {
                continue;
            }

            Match place = DetectionParsers.PostalCodeAndCity().Match(line);

            if (place.Success && postalCode is null)
            {
                postalCode = Value(place.Groups["plz"].Value, line, reason);
                city = Value(place.Groups["ort"].Value.Trim(), line, reason);

                continue;
            }

            if (street is null && DetectionParsers.Street().IsMatch(line))
            {
                street = Value(line.Trim(), line, reason);

                continue;
            }

            if (name is null && line.Trim().Length > 2 && !IsOwnCompany(line, ownCompany))
            {
                name = Value(line.Trim(), line, $"Erste Zeile unter \"{keywordLine}\".");
            }
        }

        return new DetectedParty
        {
            Name = name,
            Street = street,
            PostalCode = postalCode,
            City = city,
        };
    }

    private static DetectedValue<string> Value(string value, string source, string reason)
        => new(value, DetectionConfidence.Medium, source, reason);

    /// <summary>Enthält die Zeile eine Rechnungsangabe statt einer Anschrift?</summary>
    private static bool IsMetadata(string line)
    {
        string lower = line.ToLowerInvariant();

        return MetadataTerms.Any(t => lower.Contains(t, StringComparison.Ordinal));
    }

    /// <summary>Die eigene Firma kann nicht der Käufer sein.</summary>
    private static bool IsOwnCompany(string line, CompanyTemplate? ownCompany)
        => ownCompany?.SellerName is { Length: > 0 } own
           && line.Contains(own, StringComparison.OrdinalIgnoreCase);

    private static bool Mentions(IReadOnlyList<PdfTextLine> lines, string? value)
        => !string.IsNullOrWhiteSpace(value)
           && lines.Any(l => l.Text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool MentionsCompact(IReadOnlyList<PdfTextLine> lines, string? value)
        => !string.IsNullOrWhiteSpace(value)
           && lines.Any(l => l.Text.Replace(" ", string.Empty, StringComparison.Ordinal)
               .Contains(value, StringComparison.OrdinalIgnoreCase));
}

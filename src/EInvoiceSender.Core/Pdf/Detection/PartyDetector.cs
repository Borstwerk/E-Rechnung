using System.Text.RegularExpressions;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Verkaeufer und Kaeufer, so weit erkennbar.</summary>
public sealed record DetectedParties(DetectedParty Seller, DetectedParty Buyer)
{
    public static DetectedParties None { get; } = new(new DetectedParty(), new DetectedParty());
}

/// <summary>
/// Trennt Verkaeufer und Kaeufer.
///
/// Das ist die schwierigste Zuordnung des ganzen Verfahrens, denn beide
/// Adressen sehen gleich aus. Genutzt werden zwei Signale:
///
/// 1. **Die eigene Firmenvorlage.** Steht der gespeicherte Firmenname oder die
///    gespeicherte USt-IdNr. im Dokument, ist das ein starkes Zeichen fuer den
///    Verkaeufer - man stellt seine eigenen Rechnungen selbst aus.
/// 2. **Schluesselwoerter wie "Rechnung an".** Was darunter steht, ist der
///    Kaeufer.
///
/// Fehlt beides, wird **nichts** zugeordnet. Eine vertauschte Adresse waere
/// schlimmer als ein leeres Feld: Sie ergibt eine formal gueltige, inhaltlich
/// falsche Rechnung.
/// </summary>
internal static class PartyDetector
{
    private static readonly string[] BuyerKeywords =
    [
        "rechnung an", "rechnungsempfaenger", "rechnungsempfänger", "kunde:", "empfaenger",
        "empfänger", "bill to", "invoice to", "lieferanschrift", "rechnungsadresse",
    ];

    /// <summary>Mehr Zeilen sind erfahrungsgemaess schon der naechste Abschnitt.</summary>
    private const int AddressBlockLines = 5;

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
            if (DetectionParsers.FirstKeywordIn(lines[i].Text, BuyerKeywords) is null)
            {
                continue;
            }

            return BuildFromAddressBlock(
                [.. lines.Skip(i + 1).Take(AddressBlockLines)], lines[i].Text.Trim(), ownCompany);
        }

        return new DetectedParty();
    }

    private static DetectedParty BuildFromAddressBlock(
        IReadOnlyList<PdfTextLine> block, string keywordLine, CompanyTemplate? ownCompany)
    {
        DetectedValue<string>? name = null;
        DetectedValue<string>? street = null;
        DetectedValue<string>? postalCode = null;
        DetectedValue<string>? city = null;

        string reason = $"Adressblock unter \"{keywordLine}\".";

        foreach (PdfTextLine line in block)
        {
            Match place = DetectionParsers.PostalCodeAndCity().Match(line.Text);

            if (place.Success && postalCode is null)
            {
                postalCode = Value(place.Groups["plz"].Value, line.Text, reason);
                city = Value(place.Groups["ort"].Value.Trim(), line.Text, reason);

                continue;
            }

            if (street is null && DetectionParsers.Street().IsMatch(line.Text))
            {
                street = Value(line.Text.Trim(), line.Text, reason);

                continue;
            }

            if (name is null && line.Text.Trim().Length > 2 && !IsOwnCompany(line.Text, ownCompany))
            {
                name = Value(line.Text.Trim(), line.Text, $"Erste Zeile unter \"{keywordLine}\".");
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

    /// <summary>Die eigene Firma kann nicht der Kaeufer sein.</summary>
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

using System.Text.RegularExpressions;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Verkäufer, Käufer und gegebenenfalls das bestätigbare Seller-Proposal.</summary>
public sealed record DetectedParties(
    DetectedParty Seller,
    DetectedParty Buyer,
    DetectedOwnCompanyProposal? OwnCompanyProposal)
{
    public static DetectedParties None { get; } =
        new(new DetectedParty(), new DetectedParty(), null);
}

/// <summary>
/// Trennt Verkäufer und Käufer konservativ.
///
/// Eine vorhandene inhaltliche Firmenvorlage bleibt das stärkste Signal. Ohne
/// Vorlage wird ein Seller nur aus festen Mindestkombinationen gebildet:
///
/// * ausdrücklicher Seller-Block + Name + (vollständige Anschrift oder
///   Steuermerkmal), oder
/// * Briefkopf + Name + vollständige Anschrift + Steuermerkmal.
///
/// Käufer- und Lieferblöcke werden zuvor räumlich ausgeschlossen. Mehrere
/// gleich belastbare Kandidaten führen zu keinem Ergebnis. Bankdaten spielen
/// bei dieser Entscheidung keine Rolle und können erst danach einem bereits
/// eindeutig erkannten Proposal hinzugefügt werden.
/// </summary>
internal static class PartyDetector
{
    private static readonly string[] BuyerKeywords =
    [
        "rechnung an", "rechnungsempfänger", "rechnungsempfänger", "kunde:", "empfänger",
        "empfänger", "bill to", "invoice to", "lieferanschrift", "rechnungsadresse",
    ];

    private static readonly string[] SellerKeywords =
    [
        "rechnungssteller", "rechnungsaussteller", "aussteller", "verkäufer", "verkaufer",
        "lieferant", "leistungserbringer", "seller", "invoice from",
    ];

    /// <summary>Mehr Zeilen sind erfahrungsgemäß schon der nächste Abschnitt.</summary>
    private const int AddressBlockLines = 5;

    /// <summary>Waagerechte Toleranz für dieselbe Spalte.</summary>
    private const double SameColumnToleranceInPoints = 40;

    /// <summary>Käufer-/Lieferblock unterhalb seines Schlüssels.</summary>
    private const double RecipientBlockHeightInPoints = 90;

    /// <summary>Nur der obere Bereich der ersten Seite kommt als Briefkopf infrage.</summary>
    private const double HeaderMaximumTopInPoints = 190;

    /// <summary>
    /// Höchster Abstand zwischen IBAN und ihrer BIC. Das entspricht bei den
    /// vorhandenen PDF-Zeilen höchstens der unmittelbar benachbarten Zeile.
    /// </summary>
    private const double BankAssociationMaximumTopDistanceInPoints = 20;

    private static readonly string[] MetadataTerms =
    [
        "rechnungsnummer", "rechnungs-nr", "rechnungsnr", "rechnungsdatum", "rechnung vom",
        "leistungsdatum", "lieferdatum", "leistungszeitraum", "fällig", "fällig",
        "zahlbar", "zahlungsziel", "zahlungsbedingung", "währung", "währung", "currency",
        "netto", "brutto", "umsatzsteuer", "mehrwertsteuer", "mwst", "steuersatz",
        "gesamtbetrag", "rechnungsbetrag", "gesamtsumme", "zwischensumme", "zahlbetrag",
        "iban", "bic", "swift", "seite", "kundennummer", "kunden-nr", "bestellnummer",
        "auftragsnummer", "ust-id", "ust.-id", "ust id", "steuernummer", "telefon", "telefax",
        "invoice no", "invoice date", "belegnummer", "position", "bezeichnung", "menge",
    ];

    private static readonly string[] VatTerms =
    [
        "ust-id", "ust.-id", "ust id", "umsatzsteuer-identifikationsnummer", "vat id", "vat-id",
    ];

    private static readonly string[] TaxTerms =
    [
        "steuernummer", "steuer-nr", "steuer nr", "tax number", "tax no",
    ];

    private static readonly Regex VatIdPattern = new(
        @"\b[A-Z]{2}\s?[A-Z0-9]{8,12}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EmailPattern = new(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static DetectedParties Detect(
        IReadOnlyList<PdfTextLine> lines,
        CompanyTemplate? ownCompany,
        DetectedPayment payment)
    {
        IReadOnlyList<RecipientRegion> recipientRegions = FindRecipientRegions(lines);
        DetectedParty buyer = DetectBuyer(lines, ownCompany);

        if (ownCompany is not null && CompanyTemplateSavePlanner.HasCompanyData(ownCompany))
        {
            return new DetectedParties(DetectSellerFromTemplate(lines, ownCompany), buyer, null);
        }

        SellerCandidate? seller = DetectSellerWithoutTemplate(lines, recipientRegions);

        if (seller is null)
        {
            return new DetectedParties(new DetectedParty(), buyer, null);
        }

        DetectedOwnCompanyProposal proposal = BuildProposal(seller.Party, payment, recipientRegions);

        return new DetectedParties(seller.Party, buyer, proposal);
    }

    private static DetectedParty DetectSellerFromTemplate(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate ownCompany)
    {
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

    private static SellerCandidate? DetectSellerWithoutTemplate(
        IReadOnlyList<PdfTextLine> lines,
        IReadOnlyList<RecipientRegion> recipientRegions)
    {
        IReadOnlyList<SegmentEntry> entries = Entries(lines);
        var candidates = new List<SellerCandidate>();

        candidates.AddRange(ExplicitSellerCandidates(entries, recipientRegions));
        candidates.AddRange(HeaderSellerCandidates(entries, recipientRegions));

        SellerCandidate[] distinct =
        [
            .. candidates
                .GroupBy(candidate => candidate.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(candidate => candidate.Confidence)
                    .First()),
        ];

        // Eindeutigkeit ist eine harte Schranke, kein Punktesystem.
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static IEnumerable<SellerCandidate> ExplicitSellerCandidates(
        IReadOnlyList<SegmentEntry> entries,
        IReadOnlyList<RecipientRegion> recipientRegions)
    {
        foreach (SegmentEntry anchor in entries.Where(entry => IsSellerAnchor(entry.Text)))
        {
            List<SegmentEntry> block =
            [
                .. entries.Where(entry =>
                        entry.PageNumber == anchor.PageNumber
                        && SameColumn(entry.Left, anchor.Left)
                        && entry.Top >= anchor.Top
                        && entry.Top <= anchor.Top + RecipientBlockHeightInPoints
                        && !IsRecipient(entry, recipientRegions))
                    .OrderBy(entry => entry.Top),
            ];

            SellerCandidate? candidate = BuildExplicitCandidate(anchor, block);

            if (candidate is not null)
            {
                yield return candidate;
            }
        }
    }

    private static SellerCandidate? BuildExplicitCandidate(
        SegmentEntry anchor, IReadOnlyList<SegmentEntry> block)
    {
        string? keyword = DetectionParsers.FirstKeywordIn(anchor.Text, SellerKeywords);
        string afterKeyword = keyword is null
            ? string.Empty
            : DetectionParsers.AfterKeyword(anchor.Text, keyword).Trim(' ', ':', '-', '.');

        SegmentEntry? nameEntry = null;
        string? name = null;

        if (afterKeyword.Length > 2 && IsPlausibleName(afterKeyword))
        {
            nameEntry = anchor;
            name = afterKeyword;
        }
        else
        {
            nameEntry = block.FirstOrDefault(entry => entry != anchor && IsPlausibleName(entry.Text));
            name = nameEntry?.Text.Trim();
        }

        SegmentEntry? street = block.FirstOrDefault(entry => DetectionParsers.Street().IsMatch(entry.Text));
        (SegmentEntry? place, Match? placeMatch) = FindPlace(block);
        SegmentEntry? vat = block.FirstOrDefault(entry => TryVatId(entry.Text, out _));
        SegmentEntry? tax = block.FirstOrDefault(entry => TryTaxNumber(entry.Text, out _));

        bool hasFullAddress = street is not null && place is not null;
        bool hasTaxIdentity = vat is not null || tax is not null;

        if (nameEntry is null || name is null || (!hasFullAddress && !hasTaxIdentity))
        {
            return null;
        }

        const string reason =
            "Eindeutiger Verkäuferblock mit Name und Anschrift oder Steuermerkmal; Käufer- und Lieferblöcke ausgeschlossen.";

        return BuildCandidate(
            name, nameEntry, street, place, placeMatch, vat, tax,
            block.FirstOrDefault(entry => TryEmail(entry.Text, out _)),
            DetectionConfidence.High, reason);
    }

    private static IEnumerable<SellerCandidate> HeaderSellerCandidates(
        IReadOnlyList<SegmentEntry> entries,
        IReadOnlyList<RecipientRegion> recipientRegions)
    {
        SegmentEntry[] header =
        [
            .. entries.Where(entry => entry.PageNumber == 1
                                      && entry.Top <= HeaderMaximumTopInPoints
                                      && !IsRecipient(entry, recipientRegions)),
        ];

        foreach (SegmentEntry street in header.Where(entry => DetectionParsers.Street().IsMatch(entry.Text)))
        {
            SegmentEntry[] column =
            [
                .. header.Where(entry => SameColumn(entry.Left, street.Left))
                    .OrderBy(entry => entry.Top),
            ];

            SegmentEntry? name = column
                .Where(entry => entry.Top < street.Top
                                && street.Top - entry.Top <= 35
                                && IsPlausibleName(entry.Text))
                .OrderByDescending(entry => entry.Top)
                .FirstOrDefault();
            (SegmentEntry? place, Match? placeMatch) = FindPlace(
                column.Where(entry => entry.Top > street.Top && entry.Top - street.Top <= 45));

            if (name is null || place is null || placeMatch is null)
            {
                continue;
            }

            SegmentEntry? vat = column.FirstOrDefault(entry =>
                entry.Top >= name.Top
                && entry.Top <= place.Top + 60
                && TryVatId(entry.Text, out _));
            SegmentEntry? tax = column.FirstOrDefault(entry =>
                entry.Top >= name.Top
                && entry.Top <= place.Top + 60
                && TryTaxNumber(entry.Text, out _));

            // Der implizite Briefkopf braucht zwingend ein Steuermerkmal.
            // Name und Anschrift allein wären wieder nur "erste Adresse".
            if (vat is null && tax is null)
            {
                continue;
            }

            const string reason =
                "Briefkopf mit Name, vollständiger Anschrift und Steuermerkmal; Käufer- und Lieferblöcke ausgeschlossen.";

            yield return BuildCandidate(
                name.Text.Trim(), name, street, place, placeMatch, vat, tax,
                column.FirstOrDefault(entry =>
                    entry.Top >= name.Top
                    && entry.Top <= place.Top + 60
                    && TryEmail(entry.Text, out _)),
                DetectionConfidence.Medium, reason);
        }
    }

    private static SellerCandidate BuildCandidate(
        string name,
        SegmentEntry nameEntry,
        SegmentEntry? street,
        SegmentEntry? place,
        Match? placeMatch,
        SegmentEntry? vatEntry,
        SegmentEntry? taxEntry,
        SegmentEntry? emailEntry,
        DetectionConfidence confidence,
        string reason)
    {
        DetectedValue<string>? vat = vatEntry is not null && TryVatId(vatEntry.Text, out string? vatId)
            ? Value(vatId!, confidence, vatEntry.Text, reason)
            : null;
        DetectedValue<string>? tax = taxEntry is not null && TryTaxNumber(taxEntry.Text, out string? taxNumber)
            ? Value(taxNumber!, confidence, taxEntry.Text, reason)
            : null;
        DetectedValue<string>? email = emailEntry is not null && TryEmail(emailEntry.Text, out string? emailAddress)
            ? Value(emailAddress!, confidence, emailEntry.Text, reason)
            : null;

        var party = new DetectedParty
        {
            Name = Value(name, confidence, nameEntry.Text, reason),
            Street = street is null ? null : Value(street.Text.Trim(), confidence, street.Text, reason),
            PostalCode = placeMatch is null
                ? null
                : Value(placeMatch.Groups["plz"].Value, confidence, place?.Text, reason),
            City = placeMatch is null
                ? null
                : Value(placeMatch.Groups["ort"].Value.Trim(), confidence, place?.Text, reason),
            VatId = vat,
            TaxNumber = tax,
            Email = email,
        };

        string identity = string.Join('|',
            party.Name?.Value,
            party.Street?.Value,
            party.PostalCode?.Value,
            party.City?.Value,
            party.VatId?.Value,
            party.TaxNumber?.Value);

        return new SellerCandidate(party, confidence, identity);
    }

    private static DetectedOwnCompanyProposal BuildProposal(
        DetectedParty seller,
        DetectedPayment payment,
        IReadOnlyList<RecipientRegion> recipientRegions)
    {
        var fields = new List<DetectedOwnCompanyField>();

        Add(fields, DetectedOwnCompanyFieldKind.SellerName, seller.Name);
        Add(fields, DetectedOwnCompanyFieldKind.SellerStreet, seller.Street);
        Add(fields, DetectedOwnCompanyFieldKind.SellerPostalCode, seller.PostalCode);
        Add(fields, DetectedOwnCompanyFieldKind.SellerCity, seller.City);
        Add(fields, DetectedOwnCompanyFieldKind.SellerCountry, seller.Country);
        Add(fields, DetectedOwnCompanyFieldKind.SellerEmail, seller.Email);
        Add(fields, DetectedOwnCompanyFieldKind.SellerVatId, seller.VatId);
        Add(fields, DetectedOwnCompanyFieldKind.SellerTaxNumber, seller.TaxNumber);

        // Bankdaten haben an der Seller-Entscheidung nicht teilgenommen. Erst
        // hier dürfen sie ergänzt werden, und nur bei genau einer gültigen
        // IBAN außerhalb eines Käufer-/Lieferblocks.
        if (payment.IbanCandidates.Count == 1
            && !IsRecipient(payment.IbanCandidates[0], recipientRegions))
        {
            Add(fields, DetectedOwnCompanyFieldKind.BankIban,
                payment.IbanCandidates[0].Detection);

            if (payment.BicCandidates.Count == 1
                && !IsRecipient(payment.BicCandidates[0], recipientRegions)
                && IsAssociatedBankValue(
                    payment.IbanCandidates[0], payment.BicCandidates[0]))
            {
                Add(fields, DetectedOwnCompanyFieldKind.BankBic,
                    payment.BicCandidates[0].Detection);
            }
        }

        return new DetectedOwnCompanyProposal { Fields = fields };
    }

    private static bool IsAssociatedBankValue(
        LocatedPaymentValue iban, LocatedPaymentValue bic)
        => iban.PageNumber == bic.PageNumber
           && SameColumn(iban.Left, bic.Left)
           && Math.Abs(iban.Top - bic.Top) <= BankAssociationMaximumTopDistanceInPoints;

    private static void Add(
        List<DetectedOwnCompanyField> fields,
        DetectedOwnCompanyFieldKind kind,
        DetectedValue<string>? detected)
    {
        if (detected is null)
        {
            return;
        }

        string[] evidence =
        [
            .. new[] { detected.SourceText, detected.Reason }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!),
        ];

        fields.Add(new DetectedOwnCompanyField(
            kind, detected.Value, detected.Confidence, evidence));
    }

    private static DetectedParty DetectBuyer(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            PdfTextSegment? keywordSegment = SegmentWithKeyword(lines[index], BuyerKeywords);

            if (keywordSegment is null)
            {
                continue;
            }

            List<string> block =
            [
                .. lines.Skip(index + 1).Take(AddressBlockLines)
                    .Select(line => TextInColumn(line, keywordSegment.Left))
                    .Where(text => text is { Length: > 0 })
                    .Select(text => text!),
            ];

            return BuildFromAddressBlock(block, keywordSegment.Text.Trim(), ownCompany);
        }

        return new DetectedParty();
    }

    private static IReadOnlyList<RecipientRegion> FindRecipientRegions(
        IReadOnlyList<PdfTextLine> lines)
        => [.. Entries(lines)
            .Where(entry => ContainsKeyword(entry.Text, BuyerKeywords))
            .Select(entry => new RecipientRegion(
                entry.PageNumber,
                entry.Left,
                entry.Top,
                entry.Top + RecipientBlockHeightInPoints))];

    private static IReadOnlyList<SegmentEntry> Entries(IReadOnlyList<PdfTextLine> lines)
        => [.. lines.SelectMany(line => line.Segments.Select(segment => new SegmentEntry(
            segment.Text.Trim(), line.PageNumber, line.Top, segment.Left)))];

    private static bool IsRecipient(
        SegmentEntry entry, IReadOnlyList<RecipientRegion> regions)
        => regions.Any(region => region.Contains(entry.PageNumber, entry.Top, entry.Left));

    private static bool IsRecipient(
        LocatedPaymentValue value, IReadOnlyList<RecipientRegion> regions)
        => regions.Any(region => region.Contains(value.PageNumber, value.Top, value.Left));

    private static PdfTextSegment? SegmentWithKeyword(PdfTextLine line, string[] keywords)
        => line.Segments.FirstOrDefault(segment =>
            DetectionParsers.FirstKeywordIn(segment.Text, keywords) is not null);

    private static string? TextInColumn(PdfTextLine line, double columnLeft)
        => line.Segments
            .Where(segment => SameColumn(segment.Left, columnLeft))
            .Select(segment => segment.Text)
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
            if (IsMetadata(line))
            {
                continue;
            }

            Match place = DetectionParsers.PostalCodeAndCity().Match(line);

            if (place.Success && postalCode is null)
            {
                postalCode = Value(place.Groups["plz"].Value, DetectionConfidence.Medium, line, reason);
                city = Value(place.Groups["ort"].Value.Trim(), DetectionConfidence.Medium, line, reason);
                continue;
            }

            if (street is null && DetectionParsers.Street().IsMatch(line))
            {
                street = Value(line.Trim(), DetectionConfidence.Medium, line, reason);
                continue;
            }

            if (name is null && line.Trim().Length > 2 && !IsOwnCompany(line, ownCompany))
            {
                name = Value(
                    line.Trim(), DetectionConfidence.Medium, line,
                    $"Erste Zeile unter \"{keywordLine}\".");
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

    private static (SegmentEntry? Entry, Match? Match) FindPlace(
        IEnumerable<SegmentEntry> entries)
    {
        foreach (SegmentEntry entry in entries)
        {
            Match match = DetectionParsers.PostalCodeAndCity().Match(entry.Text);

            if (match.Success)
            {
                return (entry, match);
            }
        }

        return (null, null);
    }

    private static bool TryVatId(string text, out string? value)
    {
        if (!ContainsKeyword(text, VatTerms))
        {
            value = null;
            return false;
        }

        Match match = VatIdPattern.Match(text);
        value = match.Success
            ? match.Value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()
            : null;

        return value is not null;
    }

    private static bool TryTaxNumber(string text, out string? value)
    {
        string? keyword = DetectionParsers.FirstKeywordIn(text, TaxTerms);

        if (keyword is null)
        {
            value = null;
            return false;
        }

        string candidate = DetectionParsers.AfterKeyword(text, keyword).Trim(' ', ':', '-', '.');
        value = candidate.Length >= 4 && candidate.Any(char.IsDigit) ? candidate : null;

        return value is not null;
    }

    private static bool TryEmail(string text, out string? value)
    {
        Match match = EmailPattern.Match(text);
        value = match.Success ? match.Value : null;

        return value is not null;
    }

    private static bool IsPlausibleName(string text)
    {
        string value = text.Trim();

        return value.Length is >= 3 and <= 100
               && !char.IsDigit(value[0])
               && !IsMetadata(value)
               && !ContainsKeyword(value, BuyerKeywords)
               && !ContainsKeyword(value, SellerKeywords)
               && !DetectionParsers.Street().IsMatch(value)
               && !DetectionParsers.PostalCodeAndCity().IsMatch(value)
               && !TryEmail(value, out _);
    }

    private static bool IsSellerAnchor(string text)
    {
        string normalized = text.Trim();

        foreach (string keyword in SellerKeywords)
        {
            if (normalized.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!normalized.StartsWith(keyword, StringComparison.OrdinalIgnoreCase)
                || normalized.Length == keyword.Length)
            {
                continue;
            }

            char separator = normalized[keyword.Length];

            if (separator is ':' or '-' or '–')
            {
                return true;
            }
        }

        return false;
    }

    private static DetectedValue<string> Value(
        string value,
        DetectionConfidence confidence,
        string? source,
        string reason)
        => new(value, confidence, source, reason);

    private static bool IsMetadata(string line)
    {
        string lower = line.ToLowerInvariant();
        return MetadataTerms.Any(term => lower.Contains(term, StringComparison.Ordinal));
    }

    private static bool ContainsKeyword(string text, IReadOnlyList<string> keywords)
        => DetectionParsers.FirstKeywordIn(text, keywords) is not null;

    private static bool SameColumn(double left, double expected)
        => Math.Abs(left - expected) <= SameColumnToleranceInPoints;

    private static bool IsOwnCompany(string line, CompanyTemplate? ownCompany)
        => ownCompany?.SellerName is { Length: > 0 } own
           && line.Contains(own, StringComparison.OrdinalIgnoreCase);

    private static bool Mentions(IReadOnlyList<PdfTextLine> lines, string? value)
        => !string.IsNullOrWhiteSpace(value)
           && lines.Any(line => line.Text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool MentionsCompact(IReadOnlyList<PdfTextLine> lines, string? value)
        => !string.IsNullOrWhiteSpace(value)
           && lines.Any(line => line.Text.Replace(" ", string.Empty, StringComparison.Ordinal)
               .Contains(value, StringComparison.OrdinalIgnoreCase));

    private sealed record SegmentEntry(string Text, int PageNumber, double Top, double Left);

    private sealed record SellerCandidate(
        DetectedParty Party,
        DetectionConfidence Confidence,
        string Identity);

    private sealed record RecipientRegion(
        int PageNumber,
        double Left,
        double StartTop,
        double EndTop)
    {
        public bool Contains(int pageNumber, double top, double left)
            => pageNumber == PageNumber
               && SameColumn(left, Left)
               && top >= StartTop
               && top <= EndTop;
    }
}

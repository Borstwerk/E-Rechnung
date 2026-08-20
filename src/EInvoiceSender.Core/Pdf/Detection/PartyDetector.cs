using System.Text.RegularExpressions;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Validation.Rules;

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
        "rechnung an", "rechnungsempfänger", "rechnungsempf\u0061enger", "kunde:", "empfänger",
        "empf\u0061enger", "bill to", "invoice to", "rechnungsadresse",
    ];

    private static readonly string[] DeliveryKeywords =
    [
        "lieferanschrift", "lieferadresse", "lieferempfänger", "lieferempf\u0061enger",
        "ship to", "delivery address",
    ];

    private static readonly string[] AnchorlessBuyerExcludedSectionTerms =
    [
        "leistungsort",
    ];

    private static readonly string[] SellerKeywords =
    [
        "rechnungssteller", "rechnungsaussteller", "aussteller", "verkäufer", "verkaufer",
        "lieferant", "leistungserbringer", "seller", "invoice from",
    ];

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

    private static readonly string[] BuyerBlockBoundaryTerms =
    [
        "rechnungsnummer", "rechnungs-nr", "rechnungsnr", "rechnungsdatum",
        "leistungsdatum", "lieferdatum", "leistungszeitraum", "fällig", "zahlbar",
        "zahlungsziel", "währung", "currency", "netto", "brutto", "umsatzsteuer",
        "mehrwertsteuer", "mwst", "steuersatz", "gesamtbetrag", "rechnungsbetrag",
        "gesamtsumme", "zwischensumme", "zahlbetrag", "iban", "bic", "swift",
        "invoice no", "invoice date", "belegnummer", "position", "bezeichnung", "menge",
    ];

    private static readonly Regex VatIdPattern = new(
        @"\b[A-Z]{2}\s?[A-Z0-9]{8,12}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex BuyerVatIdPattern = new(
        @"\b[A-Z]{2}\s?[A-Z0-9]{2,12}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EmailPattern = new(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CountryLabelPattern = new(
        @"^(?:land|country)\s*[:\-]\s*(?<value>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static DetectedParties Detect(
        IReadOnlyList<PdfTextLine> lines,
        CompanyTemplate? ownCompany,
        DetectedPayment payment)
    {
        IReadOnlyList<SegmentEntry> entries = Entries(lines);
        RecipientRegion[] explicitRegions = FindExplicitRecipientRegions(entries);
        RecipientRegion[] anchorlessRegions = explicitRegions.Any(
                region => region.Kind == RecipientRegionKind.Buyer)
            ? []
            : [.. FindAnchorlessBuyerRegions(entries, explicitRegions)];
        RecipientRegion[] recipientRegions = [.. explicitRegions, .. anchorlessRegions];
        BuyerCandidate[] buyerCandidates = BuildBuyerCandidates(
            entries, recipientRegions, ownCompany);

        if (ownCompany is not null && CompanyTemplateSavePlanner.HasCompanyData(ownCompany))
        {
            return new DetectedParties(
                DetectSellerFromTemplate(lines, ownCompany),
                ResolveBuyer(buyerCandidates),
                null);
        }

        // Heuristische Empfängerbereiche dürfen ihre konkurrierende
        // Seller-Erklärung nicht vorab ausblenden. Nur ausdrückliche Buyer- und
        // Delivery-Regionen sind deshalb harte Ausschlüsse der Seller-Suche.
        SellerCandidate? seller = DetectSellerWithoutTemplate(lines, explicitRegions);
        BuyerCandidate[] conflictingBuyerCandidates = seller is null
            ? []
            :
            [
                .. buyerCandidates.Where(candidate =>
                    candidate.Region.IsAnchorless
                    && SameCoreIdentity(seller.Party, candidate.Party)),
            ];

        if (conflictingBuyerCandidates.Length > 0)
        {
            // Ein ausdrücklicher Selleranker oder eine Steuernummer ist ein
            // Seller-spezifisches Signal. Bei bloßer USt-ID bleibt derselbe
            // unbeschriftete Block dagegen sowohl als Seller als auch als Buyer
            // plausibel. Generische Kontaktwerte wie Land, USt-ID und E-Mail
            // beweisen keine Rolle. Der konfliktbehaftete Anchorless-Buyer
            // wird deshalb immer verworfen; ohne Seller-spezifisches Signal
            // bleibt auch der Seller leer.

            if (seller is { HasSellerSpecificSignal: false })
            {
                seller = null;
            }

            HashSet<RecipientRegion> conflictingRegions =
                [.. conflictingBuyerCandidates.Select(candidate => candidate.Region)];
            buyerCandidates =
            [
                .. buyerCandidates.Where(candidate => !conflictingRegions.Contains(candidate.Region)),
            ];
            recipientRegions =
            [
                .. recipientRegions.Where(region => !conflictingRegions.Contains(region)),
            ];
        }

        DetectedParty buyer = ResolveBuyer(buyerCandidates);

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
        candidates.AddRange(ComposedHeaderSellerCandidates(entries, recipientRegions));

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
        CombinedAddress? combinedAddress = block
            .Select(entry => TryCombinedAddress(entry, out CombinedAddress? address) ? address : null)
            .FirstOrDefault(address => address is not null);
        SegmentEntry? vat = block.FirstOrDefault(entry => TryVatId(entry.Text, out _));
        SegmentEntry? tax = block.FirstOrDefault(entry => TryTaxNumber(entry.Text, out _));

        bool hasFullAddress = street is not null && place is not null || combinedAddress is not null;
        bool hasTaxIdentity = vat is not null || tax is not null;

        if (nameEntry is null || name is null || (!hasFullAddress && !hasTaxIdentity))
        {
            return null;
        }

        const string reason =
            "Eindeutiger Verkäuferblock mit Name und Anschrift oder Steuermerkmal; Käufer- und Lieferblöcke ausgeschlossen.";

        if (combinedAddress is not null && (street is null || place is null))
        {
            return BuildComposedCandidate(
                name, nameEntry, combinedAddress, vat, tax,
                block.FirstOrDefault(entry => TryEmail(entry.Text, out _)),
                DetectionConfidence.High, reason,
                hasSellerSpecificSignal: true);
        }

        return BuildCandidate(
            name, nameEntry, street, place, placeMatch, vat, tax,
            block.FirstOrDefault(entry => TryEmail(entry.Text, out _)),
            DetectionConfidence.High, reason,
            hasSellerSpecificSignal: true);
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
                DetectionConfidence.Medium, reason,
                hasSellerSpecificSignal: tax is not null);
        }
    }

    private static IEnumerable<SellerCandidate> ComposedHeaderSellerCandidates(
        IReadOnlyList<SegmentEntry> entries,
        IReadOnlyList<RecipientRegion> recipientRegions)
    {
        SegmentEntry[] header =
        [
            .. entries.Where(entry => entry.PageNumber == 1
                                      && entry.Top <= HeaderMaximumTopInPoints
                                      && !IsRecipient(entry, recipientRegions)),
        ];

        CombinedAddress[] addresses =
        [
            .. header
                .Select(entry => TryCombinedAddress(entry, out CombinedAddress? address) ? address : null)
                .Where(address => address is not null)
                .Select(address => address!),
        ];

        foreach (CombinedAddress address in addresses)
        {
            SegmentEntry[] contactColumn =
            [
                .. header.Where(entry => SameColumn(entry.Left, address.Entry.Left)
                                         && entry.Top >= address.Entry.Top - 10
                                         && entry.Top <= address.Entry.Top + 60)
                    .OrderBy(entry => entry.Top),
            ];
            SegmentEntry? vat = contactColumn.FirstOrDefault(entry => TryVatId(entry.Text, out _));
            SegmentEntry? tax = contactColumn.FirstOrDefault(entry => TryTaxNumber(entry.Text, out _));

            if (vat is null && tax is null)
            {
                continue;
            }

            HeaderName[] names =
            [
                .. header
                    .Where(entry => entry.Top < address.Entry.Top
                                    && address.Entry.Top - entry.Top <= 70)
                    .Select(entry => TrySemicolonHeaderName(entry, out HeaderName? name) ? name : null)
                    .Where(name => name is not null)
                    .Select(name => name!)
                    .GroupBy(name => name.Value, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()),
            ];

            foreach (HeaderName name in names)
            {
                const string reason =
                    "Eindeutiger mehrspaltiger Briefkopf mit plausiblem Namenskopf, vollständiger Anschrift und Steuermerkmal; Empfängerbereiche ausgeschlossen.";

                yield return BuildComposedCandidate(
                    name.Value, name.Entry, address, vat, tax,
                    contactColumn.FirstOrDefault(entry => TryEmail(entry.Text, out _)),
                    DetectionConfidence.Medium, reason,
                    hasSellerSpecificSignal: tax is not null);
            }
        }
    }

    private static bool TrySemicolonHeaderName(
        SegmentEntry entry, out HeaderName? name)
    {
        int separator = entry.Text.IndexOf(';', StringComparison.Ordinal);

        if (separator <= 0
            || entry.Text.IndexOf(';', separator + 1) >= 0)
        {
            name = null;

            return false;
        }

        string prefix = entry.Text[..separator].Trim();

        if (!IsPlausibleName(prefix))
        {
            name = null;

            return false;
        }

        name = new HeaderName(prefix, entry);

        return true;
    }

    private static bool TryCombinedAddress(
        SegmentEntry entry, out CombinedAddress? address)
    {
        MatchCollection places = DetectionParsers.PostalCodeAndCity().Matches(entry.Text);

        if (places.Count != 1 || places[0].Index == 0)
        {
            address = null;

            return false;
        }

        Match place = places[0];
        string street = TrimTrailingSeparators(entry.Text[..place.Index]);

        if (!DetectionParsers.Street().IsMatch(street))
        {
            address = null;

            return false;
        }

        address = new CombinedAddress(
            entry,
            street,
            place.Groups["plz"].Value,
            place.Groups["ort"].Value.Trim());

        return true;
    }

    private static string TrimTrailingSeparators(string value)
    {
        int end = value.Length;

        while (end > 0 && !char.IsLetterOrDigit(value[end - 1]))
        {
            end--;
        }

        return value[..end].Trim();
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
        string reason,
        bool hasSellerSpecificSignal)
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

        return new SellerCandidate(party, confidence, identity, hasSellerSpecificSignal);
    }

    private static SellerCandidate BuildComposedCandidate(
        string name,
        SegmentEntry nameEntry,
        CombinedAddress address,
        SegmentEntry? vatEntry,
        SegmentEntry? taxEntry,
        SegmentEntry? emailEntry,
        DetectionConfidence confidence,
        string reason,
        bool hasSellerSpecificSignal)
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
            Street = Value(address.Street, confidence, address.Entry.Text, reason),
            PostalCode = Value(address.PostalCode, confidence, address.Entry.Text, reason),
            City = Value(address.City, confidence, address.Entry.Text, reason),
            VatId = vat,
            TaxNumber = tax,
            Email = email,
        };

        string identity = string.Join('|',
            party.Name.Value,
            party.Street.Value,
            party.PostalCode.Value,
            party.City.Value,
            party.VatId?.Value,
            party.TaxNumber?.Value);

        return new SellerCandidate(party, confidence, identity, hasSellerSpecificSignal);
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

    private static BuyerCandidate[] BuildBuyerCandidates(
        IReadOnlyList<SegmentEntry> entries,
        IReadOnlyList<RecipientRegion> recipientRegions,
        CompanyTemplate? ownCompany)
    {
        return
        [
            .. recipientRegions
                .Where(region => region.Kind == RecipientRegionKind.Buyer)
                .Select(region => BuildBuyerCandidate(entries, region, ownCompany))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!),
        ];
    }

    private static DetectedParty ResolveBuyer(IReadOnlyList<BuyerCandidate> candidates)
    {
        BuyerCandidate[][] identities =
        [
            .. candidates
                .GroupBy(candidate => candidate.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.ToArray()),
        ];

        // Mehrere plausible Empfänger sind fachlich mehrdeutig. Es gewinnt
        // ausdrücklich weder die erste Region noch ein schwaches Punktesystem.
        if (identities.Length != 1)
        {
            return new DetectedParty();
        }

        BuyerCandidate[] sameIdentity = identities[0];
        DetectedParty first = sameIdentity[0].Party;

        return first with
        {
            Country = UniqueDetected(sameIdentity.Select(candidate => candidate.Party.Country)),
            VatId = UniqueDetected(sameIdentity.Select(candidate => candidate.Party.VatId)),
            Email = UniqueDetected(sameIdentity.Select(candidate => candidate.Party.Email)),
        };
    }

    private static BuyerCandidate? BuildBuyerCandidate(
        IReadOnlyList<SegmentEntry> entries,
        RecipientRegion region,
        CompanyTemplate? ownCompany)
    {
        SegmentEntry[] regionEntries =
        [
            .. entries
                .Where(entry => region.Contains(entry.PageNumber, entry.Top, entry.Left)
                                && entry.Top > region.StartTop)
                .OrderBy(entry => entry.Top),
        ];
        SegmentEntry[] block = BuyerBlock(regionEntries, ownCompany);

        string reason =
            $"Eindeutiger Empfängerblock unter \"{region.AnchorText}\"; Lieferbereiche ausgeschlossen.";
        SegmentEntry? street = block.FirstOrDefault(entry =>
            DetectionParsers.Street().IsMatch(entry.Text));
        (SegmentEntry? place, Match? placeMatch) = FindPlace(block);
        SegmentEntry? name = block.FirstOrDefault(entry =>
            IsPlausibleBuyerName(entry.Text, ownCompany));

        // Ein bloßer Name oder eine bloße Adresse genügt nicht. Diese feste
        // Mindestkombination verhindert die frühere "erste Zeile gewinnt"-Logik.
        if (name is null || (street is null && place is null))
        {
            return null;
        }

        var party = new DetectedParty
        {
            Name = Value(name.Text.Trim(), DetectionConfidence.Medium, name.Text, reason),
            Street = street is null
                ? null
                : Value(street.Text.Trim(), DetectionConfidence.Medium, street.Text, reason),
            PostalCode = placeMatch is null
                ? null
                : Value(placeMatch.Groups["plz"].Value, DetectionConfidence.Medium, place?.Text, reason),
            City = placeMatch is null
                ? null
                : Value(placeMatch.Groups["ort"].Value.Trim(), DetectionConfidence.Medium, place?.Text, reason),
            Country = UniqueDetected(block.Select(entry => DetectBuyerCountry(entry, reason))),
            VatId = UniqueDetected(block.SelectMany(entry => DetectBuyerVatIds(entry, reason))),
            Email = UniqueDetected(block.SelectMany(entry => DetectBuyerEmails(entry, reason))),
        };

        string identity = string.Join('|',
            party.Name?.Value,
            party.Street?.Value,
            party.PostalCode?.Value,
            party.City?.Value);

        return new BuyerCandidate(party, identity, region);
    }

    private static SegmentEntry[] BuyerBlock(
        IReadOnlyList<SegmentEntry> regionEntries, CompanyTemplate? ownCompany)
    {
        var result = new List<SegmentEntry>();
        bool addressStarted = false;

        foreach (SegmentEntry entry in regionEntries)
        {
            if (IsBuyerBlockBoundary(entry.Text))
            {
                if (addressStarted)
                {
                    break;
                }

                continue;
            }

            result.Add(entry);
            addressStarted |= IsPlausibleBuyerName(entry.Text, ownCompany)
                              || DetectionParsers.Street().IsMatch(entry.Text)
                              || DetectionParsers.PostalCodeAndCity().IsMatch(entry.Text);
        }

        return [.. result];
    }

    private static bool IsPlausibleBuyerName(string text, CompanyTemplate? ownCompany)
        => IsPlausibleName(text)
           && !IsOwnCompany(text, ownCompany)
           && !TryBuyerCountry(text, out _)
           && !ContainsValidBuyerVatId(text);

    private static bool ContainsValidBuyerVatId(string text)
        => BuyerVatMatches(text)
            .Select(match => match.Value.Replace(" ", string.Empty, StringComparison.Ordinal))
            .Any(value => SharedRules.LooksLikeVatId(value)
                          && VatIdSyntax.HasKnownCountryPrefix(value));

    private static DetectedValue<string>? DetectBuyerCountry(SegmentEntry entry, string reason)
        => TryBuyerCountry(entry.Text, out string? country)
            ? Value(country!, DetectionConfidence.Medium, entry.Text, reason)
            : null;

    private static bool TryBuyerCountry(string text, out string? country)
    {
        string candidate = text.Trim();
        Match labelled = CountryLabelPattern.Match(candidate);

        if (labelled.Success)
        {
            candidate = labelled.Groups["value"].Value.Trim();
        }

        return CountryCodeList.TryGetCode(candidate, out country);
    }

    private static IEnumerable<DetectedValue<string>> DetectBuyerVatIds(
        SegmentEntry entry, string reason)
    {
        if (!ContainsKeyword(entry.Text, VatTerms))
        {
            yield break;
        }

        foreach (Match match in BuyerVatMatches(entry.Text))
        {
            string value = match.Value
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();

            if (SharedRules.LooksLikeVatId(value)
                && VatIdSyntax.HasKnownCountryPrefix(value))
            {
                yield return Value(value, DetectionConfidence.Medium, entry.Text, reason);
            }
        }
    }

    private static IEnumerable<Match> BuyerVatMatches(string text)
    {
        string? keyword = DetectionParsers.FirstKeywordIn(text, VatTerms);

        if (keyword is null)
        {
            yield break;
        }

        string candidate = DetectionParsers.AfterKeyword(text, keyword);

        foreach (Match match in BuyerVatIdPattern.Matches(candidate))
        {
            yield return match;
        }
    }

    private static bool IsBuyerBlockBoundary(string text)
        => ContainsKeyword(text, BuyerBlockBoundaryTerms)
           && !(ContainsKeyword(text, VatTerms) && ContainsValidBuyerVatId(text));

    private static IEnumerable<DetectedValue<string>> DetectBuyerEmails(
        SegmentEntry entry, string reason)
    {
        foreach (Match match in EmailPattern.Matches(entry.Text))
        {
            if (SharedRules.LooksLikeEmail(match.Value))
            {
                yield return Value(match.Value, DetectionConfidence.Medium, entry.Text, reason);
            }
        }
    }

    private static DetectedValue<string>? UniqueDetected(
        IEnumerable<DetectedValue<string>?> values)
    {
        DetectedValue<string>[] distinct =
        [
            .. values
                .Where(value => value is not null)
                .Select(value => value!)
                .GroupBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()),
        ];

        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static RecipientRegion[] FindExplicitRecipientRegions(
        IReadOnlyList<SegmentEntry> entries)
    {
        SegmentEntry[] anchors =
        [
            .. entries.Where(entry =>
                ContainsKeyword(entry.Text, BuyerKeywords)
                || ContainsKeyword(entry.Text, DeliveryKeywords)),
        ];

        return
        [
            .. anchors.Select(anchor =>
            {
                double nextAnchorTop = anchors
                    .Where(next => next.PageNumber == anchor.PageNumber
                                   && SameColumn(next.Left, anchor.Left)
                                   && next.Top > anchor.Top)
                    .Select(next => next.Top)
                    .DefaultIfEmpty(anchor.Top + RecipientBlockHeightInPoints)
                    .Min();

                RecipientRegionKind kind = ContainsKeyword(anchor.Text, DeliveryKeywords)
                    ? RecipientRegionKind.Delivery
                    : RecipientRegionKind.Buyer;

                return new RecipientRegion(
                    anchor.PageNumber,
                    anchor.Left,
                    anchor.Top,
                    Math.Min(anchor.Top + RecipientBlockHeightInPoints, nextAnchorTop),
                    kind,
                    anchor.Text.Trim());
            }),
        ];
    }

    private static IEnumerable<RecipientRegion> FindAnchorlessBuyerRegions(
        IReadOnlyList<SegmentEntry> entries,
        IReadOnlyList<RecipientRegion> explicitRegions)
    {
        SegmentEntry[] invoiceAnchors =
        [
            .. entries.Where(IsInvoiceHeading),
        ];

        if (invoiceAnchors.Length != 1)
        {
            yield break;
        }

        SegmentEntry invoiceAnchor = invoiceAnchors[0];
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SegmentEntry street in entries.Where(entry =>
                     entry.PageNumber == invoiceAnchor.PageNumber
                     && entry.Top < invoiceAnchor.Top
                     && SameColumn(entry.Left, invoiceAnchor.Left)
                     && DetectionParsers.Street().IsMatch(entry.Text)
                     && !IsRecipient(entry, explicitRegions)))
        {
            SegmentEntry? place = entries
                .Where(entry => entry.PageNumber == street.PageNumber
                                && SameColumn(entry.Left, street.Left)
                                && entry.Top > street.Top
                                && entry.Top - street.Top <= 35
                                && DetectionParsers.PostalCodeAndCity().IsMatch(entry.Text)
                                && !IsRecipient(entry, explicitRegions))
                .OrderBy(entry => entry.Top)
                .FirstOrDefault();

            if (place is null
                || invoiceAnchor.Top - place.Top is < 25 or > 160)
            {
                continue;
            }

            bool excludedSection = entries.Any(entry =>
                entry.PageNumber == street.PageNumber
                && SameColumn(entry.Left, street.Left)
                && entry.Top >= street.Top - 90
                && entry.Top <= place.Top
                && ContainsKeyword(entry.Text, AnchorlessBuyerExcludedSectionTerms));

            if (excludedSection)
            {
                continue;
            }

            SegmentEntry[] possibleNames =
            [
                .. entries.Where(entry => entry.PageNumber == street.PageNumber
                                          && SameColumn(entry.Left, street.Left)
                                          && entry.Top < street.Top
                                          && street.Top - entry.Top <= 70
                                          && !IsRecipient(entry, explicitRegions)
                                          && IsPlausibleName(entry.Text))
                    .OrderBy(entry => entry.Top),
            ];

            SegmentEntry[] identityNames =
            [
                .. possibleNames.Where(entry => !IsAddressQualifier(entry.Text)),
            ];
            SegmentEntry? name = identityNames.Length == 1 ? identityNames[0] : null;

            if (name is null)
            {
                continue;
            }

            SegmentEntry[] block =
            [
                .. entries.Where(entry => entry.PageNumber == street.PageNumber
                                          && SameColumn(entry.Left, street.Left)
                                          && entry.Top >= name.Top
                                          && entry.Top <= Math.Min(place.Top + 60, invoiceAnchor.Top))
                    .OrderBy(entry => entry.Top),
            ];

            if (block.Any(entry => IsAnchorlessBuyerBlocker(entry.Text)))
            {
                continue;
            }

            double nextBoundaryTop = entries
                .Where(entry => entry.PageNumber == place.PageNumber
                                && SameColumn(entry.Left, place.Left)
                                && entry.Top > place.Top
                                && entry.Top < invoiceAnchor.Top
                                && IsAnchorlessBuyerSectionBoundary(entry.Text))
                .Select(entry => entry.Top)
                .DefaultIfEmpty(invoiceAnchor.Top)
                .Min();

            string identity = string.Join('|', name.Text, street.Text, place.Text);

            if (!identities.Add(identity))
            {
                continue;
            }

            yield return new RecipientRegion(
                name.PageNumber,
                name.Left,
                name.Top - 0.1,
                nextBoundaryTop,
                RecipientRegionKind.Buyer,
                "unbeschrifteter Empfängerblock vor der Rechnungsüberschrift",
                IsAnchorless: true);
        }
    }

    private static bool IsInvoiceHeading(SegmentEntry entry)
    {
        const string anchor = "rechnung";
        string text = entry.Text.Trim();

        return text.StartsWith(anchor, StringComparison.OrdinalIgnoreCase)
               && text.Length > anchor.Length
               && (char.IsWhiteSpace(text[anchor.Length])
                   || text[anchor.Length] is ':' or '-' or '#')
               && text[anchor.Length..].Any(char.IsDigit);
    }

    private static bool IsAnchorlessBuyerBlocker(string text)
    {
        string lower = text.ToLowerInvariant();

        return ContainsKeyword(text, SellerKeywords)
               || ContainsKeyword(text, DeliveryKeywords)
               || ContainsKeyword(text, TaxTerms)
               || ContainsKeyword(text, AnchorlessBuyerExcludedSectionTerms)
               || lower.Contains("iban", StringComparison.Ordinal)
               || lower.Contains("bic", StringComparison.Ordinal)
               || lower.Contains("swift", StringComparison.Ordinal)
               || lower.Contains("bankverbindung", StringComparison.Ordinal);
    }

    private static bool IsAnchorlessBuyerSectionBoundary(string text)
        => IsBuyerBlockBoundary(text)
           || ContainsKeyword(text, AnchorlessBuyerExcludedSectionTerms)
           || ContainsKeyword(text, SellerKeywords)
           || ContainsKeyword(text, DeliveryKeywords)
           || ContainsKeyword(text, TaxTerms)
           || text.Contains("iban", StringComparison.OrdinalIgnoreCase)
           || text.Contains("bic", StringComparison.OrdinalIgnoreCase)
           || text.Contains("swift", StringComparison.OrdinalIgnoreCase)
           || text.Contains("bankverbindung", StringComparison.OrdinalIgnoreCase);

    private static bool SameCoreIdentity(DetectedParty seller, DetectedParty buyer)
        => Equal(seller.Name, buyer.Name)
           && Equal(seller.Street, buyer.Street)
           && Equal(seller.PostalCode, buyer.PostalCode)
           && Equal(seller.City, buyer.City);

    private static bool Equal(
        DetectedValue<string>? left, DetectedValue<string>? right)
        => left is not null
           && right is not null
           && string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);

    private static bool IsAddressQualifier(string text)
    {
        string lower = text.Trim().ToLowerInvariant();

        return lower.StartsWith("abt.", StringComparison.Ordinal)
               || lower.StartsWith("abteilung", StringComparison.Ordinal)
               || lower.StartsWith("bereich", StringComparison.Ordinal)
               || lower.StartsWith("z. hd.", StringComparison.Ordinal)
               || lower.StartsWith("zu händen", StringComparison.Ordinal)
               || lower.StartsWith("c/o", StringComparison.Ordinal);
    }

    private static IReadOnlyList<SegmentEntry> Entries(IReadOnlyList<PdfTextLine> lines)
        => [.. lines.SelectMany(line => line.Segments.Select(segment => new SegmentEntry(
            segment.Text.Trim(), line.PageNumber, line.Top, segment.Left)))];

    private static bool IsRecipient(
        SegmentEntry entry, IReadOnlyList<RecipientRegion> regions)
        => regions.Any(region => region.Contains(entry.PageNumber, entry.Top, entry.Left));

    private static bool IsRecipient(
        LocatedPaymentValue value, IReadOnlyList<RecipientRegion> regions)
        => regions.Any(region => region.Contains(value.PageNumber, value.Top, value.Left));

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
               && !ContainsKeyword(value, DeliveryKeywords)
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
        string Identity,
        bool HasSellerSpecificSignal);

    private sealed record BuyerCandidate(
        DetectedParty Party,
        string Identity,
        RecipientRegion Region);

    private sealed record CombinedAddress(
        SegmentEntry Entry,
        string Street,
        string PostalCode,
        string City);

    private sealed record HeaderName(string Value, SegmentEntry Entry);

    private enum RecipientRegionKind
    {
        Buyer,
        Delivery,
    }

    private sealed record RecipientRegion(
        int PageNumber,
        double Left,
        double StartTop,
        double EndTop,
        RecipientRegionKind Kind,
        string AnchorText,
        bool IsAnchorless = false)
    {
        public bool Contains(int pageNumber, double top, double left)
            => pageNumber == PageNumber
               && SameColumn(left, Left)
               && top >= StartTop
               && top < EndTop;
    }
}

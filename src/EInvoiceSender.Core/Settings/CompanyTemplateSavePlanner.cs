using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Validation.Rules;

namespace EInvoiceSender.Core.Settings;

/// <summary>
/// Bereitet das ausdrückliche Speichern bestätigter Unternehmensdaten vor.
///
/// Die feste Allowlist ist die Sicherheitsgrenze: Nicht der gesamte Entwurf,
/// sondern ausschließlich die hier einzeln genannten Unternehmensfelder dürfen
/// in eine <see cref="CompanyTemplate"/> gelangen. Neben manuellen Werten ist
/// nur ein unverändert zum aktuellen Draft passender Wert aus dem konkreten
/// <see cref="DetectedOwnCompanyProposal"/> zugelassen. Käufer-, Rechnungs-
/// und beliebige andere PDF-Daten sind damit konstruktiv ausgeschlossen.
/// </summary>
public static class CompanyTemplateSavePlanner
{
    /// <summary>Die vollständige Allowlist der speicherbaren Entwurfsfelder.</summary>
    public static IReadOnlyList<string> AllowedFields { get; } = Array.AsReadOnly(
    [
        nameof(InvoiceDraft.SellerName),
        nameof(InvoiceDraft.SellerStreet),
        nameof(InvoiceDraft.SellerPostalCode),
        nameof(InvoiceDraft.SellerCity),
        nameof(InvoiceDraft.SellerCountry),
        nameof(InvoiceDraft.SellerEmail),
        nameof(InvoiceDraft.SellerVatId),
        nameof(InvoiceDraft.SellerTaxNumber),
        nameof(InvoiceDraft.BankAccountHolder),
        nameof(InvoiceDraft.BankIban),
        nameof(InvoiceDraft.BankBic),
    ]);

    /// <summary>
    /// Erstellt einen Kandidaten durch feldweises Zusammenführen mit der frisch
    /// gelesenen Vorlage. Ein Wert darf nur manuell sein oder unverändert zum
    /// aktuellen Seller-Proposal passen. Einzige weitere Ausnahme ist das
    /// unveränderte Programmstandardland DE.
    /// </summary>
    public static CompanyTemplateSavePlan Plan(
        InvoiceDraft draft,
        CompanyTemplate existing,
        DetectedOwnCompanyProposal? proposal = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(existing);

        bool hasManualInput = AllowedFields.Any(
            property => draft.OriginOf(property) == FieldOrigin.Manual);
        bool hasProposalInput = proposal?.Fields.Any(field => MatchesProposal(draft, field)) == true;

        CompanyTemplate candidate = existing with
        {
            SellerName = Approved(
                draft, nameof(draft.SellerName), draft.SellerName, existing.SellerName,
                proposal, DetectedOwnCompanyFieldKind.SellerName),
            SellerStreet = Approved(
                draft, nameof(draft.SellerStreet), draft.SellerStreet, existing.SellerStreet,
                proposal, DetectedOwnCompanyFieldKind.SellerStreet),
            SellerPostalCode = Approved(
                draft, nameof(draft.SellerPostalCode), draft.SellerPostalCode, existing.SellerPostalCode,
                proposal, DetectedOwnCompanyFieldKind.SellerPostalCode),
            SellerCity = Approved(
                draft, nameof(draft.SellerCity), draft.SellerCity, existing.SellerCity,
                proposal, DetectedOwnCompanyFieldKind.SellerCity),
            SellerCountry = SellerCountry(draft, existing.SellerCountry, proposal),
            SellerEmail = Approved(
                draft, nameof(draft.SellerEmail), draft.SellerEmail, existing.SellerEmail,
                proposal, DetectedOwnCompanyFieldKind.SellerEmail),
            SellerVatId = Approved(
                draft, nameof(draft.SellerVatId), draft.SellerVatId, existing.SellerVatId,
                proposal, DetectedOwnCompanyFieldKind.SellerVatId),
            SellerTaxNumber = Approved(
                draft, nameof(draft.SellerTaxNumber), draft.SellerTaxNumber, existing.SellerTaxNumber,
                proposal, DetectedOwnCompanyFieldKind.SellerTaxNumber),
            BankAccountHolder = Manual(
                draft, nameof(draft.BankAccountHolder), draft.BankAccountHolder, existing.BankAccountHolder),
            BankIban = Approved(
                draft, nameof(draft.BankIban), draft.BankIban, existing.BankIban,
                proposal, DetectedOwnCompanyFieldKind.BankIban),
            BankBic = Approved(
                draft, nameof(draft.BankBic), draft.BankBic, existing.BankBic,
                proposal, DetectedOwnCompanyFieldKind.BankBic),
        };

        string[] changedFields =
        [
            .. AllowedFields.Where(property => FieldChanged(property, existing, candidate)),
        ];

        (string[] errors, string[] warnings) = Validate(
            candidate, hasManualInput || hasProposalInput);

        return new CompanyTemplateSavePlan(
            Existing: existing,
            Candidate: candidate,
            HasExistingCompanyData: HasCompanyData(existing),
            HasManualInput: hasManualInput,
            HasProposalInput: hasProposalInput,
            ChangedFields: changedFields,
            Errors: errors,
            Warnings: warnings);
    }

    /// <summary>
    /// Erkennt eine inhaltliche Unternehmensvorlage. Die Existenz der JSON-Datei
    /// genügt nicht, weil sie auch ausschließlich Komfortvorgaben enthalten kann.
    /// </summary>
    public static bool HasCompanyData(CompanyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return CompanyValues(template).Any(value => !string.IsNullOrWhiteSpace(value))
               || (!string.IsNullOrWhiteSpace(template.SellerCountry)
                   && !string.Equals(
                       template.SellerCountry.Trim(), "DE", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Hat der Anwender mindestens ein erlaubtes Feld selbst geändert?</summary>
    public static bool HasManualInput(InvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return AllowedFields.Any(property => draft.OriginOf(property) == FieldOrigin.Manual);
    }

    private static string? Manual(
        InvoiceDraft draft, string property, string value, string? existing)
        => draft.OriginOf(property) == FieldOrigin.Manual ? Blank(value) : existing;

    private static string? Approved(
        InvoiceDraft draft,
        string property,
        string value,
        string? existing,
        DetectedOwnCompanyProposal? proposal,
        DetectedOwnCompanyFieldKind proposalKind)
    {
        if (draft.OriginOf(property) == FieldOrigin.Manual)
        {
            return Blank(value);
        }

        DetectedOwnCompanyField? proposed = proposal?.Field(proposalKind);

        return proposed is not null && MatchesProposal(draft, property, value, proposed)
            ? Blank(value)
            : existing;
    }

    private static string? SellerCountry(
        InvoiceDraft draft,
        string? existing,
        DetectedOwnCompanyProposal? proposal)
    {
        FieldOrigin origin = draft.OriginOf(nameof(draft.SellerCountry));

        if (origin == FieldOrigin.Manual)
        {
            return Blank(draft.SellerCountry)?.ToUpperInvariant();
        }

        DetectedOwnCompanyField? proposed =
            proposal?.Field(DetectedOwnCompanyFieldKind.SellerCountry);

        if (proposed is not null
            && MatchesProposal(
                draft, nameof(draft.SellerCountry), draft.SellerCountry, proposed))
        {
            return Blank(draft.SellerCountry)?.ToUpperInvariant();
        }

        // DE ist der sichtbare Auslieferungsstandard. Er darf beim ausdrücklichen
        // Speichern mitgenommen werden, aber niemals dann, wenn er aus der PDF kam.
        if (origin == FieldOrigin.Default
            && string.IsNullOrWhiteSpace(existing)
            && string.Equals(draft.SellerCountry, "DE", StringComparison.OrdinalIgnoreCase))
        {
            return "DE";
        }

        return existing;
    }

    private static bool MatchesProposal(
        InvoiceDraft draft, DetectedOwnCompanyField proposed)
        => proposed.Kind switch
        {
            DetectedOwnCompanyFieldKind.SellerName => MatchesProposal(
                draft, nameof(draft.SellerName), draft.SellerName, proposed),
            DetectedOwnCompanyFieldKind.SellerStreet => MatchesProposal(
                draft, nameof(draft.SellerStreet), draft.SellerStreet, proposed),
            DetectedOwnCompanyFieldKind.SellerPostalCode => MatchesProposal(
                draft, nameof(draft.SellerPostalCode), draft.SellerPostalCode, proposed),
            DetectedOwnCompanyFieldKind.SellerCity => MatchesProposal(
                draft, nameof(draft.SellerCity), draft.SellerCity, proposed),
            DetectedOwnCompanyFieldKind.SellerCountry => MatchesProposal(
                draft, nameof(draft.SellerCountry), draft.SellerCountry, proposed),
            DetectedOwnCompanyFieldKind.SellerEmail => MatchesProposal(
                draft, nameof(draft.SellerEmail), draft.SellerEmail, proposed),
            DetectedOwnCompanyFieldKind.SellerVatId => MatchesProposal(
                draft, nameof(draft.SellerVatId), draft.SellerVatId, proposed),
            DetectedOwnCompanyFieldKind.SellerTaxNumber => MatchesProposal(
                draft, nameof(draft.SellerTaxNumber), draft.SellerTaxNumber, proposed),
            DetectedOwnCompanyFieldKind.BankIban => MatchesProposal(
                draft, nameof(draft.BankIban), draft.BankIban, proposed),
            DetectedOwnCompanyFieldKind.BankBic => MatchesProposal(
                draft, nameof(draft.BankBic), draft.BankBic, proposed),
            _ => false,
        };

    private static bool MatchesProposal(
        InvoiceDraft draft,
        string property,
        string currentValue,
        DetectedOwnCompanyField proposed)
    {
        FieldOrigin expectedOrigin = proposed.Confidence switch
        {
            DetectionConfidence.High => FieldOrigin.DetectedReliably,
            DetectionConfidence.Medium => FieldOrigin.DetectedUncertain,
            _ => FieldOrigin.Default,
        };

        return proposed.Confidence >= DetectionConfidence.Medium
               && draft.OriginOf(property) == expectedOrigin
               && string.Equals(currentValue, proposed.Value, StringComparison.Ordinal);
    }

    private static (string[] Errors, string[] Warnings) Validate(
        CompanyTemplate candidate, bool hasApprovedInput)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!hasApprovedInput)
        {
            errors.Add("Es wurden keine bestätigbaren Unternehmensdaten eingegeben oder erkannt.");
        }

        if (string.IsNullOrWhiteSpace(candidate.SellerName))
        {
            errors.Add("Der Firmenname fehlt.");
        }

        if (!CountryCodeList.IsValid(candidate.SellerCountry))
        {
            errors.Add("Das Land fehlt oder ist kein gültiges Länderkennzeichen.");
        }

        if (string.IsNullOrWhiteSpace(candidate.SellerVatId)
            && string.IsNullOrWhiteSpace(candidate.SellerTaxNumber))
        {
            errors.Add("Eine USt-IdNr. oder Steuernummer ist erforderlich.");
        }

        if (!string.IsNullOrWhiteSpace(candidate.SellerEmail)
            && !SharedRules.LooksLikeEmail(candidate.SellerEmail))
        {
            errors.Add("Die E-Mail-Adresse ist nicht gültig.");
        }

        if (!string.IsNullOrWhiteSpace(candidate.BankIban)
            && !Iban.TryParse(candidate.BankIban, out _))
        {
            errors.Add("Die IBAN ist nicht gültig.");
        }

        if (string.IsNullOrWhiteSpace(candidate.SellerStreet))
        {
            warnings.Add("Die Straße fehlt.");
        }

        if (string.IsNullOrWhiteSpace(candidate.SellerCity))
        {
            warnings.Add("Der Ort fehlt.");
        }

        return ([.. errors], [.. warnings]);
    }

    private static bool FieldChanged(
        string property, CompanyTemplate existing, CompanyTemplate candidate)
        => property switch
        {
            nameof(InvoiceDraft.SellerName) => existing.SellerName != candidate.SellerName,
            nameof(InvoiceDraft.SellerStreet) => existing.SellerStreet != candidate.SellerStreet,
            nameof(InvoiceDraft.SellerPostalCode) => existing.SellerPostalCode != candidate.SellerPostalCode,
            nameof(InvoiceDraft.SellerCity) => existing.SellerCity != candidate.SellerCity,
            nameof(InvoiceDraft.SellerCountry) => existing.SellerCountry != candidate.SellerCountry,
            nameof(InvoiceDraft.SellerEmail) => existing.SellerEmail != candidate.SellerEmail,
            nameof(InvoiceDraft.SellerVatId) => existing.SellerVatId != candidate.SellerVatId,
            nameof(InvoiceDraft.SellerTaxNumber) => existing.SellerTaxNumber != candidate.SellerTaxNumber,
            nameof(InvoiceDraft.BankAccountHolder) => existing.BankAccountHolder != candidate.BankAccountHolder,
            nameof(InvoiceDraft.BankIban) => existing.BankIban != candidate.BankIban,
            nameof(InvoiceDraft.BankBic) => existing.BankBic != candidate.BankBic,
            _ => throw new InvalidOperationException($"Unbekanntes Allowlist-Feld: {property}"),
        };

    private static IEnumerable<string?> CompanyValues(CompanyTemplate template)
    {
        yield return template.SellerName;
        yield return template.SellerStreet;
        yield return template.SellerPostalCode;
        yield return template.SellerCity;
        yield return template.SellerEmail;
        yield return template.SellerVatId;
        yield return template.SellerTaxNumber;
        yield return template.BankAccountHolder;
        yield return template.BankIban;
        yield return template.BankBic;
    }

    private static string? Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Das vollständige Ergebnis einer Speicherplanung.</summary>
public sealed record CompanyTemplateSavePlan(
    CompanyTemplate Existing,
    CompanyTemplate Candidate,
    bool HasExistingCompanyData,
    bool HasManualInput,
    bool HasProposalInput,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Unterscheidet sich der Kandidat tatsächlich von der gespeicherten Vorlage?</summary>
    public bool IsChanged => ChangedFields.Count > 0;

    /// <summary>Muss eine inhaltlich vorhandene Vorlage bestätigt werden?</summary>
    public bool RequiresConfirmation => HasExistingCompanyData && IsChanged;

    /// <summary>Enthält der Plan manuelle oder konkret bestätigbare Proposal-Werte?</summary>
    public bool HasApprovedInput => HasManualInput || HasProposalInput;

    /// <summary>Kann dieser Kandidat gespeichert werden?</summary>
    public bool CanSave => HasApprovedInput && IsChanged && Errors.Count == 0;
}

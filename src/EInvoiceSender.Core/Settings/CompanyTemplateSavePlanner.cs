using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Validation.Rules;

namespace EInvoiceSender.Core.Settings;

/// <summary>
/// Bereitet das ausdrückliche Speichern manuell erfasster Unternehmensdaten vor.
///
/// Die feste Allowlist ist die Sicherheitsgrenze: Nicht der gesamte Entwurf,
/// sondern ausschließlich die hier einzeln genannten Unternehmensfelder dürfen
/// in eine <see cref="CompanyTemplate"/> gelangen. PDF-Erkennung, Käufer- und
/// Rechnungsdaten sind damit konstruktiv ausgeschlossen.
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
    /// gelesenen Vorlage. Nur <see cref="FieldOrigin.Manual"/> darf einen Wert
    /// ändern. Einzige Ausnahme ist das unveränderte Programmstandardland DE.
    /// </summary>
    public static CompanyTemplateSavePlan Plan(InvoiceDraft draft, CompanyTemplate existing)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(existing);

        bool hasManualInput = AllowedFields.Any(
            property => draft.OriginOf(property) == FieldOrigin.Manual);

        CompanyTemplate candidate = existing with
        {
            SellerName = Manual(draft, nameof(draft.SellerName), draft.SellerName, existing.SellerName),
            SellerStreet = Manual(draft, nameof(draft.SellerStreet), draft.SellerStreet, existing.SellerStreet),
            SellerPostalCode = Manual(
                draft, nameof(draft.SellerPostalCode), draft.SellerPostalCode, existing.SellerPostalCode),
            SellerCity = Manual(draft, nameof(draft.SellerCity), draft.SellerCity, existing.SellerCity),
            SellerCountry = SellerCountry(draft, existing.SellerCountry),
            SellerEmail = Manual(draft, nameof(draft.SellerEmail), draft.SellerEmail, existing.SellerEmail),
            SellerVatId = Manual(draft, nameof(draft.SellerVatId), draft.SellerVatId, existing.SellerVatId),
            SellerTaxNumber = Manual(
                draft, nameof(draft.SellerTaxNumber), draft.SellerTaxNumber, existing.SellerTaxNumber),
            BankAccountHolder = Manual(
                draft, nameof(draft.BankAccountHolder), draft.BankAccountHolder, existing.BankAccountHolder),
            BankIban = Manual(draft, nameof(draft.BankIban), draft.BankIban, existing.BankIban),
            BankBic = Manual(draft, nameof(draft.BankBic), draft.BankBic, existing.BankBic),
        };

        string[] changedFields =
        [
            .. AllowedFields.Where(property => FieldChanged(property, existing, candidate)),
        ];

        (string[] errors, string[] warnings) = Validate(candidate, hasManualInput);

        return new CompanyTemplateSavePlan(
            Existing: existing,
            Candidate: candidate,
            HasExistingCompanyData: HasCompanyData(existing),
            HasManualInput: hasManualInput,
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

    private static string? SellerCountry(InvoiceDraft draft, string? existing)
    {
        FieldOrigin origin = draft.OriginOf(nameof(draft.SellerCountry));

        if (origin == FieldOrigin.Manual)
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

    private static (string[] Errors, string[] Warnings) Validate(
        CompanyTemplate candidate, bool hasManualInput)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!hasManualInput)
        {
            errors.Add("Es wurden keine Unternehmensdaten von Hand eingegeben oder geändert.");
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
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Unterscheidet sich der Kandidat tatsächlich von der gespeicherten Vorlage?</summary>
    public bool IsChanged => ChangedFields.Count > 0;

    /// <summary>Muss eine inhaltlich vorhandene Vorlage bestätigt werden?</summary>
    public bool RequiresConfirmation => HasExistingCompanyData && IsChanged;

    /// <summary>Kann dieser Kandidat gespeichert werden?</summary>
    public bool CanSave => HasManualInput && IsChanged && Errors.Count == 0;
}

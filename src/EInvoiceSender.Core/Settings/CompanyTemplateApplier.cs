using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Settings;

/// <summary>
/// Trägt die gespeicherte Firmenvorlage in das Formular ein.
///
/// **Was übernommen wird:** ausschließlich die Angaben, die aus der Vorlage
/// stammen – eigene Firma, Anschrift, Land, Steuernummern, E-Mail,
/// Bankverbindung sowie die bewusst hinterlegten Vorgaben für Währung,
/// Zahlungsbedingungen und Zahlungsziel. Rechnungsnummer, Käufer, Positionen,
/// Mengen und Preise gehören zur einzelnen Rechnung und werden nie angefasst.
///
/// **Was geschützt bleibt:** jedes Feld, das der Anwender selbst geändert hat.
/// Die Herkunft entscheidet – dieselbe Regel wie bei der PDF-Erkennung, siehe
/// <see cref="FieldOriginRules.CanReplace"/>. Übernommene Werte werden als
/// <see cref="FieldOrigin.Template"/> vermerkt; vorher galten sie als
/// Benutzereingabe, was schlicht nicht stimmte und die spätere Übernahme
/// geänderter Einstellungen unmöglich machte.
/// </summary>
public static class CompanyTemplateApplier
{
    /// <summary>
    /// Trägt die Vorlage ein und liefert die Namen der geänderten Felder.
    /// </summary>
    public static IReadOnlyList<string> Apply(InvoiceDraft draft, CompanyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(template);

        var changed = new List<string>();

        draft.Prefill(d =>
        {
            Set(d, changed, nameof(d.SellerName), template.SellerName, v => d.SellerName = v);
            Set(d, changed, nameof(d.SellerStreet), template.SellerStreet, v => d.SellerStreet = v);
            Set(d, changed, nameof(d.SellerPostalCode), template.SellerPostalCode, v => d.SellerPostalCode = v);
            Set(d, changed, nameof(d.SellerCity), template.SellerCity, v => d.SellerCity = v);
            Set(d, changed, nameof(d.SellerCountry), template.SellerCountry, v => d.SellerCountry = v);
            Set(d, changed, nameof(d.SellerEmail), template.SellerEmail, v => d.SellerEmail = v);
            Set(d, changed, nameof(d.SellerVatId), template.SellerVatId, v => d.SellerVatId = v);
            Set(d, changed, nameof(d.SellerTaxNumber), template.SellerTaxNumber, v => d.SellerTaxNumber = v);

            Set(d, changed, nameof(d.BankAccountHolder), template.BankAccountHolder, v => d.BankAccountHolder = v);
            Set(d, changed, nameof(d.BankIban), template.BankIban, v => d.BankIban = v);
            Set(d, changed, nameof(d.BankBic), template.BankBic, v => d.BankBic = v);

            Set(d, changed, nameof(d.PaymentTerms), template.DefaultPaymentTerms, v => d.PaymentTerms = v);
            Set(d, changed, nameof(d.Currency), template.DefaultCurrency, v => d.Currency = v);

            ApplyPaymentTerm(d, changed, template);
        });

        return changed;
    }

    /// <summary>
    /// Das Zahlungsziel ist kein eigener Wert, sondern eine Frist: Aus
    /// Rechnungsdatum plus Tagen ergibt sich das Fälligkeitsdatum. Hat der
    /// Anwender ein Datum von Hand gesetzt, bleibt es stehen.
    /// </summary>
    private static void ApplyPaymentTerm(
        InvoiceDraft draft, List<string> changed, CompanyTemplate template)
    {
        if (template.DefaultPaymentTermDays <= 0 || draft.IssueDate is not { } issue)
        {
            return;
        }

        if (!CanReplace(draft, nameof(draft.DueDate)))
        {
            return;
        }

        DateOnly due = issue.AddDays(template.DefaultPaymentTermDays);

        if (draft.DueDate == due)
        {
            return;
        }

        draft.DueDate = due;
        draft.MarkOrigin(nameof(draft.DueDate), FieldOrigin.Template);
        changed.Add(nameof(draft.DueDate));
    }

    private static void Set(
        InvoiceDraft draft,
        List<string> changed,
        string property,
        string? value,
        Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(value) || !CanReplace(draft, property))
        {
            return;
        }

        assign(value);
        draft.MarkOrigin(property, FieldOrigin.Template);
        changed.Add(property);
    }

    private static bool CanReplace(InvoiceDraft draft, string property)
        => FieldOriginRules.CanReplace(draft.OriginOf(property), FieldOrigin.Template);
}

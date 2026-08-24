using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Settings;

/// <summary>
/// Trägt die gespeicherte Firmenvorlage in das Formular ein.
///
/// **Was übernommen wird:** eigene Firma, Anschrift, Land, Steuernummern,
/// E-Mail und Bankverbindung als Stammdaten sowie die bewusst hinterlegten
/// Vorgaben für Währung, Zahlungsbedingungen und Zahlungsziel als
/// Komfort-Defaults. Rechnungsnummer, Käufer, Positionen, Mengen und Preise
/// gehören zur einzelnen Rechnung und werden nie angefasst.
///
/// **Vorrang:** Stammdaten aus der Vorlage stehen über der PDF-Erkennung.
/// Komfort-Defaults dagegen weichen einer brauchbaren PDF-Erkennung, weil die
/// einzelne Rechnung für Währung und Fälligkeit maßgeblich ist.
///
/// **Was geschützt bleibt:** jedes Feld, das der Anwender selbst geändert hat.
/// Die Herkunft entscheidet – dieselbe Regel wie bei der PDF-Erkennung, siehe
/// <see cref="FieldOriginRules.CanReplace"/>.
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

            // BT-30 ist Firmenstamm und kommt aus der Vorlage. BT-29 – die vom
            // Kunden vergebene Lieferantennummer – steht hier bewusst nicht:
            // Sie gehört zur einzelnen Rechnung.
            Set(d, changed, nameof(d.SellerLegalRegistrationId), template.SellerLegalRegistrationId,
                v => d.SellerLegalRegistrationId = v);

            Set(d, changed, nameof(d.BankAccountHolder), template.BankAccountHolder, v => d.BankAccountHolder = v);
            Set(d, changed, nameof(d.BankIban), template.BankIban, v => d.BankIban = v);
            Set(d, changed, nameof(d.BankBic), template.BankBic, v => d.BankBic = v);

            SetDefault(d, changed, nameof(d.PaymentTerms), template.DefaultPaymentTerms, v => d.PaymentTerms = v);
            SetDefault(d, changed, nameof(d.Currency), template.DefaultCurrency, v => d.Currency = v);

            if (d.ConfigureDefaultPaymentTermDays(template.DefaultPaymentTermDays))
            {
                changed.Add(nameof(d.DueDate));
            }
        });

        return changed;
    }

    /// <summary>Übernimmt einen echten Stammdatenwert aus der Firmenvorlage.</summary>
    private static void Set(
        InvoiceDraft draft,
        List<string> changed,
        string property,
        string? value,
        Action<string> assign)
        => Set(draft, changed, property, value, FieldOrigin.Template, assign);

    /// <summary>Übernimmt eine gespeicherte Komfortvorgabe.</summary>
    private static void SetDefault(
        InvoiceDraft draft,
        List<string> changed,
        string property,
        string? value,
        Action<string> assign)
        => Set(draft, changed, property, value, FieldOrigin.TemplateDefault, assign);

    private static void Set(
        InvoiceDraft draft,
        List<string> changed,
        string property,
        string? value,
        FieldOrigin proposedOrigin,
        Action<string> assign)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !FieldOriginRules.CanReplace(draft.OriginOf(property), proposedOrigin))
        {
            return;
        }

        assign(value);
        draft.MarkOrigin(property, proposedOrigin);
        changed.Add(property);
    }
}

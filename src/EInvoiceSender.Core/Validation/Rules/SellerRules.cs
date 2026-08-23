using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zum Rechnungssteller.
/// </summary>
internal static class SellerRules
{
    public static void Validate(SellerParty seller, ValidationReportBuilder report)
    {
        if (string.IsNullOrWhiteSpace(seller.Name))
        {
            report.Error(
                "APP-SEL-001",
                "Der Name des Rechnungsstellers fehlt.",
                "Seller.Name", normRule: "BR-06");
        }

        AddressRules.Validate(seller.Address, "Seller.Address", "des Rechnungsstellers", "APP-SEL-002", report);

        if (!CountryCodeList.IsValid(seller.Address.Country.Value))
        {
            report.Error(
                "APP-SEL-003",
                $"'{seller.Address.Country.Value}' ist kein bekanntes Länderkennzeichen.",
                "Seller.Address.Country", normRule: "BR-09");
        }

        // BR-CO-26: Der Empfänger muss den Rechnungssteller maschinell
        // identifizieren können. Dafür zählt die USt-IdNr. (BT-31) oder die
        // Handelsregisternummer (BT-30).
        //
        // **Die Steuernummer (BT-32) zählt ausdrücklich nicht.** Sie steht im
        // CII als schemeID="FC"; das CEN-Schematron prüft für BR-CO-26 aber
        // nur schemeID="VA", ram:ID, ram:GlobalID und
        // SpecifiedLegalOrganization/ram:ID. Sie hier gelten zu lassen hieße,
        // eine Rechnung durchzuwinken, die jeder externe Prüfer beanstandet –
        // und der Anwender erführe es erst beim Empfänger.
        if (string.IsNullOrWhiteSpace(seller.VatId)
            && string.IsNullOrWhiteSpace(seller.LegalRegistrationId))
        {
            report.Error(
                "APP-SEL-004",
                "Der Rechnungssteller ist nicht eindeutig identifizierbar. Bitte "
                + "hinterlegen Sie in Ihren Firmendaten die "
                + "Umsatzsteuer-Identifikationsnummer. Eine Steuernummer allein "
                + "genügt dafür nicht.",
                "Seller.VatId", normRule: "BR-CO-26");
        }

        if (!string.IsNullOrWhiteSpace(seller.VatId) && !SharedRules.LooksLikeVatId(seller.VatId))
        {
            report.Warning(
                "APP-SEL-005",
                "Die Umsatzsteuer-Identifikationsnummer hat ein ungewöhnliches Format. "
                + "Sie beginnt normalerweise mit dem Länderkennzeichen, zum Beispiel DE123456789.",
                "Seller.VatId",
                $"Gelesen: {seller.VatId}");
        }

        if (!string.IsNullOrWhiteSpace(seller.Email) && !SharedRules.LooksLikeEmail(seller.Email))
        {
            report.Error(
                "APP-SEL-006",
                "Die E-Mail-Adresse des Rechnungsstellers ist nicht gültig.",
                "Seller.Email");
        }
    }

    // ------------------------------------------------------------------ Käufer
}

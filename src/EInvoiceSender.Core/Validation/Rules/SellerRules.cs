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
        // identifizieren können. Dafür zählt jede der drei Kennungen, die das
        // CEN-Schematron an dieser Stelle prüft: die Verkäuferkennung (BT-29,
        // ram:ID), die Handelsregisternummer (BT-30,
        // SpecifiedLegalOrganization/ram:ID) und die USt-IdNr. (BT-31,
        // SpecifiedTaxRegistration/ram:ID mit schemeID="VA").
        //
        // **Die Steuernummer (BT-32) zählt ausdrücklich nicht.** Sie steht im
        // CII als schemeID="FC" und ist im Kriterium von BR-CO-26 nicht
        // enthalten. Sie hier gelten zu lassen hieße, eine Rechnung
        // durchzuwinken, die jeder externe Prüfer beanstandet – und der
        // Anwender erführe es erst beim Empfänger.
        if (string.IsNullOrWhiteSpace(seller.SellerIdentifier)
            && string.IsNullOrWhiteSpace(seller.LegalRegistrationId)
            && string.IsNullOrWhiteSpace(seller.VatId))
        {
            report.Error(
                "APP-SEL-004",
                "Für die elektronische Rechnung fehlt eine eindeutige "
                + "Verkäuferkennung. Hinterlegen Sie eine USt-ID, eine "
                + "Registerkennung oder – falls Ihr Kunde Ihnen eine mitgeteilt "
                + "hat – eine Lieferanten-/Kreditorennummer. Eine Steuernummer "
                + "allein genügt hierfür nicht.",
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

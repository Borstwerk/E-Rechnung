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
                $"'{seller.Address.Country.Value}' ist kein bekanntes Laenderkennzeichen.",
                "Seller.Address.Country", normRule: "BR-09");
        }

        // BR-CO-26: Der Rechnungssteller muss steuerlich identifizierbar sein.
        if (string.IsNullOrWhiteSpace(seller.VatId) && string.IsNullOrWhiteSpace(seller.TaxNumber))
        {
            report.Error(
                "APP-SEL-004",
                "Es fehlt die Umsatzsteuer-Identifikationsnummer oder die Steuernummer "
                + "des Rechnungsstellers. Eine der beiden Angaben ist erforderlich.",
                "Seller.VatId", normRule: "BR-CO-26");
        }

        if (!string.IsNullOrWhiteSpace(seller.VatId) && !SharedRules.LooksLikeVatId(seller.VatId))
        {
            report.Warning(
                "APP-SEL-005",
                "Die Umsatzsteuer-Identifikationsnummer hat ein ungewoehnliches Format. "
                + "Sie beginnt normalerweise mit dem Laenderkennzeichen, zum Beispiel DE123456789.",
                "Seller.VatId",
                $"Gelesen: {seller.VatId}");
        }

        if (!string.IsNullOrWhiteSpace(seller.Email) && !SharedRules.LooksLikeEmail(seller.Email))
        {
            report.Error(
                "APP-SEL-006",
                "Die E-Mail-Adresse des Rechnungsstellers ist nicht gueltig.",
                "Seller.Email");
        }
    }

    // ------------------------------------------------------------------ Kaeufer
}

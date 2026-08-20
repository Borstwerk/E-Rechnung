using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zum Rechnungsempfänger.
/// </summary>
internal static class BuyerRules
{
    public static void Validate(BuyerParty buyer, ValidationReportBuilder report)
    {
        if (string.IsNullOrWhiteSpace(buyer.Name))
        {
            report.Error(
                "APP-BUY-001",
                "Der Name des Rechnungsempfängers fehlt.",
                "Buyer.Name", normRule: "BR-07");
        }

        AddressRules.Validate(buyer.Address, "Buyer.Address", "des Rechnungsempfängers", "APP-BUY-002", report);

        if (!CountryCodeList.IsValid(buyer.Address.Country.Value))
        {
            report.Error(
                "APP-BUY-003",
                $"'{buyer.Address.Country.Value}' ist kein bekanntes Länderkennzeichen.",
                "Buyer.Address.Country", normRule: "BR-11");
        }

        if (!string.IsNullOrWhiteSpace(buyer.Email) && !SharedRules.LooksLikeEmail(buyer.Email))
        {
            report.Error(
                "APP-BUY-004",
                "Die E-Mail-Adresse des Rechnungsempfängers ist nicht gültig.",
                "Buyer.Email");
        }

        if (!string.IsNullOrWhiteSpace(buyer.VatId)
            && (!SharedRules.LooksLikeVatId(buyer.VatId)
                || !VatIdSyntax.HasKnownCountryPrefix(buyer.VatId)))
        {
            report.Error(
                "APP-BUY-007",
                "Die USt-IdNr. des Rechnungsempfängers hat kein gültiges Format.",
                "Buyer.VatId");
        }

        if (string.IsNullOrWhiteSpace(buyer.Email) && string.IsNullOrWhiteSpace(buyer.ElectronicAddress))
        {
            report.Warning(
                "APP-BUY-005",
                "Für den Empfänger ist keine elektronische Adresse hinterlegt. "
                + "Ohne sie lässt sich die Rechnung nicht per E-Mail versenden.",
                "Buyer.Email");
        }

        if (!string.IsNullOrWhiteSpace(buyer.ElectronicAddress)
            && string.IsNullOrWhiteSpace(buyer.ElectronicAddressScheme))
        {
            report.Warning(
                "APP-BUY-006",
                "Zur elektronischen Adresse des Empfängers fehlt die Angabe, um "
                + "welche Art von Adresse es sich handelt.",
                "Buyer.ElectronicAddressScheme",
                "Ohne Angabe wird 'EM' (E-Mail) verwendet.", "BR-63");
        }
    }
}

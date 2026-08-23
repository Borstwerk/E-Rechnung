using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Models;

/// <summary>Postanschrift (BG-5 Verkäufer / BG-8 Käufer).</summary>
/// <param name="Street">Straße und Hausnummer (BT-35 / BT-50).</param>
/// <param name="AdditionalLine">Adresszusatz (BT-36 / BT-51).</param>
/// <param name="PostalCode">Postleitzahl (BT-38 / BT-53).</param>
/// <param name="City">Ort (BT-37 / BT-52).</param>
/// <param name="Country">Land (BT-40 / BT-55). Pflichtangabe nach EN 16931.</param>
/// <param name="CountrySubdivision">Region oder Bundesland (BT-39 / BT-54).</param>
public sealed record PostalAddress(
    string? Street,
    string? AdditionalLine,
    string? PostalCode,
    string? City,
    CountryCode Country,
    string? CountrySubdivision = null);

/// <summary>Bankverbindung des Verkäufers (BG-17).</summary>
/// <param name="AccountHolder">Kontoinhaber (BT-85).</param>
/// <param name="Iban">IBAN (BT-84). Bereits geprüft.</param>
/// <param name="Bic">BIC (BT-86), optional im SEPA-Raum.</param>
public sealed record BankAccount(
    string AccountHolder,
    Iban Iban,
    string? Bic = null);

/// <summary>Verkäufer (BG-4).</summary>
/// <param name="Name">Firmen- oder Personenname (BT-27).</param>
/// <param name="Address">Anschrift (BG-5).</param>
/// <param name="Email">E-Mail-Adresse (BT-43).</param>
/// <param name="VatId">Umsatzsteuer-Identifikationsnummer (BT-31).</param>
/// <param name="TaxNumber">
/// Steuernummer (BT-32). Eine zulässige zusätzliche steuerliche Angabe –
/// **keine** Alternative zur USt-IdNr. Sie steht im CII als
/// <c>schemeID="FC"</c> und erfüllt BR-CO-26 nicht: Für die maschinelle
/// Identifikation des Verkäufers zählen nur <see cref="VatId"/> (BT-31) und
/// <see cref="LegalRegistrationId"/> (BT-30).
/// </param>
/// <param name="TradingName">Abweichender Handelsname (BT-28).</param>
/// <param name="ContactName">Ansprechpartner (BT-41).</param>
/// <param name="ContactPhone">Telefon des Ansprechpartners (BT-42).</param>
/// <param name="LegalRegistrationId">
/// Handelsregisternummer (BT-30). Erfüllt BR-CO-26 ebenso wie die USt-IdNr.
/// </param>
public sealed record SellerParty(
    string Name,
    PostalAddress Address,
    string? Email = null,
    string? VatId = null,
    string? TaxNumber = null,
    string? TradingName = null,
    string? ContactName = null,
    string? ContactPhone = null,
    string? LegalRegistrationId = null);

/// <summary>Käufer (BG-7).</summary>
/// <param name="Name">Firmen- oder Personenname (BT-44).</param>
/// <param name="Address">Anschrift (BG-8).</param>
/// <param name="Email">E-Mail-Adresse (BT-58). Wird auch für den Entwurf vorgeschlagen.</param>
/// <param name="VatId">Umsatzsteuer-Identifikationsnummer (BT-48).</param>
/// <param name="ElectronicAddress">Elektronische Adresse beziehungsweise Routing (BT-49).</param>
/// <param name="ElectronicAddressScheme">Schema der elektronischen Adresse (BT-49-1), z. B. EM für E-Mail.</param>
/// <param name="ContactName">Ansprechpartner (BT-56).</param>
public sealed record BuyerParty(
    string Name,
    PostalAddress Address,
    string? Email = null,
    string? VatId = null,
    string? ElectronicAddress = null,
    string? ElectronicAddressScheme = null,
    string? ContactName = null);

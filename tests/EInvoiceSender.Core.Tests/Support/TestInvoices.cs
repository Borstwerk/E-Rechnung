using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Tests.TestData;

/// <summary>
/// Bausteine für Testrechnungen. Alle Angaben sind frei erfunden.
/// Die verwendeten IBANs stammen aus öffentlich publizierten Beispielen und
/// gehören zu keinem realen Konto.
/// </summary>
public static class TestInvoices
{
    /// <summary>Beispiel-IBAN aus der öffentlichen Dokumentation, kein reales Konto.</summary>
    public const string SampleIbanDe = "DE89370400440532013000";

    /// <summary>Ein Verkäufer mit allen Pflichtangaben.</summary>
    public static SellerParty Seller { get; } = new(
        Name: "Musterbetrieb Beispiel GmbH",
        Address: new PostalAddress(
            Street: "Beispielweg 1",
            AdditionalLine: null,
            PostalCode: "10115",
            City: "Berlin",
            Country: CountryCode.Germany),
        Email: "rechnung@example.invalid",
        VatId: "DE123456789",
        TaxNumber: "11/222/33333");

    /// <summary>Ein Käufer mit allen Pflichtangaben.</summary>
    public static BuyerParty Buyer { get; } = new(
        Name: "Beispielkunde AG",
        Address: new PostalAddress(
            Street: "Kundenstraße 7",
            AdditionalLine: null,
            PostalCode: "20095",
            City: "Hamburg",
            Country: CountryCode.Germany),
        Email: "einkauf@example.invalid",
        VatId: "DE987654321");

    /// <summary>Standardzahlungsangaben mit Überweisung.</summary>
    public static PaymentDetails Payment { get; } = new(
        MeansCode: PaymentMeansCode.SepaCreditTransfer,
        BankAccount: new BankAccount("Musterbetrieb Beispiel GmbH", Iban.Parse(SampleIbanDe)),
        Terms: "Zahlbar innerhalb von 14 Tagen ohne Abzug.");

    /// <summary>
    /// Baut eine Rechnung mit den übergebenen Positionen und ansonsten
    /// vollständigen Pflichtangaben.
    /// </summary>
    public static Invoice Create(
        IReadOnlyList<InvoiceLine> lines,
        IReadOnlyList<DocumentAllowanceCharge>? allowancesAndCharges = null,
        decimal paidAmount = 0m,
        decimal roundingAmount = 0m,
        InvoiceTypeCode typeCode = InvoiceTypeCode.CommercialInvoice,
        IReadOnlyList<VatExemptionReason>? exemptionReasons = null)
        => new()
        {
            InvoiceNumber = "RE-2026-0001",
            IssueDate = new DateOnly(2026, 3, 15),
            DueDate = new DateOnly(2026, 3, 29),
            DeliveryDate = new DateOnly(2026, 3, 14),
            TypeCode = typeCode,
            Currency = CurrencyCode.Euro,
            Seller = Seller,
            Buyer = Buyer,
            Lines = lines,
            AllowancesAndCharges = allowancesAndCharges ?? [],
            ExemptionReasons = exemptionReasons ?? [],
            Payment = Payment,
            PaidAmount = paidAmount,
            RoundingAmount = roundingAmount,
        };

    /// <summary>Baut eine einzelne Position mit den üblichen Vorgaben.</summary>
    public static InvoiceLine Line(
        int number,
        decimal quantity,
        decimal netUnitPrice,
        decimal vatRate = 19m,
        VatCategory category = VatCategory.StandardRate,
        decimal allowance = 0m,
        decimal charge = 0m,
        decimal priceBaseQuantity = 1m,
        UnitCode? unit = null)
        => new(
            Number: number,
            Name: $"Leistung {number}",
            Quantity: quantity,
            Unit: unit ?? UnitCode.Piece,
            NetUnitPrice: netUnitPrice,
            VatCategory: category,
            VatRate: vatRate,
            PriceBaseQuantity: priceBaseQuantity,
            AllowanceAmount: allowance,
            ChargeAmount: charge);
}

using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Values;

namespace EInvoiceSender.TestSupport;

/// <summary>
/// Ein benannter Testfall mit einer vollstaendigen Rechnung.
/// </summary>
/// <param name="Key">Kurzname; zugleich Dateiname des Golden Master.</param>
/// <param name="Description">Was der Fall abdeckt.</param>
/// <param name="Invoice">Die Rechnung.</param>
/// <param name="ExpectedToBeValid">
/// Erwartung fuer die Gegenpruefung mit dem CEN-Schematron.
/// </param>
public sealed record InvoiceScenario(
    string Key,
    string Description,
    Invoice Invoice,
    bool ExpectedToBeValid);

/// <summary>
/// Der verbindliche Katalog der Testrechnungen aus docs/SPECIFICATION.md.
/// Alle Angaben sind frei erfunden; die IBAN stammt aus einem oeffentlich
/// publizierten Beispiel und gehoert zu keinem realen Konto.
///
/// Dieser Katalog ist die gemeinsame Grundlage fuer die Golden-Master-Tests der
/// XML-Erzeugung, fuer die Regelpruefung und fuer die Ende-zu-Ende-Tests.
/// </summary>
public static class InvoiceScenarios
{
    private const string SampleIban = "DE89370400440532013000";

    private static readonly DateOnly IssueDate = new(2026, 3, 15);
    private static readonly DateOnly DueDate = new(2026, 3, 29);
    private static readonly DateOnly DeliveryDate = new(2026, 3, 14);

    private static SellerParty Seller { get; } = new(
        Name: "Musterbetrieb Beispiel GmbH",
        Address: new PostalAddress(
            Street: "Beispielweg 1",
            AdditionalLine: null,
            PostalCode: "10115",
            City: "Berlin",
            Country: CountryCode.Germany),
        Email: "rechnung@example.invalid",
        VatId: "DE123456789",
        TaxNumber: "11/222/33333",
        ContactName: "Buchhaltung",
        ContactPhone: "+49 30 000000");

    private static BuyerParty Buyer { get; } = new(
        Name: "Beispielkunde AG",
        Address: new PostalAddress(
            Street: "Kundenstrasse 7",
            AdditionalLine: "Gebaeude B",
            PostalCode: "20095",
            City: "Hamburg",
            Country: CountryCode.Germany),
        Email: "einkauf@example.invalid",
        VatId: "DE987654321");

    private static PaymentDetails Payment { get; } = new(
        MeansCode: PaymentMeansCode.SepaCreditTransfer,
        BankAccount: new BankAccount(
            "Musterbetrieb Beispiel GmbH", Iban.Parse(SampleIban), "COBADEFFXXX"),
        Terms: "Zahlbar innerhalb von 14 Tagen ohne Abzug.",
        Reference: "RE-2026-0001");

    /// <summary>Alle Faelle des Katalogs.</summary>
    public static IReadOnlyList<InvoiceScenario> All { get; } =
    [
        new("01-dienstleistung-19",
            "Einfache Dienstleistungsrechnung mit 19 Prozent Umsatzsteuer",
            Build("RE-2026-0001",
            [
                Line(1, "Beratungsleistung Maerz 2026", 10m, UnitCode.Hour, 95.00m, VatCategory.StandardRate, 19m),
            ]),
            ExpectedToBeValid: true),

        new("02-ermaessigt-7",
            "Rechnung mit 7 Prozent Umsatzsteuer",
            Build("RE-2026-0002",
            [
                Line(1, "Fachbuch Rechnungswesen", 3m, UnitCode.Piece, 33.33m, VatCategory.StandardRate, 7m),
            ]),
            ExpectedToBeValid: true),

        new("03-mehrere-saetze",
            "Rechnung mit mehreren Steuersaetzen",
            Build("RE-2026-0003",
            [
                Line(1, "Beratungsleistung", 4m, UnitCode.Hour, 120.00m, VatCategory.StandardRate, 19m),
                Line(2, "Schulungsunterlagen gedruckt", 10m, UnitCode.Piece, 12.50m, VatCategory.StandardRate, 7m),
                Line(3, "Reisekostenpauschale", 1m, UnitCode.Piece, 85.00m, VatCategory.StandardRate, 19m),
            ]),
            ExpectedToBeValid: true),

        new("04-steuerfrei",
            "Steuerfreie Position mit zulaessiger Begruendung",
            Build("RE-2026-0004",
            [
                Line(1, "Beratungsleistung", 2m, UnitCode.Hour, 150.00m, VatCategory.StandardRate, 19m),
                Line(2, "Vermittlungsleistung", 1m, UnitCode.Piece, 500.00m, VatCategory.Exempt, 0m),
            ],
            exemptionReasons:
            [
                new VatExemptionReason(
                    VatCategory.Exempt,
                    "Steuerfrei nach Paragraf 4 Nummer 8 Buchstabe a UStG",
                    "VATEX-EU-132-1A"),
            ]),
            ExpectedToBeValid: true),

        new("05-rabatt",
            "Rechnung mit Positionsrabatt und Nachlass auf Dokumentebene",
            Build("RE-2026-0005",
            [
                Line(1, "Wartungspauschale", 12m, UnitCode.Piece, 100.00m, VatCategory.StandardRate, 19m,
                    allowance: 120.00m, allowanceReason: "Mengenrabatt 10 Prozent"),
            ],
            allowancesAndCharges:
            [
                new DocumentAllowanceCharge(
                    IsCharge: false, Amount: 54.00m, Reason: "Treuerabatt",
                    VatCategory: VatCategory.StandardRate, VatRate: 19m),
                new DocumentAllowanceCharge(
                    IsCharge: true, Amount: 9.90m, Reason: "Versandkostenpauschale",
                    VatCategory: VatCategory.StandardRate, VatRate: 19m),
            ]),
            ExpectedToBeValid: true),

        new("06-rundung",
            "Rundungsfall mit Anzahlung und Rundungsbetrag",
            Build("RE-2026-0006",
            [
                Line(1, "Kleinteil A", 3m, UnitCode.Piece, 0.335m, VatCategory.StandardRate, 19m),
                Line(2, "Kleinteil B", 7m, UnitCode.Piece, 1.115m, VatCategory.StandardRate, 19m),
                Line(3, "Kleinteil C", 11m, UnitCode.Piece, 2.225m, VatCategory.StandardRate, 7m),
            ],
            paidAmount: 10.00m,
            roundingAmount: 0.01m),
            ExpectedToBeValid: true),

        new("07-reverse-charge",
            "Steuerschuldnerschaft des Leistungsempfaengers",
            Build("RE-2026-0007",
            [
                Line(1, "Softwareentwicklung", 20m, UnitCode.Hour, 110.00m, VatCategory.ReverseCharge, 0m),
            ],
            exemptionReasons:
            [
                new VatExemptionReason(
                    VatCategory.ReverseCharge,
                    "Steuerschuldnerschaft des Leistungsempfaengers",
                    "VATEX-EU-AE"),
            ],
            buyerOverride: Buyer with { VatId = "ATU12345678", Address = Buyer.Address with { Country = CountryCode.Parse("AT") } }),
            ExpectedToBeValid: true),

        new("08-preisbasismenge",
            "Position mit Preisbasismenge und Leistungszeitraum",
            Build("RE-2026-0008",
            [
                Line(1, "Etiketten", 2500m, UnitCode.Piece, 8.90m, VatCategory.StandardRate, 19m,
                    priceBaseQuantity: 100m,
                    servicePeriodStart: new DateOnly(2026, 2, 1),
                    servicePeriodEnd: new DateOnly(2026, 2, 28)),
            ],
            billingPeriodStart: new DateOnly(2026, 2, 1),
            billingPeriodEnd: new DateOnly(2026, 2, 28)),
            ExpectedToBeValid: true),
    ];

    /// <summary>Sucht einen Fall anhand seines Kurznamens.</summary>
    public static InvoiceScenario ByKey(string key)
        => All.FirstOrDefault(s => s.Key == key)
           ?? throw new KeyNotFoundException($"Kein Testfall mit dem Namen '{key}'.");

    private static InvoiceLine Line(
        int number,
        string name,
        decimal quantity,
        UnitCode unit,
        decimal netUnitPrice,
        VatCategory category,
        decimal vatRate,
        decimal allowance = 0m,
        string? allowanceReason = null,
        decimal charge = 0m,
        string? chargeReason = null,
        decimal priceBaseQuantity = 1m,
        DateOnly? servicePeriodStart = null,
        DateOnly? servicePeriodEnd = null)
        => new(
            Number: number,
            Name: name,
            Quantity: quantity,
            Unit: unit,
            NetUnitPrice: netUnitPrice,
            VatCategory: category,
            VatRate: vatRate,
            PriceBaseQuantity: priceBaseQuantity,
            AllowanceAmount: allowance,
            AllowanceReason: allowanceReason,
            ChargeAmount: charge,
            ChargeReason: chargeReason,
            ServicePeriodStart: servicePeriodStart,
            ServicePeriodEnd: servicePeriodEnd);

    private static Invoice Build(
        string invoiceNumber,
        IReadOnlyList<InvoiceLine> lines,
        IReadOnlyList<DocumentAllowanceCharge>? allowancesAndCharges = null,
        IReadOnlyList<VatExemptionReason>? exemptionReasons = null,
        decimal paidAmount = 0m,
        decimal roundingAmount = 0m,
        DateOnly? billingPeriodStart = null,
        DateOnly? billingPeriodEnd = null,
        BuyerParty? buyerOverride = null)
        => new()
        {
            InvoiceNumber = invoiceNumber,
            IssueDate = IssueDate,
            DueDate = DueDate,
            DeliveryDate = DeliveryDate,
            TypeCode = InvoiceTypeCode.CommercialInvoice,
            Currency = CurrencyCode.Euro,
            Seller = Seller,
            Buyer = buyerOverride ?? Buyer,
            Lines = lines,
            AllowancesAndCharges = allowancesAndCharges ?? [],
            ExemptionReasons = exemptionReasons ?? [],
            Payment = Payment with { Reference = invoiceNumber },
            PaidAmount = paidAmount,
            RoundingAmount = roundingAmount,
            BillingPeriodStart = billingPeriodStart,
            BillingPeriodEnd = billingPeriodEnd,
            BuyerReference = "BR-2026-4711",
            OrderReference = "BE-2026-0815",
            Note = "Vielen Dank fuer Ihren Auftrag.",
        };
}

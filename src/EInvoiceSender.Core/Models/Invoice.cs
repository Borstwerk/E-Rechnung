using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Models;

/// <summary>
/// Eine Rechnungsposition (BG-25).
/// Die Positionssumme wird nie eingegeben, sondern immer berechnet –
/// siehe <see cref="Calculation.InvoiceCalculator"/>.
/// </summary>
/// <param name="Number">Positionsnummer (BT-126). Muss innerhalb der Rechnung eindeutig sein.</param>
/// <param name="Name">Kurzbezeichnung der Leistung (BT-153).</param>
/// <param name="Description">Ausführliche Beschreibung (BT-154), optional.</param>
/// <param name="Quantity">Menge (BT-129). Bei Gutschriften ebenfalls positiv anzugeben.</param>
/// <param name="Unit">Mengeneinheit (BT-130).</param>
/// <param name="NetUnitPrice">Netto-Einzelpreis (BT-146).</param>
/// <param name="PriceBaseQuantity">
/// Preisbasismenge (BT-149). Gibt an, auf welche Menge sich der Einzelpreis
/// bezieht; Vorgabe 1. Ein Wert von 0 ist unzulässig und wird geprüft.
/// </param>
/// <param name="AllowanceAmount">Positionsrabatt (BT-136).</param>
/// <param name="AllowanceReason">Grund des Positionsrabatts (BT-139).</param>
/// <param name="ChargeAmount">Positionszuschlag (BT-141).</param>
/// <param name="ChargeReason">Grund des Positionszuschlags (BT-144).</param>
/// <param name="VatCategory">Steuerkategorie (BT-151).</param>
/// <param name="VatRate">Steuersatz in Prozent (BT-152).</param>
/// <param name="ServicePeriodStart">Beginn des Leistungszeitraums der Position (BT-134).</param>
/// <param name="ServicePeriodEnd">Ende des Leistungszeitraums der Position (BT-135).</param>
public sealed record InvoiceLine(
    int Number,
    string Name,
    decimal Quantity,
    UnitCode Unit,
    decimal NetUnitPrice,
    VatCategory VatCategory,
    decimal VatRate,
    string? Description = null,
    decimal PriceBaseQuantity = 1m,
    decimal AllowanceAmount = 0m,
    string? AllowanceReason = null,
    decimal ChargeAmount = 0m,
    string? ChargeReason = null,
    DateOnly? ServicePeriodStart = null,
    DateOnly? ServicePeriodEnd = null);

/// <summary>
/// Nachlass oder Zuschlag auf Dokumentebene (BG-20 / BG-21).
/// </summary>
/// <param name="IsCharge">true = Zuschlag (BG-21), false = Nachlass (BG-20).</param>
/// <param name="Amount">Betrag (BT-92 beziehungsweise BT-99), immer positiv angegeben.</param>
/// <param name="Reason">Grund im Klartext (BT-97 / BT-104).</param>
/// <param name="VatCategory">Steuerkategorie des Nachlasses/Zuschlags (BT-95 / BT-102).</param>
/// <param name="VatRate">Steuersatz (BT-96 / BT-103).</param>
/// <param name="ReasonCode">Codierter Grund (BT-98 / BT-105), optional.</param>
public sealed record DocumentAllowanceCharge(
    bool IsCharge,
    decimal Amount,
    string Reason,
    VatCategory VatCategory,
    decimal VatRate,
    string? ReasonCode = null);

/// <summary>Zahlungsangaben (BG-16).</summary>
/// <param name="MeansCode">Zahlungsart (BT-81).</param>
/// <param name="BankAccount">Bankverbindung des Verkäufers (BG-17), bei Überweisung erforderlich.</param>
/// <param name="Terms">Zahlungsbedingungen im Klartext (BT-20).</param>
/// <param name="Reference">Verwendungszweck beziehungsweise Zahlungsreferenz (BT-83).</param>
public sealed record PaymentDetails(
    PaymentMeansCode MeansCode,
    BankAccount? BankAccount = null,
    string? Terms = null,
    string? Reference = null);

/// <summary>
/// Angabe zur Steuerbefreiung je Kategorie. EN 16931 verlangt bei allen
/// Kategorien außer 'S' und 'Z' eine Begründung (BT-120).
/// </summary>
/// <param name="Category">Betroffene Steuerkategorie.</param>
/// <param name="Reason">Begründung im Klartext (BT-120).</param>
/// <param name="ReasonCode">Codierte Begründung (BT-121), optional.</param>
public sealed record VatExemptionReason(
    VatCategory Category,
    string Reason,
    string? ReasonCode = null);

/// <summary>
/// Die vollständige, bereits typgeprüfte Rechnung. Aggregatwurzel der Domain.
///
/// Dieses Objekt entsteht erst, wenn alle Einzelwerte erfolgreich gelesen werden
/// konnten. Fehleingaben werden vorher im Entwurfsmodell der Application-Schicht
/// abgefangen und als Befund gemeldet, nicht als Ausnahme.
/// </summary>
public sealed record Invoice
{
    /// <summary>Rechnungsnummer (BT-1).</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>Rechnungsdatum (BT-2).</summary>
    public required DateOnly IssueDate { get; init; }

    /// <summary>Rechnungsart (BT-3).</summary>
    public InvoiceTypeCode TypeCode { get; init; } = InvoiceTypeCode.CommercialInvoice;

    /// <summary>Währung (BT-5).</summary>
    public CurrencyCode Currency { get; init; } = CurrencyCode.Euro;

    /// <summary>Verkäufer (BG-4).</summary>
    public required SellerParty Seller { get; init; }

    /// <summary>Käufer (BG-7).</summary>
    public required BuyerParty Buyer { get; init; }

    /// <summary>Rechnungspositionen (BG-25). Mindestens eine ist erforderlich.</summary>
    public required IReadOnlyList<InvoiceLine> Lines { get; init; }

    /// <summary>Nachlässe und Zuschläge auf Dokumentebene (BG-20 / BG-21).</summary>
    public IReadOnlyList<DocumentAllowanceCharge> AllowancesAndCharges { get; init; } = [];

    /// <summary>Begründungen der Steuerbefreiung je Kategorie (BT-120).</summary>
    public IReadOnlyList<VatExemptionReason> ExemptionReasons { get; init; } = [];

    /// <summary>Zahlungsangaben (BG-16).</summary>
    public PaymentDetails? Payment { get; init; }

    /// <summary>Fälligkeitsdatum (BT-9).</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>Leistungsdatum (BT-72), falls kein Zeitraum angegeben ist.</summary>
    public DateOnly? DeliveryDate { get; init; }

    /// <summary>Beginn des Abrechnungszeitraums (BT-73).</summary>
    public DateOnly? BillingPeriodStart { get; init; }

    /// <summary>Ende des Abrechnungszeitraums (BT-74).</summary>
    public DateOnly? BillingPeriodEnd { get; init; }

    /// <summary>Bestellreferenz des Käufers (BT-13).</summary>
    public string? OrderReference { get; init; }

    /// <summary>Kundenreferenz beziehungsweise Leitweg-ID (BT-10).</summary>
    public string? BuyerReference { get; init; }

    /// <summary>Vertragsreferenz (BT-12).</summary>
    public string? ContractReference { get; init; }

    /// <summary>Freitextbemerkung (BT-22).</summary>
    public string? Note { get; init; }

    /// <summary>Bereits gezahlter Betrag (BT-113), etwa eine Anzahlung.</summary>
    public decimal PaidAmount { get; init; }

    /// <summary>
    /// Rundungsbetrag (BT-114). Bewusst manuell, weil er eine fachliche
    /// Entscheidung des Rechnungsstellers ist und nicht abgeleitet werden darf.
    /// </summary>
    public decimal RoundingAmount { get; init; }
}

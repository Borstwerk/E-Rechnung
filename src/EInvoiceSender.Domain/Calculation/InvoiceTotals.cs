using EInvoiceSender.Domain.Values;

namespace EInvoiceSender.Domain.Calculation;

/// <summary>
/// Steueraufschluesselung fuer genau eine Kombination aus Steuerkategorie und
/// Steuersatz (BG-23).
/// </summary>
/// <param name="Category">Steuerkategorie (BT-118).</param>
/// <param name="Rate">Steuersatz in Prozent (BT-119).</param>
/// <param name="TaxableAmount">Steuerbasis (BT-116).</param>
/// <param name="TaxAmount">Steuerbetrag (BT-117).</param>
public sealed record VatBreakdownEntry(
    VatCategory Category,
    decimal Rate,
    decimal TaxableAmount,
    decimal TaxAmount);

/// <summary>
/// Das berechnete Summenbild einer Rechnung (BG-22).
/// Alle Werte sind abgeleitet – kein Feld stammt aus einer Benutzereingabe
/// ausser <see cref="PaidAmount"/> und <see cref="RoundingAmount"/>.
/// </summary>
/// <param name="LineTotal">Summe der Positionsnettobetraege (BT-106).</param>
/// <param name="AllowanceTotal">Summe der Nachlaesse auf Dokumentebene (BT-107).</param>
/// <param name="ChargeTotal">Summe der Zuschlaege auf Dokumentebene (BT-108).</param>
/// <param name="TaxBasisTotal">Nettosumme, also Steuerbasis gesamt (BT-109).</param>
/// <param name="TaxTotal">Gesamtbetrag der Umsatzsteuer (BT-110).</param>
/// <param name="GrandTotal">Bruttosumme (BT-112).</param>
/// <param name="PaidAmount">Bereits gezahlter Betrag (BT-113).</param>
/// <param name="RoundingAmount">Rundungsbetrag (BT-114).</param>
/// <param name="DuePayableAmount">Offener Zahlbetrag (BT-115).</param>
/// <param name="LineNetAmounts">
/// Positionsnettobetraege (BT-131) in der Reihenfolge der Positionen –
/// wird sowohl von der Oberflaeche als auch vom XML-Writer benoetigt.
/// </param>
/// <param name="VatBreakdown">Steueraufschluesselung je Kategorie und Satz (BG-23).</param>
public sealed record InvoiceTotals(
    decimal LineTotal,
    decimal AllowanceTotal,
    decimal ChargeTotal,
    decimal TaxBasisTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal RoundingAmount,
    decimal DuePayableAmount,
    IReadOnlyList<decimal> LineNetAmounts,
    IReadOnlyList<VatBreakdownEntry> VatBreakdown);

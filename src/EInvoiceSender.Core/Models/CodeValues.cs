namespace EInvoiceSender.Core.Models;

/// <summary>
/// Rechnungsart nach UNTDID 1001 (BT-3). Bewusst auf die Faelle beschraenkt,
/// die diese Anwendung fachlich beherrscht – siehe docs/STANDARDS.md, Abschnitt 5.
/// </summary>
public enum InvoiceTypeCode
{
    /// <summary>380 – Handelsrechnung. Regelfall.</summary>
    CommercialInvoice = 380,

    /// <summary>381 – Gutschrift (Storno beziehungsweise Korrektur zugunsten des Kaeufers).</summary>
    CreditNote = 381,

    /// <summary>384 – Rechnungskorrektur.</summary>
    CorrectedInvoice = 384,

    /// <summary>389 – Selbstfakturierte Rechnung (Gutschriftverfahren).</summary>
    SelfBilledInvoice = 389,
}

/// <summary>
/// Umsatzsteuerkategorie nach UNTDID 5305 (BT-118 / BT-151).
/// </summary>
public enum VatCategory
{
    /// <summary>S – Regelbesteuerung mit Steuersatz groesser null.</summary>
    StandardRate,

    /// <summary>Z – nullsatzbesteuert (Steuersatz 0 %).</summary>
    ZeroRated,

    /// <summary>E – von der Steuer befreit (z. B. Kleinunternehmer nach § 19 UStG).</summary>
    Exempt,

    /// <summary>AE – Steuerschuldnerschaft des Leistungsempfaengers (Reverse Charge).</summary>
    ReverseCharge,

    /// <summary>K – innergemeinschaftliche Lieferung.</summary>
    IntraCommunitySupply,

    /// <summary>G – Ausfuhrlieferung, steuerfrei.</summary>
    ExportOutsideEu,

    /// <summary>O – nicht im Anwendungsbereich der Umsatzsteuer.</summary>
    OutsideScope,
}

/// <summary>
/// Zahlungsart nach UNTDID 4461 (BT-81).
/// </summary>
public enum PaymentMeansCode
{
    /// <summary>1 – nicht naeher bestimmt.</summary>
    Unspecified = 1,

    /// <summary>10 – Barzahlung.</summary>
    Cash = 10,

    /// <summary>20 – Scheck.</summary>
    Cheque = 20,

    /// <summary>30 – Ueberweisung.</summary>
    CreditTransfer = 30,

    /// <summary>42 – Zahlung auf Bankkonto.</summary>
    PaymentToBankAccount = 42,

    /// <summary>48 – Kartenzahlung.</summary>
    BankCard = 48,

    /// <summary>49 – Lastschrift.</summary>
    DirectDebit = 49,

    /// <summary>57 – Dauerauftrag.</summary>
    StandingAgreement = 57,

    /// <summary>58 – SEPA-Ueberweisung.</summary>
    SepaCreditTransfer = 58,

    /// <summary>59 – SEPA-Lastschrift.</summary>
    SepaDirectDebit = 59,

    /// <summary>68 – Onlinezahlungsdienst.</summary>
    OnlinePaymentService = 68,

    /// <summary>97 – Verrechnung.</summary>
    ClearingBetweenPartners = 97,
}

/// <summary>Schweregrad eines Pruefbefundes.</summary>
public enum FindingSeverity
{
    /// <summary>Hinweis – kein Hindernis fuer die Erzeugung.</summary>
    Information,

    /// <summary>Warnung – Erzeugung moeglich, sollte aber geprueft werden.</summary>
    Warning,

    /// <summary>Fehler – die Erzeugung wird abgebrochen.</summary>
    Error,
}

/// <summary>
/// Hilfsfunktionen fuer die Umsetzung der Aufzaehlungen in Codewerte der Norm.
/// </summary>
public static class CodeValues
{
    /// <summary>Liefert den UNTDID-5305-Code einer Steuerkategorie.</summary>
    public static string ToCode(this VatCategory category) => category switch
    {
        VatCategory.StandardRate => "S",
        VatCategory.ZeroRated => "Z",
        VatCategory.Exempt => "E",
        VatCategory.ReverseCharge => "AE",
        VatCategory.IntraCommunitySupply => "K",
        VatCategory.ExportOutsideEu => "G",
        VatCategory.OutsideScope => "O",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>Liest eine Steuerkategorie aus ihrem UNTDID-5305-Code.</summary>
    public static bool TryParseVatCategory(string? code, out VatCategory category)
    {
        switch (code?.Trim().ToUpperInvariant())
        {
            case "S": category = VatCategory.StandardRate; return true;
            case "Z": category = VatCategory.ZeroRated; return true;
            case "E": category = VatCategory.Exempt; return true;
            case "AE": category = VatCategory.ReverseCharge; return true;
            case "K": category = VatCategory.IntraCommunitySupply; return true;
            case "G": category = VatCategory.ExportOutsideEu; return true;
            case "O": category = VatCategory.OutsideScope; return true;
            default: category = default; return false;
        }
    }

    /// <summary>
    /// Gibt an, ob die Kategorie zwingend einen Steuersatz groesser null verlangt.
    /// </summary>
    public static bool RequiresPositiveRate(this VatCategory category)
        => category == VatCategory.StandardRate;

    /// <summary>
    /// Gibt an, ob die Kategorie eine Begruendung der Steuerbefreiung (BT-120)
    /// benoetigt. Fuer 'O' verlangt die Norm ebenfalls eine Angabe, weil der
    /// Umsatz ausserhalb des Anwendungsbereichs liegt.
    /// </summary>
    public static bool RequiresExemptionReason(this VatCategory category) => category
        is VatCategory.Exempt
        or VatCategory.ReverseCharge
        or VatCategory.IntraCommunitySupply
        or VatCategory.ExportOutsideEu
        or VatCategory.OutsideScope;

    /// <summary>
    /// Gibt an, ob es sich um eine Gutschrift handelt. Bei Gutschriften sind
    /// negative Gesamtbetraege fachlich zulaessig.
    /// </summary>
    public static bool IsCreditNote(this InvoiceTypeCode type)
        => type == InvoiceTypeCode.CreditNote;
}

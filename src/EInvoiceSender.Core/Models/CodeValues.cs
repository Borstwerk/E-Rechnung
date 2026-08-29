namespace EInvoiceSender.Core.Models;

/// <summary>
/// Rechnungsart nach UNTDID 1001 (BT-3). Bewusst auf die Fälle beschränkt,
/// die diese Anwendung fachlich beherrscht – siehe docs/E-INVOICE-STANDARD.md, Abschnitt 5.
/// </summary>
public enum InvoiceTypeCode
{
    /// <summary>380 – Handelsrechnung. Regelfall.</summary>
    CommercialInvoice = 380,

    /// <summary>381 – Gutschrift (Storno beziehungsweise Korrektur zugunsten des Käufers).</summary>
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
    /// <summary>S – Regelbesteuerung mit Steuersatz größer null.</summary>
    StandardRate,

    /// <summary>Z – nullsatzbesteuert (Steuersatz 0 %).</summary>
    ZeroRated,

    /// <summary>E – von der Steuer befreit (z. B. Kleinunternehmer nach § 19 UStG).</summary>
    Exempt,

    /// <summary>AE – Steuerschuldnerschaft des Leistungsempfängers (Reverse Charge).</summary>
    ReverseCharge,

    /// <summary>K – innergemeinschaftliche Lieferung.</summary>
    IntraCommunitySupply,

    /// <summary>G – Ausfuhrlieferung, steuerfrei.</summary>
    ExportOutsideEu,

    /// <summary>O – nicht im Anwendungsbereich der Umsatzsteuer.</summary>
    OutsideScope,

    // Der EN-16931-Codebestand kennt darüber hinaus L (Kanarische Inseln,
    // IGIC) und M (Ceuta und Melilla, IPSI). Beide fehlen hier bewusst: Diese
    // Anwendung bietet sie nicht zur Erstellung an, weil ihre steuerliche
    // Behandlung hier niemand geprüft hat.
    //
    // Das ist eine Grenze dieses Programms, **keine** Aussage über die Norm.
    // Eine fremde Rechnung mit L oder M ist normgerecht; sie ließe sich mit
    // BorstWerk nur nicht erzeugen. Für den späteren Prüfmodus
    // (ER-030-CHK-01) muss diese Unterscheidung erhalten bleiben – siehe
    // docs/E-INVOICE-STANDARD.md, Abschnitt zu den Codelisten.
}

/// <summary>
/// Zahlungsart nach UNTDID 4461 (BT-81).
/// </summary>
public enum PaymentMeansCode
{
    /// <summary>1 – nicht näher bestimmt.</summary>
    Unspecified = 1,

    /// <summary>10 – Barzahlung.</summary>
    Cash = 10,

    /// <summary>20 – Scheck.</summary>
    Cheque = 20,

    /// <summary>30 – Überweisung.</summary>
    CreditTransfer = 30,

    /// <summary>42 – Zahlung auf Bankkonto.</summary>
    PaymentToBankAccount = 42,

    /// <summary>48 – Kartenzahlung.</summary>
    BankCard = 48,

    /// <summary>49 – Lastschrift.</summary>
    DirectDebit = 49,

    /// <summary>57 – Dauerauftrag.</summary>
    StandingAgreement = 57,

    /// <summary>58 – SEPA-Überweisung.</summary>
    SepaCreditTransfer = 58,

    /// <summary>59 – SEPA-Lastschrift.</summary>
    SepaDirectDebit = 59,

    /// <summary>68 – Onlinezahlungsdienst.</summary>
    OnlinePaymentService = 68,

    /// <summary>97 – Verrechnung.</summary>
    ClearingBetweenPartners = 97,
}

/// <summary>Schweregrad eines Prüfbefundes.</summary>
public enum FindingSeverity
{
    /// <summary>Hinweis – kein Hindernis für die Erzeugung.</summary>
    Information,

    /// <summary>Warnung – Erzeugung möglich, sollte aber geprüft werden.</summary>
    Warning,

    /// <summary>Fehler – die Erzeugung wird abgebrochen.</summary>
    Error,
}

/// <summary>
/// Hilfsfunktionen für die Umsetzung der Aufzählungen in Codewerte der Norm.
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
    /// Gibt an, ob die Kategorie zwingend einen Steuersatz größer null verlangt.
    /// </summary>
    public static bool RequiresPositiveRate(this VatCategory category)
        => category == VatCategory.StandardRate;

    /// <summary>
    /// Gibt an, ob die Kategorie eine Begründung der Steuerbefreiung (BT-120)
    /// benötigt. Für 'O' verlangt die Norm ebenfalls eine Angabe, weil der
    /// Umsatz außerhalb des Anwendungsbereichs liegt.
    /// </summary>
    public static bool RequiresExemptionReason(this VatCategory category) => category
        is VatCategory.Exempt
        or VatCategory.ReverseCharge
        or VatCategory.IntraCommunitySupply
        or VatCategory.ExportOutsideEu
        or VatCategory.OutsideScope;

    /// <summary>
    /// Gibt an, ob es sich um eine Gutschrift handelt. Bei Gutschriften sind
    /// negative Gesamtbeträge fachlich zulässig.
    /// </summary>
    public static bool IsCreditNote(this InvoiceTypeCode type)
        => type == InvoiceTypeCode.CreditNote;
}

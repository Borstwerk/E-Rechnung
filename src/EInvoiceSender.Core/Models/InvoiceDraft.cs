using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Models;

/// <summary>
/// Das bearbeitbare Rechnungsformular.
///
/// Waehrend der Eingabe ist jedes Feld eine Zeichenkette: Ein halb getipptes
/// Datum oder eine unvollstaendige IBAN darf keine Ausnahme ausloesen und das
/// Formular nicht blockieren. Erst <see cref="TryBuildInvoice"/> wandelt den
/// Entwurf in das strenge Domaenenmodell um und meldet dabei jeden Wert, der
/// sich nicht lesen laesst, als verstaendlichen Befund.
///
/// Damit gilt: Ein <see cref="Invoice"/> entsteht nur aus vollstaendig
/// lesbaren Werten. Die Fachlogik muss nie mit Halbfertigem umgehen.
/// </summary>
public sealed partial class InvoiceDraft : ObservableObject
{
    private readonly FieldOriginTracker _origins = new();

    /// <summary>Woher der Inhalt eines Feldes stammt.</summary>
    public FieldOrigin OriginOf(string propertyName) => _origins.OriginOf(propertyName);

    /// <summary>
    /// Alle vermerkten Herkuenfte. Die Oberflaeche bindet daran, um die
    /// Kennzeichnung neben den Feldern anzuzeigen.
    /// </summary>
    public IReadOnlyDictionary<string, FieldOrigin> Origins => _origins.Origins;

    /// <summary>
    /// Fuehrt eine Vorbefuellung aus. Aenderungen innerhalb von
    /// <paramref name="fill"/> gelten nicht als Benutzereingabe.
    /// </summary>
    public void Prefill(Action<InvoiceDraft> fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        _origins.DuringPrefill(() => fill(this));
    }

    /// <summary>Vermerkt, woher ein Feld stammt.</summary>
    public void MarkOrigin(string propertyName, FieldOrigin origin)
    {
        _origins.Mark(propertyName, origin);
        OnPropertyChanged(nameof(Origins));
    }

    /// <summary>
    /// Jede Aenderung durch den Anwender setzt die Herkunft auf "von Hand".
    /// Damit verschwindet die Kennzeichnung genau dann, wenn sie ihren Zweck
    /// erfuellt hat.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_origins.IsPrefilling || e.PropertyName is null or nameof(Origins))
        {
            return;
        }

        if (_origins.MarkAsManual(e.PropertyName))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Origins)));
        }
    }

    // --- Dokument ----------------------------------------------------------

    [ObservableProperty]
    private string _invoiceNumber = string.Empty;

    [ObservableProperty]
    private DateOnly? _issueDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private DateOnly? _dueDate;

    [ObservableProperty]
    private DateOnly? _deliveryDate;

    [ObservableProperty]
    private DateOnly? _billingPeriodStart;

    [ObservableProperty]
    private DateOnly? _billingPeriodEnd;

    [ObservableProperty]
    private InvoiceTypeCode _typeCode = InvoiceTypeCode.CommercialInvoice;

    [ObservableProperty]
    private string _currency = "EUR";

    [ObservableProperty]
    private string _orderReference = string.Empty;

    [ObservableProperty]
    private string _buyerReference = string.Empty;

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    private string _paidAmount = "0,00";

    [ObservableProperty]
    private string _roundingAmount = "0,00";

    // --- Verkaeufer --------------------------------------------------------

    [ObservableProperty]
    private string _sellerName = string.Empty;

    [ObservableProperty]
    private string _sellerStreet = string.Empty;

    [ObservableProperty]
    private string _sellerPostalCode = string.Empty;

    [ObservableProperty]
    private string _sellerCity = string.Empty;

    /// <summary>
    /// Das eigene Land. Anders als beim Kaeufer ist eine Vorgabe hier
    /// vertretbar: Der Anwender stellt seine eigenen Rechnungen aus und
    /// ueberschreibt den Wert einmalig ueber die Firmenvorlage.
    /// </summary>
    [ObservableProperty]
    private string _sellerCountry = "DE";

    [ObservableProperty]
    private string _sellerEmail = string.Empty;

    [ObservableProperty]
    private string _sellerVatId = string.Empty;

    [ObservableProperty]
    private string _sellerTaxNumber = string.Empty;

    [ObservableProperty]
    private string _sellerContactName = string.Empty;

    [ObservableProperty]
    private string _sellerContactPhone = string.Empty;

    // --- Kaeufer -----------------------------------------------------------

    [ObservableProperty]
    private string _buyerName = string.Empty;

    [ObservableProperty]
    private string _buyerStreet = string.Empty;

    [ObservableProperty]
    private string _buyerPostalCode = string.Empty;

    [ObservableProperty]
    private string _buyerCity = string.Empty;

    /// <summary>
    /// Das Land des Kaeufers.
    ///
    /// Bewusst **ohne** Vorgabe: Ein unbekanntes Land als "DE" auszugeben
    /// waere eine Behauptung, die niemand aufgestellt hat. Bei einem
    /// oesterreichischen oder niederlaendischen Kunden entstuende daraus eine
    /// falsche Rechnung, ohne dass irgendwo eine Warnung erschiene. Bleibt das
    /// Feld leer, beanstandet es die Datenpruefung sichtbar.
    /// </summary>
    [ObservableProperty]
    private string _buyerCountry = string.Empty;

    [ObservableProperty]
    private string _buyerEmail = string.Empty;

    [ObservableProperty]
    private string _buyerVatId = string.Empty;

    // --- Zahlung -----------------------------------------------------------

    [ObservableProperty]
    private PaymentMeansCode _paymentMeansCode = PaymentMeansCode.SepaCreditTransfer;

    [ObservableProperty]
    private string _bankAccountHolder = string.Empty;

    [ObservableProperty]
    private string _bankIban = string.Empty;

    [ObservableProperty]
    private string _bankBic = string.Empty;

    [ObservableProperty]
    private string _paymentTerms = string.Empty;

    // --- Positionen und Steuerbefreiung ------------------------------------

    /// <summary>Die Rechnungspositionen.</summary>
    public ObservableCollection<InvoiceLineDraft> Lines { get; } = [];

    /// <summary>Begruendungen der Steuerbefreiung je Kategorie.</summary>
    public ObservableCollection<ExemptionReasonDraft> ExemptionReasons { get; } = [];

    /// <summary>Nachlaesse und Zuschlaege auf Dokumentebene.</summary>
    public ObservableCollection<AllowanceChargeDraft> AllowancesAndCharges { get; } = [];

    /// <summary>
    /// Fuegt eine neue Position mit fortlaufender Nummer hinzu und liefert sie.
    /// </summary>
    public InvoiceLineDraft AddLine()
    {
        var line = new InvoiceLineDraft
        {
            Number = Lines.Count == 0 ? 1 : Lines.Max(l => l.Number) + 1,
        };

        Lines.Add(line);

        return line;
    }

    /// <summary>
    /// Vergibt die Positionsnummern neu, sodass sie wieder luecklos von eins
    /// aufsteigen. Wird nach dem Loeschen oder Verschieben aufgerufen.
    /// </summary>
    public void RenumberLines()
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            Lines[i].Number = i + 1;
        }
    }

    /// <summary>
    /// Versucht, aus dem Entwurf eine vollstaendige Rechnung zu bauen.
    /// </summary>
    /// <param name="invoice">Die gebaute Rechnung, falls alle Werte lesbar waren.</param>
    /// <returns>
    /// Ein Bericht mit allen nicht lesbaren Werten. Ist er fehlerfrei, ist
    /// <paramref name="invoice"/> gesetzt.
    /// </returns>
    public ValidationReport TryBuildInvoice(out Invoice? invoice)
    {
        var report = new ValidationReportBuilder();
        invoice = null;

        if (IssueDate is not { } issueDate)
        {
            report.Error(
                "APP-EDT-001", "Das Rechnungsdatum fehlt oder ist unlesbar.", "IssueDate");
        }
        else
        {
            issueDate = IssueDate.Value;
        }

        CurrencyCode currency = ParseCurrency(report);
        SellerParty? seller = BuildSeller(report);
        BuyerParty? buyer = BuildBuyer(report);
        List<InvoiceLine> lines = BuildLines(report);
        PaymentDetails? payment = BuildPayment(report);

        decimal paid = ParseAmount(PaidAmount, "APP-EDT-010", "Bereits gezahlter Betrag", "PaidAmount", report);
        decimal rounding = ParseAmount(
            RoundingAmount, "APP-EDT-011", "Rundungsbetrag", "RoundingAmount", report);

        ValidationReport result = report.Build();

        if (result.HasErrors || seller is null || buyer is null || IssueDate is null)
        {
            return result;
        }

        invoice = new Invoice
        {
            InvoiceNumber = InvoiceNumber.Trim(),
            IssueDate = IssueDate.Value,
            DueDate = DueDate,
            DeliveryDate = DeliveryDate,
            BillingPeriodStart = BillingPeriodStart,
            BillingPeriodEnd = BillingPeriodEnd,
            TypeCode = TypeCode,
            Currency = currency,
            Seller = seller,
            Buyer = buyer,
            Lines = lines,
            AllowancesAndCharges = BuildAllowancesAndCharges(),
            ExemptionReasons = [.. ExemptionReasons
                .Where(r => !string.IsNullOrWhiteSpace(r.Reason))
                .Select(r => new VatExemptionReason(
                    r.Category, r.Reason.Trim(),
                    string.IsNullOrWhiteSpace(r.ReasonCode) ? null : r.ReasonCode.Trim()))],
            Payment = payment,
            PaidAmount = paid,
            RoundingAmount = rounding,
            OrderReference = Blank(OrderReference),
            BuyerReference = Blank(BuyerReference),
            Note = Blank(Note),
        };

        return result;
    }

    private CurrencyCode ParseCurrency(ValidationReportBuilder report)
    {
        if (CurrencyCode.TryParse(Currency, out CurrencyCode code))
        {
            return code;
        }

        report.Error(
            "APP-EDT-002",
            "Die Waehrung muss aus genau drei Buchstaben bestehen, zum Beispiel EUR.",
            "Currency");

        return CurrencyCode.Euro;
    }

    private SellerParty? BuildSeller(ValidationReportBuilder report)
    {
        if (!CountryCode.TryParse(SellerCountry, out CountryCode country))
        {
            report.Error(
                "APP-EDT-003",
                "Das Land des Rechnungsstellers muss aus genau zwei Buchstaben bestehen, "
                + "zum Beispiel DE.",
                "Seller.Country");

            return null;
        }

        return new SellerParty(
            Name: SellerName.Trim(),
            Address: new PostalAddress(
                Blank(SellerStreet), null, Blank(SellerPostalCode), Blank(SellerCity), country),
            Email: Blank(SellerEmail),
            VatId: Blank(SellerVatId),
            TaxNumber: Blank(SellerTaxNumber),
            ContactName: Blank(SellerContactName),
            ContactPhone: Blank(SellerContactPhone));
    }

    private BuyerParty? BuildBuyer(ValidationReportBuilder report)
    {
        if (!CountryCode.TryParse(BuyerCountry, out CountryCode country))
        {
            // Der Leerfall ist seit dem Wegfall der stillen DE-Vorbelegung der
            // Regelfall bei einem neuen Formular und braucht deshalb einen
            // eigenen, verstaendlichen Satz.
            report.Error(
                "APP-EDT-004",
                string.IsNullOrWhiteSpace(BuyerCountry)
                    ? "Das Land des Rechnungsempfaengers fehlt. Bitte waehlen Sie es aus; "
                      + "es wird nicht angenommen."
                    : "Das Land des Rechnungsempfaengers muss aus genau zwei Buchstaben bestehen, "
                      + "zum Beispiel DE.",
                "Buyer.Country");

            return null;
        }

        return new BuyerParty(
            Name: BuyerName.Trim(),
            Address: new PostalAddress(
                Blank(BuyerStreet), null, Blank(BuyerPostalCode), Blank(BuyerCity), country),
            Email: Blank(BuyerEmail),
            VatId: Blank(BuyerVatId));
    }

    private List<InvoiceLine> BuildLines(ValidationReportBuilder report)
    {
        var lines = new List<InvoiceLine>(Lines.Count);

        for (int index = 0; index < Lines.Count; index++)
        {
            InvoiceLineDraft draft = Lines[index];
            string field = $"Lines[{index}]";
            string label = $"Position {draft.Number}";

            if (!UnitCode.TryParse(draft.Unit, out UnitCode unit))
            {
                report.Error(
                    "APP-EDT-020",
                    $"Die Mengeneinheit in {label} ist unlesbar.",
                    $"{field}.Unit");

                continue;
            }

            decimal quantity = ParseAmount(
                draft.Quantity, "APP-EDT-021", $"Die Menge in {label}", $"{field}.Quantity",
                report, decimals: 4);
            decimal price = ParseAmount(
                draft.NetUnitPrice, "APP-EDT-022", $"Der Einzelpreis in {label}",
                $"{field}.NetUnitPrice", report, decimals: 4);
            decimal allowance = ParseAmount(
                draft.AllowanceAmount, "APP-EDT-023", $"Der Rabatt in {label}",
                $"{field}.AllowanceAmount", report);
            decimal vatRate = ParseAmount(
                draft.VatRate, "APP-EDT-024", $"Der Steuersatz in {label}",
                $"{field}.VatRate", report, decimals: 4);
            decimal baseQuantity = ParseAmount(
                draft.PriceBaseQuantity, "APP-EDT-025", $"Die Preisbasismenge in {label}",
                $"{field}.PriceBaseQuantity", report, decimals: 4);

            lines.Add(new InvoiceLine(
                Number: draft.Number,
                Name: draft.Name.Trim(),
                Quantity: quantity,
                Unit: unit,
                NetUnitPrice: price,
                VatCategory: draft.VatCategory,
                VatRate: vatRate,
                Description: Blank(draft.Description),
                PriceBaseQuantity: baseQuantity == 0m ? 1m : baseQuantity,
                AllowanceAmount: allowance,
                AllowanceReason: Blank(draft.AllowanceReason)));
        }

        return lines;
    }

    private List<DocumentAllowanceCharge> BuildAllowancesAndCharges()
        => [.. AllowancesAndCharges
            .Where(a => !string.IsNullOrWhiteSpace(a.Amount))
            .Select(a => new DocumentAllowanceCharge(
                IsCharge: a.IsCharge,
                Amount: TryParseDecimal(a.Amount, out decimal amount) ? amount : 0m,
                Reason: a.Reason.Trim(),
                VatCategory: a.VatCategory,
                VatRate: TryParseDecimal(a.VatRate, out decimal rate) ? rate : 0m))];

    private PaymentDetails? BuildPayment(ValidationReportBuilder report)
    {
        BankAccount? account = null;

        if (!string.IsNullOrWhiteSpace(BankIban))
        {
            if (!Iban.TryParse(BankIban, out Iban iban))
            {
                report.Error(
                    "APP-EDT-030",
                    "Die IBAN ist nicht gueltig. Bitte pruefen Sie die Eingabe – "
                    + "vermutlich ist ein Zeichen vertauscht.",
                    "Payment.Iban",
                    $"Geprueft wurde die Pruefziffer nach ISO 7064: {Iban.Mask(BankIban)}");
            }
            else
            {
                account = new BankAccount(
                    BankAccountHolder.Trim(), iban, Blank(BankBic));
            }
        }

        if (account is null && string.IsNullOrWhiteSpace(PaymentTerms))
        {
            return null;
        }

        return new PaymentDetails(
            MeansCode: PaymentMeansCode,
            BankAccount: account,
            Terms: Blank(PaymentTerms));
    }

    /// <summary>
    /// Liest einen Betrag. Akzeptiert Komma und Punkt als Dezimaltrennzeichen,
    /// weil Anwender beides eingeben. Ein leeres Feld gilt als null.
    /// </summary>
    private static decimal ParseAmount(
        string value,
        string ruleId,
        string label,
        string field,
        ValidationReportBuilder report,
        int decimals = 2)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        if (!TryParseDecimal(value, out decimal parsed))
        {
            report.Error(
                ruleId,
                $"{label} ist keine gueltige Zahl.",
                field,
                $"Gelesen: '{value}'");

            return 0m;
        }

        return decimal.Round(parsed, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Liest eine Dezimalzahl in deutscher oder invarianter Schreibweise.
    /// Tausenderpunkte werden nicht unterstuetzt, weil sie sich nicht
    /// eindeutig von Dezimalpunkten unterscheiden lassen.
    /// </summary>
    public static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0m;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().Replace(",", ".", StringComparison.Ordinal);

        return decimal.TryParse(
            normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out result);
    }

    private static string? Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Eine bearbeitbare Rechnungsposition.</summary>
public sealed partial class InvoiceLineDraft : ObservableObject
{
    [ObservableProperty]
    private int _number = 1;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _quantity = "1";

    [ObservableProperty]
    private string _unit = "C62";

    [ObservableProperty]
    private string _netUnitPrice = "0,00";

    [ObservableProperty]
    private string _priceBaseQuantity = "1";

    [ObservableProperty]
    private string _allowanceAmount = "0,00";

    [ObservableProperty]
    private string _allowanceReason = string.Empty;

    [ObservableProperty]
    private VatCategory _vatCategory = VatCategory.StandardRate;

    [ObservableProperty]
    private string _vatRate = "19";
}

/// <summary>Eine bearbeitbare Begruendung der Steuerbefreiung.</summary>
public sealed partial class ExemptionReasonDraft : ObservableObject
{
    [ObservableProperty]
    private VatCategory _category = VatCategory.Exempt;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _reasonCode = string.Empty;
}

/// <summary>Ein bearbeitbarer Nachlass oder Zuschlag auf Dokumentebene.</summary>
public sealed partial class AllowanceChargeDraft : ObservableObject
{
    [ObservableProperty]
    private bool _isCharge;

    [ObservableProperty]
    private string _amount = "0,00";

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private VatCategory _vatCategory = VatCategory.StandardRate;

    [ObservableProperty]
    private string _vatRate = "19";
}

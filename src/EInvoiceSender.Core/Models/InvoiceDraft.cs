using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Models;

/// <summary>
/// Das bearbeitbare Rechnungsformular.
///
/// Während der Eingabe ist jedes Feld eine Zeichenkette: Ein halb getipptes
/// Datum oder eine unvollständige IBAN darf keine Ausnahme auslösen und das
/// Formular nicht blockieren. Erst <see cref="TryBuildInvoice"/> wandelt den
/// Entwurf in das strenge Domänenmodell um und meldet dabei jeden Wert, der
/// sich nicht lesen lässt, als verständlichen Befund.
///
/// Damit gilt: Ein <see cref="Invoice"/> entsteht nur aus vollständig
/// lesbaren Werten. Die Fachlogik muss nie mit Halbfertigem umgehen.
/// </summary>
public sealed partial class InvoiceDraft : ObservableObject
{
    private readonly FieldOriginTracker _origins = new();
    private int _defaultPaymentTermDays;

    /// <summary>Woher der Inhalt eines Feldes stammt.</summary>
    public FieldOrigin OriginOf(string propertyName) => _origins.OriginOf(propertyName);

    /// <summary>
    /// Alle vermerkten Herkünfte. Die Oberfläche bindet daran, um die
    /// Kennzeichnung neben den Feldern anzuzeigen.
    /// </summary>
    public IReadOnlyDictionary<string, FieldOrigin> Origins => _origins.Origins;

    /// <summary>
    /// Führt eine Vorbefüllung aus. Änderungen innerhalb von
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
    /// Jede Änderung durch den Anwender setzt die Herkunft auf "von Hand".
    /// Damit verschwindet die Kennzeichnung genau dann, wenn sie ihren Zweck
    /// erfüllt hat.
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

    /// <summary>
    /// Merkt sich das Standardzahlungsziel ausschließlich für die Ableitung
    /// des Fälligkeitsdatums im aktuellen Entwurf.
    ///
    /// Die Tage sind kein Rechnungsfeld und werden nicht in das strenge
    /// Rechnungsmodell übernommen. Ein erkanntes oder vom Anwender gesetztes
    /// Fälligkeitsdatum bleibt durch seine Herkunft geschützt.
    /// </summary>
    internal bool ConfigureDefaultPaymentTermDays(int days)
    {
        _defaultPaymentTermDays = Math.Max(0, days);

        return SynchronizeTemplateDefaultDueDate();
    }

    /// <summary>
    /// Hält ein automatisch aus der Vorlage abgeleitetes Fälligkeitsdatum am
    /// Rechnungsdatum. Der Hook läuft sowohl bei PDF-Vorbefüllung als auch bei
    /// einer manuellen Datumsänderung.
    /// </summary>
    partial void OnIssueDateChanged(DateOnly? value)
        => SynchronizeTemplateDefaultDueDate();

    private bool SynchronizeTemplateDefaultDueDate()
    {
        FieldOrigin currentOrigin = OriginOf(nameof(DueDate));

        if (!FieldOriginRules.CanReplace(currentOrigin, FieldOrigin.TemplateDefault))
        {
            return false;
        }

        DateOnly? synchronized = CalculateTemplateDefaultDueDate();

        // Ohne Rechnungsdatum oder positives Zahlungsziel gibt es noch gar
        // keinen abgeleiteten Wert. Ein leeres Default-Feld muss deshalb auch
        // nicht sichtbar als Vorlagenwert markiert werden.
        if (DueDate == synchronized
            && (synchronized is null || currentOrigin == FieldOrigin.TemplateDefault))
        {
            return false;
        }

        void Apply()
        {
            DueDate = synchronized;
            MarkOrigin(nameof(DueDate), FieldOrigin.TemplateDefault);
        }

        // CompanyTemplateApplier und DraftPrefiller laufen bereits in einem
        // Prefill. Der Tracker ist bewusst nicht verschachtelungssicher;
        // deshalb wird in diesem Fall kein zweiter Prefill geöffnet.
        if (_origins.IsPrefilling)
        {
            Apply();
        }
        else
        {
            Prefill(_ => Apply());
        }

        return true;
    }

    private DateOnly? CalculateTemplateDefaultDueDate()
    {
        if (_defaultPaymentTermDays <= 0 || IssueDate is not { } issueDate)
        {
            return null;
        }

        long targetDayNumber = (long)issueDate.DayNumber + _defaultPaymentTermDays;

        return targetDayNumber >= DateOnly.MinValue.DayNumber
            && targetDayNumber <= DateOnly.MaxValue.DayNumber
                ? DateOnly.FromDayNumber((int)targetDayNumber)
                : null;
    }

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

    // --- Verkäufer --------------------------------------------------------

    [ObservableProperty]
    private string _sellerName = string.Empty;

    [ObservableProperty]
    private string _sellerStreet = string.Empty;

    [ObservableProperty]
    private string _sellerPostalCode = string.Empty;

    [ObservableProperty]
    private string _sellerCity = string.Empty;

    /// <summary>
    /// Das eigene Land. Anders als beim Käufer ist eine Vorgabe hier
    /// vertretbar: Der Anwender stellt seine eigenen Rechnungen aus und
    /// überschreibt den Wert einmalig über die Firmenvorlage.
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

    // --- Käufer -----------------------------------------------------------

    [ObservableProperty]
    private string _buyerName = string.Empty;

    [ObservableProperty]
    private string _buyerStreet = string.Empty;

    [ObservableProperty]
    private string _buyerPostalCode = string.Empty;

    [ObservableProperty]
    private string _buyerCity = string.Empty;

    /// <summary>
    /// Das Land des Käufers.
    ///
    /// Bewusst **ohne** Vorgabe: Ein unbekanntes Land als "DE" auszugeben
    /// wäre eine Behauptung, die niemand aufgestellt hat. Bei einem
    /// österreichischen oder niederländischen Kunden entstünde daraus eine
    /// falsche Rechnung, ohne dass irgendwo eine Warnung erschiene. Bleibt das
    /// Feld leer, beanstandet es die Datenprüfung sichtbar.
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

    /// <summary>Begründungen der Steuerbefreiung je Kategorie.</summary>
    public ObservableCollection<ExemptionReasonDraft> ExemptionReasons { get; } = [];

    /// <summary>Nachlässe und Zuschläge auf Dokumentebene.</summary>
    public ObservableCollection<AllowanceChargeDraft> AllowancesAndCharges { get; } = [];

    /// <summary>
    /// Fügt eine neue Position mit fortlaufender Nummer hinzu und liefert sie.
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
    /// Setzt das Formular auf den Anfangszustand zurück – wie ein frisch
    /// erzeugter Entwurf.
    ///
    /// **Warum es diese Methode gibt:** „Neue Rechnung“ setzte bisher alle
    /// Schritte zurück, nur dieses Formular nicht. Rechnungsnummer, Käufer,
    /// Datumsangaben und Positionen der vorigen Rechnung standen danach noch
    /// da. Wer das übersah, verschickte die zweite Rechnung mit der Nummer der
    /// ersten.
    ///
    /// Zurückgesetzt wird **alles**, auch die Angaben zur eigenen Firma. Die
    /// dauerhaften Vorgaben des Anwenders trägt anschließend wieder die
    /// gespeicherte Firmenvorlage ein – frisch von der Festplatte, damit eine
    /// zwischenzeitliche Änderung in den Einstellungen auch greift.
    ///
    /// Die Zuweisungen laufen als Vorbefüllung, sonst würde jede einzelne als
    /// Eingabe des Anwenders vermerkt. Danach ist das Herkunftsverzeichnis
    /// leer: Ein Feld, das niemand angefasst hat, trägt einen Programmstandard.
    /// </summary>
    public void Reset()
    {
        _defaultPaymentTermDays = 0;
        Prefill(d => d.RestoreDefaults());

        _origins.Clear();
        OnPropertyChanged(nameof(Origins));
    }

    /// <summary>
    /// Die Anfangswerte, in derselben Reihenfolge wie die Felder oben.
    ///
    /// Bewacht von <c>InvoiceDraftResetTests</c>: Der Test vergleicht einen
    /// zurückgesetzten Entwurf Feld für Feld mit einem frisch erzeugten. Ein
    /// hier vergessenes Feld fällt damit sofort auf.
    /// </summary>
    private void RestoreDefaults()
    {
        InvoiceNumber = string.Empty;
        IssueDate = DateOnly.FromDateTime(DateTime.Today);
        DueDate = null;
        DeliveryDate = null;
        BillingPeriodStart = null;
        BillingPeriodEnd = null;
        TypeCode = InvoiceTypeCode.CommercialInvoice;
        Currency = "EUR";
        OrderReference = string.Empty;
        BuyerReference = string.Empty;
        Note = string.Empty;
        PaidAmount = "0,00";
        RoundingAmount = "0,00";

        SellerName = string.Empty;
        SellerStreet = string.Empty;
        SellerPostalCode = string.Empty;
        SellerCity = string.Empty;
        SellerCountry = "DE";
        SellerEmail = string.Empty;
        SellerVatId = string.Empty;
        SellerTaxNumber = string.Empty;
        SellerContactName = string.Empty;
        SellerContactPhone = string.Empty;

        BuyerName = string.Empty;
        BuyerStreet = string.Empty;
        BuyerPostalCode = string.Empty;
        BuyerCity = string.Empty;
        BuyerCountry = string.Empty;
        BuyerEmail = string.Empty;
        BuyerVatId = string.Empty;

        PaymentMeansCode = PaymentMeansCode.SepaCreditTransfer;
        BankAccountHolder = string.Empty;
        BankIban = string.Empty;
        BankBic = string.Empty;
        PaymentTerms = string.Empty;

        Lines.Clear();
        ExemptionReasons.Clear();
        AllowancesAndCharges.Clear();
    }

    /// <summary>
    /// Vergibt die Positionsnummern neu, sodass sie wieder lücklos von eins
    /// aufsteigen. Wird nach dem Löschen oder Verschieben aufgerufen.
    /// </summary>
    public void RenumberLines()
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            Lines[i].Number = i + 1;
        }
    }

    /// <summary>
    /// Rechnet die Summen aus den Positionen aus – ohne die übrigen Angaben.
    ///
    /// **Warum getrennt von <see cref="TryBuildInvoice"/>:** Die Summen hängen
    /// nur an den Positionen. Die Anzeige lief bisher trotzdem über den Bau
    /// einer vollständigen Rechnung und blieb deshalb leer, solange
    /// Rechnungsnummer, Käufer oder Land fehlten. Der Anwender trug drei
    /// Positionen ein, drückte „Summen neu berechnen“ und sah – nichts.
    ///
    /// Geliefert wird <c>null</c>, sobald eine Positionsangabe fehlt oder
    /// unlesbar ist. Dann gibt es keine belastbare Summe, und eine falsche
    /// wäre schlimmer als keine. Gemeldet wird hier nichts: Das Rechnen sagt
    /// nur, ob es geht; das Beanstanden bleibt Sache der Prüfung.
    /// </summary>
    public InvoiceTotals? TryCalculateTotals()
    {
        var report = new ValidationReportBuilder();

        List<InvoiceLine> lines = BuildLines(report);
        decimal paid = ParseAmount(PaidAmount, "APP-EDT-010", "Bereits gezahlter Betrag", "PaidAmount", report);
        decimal rounding = ParseAmount(
            RoundingAmount, "APP-EDT-011", "Rundungsbetrag", "RoundingAmount", report);

        return report.Build().HasErrors
            ? null
            : InvoiceCalculator.Calculate(lines, BuildAllowancesAndCharges(), paid, rounding);
    }

    /// <summary>
    /// Versucht, aus dem Entwurf eine vollständige Rechnung zu bauen.
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
            "Die Währung muss aus genau drei Buchstaben bestehen, zum Beispiel EUR.",
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
            // eigenen, verständlichen Satz.
            report.Error(
                "APP-EDT-004",
                string.IsNullOrWhiteSpace(BuyerCountry)
                    ? "Das Land des Rechnungsempfängers fehlt. Bitte wählen Sie es aus; "
                      + "es wird nicht angenommen."
                    : "Das Land des Rechnungsempfängers muss aus genau zwei Buchstaben bestehen, "
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

            // Menge, Einzelpreis und Steuersatz sind Pflichtangaben: Ein leeres
            // Feld wird gemeldet und nicht stillschweigend als null gelesen.
            decimal quantity = ParseRequiredAmount(
                draft.Quantity, "APP-EDT-021", $"Die Menge in {label}", $"{field}.Quantity",
                report, decimals: 4);
            decimal price = ParseRequiredAmount(
                draft.NetUnitPrice, "APP-EDT-022", $"Der Einzelpreis in {label}",
                $"{field}.NetUnitPrice", report, decimals: 4);
            decimal vatRate = ParseRequiredAmount(
                draft.VatRate, "APP-EDT-024", $"Der Steuersatz in {label}",
                $"{field}.VatRate", report, decimals: 4);

            // Ein Rabatt ist freiwillig; kein Eintrag bedeutet kein Rabatt.
            decimal allowance = ParseAmount(
                draft.AllowanceAmount, "APP-EDT-023", $"Der Rabatt in {label}",
                $"{field}.AllowanceAmount", report);
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
                    "Die IBAN ist nicht gültig. Bitte prüfen Sie die Eingabe – "
                    + "vermutlich ist ein Zeichen vertauscht.",
                    "Payment.Iban",
                    $"Geprüft wurde die Prüfziffer nach ISO 7064: {Iban.Mask(BankIban)}");
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
    /// Liest eine Pflichtangabe. Ein leeres Feld ist hier keine Null, sondern
    /// eine fehlende Angabe – und wird als solche gemeldet.
    ///
    /// Der Unterschied zählt: Bei Menge, Einzelpreis und Steuersatz ist die
    /// Null ein zulässiger Wert. Würde ein leeres Feld stillschweigend als null
    /// gelesen, entstünde aus einer vergessenen Eingabe eine Position über
    /// null Euro, ohne dass irgendwo etwas aufliefe.
    /// </summary>
    private static decimal ParseRequiredAmount(
        string value,
        string ruleId,
        string label,
        string field,
        ValidationReportBuilder report,
        int decimals = 2)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            report.Error(ruleId, $"{label} fehlt.", field);

            return 0m;
        }

        return ParseAmount(value, ruleId, label, field, report, decimals);
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
                $"{label} ist keine gültige Zahl.",
                field,
                $"Gelesen: '{value}'");

            return 0m;
        }

        return decimal.Round(parsed, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Liest eine Dezimalzahl in deutscher oder invarianter Schreibweise.
    /// Tausenderpunkte werden nicht unterstützt, weil sie sich nicht
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

    /// <summary>
    /// Menge, Einzelpreis und Steuersatz beginnen **leer**.
    ///
    /// Vorher standen dort "1", "0,00" und "19". Eine Null ist aber ein
    /// fachlich zulässiger Wert – ein Rabatt von 0,00 und ein Steuersatz von 0
    /// kommen vor. Ein vorausgefülltes 0,00 lässt sich deshalb nicht von einer
    /// bewussten Null unterscheiden: Vergisst der Anwender das Feld, entsteht
    /// eine Position über null Euro, und niemand meldet etwas.
    ///
    /// Leer ist eindeutig. Die Felder sind Zeichenketten, also braucht es dafür
    /// keinen zusätzlichen Nullwert-Typ – der leere Text ist bereits die
    /// Antwort „nichts eingetragen“. Was leer bleibt, meldet
    /// <see cref="InvoiceDraft.TryBuildInvoice"/> als fehlende Angabe.
    /// </summary>
    [ObservableProperty]
    private string _quantity = string.Empty;

    [ObservableProperty]
    private string _unit = "C62";

    [ObservableProperty]
    private string _netUnitPrice = string.Empty;

    /// <summary>
    /// Die Preisbasismenge steht nicht im Formular und ist nach EN 16931 mit 1
    /// belegt, wenn nichts anderes angegeben ist. Ein ableitbarer technischer
    /// Wert – der darf vorgegeben sein.
    /// </summary>
    [ObservableProperty]
    private string _priceBaseQuantity = "1";

    [ObservableProperty]
    private string _allowanceAmount = string.Empty;

    [ObservableProperty]
    private string _allowanceReason = string.Empty;

    [ObservableProperty]
    private VatCategory _vatCategory = VatCategory.StandardRate;

    [ObservableProperty]
    private string _vatRate = string.Empty;
}

/// <summary>Eine bearbeitbare Begründung der Steuerbefreiung.</summary>
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

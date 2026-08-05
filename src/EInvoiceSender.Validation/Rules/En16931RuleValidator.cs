using System.Globalization;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Money;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Validation.CodeLists;

namespace EInvoiceSender.Validation.Rules;

/// <summary>
/// Lokale Vorabpruefung der Rechnungsdaten mit verstaendlichen deutschen
/// Meldungen.
///
/// **Abgrenzung – das ist wichtig:** Dieser Validator ist ausdruecklich
/// **kein Ersatz** fuer Mustang, das CEN-Schematron oder veraPDF. Er dient
/// allein der fruehen Benutzerfuehrung: Er soll dem Anwender **vor** der
/// Erzeugung sagen, was in seiner Eingabe fehlt oder nicht zusammenpasst, und
/// zwar in Saetzen, die er versteht. Die verbindliche Freigabe erteilen
/// ausschliesslich die externen Werkzeuge (docs/DECISIONS.md, ADR-0004).
///
/// Daraus folgt eine bewusste Asymmetrie:
/// * Was dieser Validator beanstandet, wird nicht erzeugt.
/// * Was er durchlaesst, ist damit **nicht** als normkonform bestaetigt.
///
/// Jeder Befund traegt eine stabile interne Kennung (<c>APP-...</c>) und, soweit
/// vorhanden, die zugehoerige EN-16931-Regel. Die Kennungen sind unveraenderlich,
/// sobald sie vergeben wurden.
///
/// Der Validator korrigiert nichts. Er meldet nur.
/// </summary>
public sealed class En16931RuleValidator : IBusinessRuleValidator
{
    /// <summary>Groesste geduldete Abweichung bei Summenvergleichen.</summary>
    private const decimal ToleranceInCurrency = 0.01m;

    /// <summary>Frueheste plausible Rechnungsdatumsangabe.</summary>
    private static readonly DateOnly EarliestPlausibleDate = new(2000, 1, 1);

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Erzeugt den Validator. Die Zeitquelle ist einspeisbar, damit
    /// Datumspruefungen in Tests reproduzierbar sind.
    /// </summary>
    public En16931RuleValidator(TimeProvider? timeProvider = null)
        => _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public ValidationReport Validate(Invoice invoice, InvoiceTotals totals)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(totals);

        var report = new ValidationReportBuilder();

        ValidateDocument(invoice, totals, report);
        ValidateSeller(invoice.Seller, report);
        ValidateBuyer(invoice.Buyer, report);
        ValidateLines(invoice, report);
        ValidateVatBreakdown(invoice, totals, report);
        ValidateTotals(invoice, totals, report);
        ValidatePayment(invoice, totals, report);

        return report.Build();
    }

    // ---------------------------------------------------------------- Dokument

    private void ValidateDocument(Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            report.Error(
                "APP-DOC-001",
                "Die Rechnungsnummer fehlt. Ohne sie ist die Rechnung nicht gueltig.",
                "InvoiceNumber", normRule: "BR-02");
        }
        else if (invoice.InvoiceNumber.Length > 60)
        {
            report.Warning(
                "APP-DOC-002",
                "Die Rechnungsnummer ist ungewoehnlich lang. Manche Empfangssysteme "
                + "kuerzen sie auf wenige Zeichen.",
                "InvoiceNumber",
                $"Laenge {invoice.InvoiceNumber.Length}");
        }

        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        if (invoice.IssueDate < EarliestPlausibleDate)
        {
            report.Error(
                "APP-DOC-003",
                "Das Rechnungsdatum liegt unplausibel weit in der Vergangenheit. "
                + "Bitte pruefen Sie die Eingabe.",
                "IssueDate",
                $"Gelesen: {Format(invoice.IssueDate)}", "BR-03");
        }
        else if (invoice.IssueDate > today)
        {
            report.Warning(
                "APP-DOC-004",
                "Das Rechnungsdatum liegt in der Zukunft. Das ist erlaubt, aber "
                + "meist ein Tippfehler.",
                "IssueDate",
                $"Rechnungsdatum {Format(invoice.IssueDate)}, heute {Format(today)}");
        }

        if (invoice.DueDate is { } dueDate && dueDate < invoice.IssueDate)
        {
            report.Error(
                "APP-DOC-005",
                "Das Zahlungsziel liegt vor dem Rechnungsdatum.",
                "DueDate",
                $"Faellig {Format(dueDate)}, Rechnung vom {Format(invoice.IssueDate)}");
        }

        // BR-CO-25: Bei einem offenen Betrag braucht der Empfaenger die
        // Information, bis wann er zahlen soll.
        bool hasDueDate = invoice.DueDate is not null;
        bool hasTerms = !string.IsNullOrWhiteSpace(invoice.Payment?.Terms);

        if (totals.DuePayableAmount > 0m && !hasDueDate && !hasTerms)
        {
            report.Error(
                "APP-DOC-006",
                "Es fehlt eine Angabe, bis wann gezahlt werden soll. Geben Sie ein "
                + "Faelligkeitsdatum oder einen Zahlungsbedingungstext an.",
                "DueDate", normRule: "BR-CO-25");
        }

        if (!InvoiceTypeCodes.IsValid((int)invoice.TypeCode))
        {
            report.Error(
                "APP-DOC-007",
                "Die gewaehlte Rechnungsart wird von dieser Anwendung nicht unterstuetzt.",
                "TypeCode",
                $"Code {(int)invoice.TypeCode}", "BR-CO-03");
        }

        if (!CurrencyCodeList.IsValid(invoice.Currency.Value))
        {
            report.Error(
                "APP-DOC-008",
                $"'{invoice.Currency.Value}' ist keine bekannte Waehrung. Bitte verwenden "
                + "Sie eine Waehrungskennung nach ISO 4217, zum Beispiel EUR.",
                "Currency", normRule: "BR-05");
        }

        ValidatePeriod(
            invoice.BillingPeriodStart, invoice.BillingPeriodEnd,
            "APP-DOC-009", "BillingPeriod", "Der Abrechnungszeitraum", report);

        if (invoice.DeliveryDate is { } delivery
            && Math.Abs(delivery.DayNumber - invoice.IssueDate.DayNumber) > 365)
        {
            report.Warning(
                "APP-DOC-010",
                "Leistungsdatum und Rechnungsdatum liegen mehr als ein Jahr auseinander. "
                + "Bitte pruefen Sie die Eingabe.",
                "DeliveryDate",
                $"Leistung {Format(delivery)}, Rechnung {Format(invoice.IssueDate)}");
        }
    }

    private static void ValidatePeriod(
        DateOnly? start, DateOnly? end, string ruleId, string field, string label,
        ValidationReportBuilder report)
    {
        if (start is { } from && end is { } to && to < from)
        {
            report.Error(
                ruleId,
                $"{label} endet vor seinem Beginn.",
                field,
                $"Beginn {Format(from)}, Ende {Format(to)}", "BR-29");
        }
    }

    // -------------------------------------------------------------- Verkaeufer

    private static void ValidateSeller(SellerParty seller, ValidationReportBuilder report)
    {
        if (string.IsNullOrWhiteSpace(seller.Name))
        {
            report.Error(
                "APP-SEL-001",
                "Der Name des Rechnungsstellers fehlt.",
                "Seller.Name", normRule: "BR-06");
        }

        ValidateAddress(seller.Address, "Seller.Address", "des Rechnungsstellers", "APP-SEL-002", report);

        if (!CountryCodeList.IsValid(seller.Address.Country.Value))
        {
            report.Error(
                "APP-SEL-003",
                $"'{seller.Address.Country.Value}' ist kein bekanntes Laenderkennzeichen.",
                "Seller.Address.Country", normRule: "BR-09");
        }

        // BR-CO-26: Der Rechnungssteller muss steuerlich identifizierbar sein.
        if (string.IsNullOrWhiteSpace(seller.VatId) && string.IsNullOrWhiteSpace(seller.TaxNumber))
        {
            report.Error(
                "APP-SEL-004",
                "Es fehlt die Umsatzsteuer-Identifikationsnummer oder die Steuernummer "
                + "des Rechnungsstellers. Eine der beiden Angaben ist erforderlich.",
                "Seller.VatId", normRule: "BR-CO-26");
        }

        if (!string.IsNullOrWhiteSpace(seller.VatId) && !LooksLikeVatId(seller.VatId))
        {
            report.Warning(
                "APP-SEL-005",
                "Die Umsatzsteuer-Identifikationsnummer hat ein ungewoehnliches Format. "
                + "Sie beginnt normalerweise mit dem Laenderkennzeichen, zum Beispiel DE123456789.",
                "Seller.VatId",
                $"Gelesen: {seller.VatId}");
        }

        if (!string.IsNullOrWhiteSpace(seller.Email) && !LooksLikeEmail(seller.Email))
        {
            report.Error(
                "APP-SEL-006",
                "Die E-Mail-Adresse des Rechnungsstellers ist nicht gueltig.",
                "Seller.Email");
        }
    }

    // ------------------------------------------------------------------ Kaeufer

    private static void ValidateBuyer(BuyerParty buyer, ValidationReportBuilder report)
    {
        if (string.IsNullOrWhiteSpace(buyer.Name))
        {
            report.Error(
                "APP-BUY-001",
                "Der Name des Rechnungsempfaengers fehlt.",
                "Buyer.Name", normRule: "BR-07");
        }

        ValidateAddress(buyer.Address, "Buyer.Address", "des Rechnungsempfaengers", "APP-BUY-002", report);

        if (!CountryCodeList.IsValid(buyer.Address.Country.Value))
        {
            report.Error(
                "APP-BUY-003",
                $"'{buyer.Address.Country.Value}' ist kein bekanntes Laenderkennzeichen.",
                "Buyer.Address.Country", normRule: "BR-11");
        }

        if (!string.IsNullOrWhiteSpace(buyer.Email) && !LooksLikeEmail(buyer.Email))
        {
            report.Error(
                "APP-BUY-004",
                "Die E-Mail-Adresse des Rechnungsempfaengers ist nicht gueltig.",
                "Buyer.Email");
        }

        if (string.IsNullOrWhiteSpace(buyer.Email) && string.IsNullOrWhiteSpace(buyer.ElectronicAddress))
        {
            report.Warning(
                "APP-BUY-005",
                "Fuer den Empfaenger ist keine elektronische Adresse hinterlegt. "
                + "Ohne sie laesst sich die Rechnung nicht per E-Mail versenden.",
                "Buyer.Email");
        }

        if (!string.IsNullOrWhiteSpace(buyer.ElectronicAddress)
            && string.IsNullOrWhiteSpace(buyer.ElectronicAddressScheme))
        {
            report.Warning(
                "APP-BUY-006",
                "Zur elektronischen Adresse des Empfaengers fehlt die Angabe, um "
                + "welche Art von Adresse es sich handelt.",
                "Buyer.ElectronicAddressScheme",
                "Ohne Angabe wird 'EM' (E-Mail) verwendet.", "BR-63");
        }
    }

    private static void ValidateAddress(
        PostalAddress address, string field, string owner, string ruleId,
        ValidationReportBuilder report)
    {
        // EN 16931 verlangt zwingend nur das Land. Ort und Strasse fehlen in der
        // Praxis aber fast nie mit Absicht, deshalb hier als Warnung.
        if (string.IsNullOrWhiteSpace(address.City))
        {
            report.Warning(
                ruleId,
                $"Im Anschriftsfeld {owner} fehlt der Ort.",
                $"{field}.City");
        }

        if (string.IsNullOrWhiteSpace(address.Street))
        {
            report.Warning(
                ruleId,
                $"Im Anschriftsfeld {owner} fehlt die Strasse.",
                $"{field}.Street");
        }
    }

    // --------------------------------------------------------------- Positionen

    private static void ValidateLines(Invoice invoice, ValidationReportBuilder report)
    {
        if (invoice.Lines.Count == 0)
        {
            report.Error(
                "APP-LIN-001",
                "Die Rechnung enthaelt keine Positionen. Mindestens eine ist erforderlich.",
                "Lines", normRule: "BR-16");

            return;
        }

        var seenNumbers = new HashSet<int>();

        for (int index = 0; index < invoice.Lines.Count; index++)
        {
            InvoiceLine line = invoice.Lines[index];
            string field = $"Lines[{index}]";
            string label = $"Position {line.Number}";

            if (!seenNumbers.Add(line.Number))
            {
                report.Error(
                    "APP-LIN-002",
                    $"Die Positionsnummer {line.Number} kommt mehrfach vor. "
                    + "Jede Position braucht eine eigene Nummer.",
                    $"{field}.Number", normRule: "BR-21");
            }

            if (string.IsNullOrWhiteSpace(line.Name))
            {
                report.Error(
                    "APP-LIN-003",
                    $"{label} hat keine Bezeichnung.",
                    $"{field}.Name", normRule: "BR-25");
            }

            if (line.Quantity == 0m)
            {
                report.Error(
                    "APP-LIN-004",
                    $"{label} hat die Menge null.",
                    $"{field}.Quantity", normRule: "BR-22");
            }

            if (!UnitCodeList.IsValid(line.Unit.Value))
            {
                report.Error(
                    "APP-LIN-005",
                    $"Die Mengeneinheit '{line.Unit.Value}' in {label} ist unbekannt.",
                    $"{field}.Unit",
                    "Zulaessig sind die Codes nach UN/ECE-Empfehlung 20 und 21.", "BR-23");
            }

            if (line.NetUnitPrice < 0m)
            {
                report.Error(
                    "APP-LIN-006",
                    $"Der Einzelpreis in {label} ist negativ. Verwenden Sie fuer "
                    + "Gutschriften die Rechnungsart 381 statt negativer Preise.",
                    $"{field}.NetUnitPrice", normRule: "BR-27");
            }

            if (line.PriceBaseQuantity <= 0m)
            {
                report.Error(
                    "APP-LIN-007",
                    $"Die Preisbasismenge in {label} muss groesser als null sein.",
                    $"{field}.PriceBaseQuantity",
                    $"Gelesen: {Format(line.PriceBaseQuantity)}", "BR-DEC-24");
            }

            if (line.AllowanceAmount < 0m || line.ChargeAmount < 0m)
            {
                report.Error(
                    "APP-LIN-008",
                    $"Rabatt und Zuschlag in {label} muessen als positive Betraege "
                    + "angegeben werden.",
                    $"{field}.AllowanceAmount", normRule: "BR-31");
            }

            decimal lineNet = InvoiceCalculator.CalculateLineNetAmount(line);

            if (lineNet < 0m && line.NetUnitPrice >= 0m && line.Quantity > 0m)
            {
                report.Error(
                    "APP-LIN-009",
                    $"Der Rabatt in {label} ist groesser als der Positionsbetrag.",
                    $"{field}.AllowanceAmount",
                    $"Positionsbetrag {Format(lineNet)}");
            }

            ValidateVatCategoryAndRate(
                line.VatCategory, line.VatRate, $"{field}.VatRate", label, report);

            ValidatePeriod(
                line.ServicePeriodStart, line.ServicePeriodEnd,
                "APP-LIN-010", $"{field}.ServicePeriod", $"Der Leistungszeitraum in {label}", report);
        }
    }

    private static void ValidateVatCategoryAndRate(
        VatCategory category, decimal rate, string field, string label,
        ValidationReportBuilder report)
    {
        if (rate < 0m)
        {
            report.Error(
                "APP-VAT-001",
                $"Der Steuersatz in {label} ist negativ.",
                field, normRule: "BR-CO-17");

            return;
        }

        if (rate > 100m)
        {
            report.Error(
                "APP-VAT-002",
                $"Der Steuersatz in {label} ist groesser als 100 Prozent.",
                field,
                $"Gelesen: {Format(rate)}");

            return;
        }

        if (category.RequiresPositiveRate() && rate <= 0m)
        {
            report.Error(
                "APP-VAT-003",
                $"{label} ist als regelbesteuert gekennzeichnet, hat aber den Steuersatz null. "
                + "Waehlen Sie entweder einen Steuersatz groesser null oder eine andere "
                + "Steuerkategorie.",
                field, normRule: "BR-S-05");
        }

        if (category == VatCategory.ZeroRated && rate != 0m)
        {
            report.Error(
                "APP-VAT-004",
                $"{label} ist als nullsatzbesteuert gekennzeichnet, hat aber einen "
                + "Steuersatz groesser null.",
                field, normRule: "BR-Z-05");
        }

        if (category is VatCategory.Exempt or VatCategory.ReverseCharge
            or VatCategory.IntraCommunitySupply or VatCategory.ExportOutsideEu
            or VatCategory.OutsideScope
            && rate != 0m)
        {
            report.Error(
                "APP-VAT-005",
                $"{label} ist steuerbefreit, hat aber einen Steuersatz groesser null.",
                field,
                $"Kategorie {category.ToCode()}, Satz {Format(rate)}", "BR-E-05");
        }
    }

    // ------------------------------------------------------- Steueraufschluesselung

    private static void ValidateVatBreakdown(
        Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        if (totals.VatBreakdown.Count == 0)
        {
            report.Error(
                "APP-VAT-010",
                "Die Rechnung enthaelt keine Steueraufschluesselung.",
                "VatBreakdown", normRule: "BR-CO-18");

            return;
        }

        foreach (VatBreakdownEntry entry in totals.VatBreakdown)
        {
            string label = $"Steuersatz {Format(entry.Rate)} Prozent, Kategorie {entry.Category.ToCode()}";

            // BR-CO-17: Steuerbetrag = Steuerbasis mal Satz, auf zwei Stellen.
            decimal expected = Amounts.Round(entry.TaxableAmount * entry.Rate / 100m);

            if (entry.TaxAmount != expected)
            {
                report.Error(
                    "APP-VAT-011",
                    $"Der Steuerbetrag fuer {label} passt nicht zur Steuerbasis.",
                    "VatBreakdown",
                    $"Erwartet {Format(expected)}, berechnet {Format(entry.TaxAmount)}",
                    "BR-CO-17");
            }

            if (!entry.Category.RequiresExemptionReason())
            {
                continue;
            }

            VatExemptionReason? reason = invoice.ExemptionReasons
                .FirstOrDefault(r => r.Category == entry.Category);

            if (reason is null || string.IsNullOrWhiteSpace(reason.Reason))
            {
                report.Error(
                    "APP-VAT-012",
                    $"Fuer die Steuerkategorie '{entry.Category.ToCode()}' fehlt die "
                    + "Begruendung der Steuerbefreiung. Ohne sie ist die Rechnung "
                    + "nicht normgerecht.",
                    "ExemptionReasons",
                    $"Betroffen: {label}", "BR-E-10");
            }
            else if (!string.IsNullOrWhiteSpace(reason.ReasonCode)
                     && !VatExemptionReasonCodes.IsValidOrKnownSubcode(reason.ReasonCode))
            {
                report.Warning(
                    "APP-VAT-013",
                    "Der angegebene Code fuer die Steuerbefreiung ist nicht bekannt. "
                    + "Der Begruendungstext wird trotzdem uebernommen.",
                    "ExemptionReasons",
                    $"Code: {reason.ReasonCode}");
            }
        }
    }

    // ------------------------------------------------------------------ Summen

    private static void ValidateTotals(
        Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        // Die Summen werden hier gegen eine unabhaengige Neuberechnung geprueft.
        // Das faengt den Fall ab, dass ein veraltetes Summenobjekt zu einer
        // inzwischen geaenderten Rechnung gehoert.
        InvoiceTotals recomputed = InvoiceCalculator.Calculate(invoice);

        CompareTotal(totals.LineTotal, recomputed.LineTotal,
            "APP-SUM-001", "Die Summe der Rechnungspositionen", "BR-CO-10", report);
        CompareTotal(totals.TaxBasisTotal, recomputed.TaxBasisTotal,
            "APP-SUM-002", "Die Nettosumme", "BR-CO-13", report);
        CompareTotal(totals.TaxTotal, recomputed.TaxTotal,
            "APP-SUM-003", "Der Gesamtbetrag der Umsatzsteuer", "BR-CO-14", report);
        CompareTotal(totals.GrandTotal, recomputed.GrandTotal,
            "APP-SUM-004", "Die Bruttosumme", "BR-CO-15", report);
        CompareTotal(totals.DuePayableAmount, recomputed.DuePayableAmount,
            "APP-SUM-005", "Der offene Zahlbetrag", "BR-CO-16", report);

        // BR-DEC-*: Betragsfelder duerfen hoechstens zwei Nachkommastellen haben.
        foreach ((string label, decimal value, string field) in new[]
                 {
                     ("Summe der Positionen", totals.LineTotal, "LineTotal"),
                     ("Nettosumme", totals.TaxBasisTotal, "TaxBasisTotal"),
                     ("Gesamtsteuer", totals.TaxTotal, "TaxTotal"),
                     ("Bruttosumme", totals.GrandTotal, "GrandTotal"),
                     ("bereits gezahlter Betrag", totals.PaidAmount, "PaidAmount"),
                     ("Rundungsbetrag", totals.RoundingAmount, "RoundingAmount"),
                     ("offener Zahlbetrag", totals.DuePayableAmount, "DuePayableAmount"),
                 })
        {
            if (!Amounts.HasAtMostDecimals(value, Amounts.AmountDecimals))
            {
                report.Error(
                    "APP-SUM-006",
                    $"Der Betrag '{label}' hat mehr als zwei Nachkommastellen.",
                    field,
                    $"Gelesen: {value.ToString(CultureInfo.InvariantCulture)}", "BR-DEC-09");
            }
        }

        if (invoice.PaidAmount < 0m)
        {
            report.Error(
                "APP-SUM-007",
                "Der bereits gezahlte Betrag darf nicht negativ sein.",
                "PaidAmount");
        }
        else if (invoice.PaidAmount > totals.GrandTotal + ToleranceInCurrency)
        {
            report.Warning(
                "APP-SUM-008",
                "Der bereits gezahlte Betrag ist groesser als die Bruttosumme. "
                + "Der offene Zahlbetrag wird dadurch negativ.",
                "PaidAmount",
                $"Gezahlt {Format(invoice.PaidAmount)}, Brutto {Format(totals.GrandTotal)}");
        }

        if (Math.Abs(totals.RoundingAmount) > 0.05m)
        {
            report.Warning(
                "APP-SUM-009",
                "Der Rundungsbetrag ist ungewoehnlich hoch. Ueblich sind wenige Cent.",
                "RoundingAmount",
                $"Gelesen: {Format(totals.RoundingAmount)}");
        }

        // Eine Handelsrechnung mit negativem Zahlbetrag ist fast immer ein
        // Bedienfehler; gemeint ist dann die Rechnungsart 381.
        if (!invoice.TypeCode.IsCreditNote()
            && totals.DuePayableAmount < 0m
            && invoice.PaidAmount == 0m)
        {
            report.Error(
                "APP-SUM-010",
                "Der Zahlbetrag ist negativ, die Rechnungsart ist aber eine normale "
                + "Rechnung. Fuer eine Erstattung waehlen Sie die Rechnungsart "
                + "'Gutschrift' (381).",
                "DuePayableAmount",
                $"Zahlbetrag {Format(totals.DuePayableAmount)}");
        }
    }

    private static void CompareTotal(
        decimal actual, decimal expected, string ruleId, string label, string normRule,
        ValidationReportBuilder report)
    {
        if (actual == expected)
        {
            return;
        }

        report.Error(
            ruleId,
            $"{label} stimmt nicht mit den erfassten Daten ueberein.",
            label,
            $"Erwartet {Format(expected)}, uebergeben {Format(actual)}, "
            + $"Abweichung {Format(actual - expected)}",
            normRule);
    }

    // ---------------------------------------------------------------- Zahlung

    private static void ValidatePayment(
        Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        PaymentDetails? payment = invoice.Payment;

        if (payment is null)
        {
            if (totals.DuePayableAmount > 0m)
            {
                report.Warning(
                    "APP-PAY-001",
                    "Es sind keine Zahlungsangaben hinterlegt. Der Empfaenger erfaehrt "
                    + "damit nicht, wohin er zahlen soll.",
                    "Payment");
            }

            return;
        }

        if (!PaymentMeansCodes.IsValid((int)payment.MeansCode))
        {
            report.Error(
                "APP-PAY-002",
                "Die gewaehlte Zahlungsart ist unbekannt.",
                "Payment.MeansCode",
                $"Code {(int)payment.MeansCode}", "BR-49");
        }

        bool requiresAccount = payment.MeansCode
            is PaymentMeansCode.CreditTransfer
            or PaymentMeansCode.SepaCreditTransfer
            or PaymentMeansCode.PaymentToBankAccount;

        if (requiresAccount && payment.BankAccount is null)
        {
            report.Error(
                "APP-PAY-003",
                "Fuer eine Ueberweisung fehlt die Bankverbindung.",
                "Payment.BankAccount", normRule: "BR-50");

            return;
        }

        if (payment.BankAccount is not { } account)
        {
            return;
        }

        // Die IBAN ist bereits beim Einlesen auf ihre Pruefziffer geprueft
        // worden – der Typ existiert nur in gueltigem Zustand. Hier bleibt zu
        // pruefen, ob die uebrigen Angaben dazu passen.
        if (string.IsNullOrWhiteSpace(account.AccountHolder))
        {
            report.Warning(
                "APP-PAY-004",
                "Zur Bankverbindung fehlt der Kontoinhaber.",
                "Payment.BankAccount.AccountHolder", normRule: "BR-61");
        }

        if (!string.IsNullOrWhiteSpace(account.Bic) && !LooksLikeBic(account.Bic))
        {
            report.Error(
                "APP-PAY-005",
                "Die BIC ist nicht gueltig. Sie besteht aus 8 oder 11 Zeichen.",
                "Payment.BankAccount.Bic",
                $"Laenge {account.Bic.Length}");
        }

        if (!CountryCodeList.IsValid(account.Iban.CountryPrefix))
        {
            report.Error(
                "APP-PAY-006",
                "Das Laenderkennzeichen der IBAN ist unbekannt.",
                "Payment.BankAccount.Iban",
                $"Praefix {account.Iban.CountryPrefix}");
        }
    }

    // ------------------------------------------------------------------ Helfer

    /// <summary>
    /// Sehr einfache Syntaxpruefung fuer E-Mail-Adressen: genau ein
    /// At-Zeichen, davor und danach etwas, im hinteren Teil ein Punkt.
    /// Bewusst nicht strenger – eine formal korrekte Adresse kann trotzdem
    /// unzustellbar sein, und eine zu strenge Pruefung lehnt gueltige Adressen ab.
    /// </summary>
    private static bool LooksLikeEmail(string value)
    {
        string trimmed = value.Trim();

        int at = trimmed.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return false;
        }

        string domain = trimmed[(at + 1)..];

        return domain.Contains('.', StringComparison.Ordinal)
               && !domain.StartsWith('.')
               && !domain.EndsWith('.')
               && !trimmed.Contains(' ', StringComparison.Ordinal);
    }

    /// <summary>
    /// Prueft die Form einer Umsatzsteuer-Identifikationsnummer: zwei
    /// Buchstaben als Laenderkennzeichen, danach mindestens zwei alphanumerische
    /// Zeichen. Die laenderspezifischen Pruefziffern werden bewusst nicht
    /// geprueft – dafuer waere ein Abgleich mit dem MIAS-Dienst noetig, und der
    /// wuerde Daten nach draussen geben.
    /// </summary>
    private static bool LooksLikeVatId(string value)
    {
        string trimmed = value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

        return trimmed.Length >= 4
               && char.IsAsciiLetter(trimmed[0])
               && char.IsAsciiLetter(trimmed[1])
               && trimmed.Skip(2).All(char.IsAsciiLetterOrDigit);
    }

    /// <summary>Prueft die Form einer BIC nach ISO 9362 (8 oder 11 Zeichen).</summary>
    private static bool LooksLikeBic(string value)
    {
        string trimmed = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        if (trimmed.Length is not (8 or 11))
        {
            return false;
        }

        // Aufbau: 4 Buchstaben Bank, 2 Buchstaben Land, 2 alphanumerisch Ort,
        // optional 3 alphanumerisch Filiale.
        for (int i = 0; i < 6; i++)
        {
            if (!char.IsAsciiLetterUpper(trimmed[i]))
            {
                return false;
            }
        }

        return trimmed.Skip(6).All(char.IsAsciiLetterOrDigit);
    }

    private static string Format(decimal value)
        => value.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));

    private static string Format(DateOnly value)
        => value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));
}

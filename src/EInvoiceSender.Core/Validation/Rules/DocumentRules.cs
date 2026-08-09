using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zum Rechnungsdokument selbst: Nummer, Datum, Zeitraum, Art,
/// Waehrung und Verweise.
/// </summary>
internal static class DocumentRules
{
    /// <summary>Frueheste plausible Rechnungsdatumsangabe.</summary>
    private static readonly DateOnly EarliestPlausibleDate = new(2000, 1, 1);

    public static void Validate(
        Invoice invoice,
        InvoiceTotals totals,
        ValidationReportBuilder report,
        TimeProvider timeProvider)
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

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        if (invoice.IssueDate < EarliestPlausibleDate)
        {
            report.Error(
                "APP-DOC-003",
                "Das Rechnungsdatum liegt unplausibel weit in der Vergangenheit. "
                + "Bitte pruefen Sie die Eingabe.",
                "IssueDate",
                $"Gelesen: {SharedRules.Format(invoice.IssueDate)}", "BR-03");
        }
        else if (invoice.IssueDate > today)
        {
            report.Warning(
                "APP-DOC-004",
                "Das Rechnungsdatum liegt in der Zukunft. Das ist erlaubt, aber "
                + "meist ein Tippfehler.",
                "IssueDate",
                $"Rechnungsdatum {SharedRules.Format(invoice.IssueDate)}, heute {SharedRules.Format(today)}");
        }

        if (invoice.DueDate is { } dueDate && dueDate < invoice.IssueDate)
        {
            report.Error(
                "APP-DOC-005",
                "Das Zahlungsziel liegt vor dem Rechnungsdatum.",
                "DueDate",
                $"Faellig {SharedRules.Format(dueDate)}, Rechnung vom {SharedRules.Format(invoice.IssueDate)}");
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
                $"Leistung {SharedRules.Format(delivery)}, Rechnung {SharedRules.Format(invoice.IssueDate)}");
        }
    }

    /// <summary>
    /// Prueft einen Zeitraum. Wird auch je Position gebraucht: Die
    /// Anforderungen an einen Leistungszeitraum sind auf beiden Ebenen
    /// dieselben.
    /// </summary>
    public static void ValidatePeriod(
        DateOnly? start, DateOnly? end, string ruleId, string field, string label,
        ValidationReportBuilder report)
    {
        if (start is { } from && end is { } to && to < from)
        {
            report.Error(
                ruleId,
                $"{label} endet vor seinem Beginn.",
                field,
                $"Beginn {SharedRules.Format(from)}, Ende {SharedRules.Format(to)}", "BR-29");
        }
    }

    // -------------------------------------------------------------- Verkaeufer
}

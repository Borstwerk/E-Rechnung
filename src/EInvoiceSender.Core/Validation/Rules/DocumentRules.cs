using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zum Rechnungsdokument selbst: Nummer, Datum, Zeitraum, Art,
/// Währung und Verweise.
/// </summary>
internal static class DocumentRules
{
    /// <summary>Früheste plausible Rechnungsdatumsangabe.</summary>
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
                "Die Rechnungsnummer fehlt. Ohne sie ist die Rechnung nicht gültig.",
                "InvoiceNumber", normRule: "BR-02");
        }
        else if (invoice.InvoiceNumber.Length > 60)
        {
            report.Warning(
                "APP-DOC-002",
                "Die Rechnungsnummer ist ungewöhnlich lang. Manche Empfangssysteme "
                + "kürzen sie auf wenige Zeichen.",
                "InvoiceNumber",
                $"Länge {invoice.InvoiceNumber.Length}");
        }

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        if (invoice.IssueDate < EarliestPlausibleDate)
        {
            report.Error(
                "APP-DOC-003",
                "Das Rechnungsdatum liegt unplausibel weit in der Vergangenheit. "
                + "Bitte prüfen Sie die Eingabe.",
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
                $"Fällig {SharedRules.Format(dueDate)}, Rechnung vom {SharedRules.Format(invoice.IssueDate)}");
        }

        // BR-CO-25: Bei einem offenen Betrag braucht der Empfänger die
        // Information, bis wann er zahlen soll.
        bool hasDueDate = invoice.DueDate is not null;
        bool hasTerms = !string.IsNullOrWhiteSpace(invoice.Payment?.Terms);

        if (totals.DuePayableAmount > 0m && !hasDueDate && !hasTerms)
        {
            report.Error(
                "APP-DOC-006",
                "Es fehlt eine Angabe, bis wann gezahlt werden soll. Geben Sie ein "
                + "Fälligkeitsdatum oder einen Zahlungsbedingungstext an.",
                "DueDate", normRule: "BR-CO-25");
        }

        // Der frühere Normverweis BR-CO-03 war schlicht falsch. Im
        // EN-16931-Schematron des gepinnten Prüfwerkzeugs lautet die Regel:
        // „Value added tax point date (BT-7) and Value added tax point date
        // code (BT-8) are mutually exclusive.“ Mit der Rechnungsart hat sie
        // nichts zu tun.
        //
        // Ersetzt wird sie durch keinen anderen Verweis, und das mit Absicht:
        // Über die Zugehörigkeit zu UNTDID 1001 wacht BR-CL-01, und die
        // Liste hier ist nur die Teilmenge, die diese Anwendung beherrscht.
        // Ein hier abgelehnter Code kann in UNTDID 1001 stehen – dasselbe
        // Verhältnis wie bei Währung und Mengeneinheit.
        if (!InvoiceTypeCodes.IsValid((int)invoice.TypeCode))
        {
            report.Error(
                "APP-DOC-007",
                "Die gewählte Rechnungsart wird von dieser Anwendung nicht unterstützt.",
                "TypeCode",
                $"Code {(int)invoice.TypeCode}; über die Zugehörigkeit zu UNTDID 1001 "
                + "entscheidet die externe Prüfung (BR-CL-01).");
        }

        // Zwei verschiedene Aussagen, und sie dürfen nicht zusammenfallen:
        //
        // Ein aus dem gepinnten EN-16931-Codebestand entfernter Code ist ein
        // echter Normbefund – BR-05 verlangt eine gültige ISO-4217-Kennung,
        // und für diese wenigen Codes ist der Rückzug belegt.
        //
        // Ein Code dagegen, den diese Anwendung nur nicht anbietet, sagt über
        // die Norm nichts. Rund 180 Währungen sind aktiv; die volle Liste ist
        // hier nicht abgebildet. Ihn als Normverstoß auszugeben hieße, eine
        // Grenze dieses Programms als Grenze der Norm auszugeben.
        if (CurrencyCodeList.IsWithdrawnFromEn16931(invoice.Currency.Value))
        {
            CurrencyCodeList.TryGetWithdrawalReason(invoice.Currency.Value, out string? reason);

            report.Error(
                "APP-DOC-008",
                $"'{invoice.Currency.Value}' ist keine gültige Währungskennung mehr. Bitte "
                + "verwenden Sie eine aktuelle Kennung nach ISO 4217, zum Beispiel EUR.",
                "Currency", reason, "BR-05");
        }
        else if (!CurrencyCodeList.IsOffered(invoice.Currency.Value))
        {
            report.Warning(
                "APP-DOC-011",
                $"Die Währung '{invoice.Currency.Value}' wird von dieser Anwendung nicht "
                + "angeboten. Ob sie nach ISO 4217 gültig ist, entscheidet die externe "
                + "Prüfung – hier wird darüber keine Aussage getroffen.",
                "Currency",
                "Nicht in der kuratierten Auswahl dieser Anwendung enthalten.");
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
                + "Bitte prüfen Sie die Eingabe.",
                "DeliveryDate",
                $"Leistung {SharedRules.Format(delivery)}, Rechnung {SharedRules.Format(invoice.IssueDate)}");
        }
    }

    /// <summary>
    /// Prüft einen Zeitraum. Wird auch je Position gebraucht: Die
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

    // -------------------------------------------------------------- Verkäufer
}

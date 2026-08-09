using System.Globalization;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Regeln zur Zahlung.
/// </summary>
internal static class PaymentRules
{
    public static void Validate(
        Invoice invoice, InvoiceTotals totals, ValidationReportBuilder report)
    {
        PaymentDetails? payment = invoice.Payment;

        if (payment is null)
        {
            if (totals.DuePayableAmount > 0m)
            {
                report.Warning(
                    "APP-PAY-001",
                    "Es sind keine Zahlungsangaben hinterlegt. Der Empfänger erfährt "
                    + "damit nicht, wohin er zahlen soll.",
                    "Payment");
            }

            return;
        }

        if (!PaymentMeansCodes.IsValid((int)payment.MeansCode))
        {
            report.Error(
                "APP-PAY-002",
                "Die gewählte Zahlungsart ist unbekannt.",
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
                "Für eine Überweisung fehlt die Bankverbindung.",
                "Payment.BankAccount", normRule: "BR-50");

            return;
        }

        if (payment.BankAccount is not { } account)
        {
            return;
        }

        // Die IBAN ist bereits beim Einlesen auf ihre Prüfziffer geprüft
        // worden – der Typ existiert nur in gültigem Zustand. Hier bleibt zu
        // prüfen, ob die übrigen Angaben dazu passen.
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
                "Die BIC ist nicht gültig. Sie besteht aus 8 oder 11 Zeichen.",
                "Payment.BankAccount.Bic",
                $"Länge {account.Bic.Length}");
        }

        if (!CountryCodeList.IsValid(account.Iban.CountryPrefix))
        {
            report.Error(
                "APP-PAY-006",
                "Das Länderkennzeichen der IBAN ist unbekannt.",
                "Payment.BankAccount.Iban",
                $"Präfix {account.Iban.CountryPrefix}");
        }
    }

    // ------------------------------------------------------------------ Helfer

    /// <summary>Prüft die Form einer BIC nach ISO 9362 (8 oder 11 Zeichen).</summary>
    internal static bool LooksLikeBic(string value)
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

}

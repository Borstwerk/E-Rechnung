using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation.Rules;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Lokale Vorabprüfung der Rechnungsdaten mit verständlichen deutschen
/// Meldungen.
///
/// **Abgrenzung – das ist wichtig:** Dieser Validator ist ausdrücklich
/// **kein Ersatz** für Mustang, das CEN-Schematron oder veraPDF. Er dient
/// allein der frühen Benutzerführung: Er soll dem Anwender **vor** der
/// Erzeugung sagen, was in seiner Eingabe fehlt oder nicht zusammenpasst, und
/// zwar in Sätzen, die er versteht. Die verbindliche Freigabe erteilen
/// ausschließlich die externen Werkzeuge (docs/TESTING.md).
///
/// Daraus folgt eine bewusste Asymmetrie:
/// * Was dieser Validator beanstandet, wird nicht erzeugt.
/// * Was er durchlässt, ist damit **nicht** als normkonform bestätigt.
///
/// Jeder Befund trägt eine stabile interne Kennung (<c>APP-...</c>) und, soweit
/// vorhanden, die zugehörige EN-16931-Regel. Die Kennungen sind unveränderlich,
/// sobald sie vergeben wurden.
///
/// Der Validator korrigiert nichts. Er meldet nur.
///
/// Diese Klasse führt lediglich die fachlichen Regelgruppen zusammen; die
/// Regeln selbst stehen unter <c>Validation/Rules</c>.
/// </summary>
public sealed class En16931RuleValidator : IBusinessRuleValidator
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Erzeugt den Validator. Die Zeitquelle ist einspeisbar, damit
    /// Datumsprüfungen in Tests reproduzierbar sind.
    /// </summary>
    public En16931RuleValidator(TimeProvider? timeProvider = null)
        => _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public ValidationReport Validate(Invoice invoice, InvoiceTotals totals)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(totals);

        var report = new ValidationReportBuilder();

        DocumentRules.Validate(invoice, totals, report, _timeProvider);
        SellerRules.Validate(invoice.Seller, report);
        BuyerRules.Validate(invoice.Buyer, report);
        InvoiceLineRules.Validate(invoice, report);
        VatRules.Validate(invoice, totals, report);
        TotalsRules.Validate(invoice, totals, report);
        PaymentRules.Validate(invoice, totals, report);

        return report.Build();
    }
}

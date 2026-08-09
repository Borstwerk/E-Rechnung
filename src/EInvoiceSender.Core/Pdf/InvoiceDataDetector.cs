using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Pdf;

/// <summary>Liest typische Rechnungsangaben aus dem Text einer PDF.</summary>
public interface IInvoiceDataDetector
{
    /// <summary>
    /// Wertet die PDF örtlich aus. <paramref name="ownCompany"/> ist die
    /// gespeicherte eigene Firmenvorlage; sie hilft dabei, Verkäufer und
    /// Käufer auseinanderzuhalten, und wird nur zum Vergleichen verwendet.
    /// </summary>
    Task<InvoiceDetectionResult> DetectAsync(
        string pdfPath,
        CompanyTemplate? ownCompany = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Erkennt Rechnungsangaben im bereits vorhandenen PDF-Text.
///
/// **Was diese Klasse ist:** eine Schreibhilfe. Sie füllt das Formular vor,
/// damit der Anwender nicht abtippen muss, was ohnehin schon dasteht.
///
/// **Was sie nicht ist:** eine Quelle der Wahrheit. Kein gelesener Wert geht
/// je unmittelbar in die E-Rechnung; der Weg führt immer über das Formular
/// und die Bestätigung durch den Menschen.
///
/// Diese Klasse koordiniert nur. Die Erkennungsregeln stehen in den vier
/// fachlich getrennten Detektoren unter <c>Detection</c>; die kleinen
/// Umwandlungen in <c>DetectionParsers</c>.
///
/// **Die Leitregel bei allen Zweifelsfällen lautet: lieber nichts vorschlagen
/// als etwas Falsches.** Ein leeres Feld kostet Tippen. Ein falsch gefülltes
/// Feld, das jemand übersieht, kostet eine fehlerhafte Rechnung.
/// </summary>
public sealed partial class InvoiceDataDetector(
    IPdfTextExtractor extractor,
    ILogger<InvoiceDataDetector> logger) : IInvoiceDataDetector
{
    private readonly IPdfTextExtractor _extractor = extractor;
    private readonly ILogger<InvoiceDataDetector> _logger = logger;

    /// <inheritdoc />
    public async Task<InvoiceDetectionResult> DetectAsync(
        string pdfPath,
        CompanyTemplate? ownCompany = null,
        CancellationToken cancellationToken = default)
    {
        PdfTextResult text = await _extractor.ExtractAsync(pdfPath, cancellationToken)
            .ConfigureAwait(false);

        if (!text.HasUsableText)
        {
            return InvoiceDetectionResult.WithoutText;
        }

        try
        {
            return Detect(text, ownCompany);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Die Erkennung ist eine Komfortfunktion und darf den Ablauf nie
            // aufhalten. Protokolliert wird nur die Fehlerart, nie Dateiinhalt.
            string reason = exception.GetType().Name;
            LogDetectionFailed(reason);

            return InvoiceDetectionResult.WithoutText with { HasUsableText = true };
        }
    }

    private static InvoiceDetectionResult Detect(PdfTextResult text, CompanyTemplate? ownCompany)
    {
        DetectedDocument document = DocumentFieldDetector.Detect(text.Lines);
        DetectedParties parties = PartyDetector.Detect(text.Lines, ownCompany);
        DetectedPayment payment = PaymentDetector.Detect(text.Lines);
        DetectedTotals totals = TotalsDetector.Detect(text.Lines);

        return Combine(text, document, parties, payment, totals);
    }

    private static InvoiceDetectionResult Combine(
        PdfTextResult text,
        DetectedDocument document,
        DetectedParties parties,
        DetectedPayment payment,
        DetectedTotals totals) => new()
        {
            HasUsableText = true,
            PageCount = text.PageCount,
            InvoiceNumber = document.InvoiceNumber,
            IssueDate = document.IssueDate,
            DeliveryDate = document.DeliveryDate,
            DueDate = document.DueDate,
            Currency = document.Currency,
            Seller = parties.Seller,
            Buyer = parties.Buyer,
            Iban = payment.Iban,
            Bic = payment.Bic,
            Totals = totals,
        };

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "Die Rechnungserkennung ist fehlgeschlagen ({Reason}). Die Daten werden von Hand erfasst.")]
    private partial void LogDetectionFailed(string reason);
}

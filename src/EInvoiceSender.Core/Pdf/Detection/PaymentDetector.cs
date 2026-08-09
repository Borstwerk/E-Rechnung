using System.Text.RegularExpressions;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Die erkannten Zahlungsangaben.</summary>
public sealed record DetectedPayment
{
    public DetectedValue<string>? Iban { get; init; }
    public DetectedValue<string>? Bic { get; init; }
}

/// <summary>
/// Erkennt IBAN und BIC.
///
/// Bei der IBAN genuegt das Muster ausdruecklich nicht: Erst die Pruefsumme
/// nach ISO 7064 macht aus einer Zeichenfolge eine IBAN. Eine falsche
/// Bankverbindung auf einer Rechnung ist teuer, deshalb wird eine syntaktisch
/// passende, rechnerisch falsche IBAN verworfen statt uebernommen.
/// </summary>
internal static class PaymentDetector
{
    public static DetectedPayment Detect(IReadOnlyList<PdfTextLine> lines) => new()
    {
        Iban = DetectIban(lines),
        Bic = DetectBic(lines),
    };

    private static DetectedValue<string>? DetectIban(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (PdfTextLine line in lines)
        {
            foreach (Match match in DetectionParsers.Iban().Matches(line.Text))
            {
                string candidate = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);

                if (!Iban.TryParse(candidate, out Iban iban))
                {
                    continue;
                }

                return new DetectedValue<string>(
                    iban.Value, DetectionConfidence.High,
                    DetectionParsers.MaskIbans(line.Text),
                    "Muster erkannt und Pruefsumme nach ISO 7064 bestaetigt.");
            }
        }

        return null;
    }

    private static DetectedValue<string>? DetectBic(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (PdfTextLine line in lines)
        {
            string lower = line.Text.ToLowerInvariant();

            if (!lower.Contains("bic", StringComparison.Ordinal)
                && !lower.Contains("swift", StringComparison.Ordinal))
            {
                continue;
            }

            Match match = DetectionParsers.Bic().Match(line.Text);

            if (match.Success)
            {
                return new DetectedValue<string>(
                    match.Value, DetectionConfidence.Medium, line.Text, "Stand hinter \"BIC\".");
            }
        }

        return null;
    }
}

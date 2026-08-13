using System.Text.RegularExpressions;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Die erkannten Zahlungsangaben.</summary>
public sealed record DetectedPayment
{
    public DetectedValue<string>? Iban { get; init; }
    public DetectedValue<string>? Bic { get; init; }

    internal IReadOnlyList<LocatedPaymentValue> IbanCandidates { get; init; } = [];
    internal IReadOnlyList<LocatedPaymentValue> BicCandidates { get; init; } = [];
}

/// <summary>Bankwert samt räumlicher Fundstelle für die Seller-Zuordnung.</summary>
internal sealed record LocatedPaymentValue(
    DetectedValue<string> Detection,
    int PageNumber,
    double Top,
    double Left);

/// <summary>
/// Erkennt IBAN und BIC.
///
/// Bei der IBAN genügt das Muster ausdrücklich nicht: Erst die Prüfsumme
/// nach ISO 7064 macht aus einer Zeichenfolge eine IBAN. Eine falsche
/// Bankverbindung auf einer Rechnung ist teuer, deshalb wird eine syntaktisch
/// passende, rechnerisch falsche IBAN verworfen statt übernommen.
/// </summary>
internal static class PaymentDetector
{
    public static DetectedPayment Detect(IReadOnlyList<PdfTextLine> lines)
    {
        IReadOnlyList<LocatedPaymentValue> ibans = DetectIbans(lines);
        IReadOnlyList<LocatedPaymentValue> bics = DetectBics(lines);

        return new DetectedPayment
        {
            // Mehrere verschiedene gültige IBANs sind mehrdeutig. Dann wird
            // weder im allgemeinen Bankfeld noch im eigenen Unternehmens-
            // Proposal eine Bankverbindung vorausgewählt.
            Iban = ibans.Count == 1 ? ibans[0].Detection : null,
            Bic = ibans.Count <= 1 && bics.Count == 1 ? bics[0].Detection : null,
            IbanCandidates = ibans,
            BicCandidates = bics,
        };
    }

    private static IReadOnlyList<LocatedPaymentValue> DetectIbans(
        IReadOnlyList<PdfTextLine> lines)
    {
        var found = new List<LocatedPaymentValue>();

        foreach (PdfTextLine line in lines)
        {
            foreach (PdfTextSegment segment in line.Segments)
            {
                foreach (Match match in DetectionParsers.Iban().Matches(segment.Text))
                {
                    string candidate = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);

                    if (!Iban.TryParse(candidate, out Iban iban))
                    {
                        continue;
                    }

                    found.Add(new LocatedPaymentValue(
                        new DetectedValue<string>(
                            iban.Value, DetectionConfidence.High,
                            DetectionParsers.MaskIbans(segment.Text),
                            "Muster erkannt und Prüfsumme nach ISO 7064 bestätigt."),
                        line.PageNumber,
                        line.Top,
                        segment.Left));
                }
            }
        }

        return DistinctByValue(found);
    }

    private static IReadOnlyList<LocatedPaymentValue> DetectBics(
        IReadOnlyList<PdfTextLine> lines)
    {
        var found = new List<LocatedPaymentValue>();

        foreach (PdfTextLine line in lines)
        {
            foreach (PdfTextSegment segment in line.Segments)
            {
                string lower = segment.Text.ToLowerInvariant();

                if (!lower.Contains("bic", StringComparison.Ordinal)
                    && !lower.Contains("swift", StringComparison.Ordinal))
                {
                    continue;
                }

                Match match = DetectionParsers.Bic().Match(segment.Text);

                if (match.Success)
                {
                    found.Add(new LocatedPaymentValue(
                        new DetectedValue<string>(
                            match.Value, DetectionConfidence.Medium,
                            segment.Text, "Stand hinter \"BIC\"."),
                        line.PageNumber,
                        line.Top,
                        segment.Left));
                }
            }
        }

        return DistinctByValue(found);
    }

    private static IReadOnlyList<LocatedPaymentValue> DistinctByValue(
        IEnumerable<LocatedPaymentValue> values)
        => [.. values
            .GroupBy(value => value.Detection.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())];
}

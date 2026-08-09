using System.Text.RegularExpressions;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Erkennt Netto, Umsatzsteuer, Brutto, Zahlbetrag und die Steuersaetze.
///
/// Betraege werden ausschliesslich ueber Schluesselwoerter zugeordnet. Ein
/// Prozentwert gilt nur dann als Steuersatz, wenn die Zeile einen echten
/// Steuerbezug hat - sonst wuerde jeder Rabatt zur Umsatzsteuer.
/// </summary>
internal static class TotalsDetector
{
    /// <summary>
    /// Reihenfolge zaehlt: Die spezifischeren Begriffe stehen oben. "Zahlbetrag"
    /// muss vor "Betrag" greifen, sonst landet er in der falschen Summe.
    /// </summary>
    private static readonly (string[] Keywords, TotalKind Kind)[] Keywords =
    [
        (["zahlbetrag", "zu zahlen", "zahlungsbetrag"], TotalKind.Payable),
        (["gesamtbetrag", "rechnungsbetrag", "gesamtsumme", "brutto", "endbetrag", "gesamt brutto"], TotalKind.Gross),
        (["umsatzsteuer", "mehrwertsteuer", "mwst", "ust."], TotalKind.Tax),
        (["nettobetrag", "netto", "zwischensumme", "summe netto", "gesamt netto"], TotalKind.Net),
    ];

    private enum TotalKind { Net, Tax, Gross, Payable }

    public static DetectedTotals Detect(IReadOnlyList<PdfTextLine> lines)
    {
        var amounts = new Dictionary<TotalKind, DetectedValue<decimal>>();
        var rates = new List<DetectedValue<decimal>>();

        foreach (PdfTextLine line in lines)
        {
            CollectAmount(line, amounts);
            CollectVatRates(line, rates);
        }

        return new DetectedTotals
        {
            Net = Value(amounts, TotalKind.Net),
            Tax = Value(amounts, TotalKind.Tax),
            Gross = Value(amounts, TotalKind.Gross),
            Payable = Value(amounts, TotalKind.Payable),
            VatRates = rates,
        };
    }

    private static DetectedValue<decimal>? Value(
        Dictionary<TotalKind, DetectedValue<decimal>> amounts, TotalKind kind)
        => amounts.GetValueOrDefault(kind);

    private static void CollectAmount(
        PdfTextLine line, Dictionary<TotalKind, DetectedValue<decimal>> amounts)
    {
        foreach ((string[] keywords, TotalKind kind) in Keywords)
        {
            string? keyword = DetectionParsers.FirstKeywordIn(line.Text, keywords);

            if (keyword is null || !DetectionParsers.TryParseLastAmount(line.Text, out decimal amount))
            {
                continue;
            }

            // Die erste Fundstelle gewinnt: Summen stehen in Rechnungen unten,
            // spaetere Wiederholungen sind meist Uebertraege.
            amounts.TryAdd(kind, new DetectedValue<decimal>(
                amount, DetectionConfidence.High, line.Text,
                $"Stand in der Zeile mit \"{keyword}\"."));

            return;
        }
    }

    private static void CollectVatRates(PdfTextLine line, List<DetectedValue<decimal>> rates)
    {
        if (!HasTaxContext(line.Text))
        {
            return;
        }

        foreach (Match match in DetectionParsers.Percentage().Matches(line.Text))
        {
            if (!DetectionParsers.TryParseGermanDecimal(match.Groups["satz"].Value, out decimal rate)
                || rate is < 0 or > 30
                || rates.Any(r => r.Value == rate))
            {
                continue;
            }

            rates.Add(new DetectedValue<decimal>(
                rate, DetectionConfidence.Medium, line.Text,
                "Prozentwert in einer Zeile mit Steuerbezug."));
        }
    }

    /// <summary>
    /// Ein Prozentwert ist nur im Steuerzusammenhang ein Steuersatz. Rabatt,
    /// Skonto und Nachlass schliessen die Zeile ausdruecklich aus.
    /// </summary>
    private static bool HasTaxContext(string line)
    {
        string lower = line.ToLowerInvariant();

        bool mentionsTax =
            lower.Contains("umsatzsteuer", StringComparison.Ordinal)
            || lower.Contains("mehrwertsteuer", StringComparison.Ordinal)
            || lower.Contains("mwst", StringComparison.Ordinal)
            || lower.Contains("ust", StringComparison.Ordinal)
            || lower.Contains("steuersatz", StringComparison.Ordinal);

        bool mentionsDiscount =
            lower.Contains("rabatt", StringComparison.Ordinal)
            || lower.Contains("skonto", StringComparison.Ordinal)
            || lower.Contains("nachlass", StringComparison.Ordinal);

        return mentionsTax && !mentionsDiscount;
    }
}

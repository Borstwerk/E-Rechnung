using System.Text.RegularExpressions;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Erkennt Netto, Umsatzsteuer, Brutto, Zahlbetrag und die Steuersätze.
///
/// Beträge werden ausschließlich über Schlüsselwörter zugeordnet. Alle
/// Fundstellen werden zunächst als Kandidaten gesammelt. Dadurch kann ein
/// ausdrückliches "Gesamt Netto" einen früheren Einzelpreis schlagen, ohne
/// pauschal den letzten Betrag der Seite zu wählen.
/// </summary>
internal static partial class TotalsDetector
{
    private const double TotalsBlockMaximumHeightInPoints = 50;
    private const decimal ArithmeticTolerance = 0.02m;

    private static readonly TotalLabel[] Labels =
    [
        new("zahlbetrag", TotalKind.Payable, LabelStrength.Explicit),
        new("zu zahlen", TotalKind.Payable, LabelStrength.Explicit),
        new("zahlungsbetrag", TotalKind.Payable, LabelStrength.Explicit),

        new("gesamt brutto", TotalKind.Gross, LabelStrength.Explicit),
        new("gesamtbetrag", TotalKind.Gross, LabelStrength.Explicit),
        new("rechnungsbetrag", TotalKind.Gross, LabelStrength.Explicit),
        new("gesamtsumme", TotalKind.Gross, LabelStrength.Explicit),
        new("endbetrag", TotalKind.Gross, LabelStrength.Explicit),
        new("brutto", TotalKind.Gross, LabelStrength.Normal),

        new("umsatzsteuer", TotalKind.Tax, LabelStrength.Explicit),
        new("mehrwertsteuer", TotalKind.Tax, LabelStrength.Explicit),
        new("mwst", TotalKind.Tax, LabelStrength.Explicit),
        new("ust.", TotalKind.Tax, LabelStrength.Explicit),

        new("gesamt netto", TotalKind.Net, LabelStrength.Explicit),
        new("summe netto", TotalKind.Net, LabelStrength.Explicit),
        new("nettosumme", TotalKind.Net, LabelStrength.Explicit),
        new("nettobetrag", TotalKind.Net, LabelStrength.Explicit),
        new("zwischensumme", TotalKind.Net, LabelStrength.Normal),
        new("netto", TotalKind.Net, LabelStrength.Weak),
    ];

    private enum TotalKind { Net, Tax, Gross, Payable }

    private enum LabelStrength { Weak, Normal, Explicit }

    public static DetectedTotals Detect(IReadOnlyList<PdfTextLine> lines)
    {
        var candidates = new List<TotalCandidate>();
        var rates = new List<DetectedValue<decimal>>();

        foreach (PdfTextLine line in lines)
        {
            CollectAmount(line, candidates);
            CollectVatRates(line, rates);
        }

        return new DetectedTotals
        {
            Net = Select(candidates, TotalKind.Net),
            Tax = Select(candidates, TotalKind.Tax),
            Gross = Select(candidates, TotalKind.Gross),
            Payable = Select(candidates, TotalKind.Payable),
            VatRates = rates,
        };
    }

    private static void CollectAmount(
        PdfTextLine line, List<TotalCandidate> candidates)
    {
        string lower = line.Text.ToLowerInvariant();
        TotalLabel? label = Labels
            .Where(candidate => lower.Contains(candidate.Text, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Strength)
            .ThenByDescending(candidate => candidate.Text.Length)
            .FirstOrDefault();

        if (label is null
            || !DetectionParsers.TryParseLastAmount(line.Text, out decimal amount)
            || HasPositionOrUnitPriceContext(line.Text))
        {
            return;
        }

        candidates.Add(new TotalCandidate(
            label.Kind, amount, label.Text, label.Strength,
            line.PageNumber, line.Top, line.Text));
    }

    private static DetectedValue<decimal>? Select(
        IReadOnlyList<TotalCandidate> allCandidates, TotalKind kind)
    {
        TotalCandidate[] candidates = [.. allCandidates.Where(candidate => candidate.Kind == kind)];

        if (candidates.Length == 0)
        {
            return null;
        }

        LabelStrength strongest = candidates.Max(candidate => candidate.Strength);
        TotalCandidate[][] values =
        [
            .. candidates
                .Where(candidate => candidate.Strength == strongest)
                .GroupBy(candidate => candidate.Amount)
                .Select(group => group.ToArray()),
        ];

        TotalCandidate? selected;

        if (values.Length == 1)
        {
            selected = BestOccurrence(values[0], allCandidates);
        }
        else
        {
            var scored = values
                .Select(group => new
                {
                    Candidate = BestOccurrence(group, allCandidates),
                    Score = group.Max(candidate => CoherenceScore(candidate, allCandidates)),
                })
                .ToArray();
            int best = scored.Max(item => item.Score);
            var winners = scored.Where(item => item.Score == best).ToArray();

            selected = winners.Length == 1 ? winners[0].Candidate : null;
        }

        if (selected is null)
        {
            return null;
        }

        DetectionConfidence confidence = selected.Strength == LabelStrength.Weak
            ? DetectionConfidence.Medium
            : DetectionConfidence.High;

        return new DetectedValue<decimal>(
            selected.Amount, confidence, selected.Source,
            $"Deterministisch ausgewählte Summenfundstelle mit \"{selected.Keyword}\".");
    }

    private static TotalCandidate BestOccurrence(
        IReadOnlyList<TotalCandidate> candidates,
        IReadOnlyList<TotalCandidate> allCandidates)
        => candidates
            .OrderByDescending(candidate => CoherenceScore(candidate, allCandidates))
            .ThenBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Top)
            .First();

    private static int CoherenceScore(
        TotalCandidate candidate, IReadOnlyList<TotalCandidate> allCandidates)
    {
        TotalCandidate[] nearby =
        [
            .. allCandidates.Where(other =>
                other != candidate
                && other.PageNumber == candidate.PageNumber
                && other.Kind != candidate.Kind
                && Math.Abs(other.Top - candidate.Top) <= TotalsBlockMaximumHeightInPoints),
        ];

        int distinctKinds = nearby.Select(other => other.Kind).Distinct().Count();
        bool arithmetic = IsPartOfCoherentNetTaxGross(candidate, allCandidates);

        return distinctKinds + (arithmetic ? 3 : 0);
    }

    private static bool IsPartOfCoherentNetTaxGross(
        TotalCandidate candidate, IReadOnlyList<TotalCandidate> allCandidates)
    {
        TotalCandidate[] samePage =
        [
            .. allCandidates.Where(other => other.PageNumber == candidate.PageNumber),
        ];

        foreach (TotalCandidate net in samePage.Where(other => other.Kind == TotalKind.Net))
        {
            foreach (TotalCandidate tax in samePage.Where(other => other.Kind == TotalKind.Tax))
            {
                foreach (TotalCandidate gross in samePage.Where(other => other.Kind == TotalKind.Gross))
                {
                    double topSpan = new[] { net.Top, tax.Top, gross.Top }.Max()
                                     - new[] { net.Top, tax.Top, gross.Top }.Min();

                    if (topSpan <= TotalsBlockMaximumHeightInPoints
                        && Math.Abs(net.Amount + tax.Amount - gross.Amount) <= ArithmeticTolerance
                        && (candidate == net || candidate == tax || candidate == gross))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasPositionOrUnitPriceContext(string text)
    {
        string lower = text.ToLowerInvariant();

        return lower.Contains("/ je", StringComparison.Ordinal)
               || lower.Contains(" je ", StringComparison.Ordinal)
               || lower.Contains(" pro ", StringComparison.Ordinal)
               || lower.Contains("einzelpreis", StringComparison.Ordinal)
               || lower.Contains("stückpreis", StringComparison.Ordinal)
               || lower.Contains("stueckpreis", StringComparison.Ordinal)
               || lower.Contains("stundenpreis", StringComparison.Ordinal)
               || MultiplicationExpression().IsMatch(lower);
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
                || rates.Any(existing => existing.Value == rate))
            {
                continue;
            }

            rates.Add(new DetectedValue<decimal>(
                rate, DetectionConfidence.Medium, line.Text,
                "Prozentwert in einer Zeile mit Steuerbezug."));
        }
    }

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

    private sealed record TotalLabel(
        string Text, TotalKind Kind, LabelStrength Strength);

    private sealed record TotalCandidate(
        TotalKind Kind,
        decimal Amount,
        string Keyword,
        LabelStrength Strength,
        int PageNumber,
        double Top,
        string Source);

    [GeneratedRegex(@"\b\d+(?:[.,]\d+)?\s*[x×]\s*\d", RegexOptions.CultureInvariant)]
    private static partial Regex MultiplicationExpression();
}

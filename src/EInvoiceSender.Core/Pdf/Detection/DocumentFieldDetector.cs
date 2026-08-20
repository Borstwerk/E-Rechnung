using System.Text.RegularExpressions;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Die Kopfdaten eines Rechnungsdokuments.</summary>
public sealed record DetectedDocument
{
    public DetectedValue<string>? InvoiceNumber { get; init; }
    public DetectedValue<DateOnly>? IssueDate { get; init; }
    public DetectedValue<DateOnly>? DeliveryDate { get; init; }
    public DetectedValue<DateOnly>? BillingPeriodStart { get; init; }
    public DetectedValue<DateOnly>? BillingPeriodEnd { get; init; }
    public DetectedValue<DateOnly>? DueDate { get; init; }
    public DetectedValue<string>? Currency { get; init; }
}

/// <summary>
/// Erkennt Rechnungsnummer, Datumsangaben und Währung.
///
/// Alle Werte werden ausschließlich über Schlüsselwörter zugeordnet. Ein
/// Muster allein genügt nie: In einer Rechnung stehen Dutzende Ziffernfolgen,
/// von denen genau eine die Rechnungsnummer ist.
/// </summary>
internal static partial class DocumentFieldDetector
{
    private static readonly string[] InvoiceNumberKeywords =
    [
        "rechnungsnummer", "rechnungs-nr", "rechnungsnr", "rechnung nr", "rechnung-nr",
        "invoice number", "invoice no", "invoice-no", "invoice nr", "belegnummer",
    ];

    /// <summary>
    /// Wörter, die eine Zeile als Fundstelle der Rechnungsnummer ausschließen.
    ///
    /// Ohne diese Sperre würde in "Telefon 0381 123456 - Kundennummer 4711"
    /// irgendeine Ziffernfolge zur Rechnungsnummer. Solche Verwechslungen sind
    /// der häufigste Weg, auf dem eine automatische Erkennung Schaden
    /// anrichtet.
    /// </summary>
    private static readonly string[] NumberBlockers =
    [
        "telefon", "telefax", "tel.", "fax", "mobil", "ust-id", "ust.-id", "umsatzsteuer",
        "steuernummer", "steuer-nr", "kundennummer", "kunden-nr", "kundennr", "seite",
        "iban", "bic", "auftragsnummer", "bestellnummer", "lieferschein",
    ];

    private static readonly string[] IssueDateKeywords =
        ["rechnungsdatum", "rechnung vom", "datum der rechnung", "invoice date", "belegdatum"];

    private static readonly string[] DeliveryDateKeywords =
        ["leistungsdatum", "lieferdatum", "leistungszeitpunkt", "liefer- und leistungsdatum", "leistung am"];

    private static readonly string[] DueDateKeywords =
        ["fällig am", "fällig am", "zahlbar bis", "fälligkeit", "fälligkeit", "due date", "zahlungsziel"];

    private static readonly string[] BillingPeriodKeywords =
        ["leistungszeitraum", "abrechnungszeitraum", "leistungsperiode", "service period"];

    private static readonly string[] LocationDateBlockers =
    [
        "leistung", "liefer", "fällig", "zahlbar", "zahlungsziel",
        "vertrag", "bestellung", "auftrag", "beleg", "gültig",
    ];

    public static DetectedDocument Detect(IReadOnlyList<PdfTextLine> lines)
    {
        DetectedValue<string>? invoiceNumber = DetectInvoiceNumber(lines);
        DetectedValue<DateOnly>? issueDate = DetectDate(lines, IssueDateKeywords)
            ?? DetectLocationDate(lines, invoiceNumber is not null);
        (DetectedValue<DateOnly>? periodStart, DetectedValue<DateOnly>? periodEnd) =
            DetectBillingPeriod(lines);

        return new DetectedDocument
        {
            InvoiceNumber = invoiceNumber,
            IssueDate = issueDate,
            DeliveryDate = DetectDate(lines, DeliveryDateKeywords),
            BillingPeriodStart = periodStart,
            BillingPeriodEnd = periodEnd,
            DueDate = DetectDate(lines, DueDateKeywords),
            Currency = DetectCurrency(lines),
        };
    }

    private static DetectedValue<string>? DetectInvoiceNumber(IReadOnlyList<PdfTextLine> lines)
    {
        var candidates = new List<DetectedValue<string>>();

        foreach (PdfTextLine line in lines)
        {
            string? keyword = DetectionParsers.FirstKeywordIn(line.Text, InvoiceNumberKeywords);

            if (keyword is null || ContainsBlocker(line.Text))
            {
                continue;
            }

            Match match = DetectionParsers.ReferenceNumber()
                .Match(DetectionParsers.AfterKeyword(line.Text, keyword));

            if (!match.Success)
            {
                continue;
            }

            string value = match.Groups["nr"].Value.Trim();

            if (DetectionParsers.LooksLikeDate(value))
            {
                continue;
            }

            candidates.Add(new DetectedValue<string>(
                value, DetectionConfidence.High, line.Text,
                $"Stand unmittelbar hinter \"{keyword}\"."));
        }

        foreach (PdfTextLine line in lines)
        {
            if (ContainsBlocker(line.Text))
            {
                continue;
            }

            foreach (PdfTextSegment segment in line.Segments)
            {
                string text = segment.Text.Trim();

                if (!TryAfterInvoiceHeading(text, out string remainder))
                {
                    continue;
                }

                Match match = DetectionParsers.ReferenceNumber().Match(remainder);

                if (!match.Success || match.Index != 0)
                {
                    continue;
                }

                string value = match.Groups["nr"].Value.Trim();
                string trailing = remainder[match.Length..].Trim(' ', ':', '-', '.', '#');

                if (trailing.Length > 0
                    || !value.Any(char.IsDigit)
                    || DetectionParsers.LooksLikeDate(value))
                {
                    continue;
                }

                candidates.Add(new DetectedValue<string>(
                    value, DetectionConfidence.High, segment.Text,
                    "Eindeutige Rechnungsüberschrift mit unmittelbar folgender Referenz."));
            }
        }

        DetectedValue<string>[] distinct =
        [
            .. candidates
                .GroupBy(candidate => candidate.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()),
        ];

        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static bool TryAfterInvoiceHeading(string text, out string remainder)
    {
        const string anchor = "rechnung";

        if (!text.StartsWith(anchor, StringComparison.OrdinalIgnoreCase)
            || text.Length == anchor.Length)
        {
            remainder = string.Empty;

            return false;
        }

        char separator = text[anchor.Length];

        if (!char.IsWhiteSpace(separator) && separator is not ':' and not '-' and not '#')
        {
            remainder = string.Empty;

            return false;
        }

        remainder = text[anchor.Length..].TrimStart(' ', ':', '-', '#');

        return remainder.Length > 0;
    }

    private static bool ContainsBlocker(string line)
    {
        string lower = line.ToLowerInvariant();

        return NumberBlockers.Any(b => lower.Contains(b, StringComparison.Ordinal));
    }

    private static DetectedValue<DateOnly>? DetectDate(
        IReadOnlyList<PdfTextLine> lines, string[] keywords)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            PdfTextLine line = lines[index];
            string? keyword = DetectionParsers.FirstKeywordIn(line.Text, keywords);

            if (keyword is null)
            {
                continue;
            }

            if (DetectionParsers.TryParseFirstDate(
                    DetectionParsers.AfterKeyword(line.Text, keyword), out DateOnly sameLine))
            {
                return new DetectedValue<DateOnly>(
                    sameLine, DetectionConfidence.High, line.Text,
                    $"Stand unmittelbar hinter \"{keyword}\".");
            }

            // Manche Vorlagen setzen den Wert in die nächste Zeile. Der
            // Zusammenhang ist dann schwächer, deshalb nur mittlere Sicherheit.
            if (index + 1 < lines.Count
                && DetectionParsers.TryParseFirstDate(lines[index + 1].Text, out DateOnly nextLine))
            {
                return new DetectedValue<DateOnly>(
                    nextLine, DetectionConfidence.Medium, lines[index + 1].Text,
                    $"Stand in der Zeile unter \"{keyword}\".");
            }
        }

        return null;
    }

    private static DetectedValue<DateOnly>? DetectLocationDate(
        IReadOnlyList<PdfTextLine> lines, bool hasInvoiceAnchor)
    {
        if (!hasInvoiceAnchor)
        {
            return null;
        }

        var candidates = new List<(DateOnly Date, string Source)>();

        foreach (PdfTextSegment segment in lines.SelectMany(line => line.Segments))
        {
            string text = segment.Text.Trim();
            string lower = text.ToLowerInvariant();

            if (LocationDateBlockers.Any(blocker => lower.Contains(blocker, StringComparison.Ordinal)))
            {
                continue;
            }

            Match match = LocationDateLine().Match(text);

            if (match.Success
                && DetectionParsers.TryParseFirstDate(match.Groups["date"].Value, out DateOnly date))
            {
                candidates.Add((date, segment.Text));
            }
        }

        return candidates.Count == 1
            ? new DetectedValue<DateOnly>(
                candidates[0].Date, DetectionConfidence.Medium, candidates[0].Source,
                "Einzige plausible Orts- und Datumszeile im Rechnungsdokument.")
            : null;
    }

    private static (DetectedValue<DateOnly>? Start, DetectedValue<DateOnly>? End)
        DetectBillingPeriod(IReadOnlyList<PdfTextLine> lines)
    {
        var candidates = new List<(DateOnly Start, DateOnly End, string Source, string Keyword)>();

        foreach (PdfTextLine line in lines)
        {
            string? keyword = DetectionParsers.FirstKeywordIn(line.Text, BillingPeriodKeywords);

            if (keyword is null
                || !DetectionParsers.TryParseDateRange(
                    DetectionParsers.AfterKeyword(line.Text, keyword),
                    out DateOnly start, out DateOnly end))
            {
                continue;
            }

            candidates.Add((start, end, line.Text, keyword));
        }

        (DateOnly Start, DateOnly End, string Source, string Keyword)[] distinct =
        [
            .. candidates
                .GroupBy(candidate => (candidate.Start, candidate.End))
                .Select(group => group.First()),
        ];

        if (distinct.Length != 1)
        {
            return (null, null);
        }

        var candidate = distinct[0];
        string reason = $"Stand unmittelbar hinter \"{candidate.Keyword}\".";

        return (
            new DetectedValue<DateOnly>(
                candidate.Start, DetectionConfidence.High, candidate.Source, reason),
            new DetectedValue<DateOnly>(
                candidate.End, DetectionConfidence.High, candidate.Source, reason));
    }

    private static DetectedValue<string>? DetectCurrency(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (PdfTextLine line in lines)
        {
            if (line.Text.Contains('€', StringComparison.Ordinal)
                || line.Text.Contains("EUR", StringComparison.Ordinal))
            {
                return new DetectedValue<string>(
                    "EUR", DetectionConfidence.High, line.Text,
                    "Währungszeichen im Dokument gefunden.");
            }
        }

        return null;
    }

    [GeneratedRegex(
        @"^(?<place>[A-Za-zÄÖÜäöüß][A-Za-zÄÖÜäöüß.'\-\s]{1,60}),\s*(?<date>\d{1,2}\.\d{1,2}\.\d{4})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LocationDateLine();
}

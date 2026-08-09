using System.Text.RegularExpressions;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Die Kopfdaten eines Rechnungsdokuments.</summary>
public sealed record DetectedDocument
{
    public DetectedValue<string>? InvoiceNumber { get; init; }
    public DetectedValue<DateOnly>? IssueDate { get; init; }
    public DetectedValue<DateOnly>? DeliveryDate { get; init; }
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
internal static class DocumentFieldDetector
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

    public static DetectedDocument Detect(IReadOnlyList<PdfTextLine> lines) => new()
    {
        InvoiceNumber = DetectInvoiceNumber(lines),
        IssueDate = DetectDate(lines, IssueDateKeywords),
        DeliveryDate = DetectDate(lines, DeliveryDateKeywords),
        DueDate = DetectDate(lines, DueDateKeywords),
        Currency = DetectCurrency(lines),
    };

    private static DetectedValue<string>? DetectInvoiceNumber(IReadOnlyList<PdfTextLine> lines)
    {
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

            return new DetectedValue<string>(
                value, DetectionConfidence.High, line.Text,
                $"Stand unmittelbar hinter \"{keyword}\".");
        }

        return null;
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
}

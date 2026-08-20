using System.Globalization;
using System.Text.RegularExpressions;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Die kleinen, deterministischen Bausteine der Erkennung: Muster, Datums- und
/// Zahlenumwandlung, Maskierung.
///
/// Hier steht bewusst keine Fachlogik darüber, **welcher** Wert wohin gehört
/// – das entscheiden die einzelnen Detektoren. Diese Klasse beantwortet nur:
/// "Lässt sich diese Zeichenfolge als Datum lesen?"
/// </summary>
internal static partial class DetectionParsers
{
    /// <summary>
    /// Liest das erste Datum aus einem Textstück. Unterstützt die
    /// gebräuchlichen deutschen Schreibweisen und ISO-Datumswerte.
    /// </summary>
    public static bool TryParseFirstDate(string text, out DateOnly value)
    {
        Match german = GermanDate().Match(text);

        if (german.Success)
        {
            int day = int.Parse(german.Groups["d"].Value, CultureInfo.InvariantCulture);
            int month = int.Parse(german.Groups["m"].Value, CultureInfo.InvariantCulture);
            int year = int.Parse(german.Groups["y"].Value, CultureInfo.InvariantCulture);

            // Zweistellige Jahreszahlen: 26 meint 2026, nicht 1926.
            if (year < 100)
            {
                year += 2000;
            }

            if (month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                value = new DateOnly(year, month, day);

                return true;
            }
        }

        Match iso = IsoDate().Match(text);

        if (iso.Success
            && DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out value))
        {
            return true;
        }

        value = default;

        return false;
    }

    /// <summary>Enthält der Text überhaupt ein Datum?</summary>
    public static bool LooksLikeDate(string text)
        => GermanDate().IsMatch(text) || IsoDate().IsMatch(text);

    /// <summary>
    /// Liest einen ausdrücklich dargestellten Zeitraum. Neben zwei
    /// vollständigen Daten wird die in Rechnungen übliche Kurzform
    /// <c>01.04. - 07.04. 2026</c> unterstützt. Das einzige angegebene Jahr
    /// gilt dabei nur dann für beide Grenzen, wenn im selben Jahr ein
    /// vorwärts laufender Zeitraum entsteht.
    /// </summary>
    public static bool TryParseDateRange(
        string text, out DateOnly start, out DateOnly end)
    {
        Match full = FullDateRange().Match(text);

        if (full.Success
            && TryCreateDate(full, "sd", "sm", "sy", out start)
            && TryCreateDate(full, "ed", "em", "ey", out end)
            && start <= end)
        {
            return true;
        }

        Match sharedYear = SharedYearDateRange().Match(text);

        if (sharedYear.Success
            && int.TryParse(sharedYear.Groups["y"].Value, CultureInfo.InvariantCulture, out int year)
            && TryCreateDate(sharedYear, "sd", "sm", year, out start)
            && TryCreateDate(sharedYear, "ed", "em", year, out end)
            && start <= end)
        {
            return true;
        }

        start = default;
        end = default;

        return false;
    }

    private static bool TryCreateDate(
        Match match, string dayGroup, string monthGroup, string yearGroup,
        out DateOnly value)
    {
        if (int.TryParse(
                match.Groups[yearGroup].Value, CultureInfo.InvariantCulture, out int year))
        {
            return TryCreateDate(match, dayGroup, monthGroup, year, out value);
        }

        value = default;

        return false;
    }

    private static bool TryCreateDate(
        Match match, string dayGroup, string monthGroup, int year,
        out DateOnly value)
    {
        if (int.TryParse(match.Groups[dayGroup].Value, CultureInfo.InvariantCulture, out int day)
            && int.TryParse(match.Groups[monthGroup].Value, CultureInfo.InvariantCulture, out int month)
            && month is >= 1 and <= 12
            && day >= 1
            && day <= DateTime.DaysInMonth(year, month))
        {
            value = new DateOnly(year, month, day);

            return true;
        }

        value = default;

        return false;
    }

    /// <summary>
    /// Liest den letzten Betrag einer Zeile.
    ///
    /// In Summenzeilen steht der Wert rechts. Prozentangaben werden vorher
    /// entfernt, sonst gewänne in "Umsatzsteuer 19 % 190,00" die 19.
    /// </summary>
    public static bool TryParseLastAmount(string text, out decimal value)
    {
        string withoutPercentages = Percentage().Replace(text, " ");
        MatchCollection matches = Amount().Matches(withoutPercentages);

        if (matches.Count == 0)
        {
            value = 0m;

            return false;
        }

        return TryParseGermanDecimal(matches[^1].Groups["betrag"].Value, out value);
    }

    /// <summary>Liest eine Zahl in deutscher Schreibweise (1.234,56).</summary>
    public static bool TryParseGermanDecimal(string text, out decimal value)
    {
        string normalized = text.Trim()
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');

        return decimal.TryParse(
            normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Maskiert IBAN-ähnliche Zeichenfolgen in einem Text, der angezeigt oder
    /// protokolliert wird.
    /// </summary>
    public static string MaskIbans(string text)
        => Iban().Replace(text, m =>
        {
            string compact = m.Value.Replace(" ", string.Empty, StringComparison.Ordinal);

            return compact.Length <= 8
                ? compact
                : compact[..4] + new string('*', compact.Length - 8) + compact[^4..];
        });

    /// <summary>
    /// Liefert den Text hinter dem ersten Vorkommen eines Schlüsselworts.
    /// Nur dieser Teil kommt als Wert in Frage.
    /// </summary>
    public static string AfterKeyword(string line, string keyword)
    {
        int index = line.ToLowerInvariant().IndexOf(keyword, StringComparison.Ordinal);

        return index < 0 ? line : line[(index + keyword.Length)..];
    }

    /// <summary>Findet das erste in der Zeile enthaltene Schlüsselwort.</summary>
    public static string? FirstKeywordIn(string line, IReadOnlyList<string> keywords)
    {
        string lower = line.ToLowerInvariant();

        return keywords.FirstOrDefault(k => lower.Contains(k, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\b(?<d>\d{1,2})\.(?<m>\d{1,2})\.(?<y>\d{2}|\d{4})\b", RegexOptions.CultureInvariant)]
    public static partial Regex GermanDate();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
    public static partial Regex IsoDate();

    [GeneratedRegex(
        @"\b(?<sd>\d{1,2})\.(?<sm>\d{1,2})\.\s*(?<sy>\d{4})\s*[-–—]\s*(?<ed>\d{1,2})\.(?<em>\d{1,2})\.\s*(?<ey>\d{4})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex FullDateRange();

    [GeneratedRegex(
        @"\b(?<sd>\d{1,2})\.(?<sm>\d{1,2})\.?\s*[-–—]\s*(?<ed>\d{1,2})\.(?<em>\d{1,2})\.\s*(?<y>\d{4})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex SharedYearDateRange();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:\s?[A-Z0-9]{4}){2,7}\s?[A-Z0-9]{0,4}\b", RegexOptions.CultureInvariant)]
    public static partial Regex Iban();

    [GeneratedRegex(@"\b[A-Z]{6}[A-Z0-9]{2}(?:[A-Z0-9]{3})?\b", RegexOptions.CultureInvariant)]
    public static partial Regex Bic();

    [GeneratedRegex(@"(?<betrag>\d{1,3}(?:\.\d{3})+,\d{2}|\d+,\d{2})", RegexOptions.CultureInvariant)]
    public static partial Regex Amount();

    [GeneratedRegex(@"(?<satz>\d{1,2}(?:,\d{1,2})?)\s*%", RegexOptions.CultureInvariant)]
    public static partial Regex Percentage();

    [GeneratedRegex(@"[:\s]*(?<nr>[A-Za-z0-9][A-Za-z0-9\-/_.]{2,29})", RegexOptions.CultureInvariant)]
    public static partial Regex ReferenceNumber();

    [GeneratedRegex(@"\b(?<plz>\d{5})\s+(?<ort>[A-Za-zÄÖÜäöüß][A-Za-zÄÖÜäöüß\-.\s]{1,40})$", RegexOptions.CultureInvariant)]
    public static partial Regex PostalCodeAndCity();

    [GeneratedRegex(@"^[A-Za-zÄÖÜäöüß][A-Za-zÄÖÜäöüß\-.\s]{2,40}\s\d+[a-zA-Z]?$", RegexOptions.CultureInvariant)]
    public static partial Regex Street();
}

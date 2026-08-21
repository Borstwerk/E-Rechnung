using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Eine vollständig erkannte Rechnungsposition. Der explizite Gesamtpreis
/// bleibt reine Evidenz und wird nicht zu einem zweiten Rechnungsfeld.
/// </summary>
internal sealed record DetectedInvoiceLine(
    int Number,
    string Name,
    string? Description,
    decimal Quantity,
    string UnitCode,
    decimal NetUnitPrice,
    decimal? ExplicitLineTotal,
    VatCategory VatCategory,
    decimal VatRate,
    IReadOnlyList<string> Evidence);

/// <summary>Das isolierte Phase-A-Ergebnis einer vollständigen Tabelle.</summary>
internal sealed record PositionDetectionResult
{
    public static PositionDetectionResult Empty { get; } = new();

    public IReadOnlyList<DetectedInvoiceLine> Lines { get; init; } = [];
    public int? PageNumber { get; init; }
    public string? HeaderText { get; init; }

    public bool HasPositions => Lines.Count > 0;
}

/// <summary>
/// Erkennt ausschließlich eine eng begrenzte, einseitige Positionstabelle.
/// Die Klasse ist in Phase A absichtlich nicht mit dem produktiven
/// <see cref="InvoiceDataDetector"/> verbunden.
/// </summary>
internal static partial class PositionDetector
{
    private const decimal DocumentTotalTolerance = 0.02m;
    private const double MaximumDescriptionContinuationDistanceInPoints = 24d;

    private static readonly FrozenDictionary<string, string> UnitMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["c62"] = "C62",
            ["stk"] = "C62",
            ["stueck"] = "C62",
            ["stück"] = "C62",
            ["hur"] = "HUR",
            ["h"] = "HUR",
            ["std"] = "HUR",
            ["stunde"] = "HUR",
            ["stunden"] = "HUR",
            ["kgm"] = "KGM",
            ["kg"] = "KGM",
            ["kilogramm"] = "KGM",
            ["mtr"] = "MTR",
            ["m"] = "MTR",
            ["meter"] = "MTR",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly HeaderAlias[] HeaderAliases =
    [
        Alias(ColumnRole.Position, "pos"),
        Alias(ColumnRole.Position, "position"),
        Alias(ColumnRole.ArticleNumber, "artnr"),
        Alias(ColumnRole.ArticleNumber, "artikelnummer"),
        Alias(ColumnRole.ArticleNumber, "artikel", "nr"),
        Alias(ColumnRole.Description, "beschreibung"),
        Alias(ColumnRole.Description, "bezeichnung"),
        Alias(ColumnRole.Description, "leistung"),
        Alias(ColumnRole.Quantity, "menge"),
        Alias(ColumnRole.Quantity, "anzahl"),
        Alias(ColumnRole.Unit, "einheit"),
        Alias(ColumnRole.Unit, "einh"),
        Alias(ColumnRole.UnitPrice, "einzelpreis"),
        Alias(ColumnRole.UnitPrice, "ep"),
        Alias(ColumnRole.UnitPrice, "nettoeinzelpreis"),
        Alias(ColumnRole.UnitPrice, "netto", "einzelpreis"),
        Alias(ColumnRole.UnitPrice, "preis"),
        Alias(ColumnRole.LineTotal, "gesamt"),
        Alias(ColumnRole.LineTotal, "gesamtpreis"),
        Alias(ColumnRole.LineTotal, "gesamt", "preis"),
        Alias(ColumnRole.LineTotal, "betrag"),
        Alias(ColumnRole.Vat, "mwst"),
        Alias(ColumnRole.Vat, "mwst", "%"),
        Alias(ColumnRole.Vat, "ust"),
        Alias(ColumnRole.Vat, "ust", "%"),
        Alias(ColumnRole.Vat, "steuersatz"),
        Alias(ColumnRole.Vat, "steuersatz", "%"),
        Alias(ColumnRole.Vat, "%"),
    ];

    private static readonly string[][] UnsupportedHeaderAliases =
    [
        ["rabatt"],
        ["nachlass"],
        ["zuschlag"],
        ["preisbasismenge"],
    ];

    private static readonly string[] EndMarkers =
    [
        "gesamt netto",
        "summe netto",
        "nettosumme",
        "nettobetrag",
        "zwischensumme",
        "umsatzsteuer",
        "mehrwertsteuer",
        "gesamt brutto",
        "gesamtbetrag",
        "rechnungsbetrag",
        "gesamtsumme",
        "zahlbetrag",
        "zu zahlen",
    ];

    private static readonly string[] DescriptionExclusions =
    [
        "bankverbindung",
        "iban",
        "bic",
        "swift",
        "rechnungsnummer",
        "rechnungsnr",
        "rechnungsdatum",
        "bestellnummer",
        "bestellnr",
        "kundennummer",
        "kundennr",
        "leistungsdatum",
        "fälligkeit",
        "fälligkeitsdatum",
        "zahlungsziel",
        "steuernummer",
        "ust-id",
        "ust id",
        "zahlungsbedingungen",
        "vielen dank",
    ];

    public static PositionDetectionResult Detect(
        IReadOnlyList<PdfTextLine> lines,
        DetectedTotals totals)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(totals);

        if (lines.Count == 0
            || lines.Select(line => line.PageNumber).Distinct().Count() != 1)
        {
            return PositionDetectionResult.Empty;
        }

        HeaderLayout[] headers =
        [
            .. lines.Select(TryBuildHeader)
                .Where(header => header is not null)
                .Cast<HeaderLayout>(),
        ];

        if (headers.Length != 1)
        {
            return PositionDetectionResult.Empty;
        }

        HeaderLayout header = headers[0];
        decimal? documentVatRate = ResolveDocumentVatRate(header, totals);

        if (!header.Has(ColumnRole.Vat) && documentVatRate is null)
        {
            return PositionDetectionResult.Empty;
        }

        PdfTextLine[] following =
        [
            .. lines.Where(line => line.PageNumber == header.Line.PageNumber
                                   && line.Top > header.Line.Top)
                .OrderBy(line => line.Top),
        ];

        var detected = new List<DetectedInvoiceLine>();
        var numbers = new HashSet<int>();
        int boundaryIndex = -1;
        double lastConsumedTop = header.Line.Top;

        for (int index = 0; index < following.Length; index++)
        {
            PdfTextLine line = following[index];

            if (IsEndBoundary(line.Text))
            {
                boundaryIndex = index;
                break;
            }

            if (TryReadPrimaryLine(
                    line, header, documentVatRate, detected.Count + 1,
                    out DetectedInvoiceLine item))
            {
                if (!numbers.Add(item.Number))
                {
                    return PositionDetectionResult.Empty;
                }

                detected.Add(item);
                lastConsumedTop = line.Top;
                continue;
            }

            if (detected.Count > 0
                && TryReadDescriptionContinuation(
                    line, header, lastConsumedTop, out string continuation))
            {
                DetectedInvoiceLine previous = detected[^1];
                string description = string.IsNullOrWhiteSpace(previous.Description)
                    ? continuation
                    : previous.Description + " " + continuation;

                detected[^1] = previous with
                {
                    Description = description,
                    Evidence = [.. previous.Evidence, line.Text],
                };
                lastConsumedTop = line.Top;
                continue;
            }

            // Zwischen Kopf und Summengrenze wird keine inhaltliche Zeile
            // übersprungen. Was weder Position noch Fortsetzung ist, macht
            // die scheinbar vollständige Tabelle unzuverlässig.
            return PositionDetectionResult.Empty;
        }

        if (detected.Count == 0 || boundaryIndex < 0)
        {
            return PositionDetectionResult.Empty;
        }

        // Eine Zwischensumme darf keine späteren Positionen verstecken. Auch
        // ein zweiter Tabellenkörper ohne eigenen Kopf wird so verworfen.
        foreach (PdfTextLine line in following.Skip(boundaryIndex + 1))
        {
            if (TryReadPrimaryLine(
                    line, header, documentVatRate, detected.Count + 1, out _))
            {
                return PositionDetectionResult.Empty;
            }
        }

        if (!PassesDocumentTotalsGate(detected, totals))
        {
            return PositionDetectionResult.Empty;
        }

        return new PositionDetectionResult
        {
            Lines = detected,
            PageNumber = header.Line.PageNumber,
            HeaderText = header.Line.Text,
        };
    }

    private static HeaderLayout? TryBuildHeader(PdfTextLine line)
    {
        PdfTextToken[] tokens = [.. line.Tokens.OrderBy(token => token.Left)];

        if (tokens.Length == 0
            || tokens.Any(token => token.Right <= token.Left))
        {
            return null;
        }

        string[] normalized = [.. tokens.Select(token => NormalizeHeaderToken(token.Text))];

        if (UnsupportedHeaderAliases.Any(alias => ContainsSequence(normalized, alias)))
        {
            return null;
        }

        var selected = new List<HeaderSpan>();

        foreach (ColumnRole role in Enum.GetValues<ColumnRole>())
        {
            HeaderSpan[] matches = FindHeaderMatches(role, tokens, normalized);

            if (matches.Length == 0)
            {
                continue;
            }

            int longest = matches.Max(match => match.EndIndex - match.StartIndex);
            HeaderSpan[] longestMatches =
            [
                .. matches.Where(match => match.EndIndex - match.StartIndex == longest)
                    .DistinctBy(match => (match.StartIndex, match.EndIndex)),
            ];

            if (longestMatches.Length != 1)
            {
                return null;
            }

            selected.Add(longestMatches[0]);
        }

        ColumnRole[] mandatory =
        [ColumnRole.Description, ColumnRole.Quantity, ColumnRole.Unit, ColumnRole.UnitPrice];

        if (mandatory.Any(role => selected.Count(span => span.Role == role) != 1))
        {
            return null;
        }

        if (selected.SelectMany(span => Enumerable.Range(
                span.StartIndex, span.EndIndex - span.StartIndex + 1))
            .GroupBy(index => index)
            .Any(group => group.Count() != 1))
        {
            return null;
        }

        var covered = selected.SelectMany(span => Enumerable.Range(
            span.StartIndex, span.EndIndex - span.StartIndex + 1)).ToHashSet();

        if (covered.Count != tokens.Length)
        {
            return null;
        }

        HeaderSpan[] ordered = [.. selected.OrderBy(span => span.Left)];

        if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.Right >= pair.Second.Left)
            || !HasSupportedOrder(ordered))
        {
            return null;
        }

        HeaderSpan unitPrice = ordered.Single(span => span.Role == ColumnRole.UnitPrice);

        if (unitPrice.NormalizedText == "preis"
            && ordered.All(span => span.Role != ColumnRole.LineTotal))
        {
            return null;
        }

        var columns = new List<HeaderColumn>(ordered.Length);

        for (int index = 0; index < ordered.Length; index++)
        {
            double minimum = index == 0
                ? double.NegativeInfinity
                : Midpoint(ordered[index - 1].Right, ordered[index].Left);
            double maximum = index == ordered.Length - 1
                ? double.PositiveInfinity
                : Midpoint(ordered[index].Right, ordered[index + 1].Left);

            columns.Add(new HeaderColumn(ordered[index].Role, minimum, maximum));
        }

        return new HeaderLayout(line, columns);
    }

    private static HeaderSpan[] FindHeaderMatches(
        ColumnRole role,
        PdfTextToken[] tokens,
        string[] normalized)
    {
        var matches = new List<HeaderSpan>();

        foreach (HeaderAlias alias in HeaderAliases.Where(alias => alias.Role == role))
        {
            for (int start = 0; start <= normalized.Length - alias.Tokens.Length; start++)
            {
                if (!alias.Tokens.Select((value, offset) => normalized[start + offset] == value).All(v => v))
                {
                    continue;
                }

                int end = start + alias.Tokens.Length - 1;
                matches.Add(new HeaderSpan(
                    role, start, end, tokens[start].Left, tokens[end].Right,
                    string.Join("", alias.Tokens)));
            }
        }

        // Bei „MwSt %“ ist das Prozentzeichen nur Teil derselben Beschriftung,
        // kein zweiter plausibler VAT-Bereich.
        if (role == ColumnRole.Vat
            && matches.Any(match => match.NormalizedText != "%"))
        {
            matches.RemoveAll(match => match.NormalizedText == "%");
        }

        // In „EP ... Gesamt Preis“ bezeichnet das zweite Wort nicht noch
        // einmal den Einzelpreis. Ein vorhandener spezifischer Preisheader
        // schlägt deshalb den bloßen Resttoken „Preis“.
        if (role == ColumnRole.UnitPrice
            && matches.Any(match => match.NormalizedText != "preis"))
        {
            matches.RemoveAll(match => match.NormalizedText == "preis");
        }

        return [.. matches];
    }

    private static bool HasSupportedOrder(IReadOnlyList<HeaderSpan> columns)
    {
        int description = IndexOf(columns, ColumnRole.Description);
        int quantity = IndexOf(columns, ColumnRole.Quantity);
        int unit = IndexOf(columns, ColumnRole.Unit);
        int price = IndexOf(columns, ColumnRole.UnitPrice);

        if (!(description < quantity && quantity < unit && unit < price))
        {
            return false;
        }

        if (columns.Any(column => (column.Role is ColumnRole.Position or ColumnRole.ArticleNumber)
                                  && IndexOf(columns, column.Role) > description))
        {
            return false;
        }

        return columns
            .Where(column => column.Role is ColumnRole.LineTotal or ColumnRole.Vat)
            .All(column => IndexOf(columns, column.Role) > price);
    }

    private static bool TryReadPrimaryLine(
        PdfTextLine line,
        HeaderLayout header,
        decimal? documentVatRate,
        int fallbackNumber,
        out DetectedInvoiceLine detected)
    {
        detected = null!;

        if (!TryAssignCells(line, header, out IReadOnlyDictionary<ColumnRole, string> cells))
        {
            return false;
        }

        string name = Cell(cells, ColumnRole.Description);
        string quantityText = Cell(cells, ColumnRole.Quantity);
        string unitText = Cell(cells, ColumnRole.Unit);
        string priceText = Cell(cells, ColumnRole.UnitPrice);

        if (name.Length == 0 || quantityText.Length == 0
            || unitText.Length == 0 || priceText.Length == 0
            || !TryParseQuantity(quantityText, out decimal quantity)
            || quantity <= 0m
            || !TryMapUnit(unitText, out string unitCode)
            || !TryParseMoneyCell(priceText, out decimal unitPrice)
            || unitPrice < 0m)
        {
            return false;
        }

        int number = fallbackNumber;

        if (header.Has(ColumnRole.Position)
            && (!int.TryParse(Cell(cells, ColumnRole.Position), NumberStyles.None,
                              CultureInfo.InvariantCulture, out number)
                || number <= 0))
        {
            return false;
        }

        decimal vatRate;

        if (header.Has(ColumnRole.Vat))
        {
            if (!TryParseVatRate(Cell(cells, ColumnRole.Vat), out vatRate))
            {
                return false;
            }
        }
        else if (documentVatRate is { } rate)
        {
            vatRate = rate;
        }
        else
        {
            return false;
        }

        decimal? explicitTotal = null;

        if (header.Has(ColumnRole.LineTotal))
        {
            if (!TryParseMoneyCell(Cell(cells, ColumnRole.LineTotal), out decimal parsedTotal)
                || parsedTotal != Amounts.Round(quantity * unitPrice))
            {
                return false;
            }

            explicitTotal = parsedTotal;
        }

        detected = new DetectedInvoiceLine(
            number,
            name,
            null,
            quantity,
            unitCode,
            unitPrice,
            explicitTotal,
            VatCategory.StandardRate,
            vatRate,
            [header.Line.Text, line.Text]);

        return true;
    }

    private static bool TryReadDescriptionContinuation(
        PdfTextLine line,
        HeaderLayout header,
        double previousTop,
        out string continuation)
    {
        continuation = string.Empty;

        double distance = line.Top - previousTop;

        if (distance <= 0d
            || distance > MaximumDescriptionContinuationDistanceInPoints
            || DescriptionExclusions.Any(marker =>
                line.Text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!TryAssignCells(line, header, out IReadOnlyDictionary<ColumnRole, string> cells))
        {
            return false;
        }

        string description = Cell(cells, ColumnRole.Description);

        if (description.Length == 0
            || cells.Any(cell => cell.Key != ColumnRole.Description
                                 && cell.Value.Length > 0))
        {
            return false;
        }

        continuation = description;
        return true;
    }

    private static bool TryAssignCells(
        PdfTextLine line,
        HeaderLayout header,
        out IReadOnlyDictionary<ColumnRole, string> cells)
    {
        var assigned = header.Columns.ToDictionary(column => column.Role, _ => new List<PdfTextToken>());

        foreach (PdfTextToken token in line.Tokens.OrderBy(token => token.Left))
        {
            HeaderColumn[] matching =
            [
                .. header.Columns.Where(column => token.Left >= column.Minimum
                                                   && token.Right <= column.Maximum),
            ];

            if (matching.Length != 1)
            {
                cells = null!;
                return false;
            }

            assigned[matching[0].Role].Add(token);
        }

        cells = assigned.ToDictionary(
            pair => pair.Key,
            pair => string.Join(" ", pair.Value.Select(token => token.Text)).Trim());
        return true;
    }

    private static decimal? ResolveDocumentVatRate(
        HeaderLayout header,
        DetectedTotals totals)
    {
        if (header.Has(ColumnRole.Vat))
        {
            return null;
        }

        if (totals.VatRates.Count != 1
            || totals.VatRates[0].Confidence != DetectionConfidence.High
            || totals.VatRates[0].Value is not (7m or 19m))
        {
            return null;
        }

        return totals.VatRates[0].Value;
    }

    private static bool PassesDocumentTotalsGate(
        List<DetectedInvoiceLine> detected,
        DetectedTotals document)
    {
        var lines = new List<InvoiceLine>(detected.Count);

        foreach (DetectedInvoiceLine item in detected)
        {
            if (!UnitCode.TryParse(item.UnitCode, out UnitCode unit))
            {
                return false;
            }

            lines.Add(new InvoiceLine(
                item.Number, item.Name, item.Quantity, unit, item.NetUnitPrice,
                item.VatCategory, item.VatRate, item.Description));
        }

        InvoiceTotals calculated = InvoiceCalculator.Calculate(lines, [], 0m, 0m);
        bool hasHighNet = IsHigh(document.Net);
        bool hasHighTaxAndGross = IsHigh(document.Tax) && IsHigh(document.Gross);

        if (!hasHighNet && !hasHighTaxAndGross)
        {
            return false;
        }

        return MatchesIfHigh(document.Net, calculated.TaxBasisTotal)
               && MatchesIfHigh(document.Tax, calculated.TaxTotal)
               && MatchesIfHigh(document.Gross, calculated.GrandTotal)
               && MatchesIfHigh(document.Payable, calculated.DuePayableAmount);
    }

    private static bool IsHigh(DetectedValue<decimal>? value)
        => value?.Confidence == DetectionConfidence.High;

    private static bool MatchesIfHigh(DetectedValue<decimal>? value, decimal calculated)
        => !IsHigh(value) || Math.Abs(value!.Value - calculated) <= DocumentTotalTolerance;

    private static bool TryMapUnit(string text, out string code)
    {
        string normalized = NormalizeUnit(text);

        if (UnitMappings.TryGetValue(normalized, out string? mapped)
            && mapped is not null
            && UnitCodeList.IsValid(mapped))
        {
            code = mapped;
            return true;
        }

        code = string.Empty;
        return false;
    }

    private static bool TryParseQuantity(string text, out decimal value)
    {
        value = 0m;

        return GermanQuantity().IsMatch(text.Trim())
               && DetectionParsers.TryParseGermanDecimal(text, out value);
    }

    private static bool TryParseMoneyCell(string text, out decimal value)
    {
        Match match = MoneyCell().Match(text.Trim());

        if (!match.Success)
        {
            value = 0m;
            return false;
        }

        return DetectionParsers.TryParseGermanDecimal(match.Groups["betrag"].Value, out value);
    }

    private static bool TryParseVatRate(string text, out decimal value)
    {
        Match match = VatCell().Match(text.Trim());

        if (!match.Success
            || !DetectionParsers.TryParseGermanDecimal(match.Groups["satz"].Value, out value))
        {
            value = 0m;
            return false;
        }

        return value is 7m or 19m;
    }

    private static bool IsEndBoundary(string text)
    {
        string lower = text.ToLowerInvariant();
        return EndMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    private static string Cell(IReadOnlyDictionary<ColumnRole, string> cells, ColumnRole role)
        => cells.GetValueOrDefault(role, string.Empty);

    private static string NormalizeHeaderToken(string text)
    {
        string trimmed = text.Trim().ToLowerInvariant();

        if (trimmed == "%")
        {
            return trimmed;
        }

        return string.Concat(trimmed.Where(char.IsLetterOrDigit));
    }

    private static string NormalizeUnit(string text)
        => string.Concat(text.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit));

    private static bool ContainsSequence(string[] values, string[] expected)
    {
        for (int start = 0; start <= values.Length - expected.Length; start++)
        {
            if (expected.Select((value, offset) => values[start + offset] == value).All(v => v))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(IReadOnlyList<HeaderSpan> columns, ColumnRole role)
        => Enumerable.Range(0, columns.Count).Single(index => columns[index].Role == role);

    private static double Midpoint(double left, double right) => left + ((right - left) / 2d);

    private static HeaderAlias Alias(ColumnRole role, params string[] tokens) => new(role, tokens);

    private enum ColumnRole
    {
        Position,
        ArticleNumber,
        Description,
        Quantity,
        Unit,
        UnitPrice,
        LineTotal,
        Vat,
    }

    private sealed record HeaderAlias(ColumnRole Role, string[] Tokens);

    private sealed record HeaderSpan(
        ColumnRole Role,
        int StartIndex,
        int EndIndex,
        double Left,
        double Right,
        string NormalizedText);

    private sealed record HeaderColumn(ColumnRole Role, double Minimum, double Maximum);

    private sealed record HeaderLayout(PdfTextLine Line, IReadOnlyList<HeaderColumn> Columns)
    {
        public bool Has(ColumnRole role) => Columns.Any(column => column.Role == role);
    }

    [GeneratedRegex(
        @"^[+]?(?:\d{1,3}(?:\.\d{3})+|\d+)(?:,\d{1,4})?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GermanQuantity();

    [GeneratedRegex(
        @"^(?<betrag>(?:\d{1,3}(?:\.\d{3})+|\d+),\d{2})(?:\s*(?:€|EUR))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyCell();

    [GeneratedRegex(
        @"^(?<satz>7|19)(?:,0+)?\s*%?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex VatCell();
}

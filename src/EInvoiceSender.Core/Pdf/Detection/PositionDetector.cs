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
/// <param name="UnitCode">
/// Die Mengeneinheit – oder <see langword="null"/>, wenn die Rechnung gar
/// keine nennt.
///
/// **Diese drei Fälle dürfen nie zusammenfallen:** eine vorhandene und
/// verstandene Einheit ergibt einen Code; eine im Dokument nicht vorhandene
/// Einheit ergibt <see langword="null"/>; eine vorhandene, aber nicht
/// unterstützte Einheit verwirft die gesamte Tabelle und erreicht diesen Typ
/// nie. Ein Platzhalter wie <c>"XXX"</c> oder ein stiller <c>"C62"</c> würde
/// den zweiten und den dritten Fall verwechseln – und zwar in der Richtung,
/// die eine falsche Rechnung erzeugt.
/// </param>
internal sealed record DetectedInvoiceLine(
    int Number,
    string Name,
    string? Description,
    decimal Quantity,
    string? UnitCode,
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
/// Erkennt ausschließlich eine eng begrenzte Positionstabelle, die
/// vollständig auf einer PDF-Seite liegt. Weitere Dokumentseiten dürfen
/// Begleittext, aber keinen zweiten oder fortgesetzten Tabellenkörper tragen.
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
        Alias(ColumnRole.Description, "artikelbeschreibung"),
        Alias(ColumnRole.Description, "namebeschreibung"),
        Alias(ColumnRole.Quantity, "menge"),
        Alias(ColumnRole.Quantity, "anzahl"),
        Alias(ColumnRole.Unit, "einheit"),
        Alias(ColumnRole.Unit, "einh"),

        // Lieferscheinnahe Rechnungen führen ein Lieferdatum je Position. Die
        // Spalte erzeugt kein Rechnungsfeld – sie muss nur benannt sein, damit
        // die Spaltengrenzen eindeutig bleiben. Das ist ausdrücklich keine
        // allgemeine Nachsicht gegenüber unbekannten Spalten: Jede andere
        // unbekannte Beschriftung verwirft die Tabelle weiterhin.
        Alias(ColumnRole.DeliveryDate, "lieferdatum"),

        Alias(ColumnRole.UnitPrice, "einzelpreis"),
        Alias(ColumnRole.UnitPrice, "ep"),
        Alias(ColumnRole.UnitPrice, "epreis"),
        Alias(ColumnRole.UnitPrice, "nettoeinzelpreis"),
        Alias(ColumnRole.UnitPrice, "netto", "einzelpreis"),
        Alias(ColumnRole.UnitPrice, "preis"),
        Alias(ColumnRole.UnitPrice, "preis", "in", "€"),
        Alias(ColumnRole.LineTotal, "gesamt"),
        Alias(ColumnRole.LineTotal, "gesamtpreis"),
        Alias(ColumnRole.LineTotal, "gesamt", "preis"),
        Alias(ColumnRole.LineTotal, "betrag"),
        Alias(ColumnRole.LineTotal, "netto"),
        Alias(ColumnRole.Vat, "mwst"),
        Alias(ColumnRole.Vat, "mwst", "%"),
        Alias(ColumnRole.Vat, "ust"),
        Alias(ColumnRole.Vat, "ust", "%"),
        Alias(ColumnRole.Vat, "ust", "in", "%"),
        Alias(ColumnRole.Vat, "mwst", "in", "%"),
        Alias(ColumnRole.Vat, "steuersatz"),
        Alias(ColumnRole.Vat, "steuersatz", "%"),
        Alias(ColumnRole.Vat, "%"),

        // Kontrollbeträge je Position. Sie erzeugen kein Rechnungsfeld – sie
        // müssen stimmen, sonst ist die Tabelle nicht verstanden.
        Alias(ColumnRole.LineVatAmount, "ust", "in", "€"),
        Alias(ColumnRole.LineVatAmount, "mwst", "in", "€"),
        Alias(ColumnRole.LineGrossTotal, "brutto"),
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

        if (lines.Count == 0)
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
        int tablePage = header.Line.PageNumber;
        decimal? documentVatRate = ResolveDocumentVatRate(header, totals);

        if (!header.Has(ColumnRole.Vat) && documentVatRate is null)
        {
            return PositionDetectionResult.Empty;
        }

        PdfTextLine[] following =
        [
            .. lines.Where(line => line.PageNumber == tablePage
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

        // Andere Seiten dürfen Begleittext enthalten, aber keinen zweiten
        // Tabellenkörper ohne eigenen Kopf. Geprüft wird ausschließlich mit
        // derselben Spaltengeometrie und derselben vollständigen Zeilenlogik;
        // ein beliebiger Text in der Beschreibungsspalte ist kein Verdacht.
        foreach (PdfTextLine line in lines.Where(
                     line => line.PageNumber != tablePage))
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
            PageNumber = tablePage,
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

        HeaderSpan[] candidates =
        [
            .. Enum.GetValues<ColumnRole>()
                .SelectMany(role => FindHeaderMatches(role, tokens, normalized)),
        ];

        var selected = new List<HeaderSpan>();

        foreach (ColumnRole role in Enum.GetValues<ColumnRole>())
        {
            HeaderSpan[] matches =
            [
                .. candidates.Where(match => match.Role == role && !IsShadowed(match, candidates)),
            ];

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

        // Die Einheitsspalte ist ausdrücklich **nicht** dabei. Sehr viele
        // Rechnungen führen keine; die Menge steht dort ohne Einheit. Das ist
        // eine Lücke im Dokument und kein unsicherer Aufbau – die Tabelle
        // deswegen zu verwerfen hieß, eine sichere Aussage wegzuwerfen.
        ColumnRole[] mandatory =
        [ColumnRole.Description, ColumnRole.Quantity, ColumnRole.UnitPrice];

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

    /// <summary>
    /// Die längere, genauere Beschriftung schlägt die kürzere.
    ///
    /// **Warum das nötig ist:** Mehrere Familien teilen sich ein Wort.
    /// <c>Netto Einzelpreis</c> enthält <c>Netto</c>, <c>USt in €</c> enthält
    /// <c>USt</c>. Ohne diese Regel meldeten zwei Rollen einen Anspruch auf
    /// dieselben Tokens, und der Kopf gälte als mehrdeutig – obwohl er für
    /// einen Menschen völlig eindeutig ist.
    ///
    /// Verdrängt wird nur, was **vollständig** in einer echt längeren
    /// Beschriftung einer **anderen** Rolle steckt. Zwei gleich lange
    /// Ansprüche bleiben beide stehen und führen weiterhin dazu, dass der Kopf
    /// verworfen wird. Das ist die Absicht: Echte Mehrdeutigkeit soll auffallen.
    /// </summary>
    private static bool IsShadowed(HeaderSpan span, IReadOnlyList<HeaderSpan> candidates)
        => candidates.Any(other
            => other.Role != span.Role
               && other.StartIndex <= span.StartIndex
               && other.EndIndex >= span.EndIndex
               && other.EndIndex - other.StartIndex > span.EndIndex - span.StartIndex);

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
        int price = IndexOf(columns, ColumnRole.UnitPrice);

        if (!(description < quantity && quantity < price))
        {
            return false;
        }

        // Wenn es eine Einheitsspalte gibt, steht sie zwischen Menge und
        // Preis. Fehlt sie, entfällt die Bedingung – nicht die Prüfung.
        if (columns.Any(column => column.Role == ColumnRole.Unit))
        {
            int unit = IndexOf(columns, ColumnRole.Unit);

            if (!(quantity < unit && unit < price))
            {
                return false;
            }
        }

        if (columns.Any(column => (column.Role is ColumnRole.Position or ColumnRole.ArticleNumber)
                                  && IndexOf(columns, column.Role) > description))
        {
            return false;
        }

        // Alle Ergebnisspalten stehen rechts vom Einzelpreis. Eine Summe
        // links davon wäre kein bekannter Rechnungsaufbau.
        return columns
            .Where(column => column.Role is ColumnRole.LineTotal or ColumnRole.Vat
                             or ColumnRole.LineVatAmount or ColumnRole.LineGrossTotal)
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
        string priceText = Cell(cells, ColumnRole.UnitPrice);

        if (name.Length == 0 || quantityText.Length == 0 || priceText.Length == 0
            || !TryParseMoneyCell(priceText, out decimal unitPrice)
            || unitPrice < 0m)
        {
            return false;
        }

        if (!TryReadQuantityAndUnit(
                cells, header, quantityText, out decimal quantity, out string? unitCode))
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

        if (!PassesLineEvidence(cells, header, quantity, unitPrice, vatRate))
        {
            return false;
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

    /// <summary>
    /// Prüft die ausgeschriebenen Kontrollbeträge einer Position.
    ///
    /// **Diese Spalten werden geprüft und danach verworfen.** Sie wandern
    /// nicht in <see cref="DetectedInvoiceLine"/> und werden nie zu einem
    /// zweiten Rechnungsfeld – die fertige Rechnung entsteht weiterhin
    /// ausschließlich aus Menge, Einzelpreis und Steuersatz über den
    /// bestehenden Rechner.
    ///
    /// Ihr Wert liegt in der Gegenprobe: Eine Tabelle, deren eigene Beträge
    /// nicht zu ihren eigenen Mengen passen, ist nicht verstanden. Eine
    /// einzige Abweichung verwirft sie vollständig.
    ///
    /// Gerundet wird mit <see cref="Amounts.Round"/> – denselben Regeln wie in
    /// der Rechnung. Eine eigene Rundungsdefinition an dieser Stelle wäre der
    /// Anfang zweier Wahrheiten.
    /// </summary>
    private static bool PassesLineEvidence(
        IReadOnlyDictionary<ColumnRole, string> cells,
        HeaderLayout header,
        decimal quantity,
        decimal unitPrice,
        decimal vatRate)
    {
        decimal lineNet = Amounts.Round(quantity * unitPrice);
        decimal lineVat = Amounts.Round(lineNet * vatRate / 100m);

        if (header.Has(ColumnRole.LineVatAmount)
            && (!TryParseMoneyCell(Cell(cells, ColumnRole.LineVatAmount), out decimal parsedVat)
                || parsedVat != lineVat))
        {
            return false;
        }

        if (header.Has(ColumnRole.LineGrossTotal)
            && (!TryParseMoneyCell(Cell(cells, ColumnRole.LineGrossTotal), out decimal parsedGross)
                || parsedGross != lineNet + lineVat))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Liest Menge und Mengeneinheit einer Zeile.
    ///
    /// Die Einheit kann auf drei Wegen im Dokument stehen: in einer eigenen
    /// Spalte, unmittelbar hinter der Menge (<c>4,00 HUR</c>) oder gar nicht.
    /// Der Rückgabewert unterscheidet ausdrücklich zwischen „nicht angegeben“
    /// (<see langword="null"/>) und „angegeben, aber nicht verstanden“
    /// (<see langword="false"/>, also Tabelle verwerfen).
    ///
    /// Die zusammengesetzte Schreibweise wird **nur** ausgewertet, wenn es
    /// keine Einheitsspalte gibt. Gibt es eine, ist die Menge eine Menge; ein
    /// Wortanhang wäre dort ein unerwarteter Aufbau und kein Fund.
    /// </summary>
    private static bool TryReadQuantityAndUnit(
        IReadOnlyDictionary<ColumnRole, string> cells,
        HeaderLayout header,
        string quantityText,
        out decimal quantity,
        out string? unitCode)
    {
        quantity = 0m;
        unitCode = null;

        if (header.Has(ColumnRole.Unit))
        {
            string unitText = Cell(cells, ColumnRole.Unit);

            if (unitText.Length == 0 || !TryMapUnit(unitText, out string mapped))
            {
                return false;
            }

            unitCode = mapped;

            return TryParseQuantity(quantityText, out quantity) && quantity > 0m;
        }

        if (TryParseQuantity(quantityText, out quantity))
        {
            // Nur eine Zahl: Die Rechnung nennt keine Mengeneinheit.
            return quantity > 0m;
        }

        Match combined = QuantityWithUnit().Match(quantityText.Trim());

        if (!combined.Success
            || !TryParseQuantity(combined.Groups["menge"].Value, out quantity)
            || quantity <= 0m)
        {
            return false;
        }

        // Hier steht eine Einheit. Verstehen wir sie nicht, ist das kein
        // Grund, sie für nicht vorhanden zu erklären – die Tabelle fällt.
        if (!TryMapUnit(combined.Groups["einheit"].Value, out string suffix))
        {
            return false;
        }

        unitCode = suffix;

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

    /// <summary>
    /// Der Steuersatz für Tabellen ohne eigene Steuerspalte.
    ///
    /// Es gibt genau zwei Wege, und beide verlangen **einen einzigen**
    /// Dokumentsteuersatz von 7 oder 19 Prozent:
    ///
    /// * ein zweifelsfrei gelesener Satz, oder
    /// * ein unsicher gelesener Satz, den die Dokumentsummen bestätigen.
    ///
    /// **Warum der zweite Weg nötig ist:** Der <c>TotalsDetector</c> vergibt
    /// für Steuersätze nie die Stufe <c>High</c> – ein Prozentwert in einer
    /// Zeile mit Steuerbezug ist für sich genommen eben nicht zweifelsfrei.
    /// Ohne diesen Weg wäre der Rückfall unerreichbar und jede Rechnung ohne
    /// Steuerspalte ergäbe null Positionen.
    ///
    /// **Warum das nichts lockert:** Ein unsicherer Satz genügt nach wie vor
    /// nicht. Er wird nur verwendet, wenn er sich aus den sicher gelesenen
    /// Dokumentsummen nachrechnen lässt. Danach greift unverändert
    /// <see cref="PassesDocumentTotalsGate"/> und misst die erkannten
    /// Positionen an denselben Summen. Beides zusammen ergibt eine
    /// zweistufige Beweiskette – und nicht ein „Medium reicht jetzt“.
    /// </summary>
    private static decimal? ResolveDocumentVatRate(
        HeaderLayout header,
        DetectedTotals totals)
    {
        if (header.Has(ColumnRole.Vat) || totals.VatRates.Count != 1)
        {
            return null;
        }

        DetectedValue<decimal> rate = totals.VatRates[0];

        if (rate.Value is not (7m or 19m))
        {
            return null;
        }

        return rate.Confidence switch
        {
            DetectionConfidence.High => rate.Value,
            DetectionConfidence.Medium when IsConfirmedByTotals(rate.Value, totals) => rate.Value,
            _ => null,
        };
    }

    /// <summary>
    /// Rechnet einen unsicher gelesenen Steuersatz gegen die Dokumentsummen
    /// nach.
    ///
    /// Alle drei Summen müssen zweifelsfrei gelesen sein – eine Bestätigung
    /// ist nur so viel wert wie die Zahlen, die sie tragen. Gerundet und
    /// verglichen wird mit den vorhandenen Mitteln: <see cref="Amounts.Round"/>
    /// und <see cref="DocumentTotalTolerance"/>. Eine eigene Rundungs- oder
    /// Toleranzdefinition an dieser Stelle wäre der Anfang zweier Wahrheiten.
    /// </summary>
    private static bool IsConfirmedByTotals(decimal rate, DetectedTotals totals)
    {
        if (!IsHigh(totals.Net) || !IsHigh(totals.Tax) || !IsHigh(totals.Gross))
        {
            return false;
        }

        decimal net = totals.Net!.Value;
        decimal tax = totals.Tax!.Value;
        decimal gross = totals.Gross!.Value;

        return Math.Abs(tax - Amounts.Round(net * rate / 100m)) <= DocumentTotalTolerance
               && Math.Abs(gross - (net + tax)) <= DocumentTotalTolerance;
    }

    private static bool PassesDocumentTotalsGate(
        List<DetectedInvoiceLine> detected,
        DetectedTotals document)
    {
        var lines = new List<InvoiceLine>(detected.Count);

        foreach (DetectedInvoiceLine item in detected)
        {
            // Für die reine Rechenprobe ist die Mengeneinheit bedeutungslos –
            // gerechnet wird über Menge und Einzelpreis. Nennt die Rechnung
            // keine Einheit, steht hier deshalb ein Platzhalter, der diese
            // Methode nie verlässt. Er wird nirgends gespeichert, nirgends
            // angezeigt und wandert nicht in den Entwurf; dort bleibt die
            // Einheit ausdrücklich leer.
            UnitCode unit = UnitCode.Piece;

            if (item.UnitCode is { } code && !UnitCode.TryParse(code, out unit))
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
            && UnitCodeList.IsSupported(mapped))
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

    /// <summary>
    /// Reduziert einen Kopf-Token auf seinen vergleichbaren Kern.
    ///
    /// Prozent- und Eurozeichen bleiben erhalten. Sie gehören zu
    /// Beschriftungen wie <c>USt in %</c> oder <c>Preis in €</c> und sind dort
    /// bedeutungstragend. Sie allgemein wegzuwerfen wäre bequem und falsch:
    /// Ein weggeworfenes Zeichen macht einen unbekannten Kopf unauffällig, und
    /// unauffällige unbekannte Köpfe sind genau das, was diese Erkennung
    /// verhindern soll.
    /// </summary>
    private static string NormalizeHeaderToken(string text)
    {
        string trimmed = text.Trim().ToLowerInvariant();

        if (trimmed is "%" or "€")
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

        /// <summary>
        /// Bekannt, aber fachlich unbeteiligt: Das Lieferdatum je Position
        /// wird gelesen, damit die Spalte zugeordnet werden kann, und danach
        /// verworfen. Es erzeugt kein Rechnungsfeld.
        /// </summary>
        DeliveryDate,

        UnitPrice,

        /// <summary>
        /// Der Nettogesamtpreis der Position. Er wird gegen Menge mal
        /// Einzelpreis geprüft und nicht als eigener Wert weitergetragen.
        /// </summary>
        LineTotal,

        Vat,

        /// <summary>
        /// Der ausgeschriebene Steuerbetrag der Position – reine Evidenz.
        /// Stimmt er nicht, ist die Tabelle nicht verstanden.
        /// </summary>
        LineVatAmount,

        /// <summary>
        /// Der ausgeschriebene Bruttobetrag der Position – ebenfalls reine
        /// Evidenz.
        /// </summary>
        LineGrossTotal,
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

    /// <summary>
    /// Menge und unmittelbar folgende Mengeneinheit, etwa <c>4,00 HUR</c>,
    /// <c>2,5 Stunden</c> oder <c>1,00 C62</c>. Die Einheit ist bewusst nur ein
    /// einzelnes Wort: Alles darüber hinaus ist Fließtext und keine Einheit.
    ///
    /// **Der Einheitenteil ist alphanumerisch, nicht rein alphabetisch.**
    /// UN/ECE-Codes tragen Ziffern – <c>C62</c> für Stück ist der häufigste
    /// überhaupt. Nur Buchstaben zuzulassen ließ <c>HUR</c> durch und
    /// <c>C62</c> auffliegen; weil eine nicht verstandene Zeile die ganze
    /// Tabelle verwirft, fiel damit eine vollständige Rechnung an einer
    /// einzigen Zeile.
    ///
    /// Dieses Muster entscheidet **nur über die Form**. Ob es die Einheit
    /// überhaupt gibt, entscheidet unverändert <see cref="TryMapUnit"/> gegen
    /// <see cref="UnitMappings"/> und <see cref="UnitCodeList"/>. Ein
    /// alphanumerischer Token wird also bis zu dieser Allowlist durchgereicht
    /// und dort abgelehnt, wenn er nicht dazugehört – die Form wird
    /// durchlässiger, die fachliche Prüfung nicht.
    /// </summary>
    [GeneratedRegex(
        @"^(?<menge>[+]?(?:\d{1,3}(?:\.\d{3})+|\d+)(?:,\d{1,4})?)\s+(?<einheit>[\p{L}\d]{1,12}\.?)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex QuantityWithUnit();

    [GeneratedRegex(
        @"^(?<betrag>(?:\d{1,3}(?:\.\d{3})+|\d+),\d{2})(?:\s*(?:€|EUR))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyCell();

    [GeneratedRegex(
        @"^(?<satz>7|19)(?:,0+)?\s*%?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex VatCell();
}

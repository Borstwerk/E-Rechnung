using System.Globalization;
using System.Text.RegularExpressions;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Pdf;

/// <summary>Liest typische Rechnungsangaben aus dem Text einer PDF.</summary>
public interface IInvoiceDataDetector
{
    /// <summary>
    /// Wertet die PDF oertlich aus. <paramref name="ownCompany"/> ist die
    /// gespeicherte eigene Firmenvorlage; sie hilft dabei, Verkaeufer und
    /// Kaeufer auseinanderzuhalten, und wird nur zum Vergleichen verwendet.
    /// </summary>
    Task<InvoiceDetectionResult> DetectAsync(
        string pdfPath,
        CompanyTemplate? ownCompany = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Erkennt Rechnungsangaben im bereits vorhandenen PDF-Text.
///
/// **Was diese Klasse ist:** eine Schreibhilfe. Sie fuellt das Formular vor,
/// damit der Anwender nicht abtippen muss, was ohnehin schon dasteht.
///
/// **Was sie nicht ist:** eine Quelle der Wahrheit. Kein gelesener Wert geht
/// je unmittelbar in die E-Rechnung; der Weg fuehrt immer ueber das Formular
/// und die Bestaetigung durch den Menschen.
///
/// Das Verfahren ist bewusst regelbasiert und damit nachvollziehbar: Jeder
/// Wert traegt die Zeile, aus der er stammt, und die Begruendung seiner
/// Zuordnung. Ein Anwender kann so in Sekunden pruefen, statt zu raten.
///
/// **Die Leitregel bei allen Zweifelsfaellen lautet: lieber nichts vorschlagen
/// als etwas Falsches.** Ein leeres Feld kostet Tippen. Ein falsch gefuelltes
/// Feld, das jemand uebersieht, kostet eine fehlerhafte Rechnung.
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
            // Auch hier gilt: Die Erkennung darf den Ablauf nie aufhalten.
            string reason = exception.GetType().Name;
            LogDetectionFailed(reason);

            return InvoiceDetectionResult.WithoutText with { HasUsableText = true };
        }
    }

    private static InvoiceDetectionResult Detect(PdfTextResult text, CompanyTemplate? ownCompany)
    {
        IReadOnlyList<PdfTextLine> lines = text.Lines;

        (DetectedParty seller, DetectedParty buyer) = DetectParties(lines, ownCompany);

        return new InvoiceDetectionResult
        {
            HasUsableText = true,
            PageCount = text.PageCount,
            InvoiceNumber = DetectInvoiceNumber(lines),
            IssueDate = DetectDate(lines, IssueDateKeywords),
            DeliveryDate = DetectDate(lines, DeliveryDateKeywords),
            DueDate = DetectDate(lines, DueDateKeywords),
            Currency = DetectCurrency(lines),
            Seller = seller,
            Buyer = buyer,
            Iban = DetectIban(lines),
            Bic = DetectBic(lines),
            Totals = DetectTotals(lines),
            Lines = [],
            LinesConfidence = DetectionConfidence.Low,
        };
    }

    // ================================================================== Nummer

    private static readonly string[] InvoiceNumberKeywords =
    [
        "rechnungsnummer", "rechnungs-nr", "rechnungsnr", "rechnung nr", "rechnung-nr",
        "invoice number", "invoice no", "invoice-no", "invoice nr", "belegnummer",
    ];

    /// <summary>
    /// Woerter, die eine Zeile als Fundstelle ausschliessen.
    ///
    /// Ohne diese Sperre wuerde in "Telefon 0381 123456 - Kundennummer 4711"
    /// irgendeine Ziffernfolge zur Rechnungsnummer. Genau solche Verwechslungen
    /// sind der haeufigste Weg, wie eine automatische Erkennung Schaden
    /// anrichtet.
    /// </summary>
    private static readonly string[] NumberBlockers =
    [
        "telefon", "telefax", "tel.", "fax", "mobil", "ust-id", "ust.-id", "umsatzsteuer",
        "steuernummer", "steuer-nr", "kundennummer", "kunden-nr", "kundennr", "seite",
        "iban", "bic", "auftragsnummer", "bestellnummer", "lieferschein",
    ];

    private static DetectedValue<string>? DetectInvoiceNumber(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (PdfTextLine line in lines)
        {
            string lower = line.Text.ToLowerInvariant();

            string? keyword = InvoiceNumberKeywords.FirstOrDefault(k => lower.Contains(k, StringComparison.Ordinal));

            if (keyword is null)
            {
                continue;
            }

            // "Rechnungsnummer" und "Telefon" in derselben Zeile: zu unsicher.
            if (NumberBlockers.Any(b => lower.Contains(b, StringComparison.Ordinal)))
            {
                continue;
            }

            // Nur der Text **nach** dem Schluesselwort kommt in Frage.
            string tail = line.Text[(lower.IndexOf(keyword, StringComparison.Ordinal) + keyword.Length)..];
            Match match = InvoiceNumberPattern().Match(tail);

            if (!match.Success)
            {
                continue;
            }

            string value = match.Groups["nr"].Value.Trim();

            // Ein reines Datum ist keine Rechnungsnummer.
            if (GermanDatePattern().IsMatch(value) || IsoDatePattern().IsMatch(value))
            {
                continue;
            }

            return new DetectedValue<string>(
                value,
                DetectionConfidence.High,
                line.Text,
                $"Stand unmittelbar hinter \"{keyword}\".");
        }

        return null;
    }

    // =================================================================== Datum

    private static readonly string[] IssueDateKeywords =
        ["rechnungsdatum", "rechnung vom", "datum der rechnung", "invoice date", "belegdatum"];

    private static readonly string[] DeliveryDateKeywords =
        ["leistungsdatum", "lieferdatum", "leistungszeitpunkt", "liefer- und leistungsdatum", "leistung am"];

    private static readonly string[] DueDateKeywords =
        ["faellig am", "fällig am", "zahlbar bis", "faelligkeit", "fälligkeit", "due date", "zahlungsziel"];

    private static DetectedValue<DateOnly>? DetectDate(
        IReadOnlyList<PdfTextLine> lines, string[] keywords)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            PdfTextLine line = lines[index];
            string lower = line.Text.ToLowerInvariant();
            string? keyword = keywords.FirstOrDefault(k => lower.Contains(k, StringComparison.Ordinal));

            if (keyword is null)
            {
                continue;
            }

            string tail = line.Text[(lower.IndexOf(keyword, StringComparison.Ordinal) + keyword.Length)..];

            if (TryParseFirstDate(tail, out DateOnly value))
            {
                return new DetectedValue<DateOnly>(
                    value, DetectionConfidence.High, line.Text,
                    $"Stand unmittelbar hinter \"{keyword}\".");
            }

            // Manche Vorlagen setzen den Wert in die naechste Zeile.
            if (index + 1 < lines.Count && TryParseFirstDate(lines[index + 1].Text, out DateOnly next))
            {
                return new DetectedValue<DateOnly>(
                    next, DetectionConfidence.Medium, lines[index + 1].Text,
                    $"Stand in der Zeile unter \"{keyword}\".");
            }
        }

        return null;
    }

    private static bool TryParseFirstDate(string text, out DateOnly value)
    {
        Match german = GermanDatePattern().Match(text);

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

        Match iso = IsoDatePattern().Match(text);

        if (iso.Success
            && DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.None, out value))
        {
            return true;
        }

        value = default;

        return false;
    }

    // ================================================================ Waehrung

    private static DetectedValue<string>? DetectCurrency(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (PdfTextLine line in lines)
        {
            if (line.Text.Contains('€', StringComparison.Ordinal)
                || line.Text.Contains("EUR", StringComparison.Ordinal))
            {
                return new DetectedValue<string>(
                    "EUR", DetectionConfidence.High, line.Text, "Waehrungszeichen im Dokument gefunden.");
            }
        }

        return null;
    }

    // ==================================================================== IBAN

    private static DetectedValue<string>? DetectIban(IReadOnlyList<PdfTextLine> lines)
    {
        foreach (PdfTextLine line in lines)
        {
            foreach (Match match in IbanPattern().Matches(line.Text))
            {
                string candidate = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);

                // Entscheidend: Das Muster allein genuegt nicht. Erst die
                // Pruefsumme nach ISO 7064 macht aus einer Zeichenfolge eine
                // IBAN. Eine syntaktisch passende, rechnerisch falsche IBAN
                // wird verworfen statt uebernommen.
                if (!Iban.TryParse(candidate, out Iban iban))
                {
                    continue;
                }

                return new DetectedValue<string>(
                    iban.Value, DetectionConfidence.High, MaskIban(line.Text),
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

            Match match = BicPattern().Match(line.Text);

            if (match.Success)
            {
                return new DetectedValue<string>(
                    match.Value, DetectionConfidence.Medium, line.Text, "Stand hinter \"BIC\".");
            }
        }

        return null;
    }

    // ================================================================ Parteien

    private static readonly string[] BuyerKeywords =
    [
        "rechnung an", "rechnungsempfaenger", "rechnungsempfänger", "kunde:", "empfaenger",
        "empfänger", "bill to", "invoice to", "lieferanschrift", "rechnungsadresse",
    ];

    /// <summary>
    /// Trennt Verkaeufer und Kaeufer.
    ///
    /// Das ist die schwierigste Zuordnung im ganzen Verfahren, denn beide
    /// Adressen sehen gleich aus. Zwei Signale werden genutzt:
    ///
    /// 1. **Die eigene Firmenvorlage.** Steht der gespeicherte Firmenname oder
    ///    die gespeicherte USt-IdNr. im Dokument, ist das ein starkes Zeichen
    ///    fuer den Verkaeufer – man stellt seine eigenen Rechnungen selbst aus.
    /// 2. **Schluesselwoerter wie "Rechnung an".** Was darunter steht, ist der
    ///    Kaeufer.
    ///
    /// Fehlt beides, wird **nichts** zugeordnet. Eine vertauschte Adresse waere
    /// schlimmer als ein leeres Feld.
    /// </summary>
    private static (DetectedParty Seller, DetectedParty Buyer) DetectParties(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
    {
        DetectedParty seller = DetectSellerFromTemplate(lines, ownCompany);
        DetectedParty buyer = DetectBuyerFromKeyword(lines, ownCompany);

        return (seller, buyer);
    }

    private static DetectedParty DetectSellerFromTemplate(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
    {
        if (ownCompany is null)
        {
            return new DetectedParty();
        }

        bool nameFound = !string.IsNullOrWhiteSpace(ownCompany.SellerName)
            && lines.Any(l => l.Text.Contains(ownCompany.SellerName, StringComparison.OrdinalIgnoreCase));

        bool vatFound = !string.IsNullOrWhiteSpace(ownCompany.SellerVatId)
            && lines.Any(l => l.Text.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Contains(ownCompany.SellerVatId, StringComparison.OrdinalIgnoreCase));

        if (!nameFound && !vatFound)
        {
            return new DetectedParty();
        }

        string reason = nameFound && vatFound
            ? "Firmenname und USt-IdNr. der gespeicherten Vorlage stehen im Dokument."
            : nameFound
                ? "Der Firmenname der gespeicherten Vorlage steht im Dokument."
                : "Die USt-IdNr. der gespeicherten Vorlage steht im Dokument.";

        DetectedValue<string>? From(string? value) => string.IsNullOrWhiteSpace(value)
            ? null
            : new DetectedValue<string>(value, DetectionConfidence.High, null, reason);

        return new DetectedParty
        {
            Name = From(ownCompany.SellerName),
            Street = From(ownCompany.SellerStreet),
            PostalCode = From(ownCompany.SellerPostalCode),
            City = From(ownCompany.SellerCity),
            Country = From(ownCompany.SellerCountry),
            VatId = From(ownCompany.SellerVatId),
            TaxNumber = From(ownCompany.SellerTaxNumber),
            Email = From(ownCompany.SellerEmail),
        };
    }

    private static DetectedParty DetectBuyerFromKeyword(
        IReadOnlyList<PdfTextLine> lines, CompanyTemplate? ownCompany)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string lower = lines[i].Text.ToLowerInvariant();

            if (!BuyerKeywords.Any(k => lower.Contains(k, StringComparison.Ordinal)))
            {
                continue;
            }

            // Der Adressblock steht direkt unter dem Schluesselwort. Mehr als
            // fuenf Zeilen sind erfahrungsgemaess schon der naechste Abschnitt.
            List<PdfTextLine> block = [.. lines.Skip(i + 1).Take(5)];

            return BuildPartyFromBlock(block, lines[i].Text, ownCompany);
        }

        return new DetectedParty();
    }

    private static DetectedParty BuildPartyFromBlock(
        IReadOnlyList<PdfTextLine> block, string keywordLine, CompanyTemplate? ownCompany)
    {
        DetectedValue<string>? name = null;
        DetectedValue<string>? street = null;
        DetectedValue<string>? postalCode = null;
        DetectedValue<string>? city = null;

        foreach (PdfTextLine line in block)
        {
            Match place = PostalCodeAndCityPattern().Match(line.Text);

            if (place.Success && postalCode is null)
            {
                postalCode = new DetectedValue<string>(
                    place.Groups["plz"].Value, DetectionConfidence.Medium, line.Text,
                    $"Adressblock unter \"{keywordLine.Trim()}\".");
                city = new DetectedValue<string>(
                    place.Groups["ort"].Value.Trim(), DetectionConfidence.Medium, line.Text,
                    $"Adressblock unter \"{keywordLine.Trim()}\".");

                continue;
            }

            if (StreetPattern().IsMatch(line.Text) && street is null)
            {
                street = new DetectedValue<string>(
                    line.Text.Trim(), DetectionConfidence.Medium, line.Text,
                    $"Adressblock unter \"{keywordLine.Trim()}\".");

                continue;
            }

            if (name is null && line.Text.Trim().Length > 2)
            {
                // Die eigene Firma kann nicht der Kaeufer sein.
                if (ownCompany?.SellerName is { Length: > 0 } own
                    && line.Text.Contains(own, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                name = new DetectedValue<string>(
                    line.Text.Trim(), DetectionConfidence.Medium, line.Text,
                    $"Erste Zeile unter \"{keywordLine.Trim()}\".");
            }
        }

        return new DetectedParty
        {
            Name = name,
            Street = street,
            PostalCode = postalCode,
            City = city,
            VatId = null,
            Email = null,
        };
    }

    // ================================================================== Summen

    private static readonly (string[] Keywords, string Field)[] TotalKeywords =
    [
        (["zahlbetrag", "zu zahlen", "zahlungsbetrag"], "Payable"),
        (["gesamtbetrag", "rechnungsbetrag", "gesamtsumme", "brutto", "endbetrag", "gesamt brutto"], "Gross"),
        (["umsatzsteuer", "mehrwertsteuer", "mwst", "ust."], "Tax"),
        (["nettobetrag", "netto", "zwischensumme", "summe netto", "gesamt netto"], "Net"),
    ];

    private static DetectedTotals DetectTotals(IReadOnlyList<PdfTextLine> lines)
    {
        DetectedValue<decimal>? net = null;
        DetectedValue<decimal>? tax = null;
        DetectedValue<decimal>? gross = null;
        DetectedValue<decimal>? payable = null;
        var rates = new List<DetectedValue<decimal>>();

        foreach (PdfTextLine line in lines)
        {
            string lower = line.Text.ToLowerInvariant();

            foreach ((string[] keywords, string field) in TotalKeywords)
            {
                string? keyword = keywords.FirstOrDefault(k => lower.Contains(k, StringComparison.Ordinal));

                if (keyword is null || !TryParseLastAmount(line.Text, out decimal amount))
                {
                    continue;
                }

                var detected = new DetectedValue<decimal>(
                    amount, DetectionConfidence.High, line.Text, $"Stand in der Zeile mit \"{keyword}\".");

                switch (field)
                {
                    case "Payable": payable ??= detected; break;
                    case "Gross": gross ??= detected; break;
                    case "Tax": tax ??= detected; break;
                    default: net ??= detected; break;
                }

                break;
            }

            // Steuersaetze nur in einem echten Steuerzusammenhang. Ein
            // Rabatt von 3 % ist kein Steuersatz - und ohne diese Bedingung
            // wuerde er als einer gelesen.
            if (IsTaxContext(lower))
            {
                foreach (Match match in PercentPattern().Matches(line.Text))
                {
                    if (TryParseGermanDecimal(match.Groups["satz"].Value, out decimal rate)
                        && rate is >= 0 and <= 30
                        && !rates.Any(r => r.Value == rate))
                    {
                        rates.Add(new DetectedValue<decimal>(
                            rate, DetectionConfidence.Medium, line.Text,
                            "Prozentwert in einer Zeile mit Steuerbezug."));
                    }
                }
            }
        }

        return new DetectedTotals
        {
            Net = net,
            Tax = tax,
            Gross = gross,
            Payable = payable,
            VatRates = rates,
        };
    }

    private static bool IsTaxContext(string lowerLine)
        => (lowerLine.Contains("umsatzsteuer", StringComparison.Ordinal)
            || lowerLine.Contains("mehrwertsteuer", StringComparison.Ordinal)
            || lowerLine.Contains("mwst", StringComparison.Ordinal)
            || lowerLine.Contains("ust", StringComparison.Ordinal)
            || lowerLine.Contains("steuersatz", StringComparison.Ordinal))
           && !lowerLine.Contains("rabatt", StringComparison.Ordinal)
           && !lowerLine.Contains("skonto", StringComparison.Ordinal)
           && !lowerLine.Contains("nachlass", StringComparison.Ordinal);

    /// <summary>
    /// Liest den letzten Betrag einer Zeile. In Summenzeilen steht der Wert
    /// rechts; ein "19" aus "Umsatzsteuer 19 %" darf nicht gewinnen.
    /// </summary>
    private static bool TryParseLastAmount(string text, out decimal value)
    {
        value = 0m;

        // Prozentangaben zuerst entfernen, sonst wird "19" zum Betrag.
        string cleaned = PercentPattern().Replace(text, " ");

        MatchCollection matches = AmountPattern().Matches(cleaned);

        if (matches.Count == 0)
        {
            return false;
        }

        return TryParseGermanDecimal(matches[^1].Groups["betrag"].Value, out value);
    }

    private static bool TryParseGermanDecimal(string text, out decimal value)
    {
        string normalized = text.Trim().Replace(".", string.Empty, StringComparison.Ordinal)
                                       .Replace(',', '.');

        return decimal.TryParse(
            normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Maskiert IBAN-aehnliche Zeichenfolgen in einem Text, der protokolliert
    /// oder als Fundstelle angezeigt wird.
    /// </summary>
    private static string MaskIban(string text)
        => IbanPattern().Replace(text, m =>
        {
            string compact = m.Value.Replace(" ", string.Empty, StringComparison.Ordinal);

            return compact.Length <= 8 ? compact : compact[..4] + new string('*', compact.Length - 8) + compact[^4..];
        });

    // ================================================================= Muster

    [GeneratedRegex(@"[:\s]*(?<nr>[A-Za-z0-9][A-Za-z0-9\-/_.]{2,29})", RegexOptions.CultureInvariant)]
    private static partial Regex InvoiceNumberPattern();

    [GeneratedRegex(@"\b(?<d>\d{1,2})\.(?<m>\d{1,2})\.(?<y>\d{2}|\d{4})\b", RegexOptions.CultureInvariant)]
    private static partial Regex GermanDatePattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDatePattern();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:\s?[A-Z0-9]{4}){2,7}\s?[A-Z0-9]{0,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"\b[A-Z]{6}[A-Z0-9]{2}(?:[A-Z0-9]{3})?\b", RegexOptions.CultureInvariant)]
    private static partial Regex BicPattern();

    [GeneratedRegex(@"(?<betrag>\d{1,3}(?:\.\d{3})+,\d{2}|\d+,\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"(?<satz>\d{1,2}(?:,\d{1,2})?)\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentPattern();

    [GeneratedRegex(@"\b(?<plz>\d{5})\s+(?<ort>[A-Za-zÄÖÜäöüß][A-Za-zÄÖÜäöüß\-.\s]{1,40})$", RegexOptions.CultureInvariant)]
    private static partial Regex PostalCodeAndCityPattern();

    [GeneratedRegex(@"^[A-Za-zÄÖÜäöüß][A-Za-zÄÖÜäöüß\-.\s]{2,40}\s\d+[a-zA-Z]?$", RegexOptions.CultureInvariant)]
    private static partial Regex StreetPattern();

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "Die Rechnungserkennung ist fehlgeschlagen ({Reason}). Die Daten werden von Hand erfasst.")]
    private partial void LogDetectionFailed(string reason);
}

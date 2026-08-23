using System.Globalization;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Was die Vorbefüllung getan hat – Grundlage der Anzeige.</summary>
/// <param name="FilledFields">Anzahl übernommener Felder.</param>
/// <param name="UncertainFields">Bezeichnungen der Felder, die zu prüfen sind.</param>
/// <param name="SkippedLowConfidence">Werte, die zu unsicher zur Übernahme waren.</param>
/// <param name="SkippedProtected">Felder, die bereits einen höherrangigen Wert trugen.</param>
/// <param name="FilledLines">
/// Anzahl übernommener Rechnungspositionen. Eine Position zählt als **eine**
/// Position und nie als mehrere ausgefüllte Felder – sonst behauptete die
/// Meldung in Schritt 2 eine Arbeitsersparnis, die es so nicht gab.
/// </param>
/// <param name="SkippedExistingLines">
/// Anzahl erkannter Positionen, die nicht übernommen wurden, weil der Entwurf
/// bereits Positionen enthielt.
/// </param>
/// <param name="LinesMissingUnit">
/// Anzahl **übernommener** Positionen, bei denen die Rechnung keine
/// Mengeneinheit nennt und das Feld im Entwurf deshalb leer bleibt.
///
/// Ausdrücklich nur die übernommenen: Wurde wegen bereits erfasster
/// Benutzerpositionen gar nichts übernommen, fehlt im Entwurf auch keine
/// Einheit. Diese Zahl schickt den Anwender an eine Stelle im Formular –
/// sie muss dorthin zeigen, wo tatsächlich etwas leer ist.
/// </param>
public sealed record PrefillSummary(
    int FilledFields,
    IReadOnlyList<string> UncertainFields,
    IReadOnlyList<string> SkippedLowConfidence,
    IReadOnlyList<string> SkippedProtected,
    int FilledLines = 0,
    int SkippedExistingLines = 0,
    int LinesMissingUnit = 0);

/// <summary>
/// Trägt ein Erkennungsergebnis in das Eingabeformular ein.
///
/// Das ist die einzige Stelle, an der erkannte Werte den Weg ins Formular
/// finden. Jedes Feld läuft durch dieselbe Entscheidung
/// (<see cref="FieldOriginRules.CanReplace"/>) – es gibt keine Sonderregel
/// pro Feld mehr. Damit gilt für alle Felder gleichermaßen:
///
/// * Ein vom Anwender bearbeitetes Feld wird nie überschrieben.
/// * Ein Programmstandard darf von jeder Quelle ersetzt werden.
/// * Ein Wert aus der Firmenvorlage weicht nicht der PDF-Erkennung.
/// * Ein unsicher gelesener Wert füllt gar nichts.
/// </summary>
public static class DraftPrefiller
{
    /// <summary>Trägt das Erkennungsergebnis in den Entwurf ein.</summary>
    public static PrefillSummary Apply(
        InvoiceDraft draft, InvoiceDetectionResult detection, CompanyTemplate? ownCompany = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(detection);

        var log = new PrefillLog();

        draft.Prefill(d =>
        {
            ApplyDocumentFields(d, detection, ownCompany, log);
            ApplyPartyFields(d, detection, ownCompany, log);
            ApplyPaymentFields(d, detection, ownCompany, log);
            ApplyLines(d, detection, log);
        });

        return log.ToSummary();
    }

    /// <summary>
    /// Übernimmt die erkannte Positionstabelle – geschlossen oder gar nicht.
    ///
    /// **Zwei Regeln, beide unverhandelbar:**
    ///
    /// 1. Enthält der Entwurf bereits Positionen, wird nichts übernommen. Sie
    ///    sind Benutzerarbeit; sie zu ergänzen, zu ersetzen oder mit erkannten
    ///    Zeilen zu vermischen wäre in jeder Ausprägung falsch. Gemeldet wird
    ///    das trotzdem – stillschweigend nichts zu tun wäre ebenso falsch.
    /// 2. Erst wenn **alle** Zeilen umgewandelt sind, wandert die erste in die
    ///    Collection. Ein Entwurf mit zwei von drei Positionen sieht
    ///    vollständig aus und ist es nicht.
    ///
    /// Der Entwurf bekommt jeden fachlichen Wert ausdrücklich zugewiesen. Die
    /// Vorgaben von <see cref="InvoiceLineDraft"/> – insbesondere
    /// <c>Unit = "C62"</c> – dürfen nie als erkannte Information durchgehen.
    /// Die Preisbasismenge bleibt beim technischen Standard 1: Phase A lehnt
    /// Tabellen mit eigener Preisbasismengenspalte bereits am Kopf ab.
    /// </summary>
    private static void ApplyLines(
        InvoiceDraft draft, InvoiceDetectionResult detection, PrefillLog log)
    {
        if (detection.Lines.Count == 0)
        {
            return;
        }

        if (draft.Lines.Count > 0)
        {
            log.SkippedExistingLines(detection.Lines.Count);

            return;
        }

        var converted = new List<InvoiceLineDraft>(detection.Lines.Count);

        foreach (DetectedInvoiceLine line in detection.Lines)
        {
            if (!TryConvert(line, out InvoiceLineDraft item))
            {
                return;
            }

            converted.Add(item);
        }

        foreach (InvoiceLineDraft item in converted)
        {
            draft.Lines.Add(item);
        }

        log.FilledLines(
            converted.Count,
            converted.Count(item => item.Unit.Length == 0));
    }

    /// <summary>
    /// Wandelt eine erkannte Position in eine bearbeitbare um.
    ///
    /// Die Einheit wird gegen die Codeliste geprüft, obwohl Phase A das
    /// bereits getan hat. Die Prüfung kostet nichts und hält die Zusicherung
    /// „alles oder nichts“ auch dann, wenn jemand später einen anderen Weg zu
    /// dieser Methode baut.
    ///
    /// **Beide Prüfungen sind nötig.** <see cref="UnitCode.TryParse"/> prüft
    /// nur die Form – ein bis drei Buchstaben oder Ziffern; „XXX“ besteht sie
    /// anstandslos. Erst <see cref="UnitCodeList.IsValid"/> entscheidet, ob es
    /// die Einheit überhaupt gibt. Das ist dieselbe Liste, an der später
    /// <c>InvoiceLineRules</c> misst: Was hier durchkommt und dort scheitert,
    /// wäre ein Entwurf, der erst beim Erzeugen der Rechnung auffliegt.
    ///
    /// **Nennt die Rechnung keine Einheit, wird die Einheit ausdrücklich
    /// geleert.** <see cref="InvoiceLineDraft"/> beginnt mit <c>"C62"</c>;
    /// dieses Feld unberührt zu lassen hieße, den Programmstandard als
    /// erkannte Information auszugeben. Bei einer Stundenrechnung stünde dann
    /// „Stück“ im Formular, ohne dass irgendetwas darauf hinwiese. Die
    /// bestehende Entwurfsprüfung hält die Rechnung anschließend von selbst
    /// auf, bis der Anwender die Einheit ergänzt.
    /// </summary>
    private static bool TryConvert(DetectedInvoiceLine line, out InvoiceLineDraft draft)
    {
        draft = null!;

        string unit = string.Empty;

        if (line.UnitCode is { } code)
        {
            if (!UnitCode.TryParse(code, out UnitCode parsed)
                || !UnitCodeList.IsValid(parsed.Value))
            {
                return false;
            }

            unit = parsed.Value;
        }

        draft = new InvoiceLineDraft
        {
            Number = line.Number,
            Name = line.Name,
            Description = line.Description ?? string.Empty,
            Quantity = Number(line.Quantity, QuantityFormat),
            Unit = unit,
            NetUnitPrice = Number(line.NetUnitPrice, AmountFormat),
            VatCategory = line.VatCategory,
            VatRate = Number(line.VatRate, RateFormat),
        };

        return true;
    }

    /// <summary>
    /// Mengen behalten bis zu vier Nachkommastellen – so weit rechnet
    /// <c>InvoiceDraft</c> beim Zurücklesen.
    /// </summary>
    private const string QuantityFormat = "0.####";

    /// <summary>Beträge stehen im Formular immer mit zwei Nachkommastellen.</summary>
    private const string AmountFormat = "0.00";

    /// <summary>Steuersätze ohne unnötige Nullen: 19, nicht 19,00.</summary>
    private const string RateFormat = "0.##";

    /// <summary>
    /// Schreibt eine Zahl so, wie das Formular sie erwartet.
    ///
    /// **Ohne Tausendertrennzeichen, und das ist keine Geschmacksfrage.**
    /// <see cref="InvoiceDraft.TryParseDecimal"/> ersetzt das Komma durch
    /// einen Punkt und liest invariant; ein Tausenderpunkt würde aus
    /// „1.234,56“ ein „1.234.56“ machen, das niemand mehr lesen kann. Deshalb
    /// invariant formatieren und nur das Dezimaltrennzeichen tauschen – das
    /// ergibt dieselbe Zeichenkette unter Linux wie unter Windows.
    /// </summary>
    private static string Number(decimal value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

    private static void ApplyDocumentFields(
        InvoiceDraft d, InvoiceDetectionResult detection, CompanyTemplate? own, PrefillLog log)
    {
        Set(d, log, own, "Rechnungsnummer", nameof(d.InvoiceNumber),
            detection.InvoiceNumber, v => d.InvoiceNumber = v);
        Set(d, log, own, "Währung", nameof(d.Currency),
            detection.Currency, v => d.Currency = v);

        SetDate(d, log, "Rechnungsdatum", nameof(d.IssueDate),
            detection.IssueDate, v => d.IssueDate = v);
        SetDate(d, log, "Leistungsdatum", nameof(d.DeliveryDate),
            detection.DeliveryDate, v => d.DeliveryDate = v);
        SetDate(d, log, "Leistungszeitraum von", nameof(d.BillingPeriodStart),
            detection.BillingPeriodStart, v => d.BillingPeriodStart = v);
        SetDate(d, log, "Leistungszeitraum bis", nameof(d.BillingPeriodEnd),
            detection.BillingPeriodEnd, v => d.BillingPeriodEnd = v);
        SetDate(d, log, "Fälligkeitsdatum", nameof(d.DueDate),
            detection.DueDate, v => d.DueDate = v);
    }

    private static void ApplyPartyFields(
        InvoiceDraft d, InvoiceDetectionResult detection, CompanyTemplate? own, PrefillLog log)
    {
        DetectedParty seller = detection.Seller;
        Set(d, log, own, "Verkäufer", nameof(d.SellerName), seller.Name, v => d.SellerName = v);
        Set(d, log, own, "Straße (Verkäufer)", nameof(d.SellerStreet), seller.Street, v => d.SellerStreet = v);
        Set(d, log, own, "PLZ (Verkäufer)", nameof(d.SellerPostalCode), seller.PostalCode, v => d.SellerPostalCode = v);
        Set(d, log, own, "Ort (Verkäufer)", nameof(d.SellerCity), seller.City, v => d.SellerCity = v);
        Set(d, log, own, "Land (Verkäufer)", nameof(d.SellerCountry), seller.Country, v => d.SellerCountry = v);
        Set(d, log, own, "USt-IdNr. (Verkäufer)", nameof(d.SellerVatId), seller.VatId, v => d.SellerVatId = v);
        Set(d, log, own, "Steuernummer", nameof(d.SellerTaxNumber), seller.TaxNumber, v => d.SellerTaxNumber = v);
        Set(d, log, own, "E-Mail (Verkäufer)", nameof(d.SellerEmail), seller.Email, v => d.SellerEmail = v);

        DetectedParty buyer = detection.Buyer;
        Set(d, log, own, "Käufer", nameof(d.BuyerName), buyer.Name, v => d.BuyerName = v);
        Set(d, log, own, "Straße (Käufer)", nameof(d.BuyerStreet), buyer.Street, v => d.BuyerStreet = v);
        Set(d, log, own, "PLZ (Käufer)", nameof(d.BuyerPostalCode), buyer.PostalCode, v => d.BuyerPostalCode = v);
        Set(d, log, own, "Ort (Käufer)", nameof(d.BuyerCity), buyer.City, v => d.BuyerCity = v);
        Set(d, log, own, "Land (Käufer)", nameof(d.BuyerCountry), buyer.Country, v => d.BuyerCountry = v);
        Set(d, log, own, "USt-IdNr. (Käufer)", nameof(d.BuyerVatId), buyer.VatId, v => d.BuyerVatId = v);
        Set(d, log, own, "E-Mail (Käufer)", nameof(d.BuyerEmail), buyer.Email, v => d.BuyerEmail = v);
    }

    private static void ApplyPaymentFields(
        InvoiceDraft d, InvoiceDetectionResult detection, CompanyTemplate? own, PrefillLog log)
    {
        Set(d, log, own, "IBAN", nameof(d.BankIban), detection.Iban, v => d.BankIban = v);
        Set(d, log, own, "BIC", nameof(d.BankBic), detection.Bic, v => d.BankBic = v);
    }

    private static void Set(
        InvoiceDraft draft,
        PrefillLog log,
        CompanyTemplate? ownCompany,
        string label,
        string property,
        DetectedValue<string>? detected,
        Action<string> assign)
        => Set(draft, log, label, property, detected, OriginOf(detected, ownCompany), assign);

    private static void SetDate(
        InvoiceDraft draft,
        PrefillLog log,
        string label,
        string property,
        DetectedValue<DateOnly>? detected,
        Action<DateOnly> assign)
        => Set(draft, log, label, property, detected, OriginOf(detected), assign);

    /// <summary>
    /// Die gemeinsame Entscheidung für jedes Feld – unabhängig vom Datentyp.
    /// </summary>
    private static void Set<T>(
        InvoiceDraft draft,
        PrefillLog log,
        string label,
        string property,
        DetectedValue<T>? detected,
        FieldOrigin proposedOrigin,
        Action<T> assign)
    {
        if (detected is null)
        {
            return;
        }

        if (!detected.IsUsable)
        {
            log.Skipped(label);

            return;
        }

        if (!FieldOriginRules.CanReplace(draft.OriginOf(property), proposedOrigin))
        {
            log.Protected(label);

            return;
        }

        assign(detected.Value);
        draft.MarkOrigin(property, proposedOrigin);
        log.Filled(label, proposedOrigin);
    }

    /// <summary>
    /// Ein Wert, der wortgleich in der gespeicherten Vorlage steht, gilt als
    /// aus der Vorlage stammend. Das ist ehrlicher als "aus PDF erkannt": Die
    /// PDF hat ihn nur bestätigt.
    /// </summary>
    private static FieldOrigin OriginOf(DetectedValue<string>? value, CompanyTemplate? ownCompany)
        => value is not null && ownCompany is not null && ComesFromTemplate(ownCompany, value.Value)
            ? FieldOrigin.Template
            : OriginOf(value);

    private static FieldOrigin OriginOf<T>(DetectedValue<T>? value)
        => value?.Confidence == DetectionConfidence.High
            ? FieldOrigin.DetectedReliably
            : FieldOrigin.DetectedUncertain;

    private static bool ComesFromTemplate(CompanyTemplate template, string value)
        => new[]
        {
            template.SellerName, template.SellerStreet, template.SellerPostalCode,
            template.SellerCity, template.SellerCountry, template.SellerVatId,
            template.SellerTaxNumber, template.SellerEmail, template.BankIban, template.BankBic,
        }.Any(t => !string.IsNullOrWhiteSpace(t)
                   && string.Equals(t, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sammelt, was die Vorbefüllung getan und was sie gelassen hat.</summary>
    private sealed class PrefillLog
    {
        private readonly List<string> _uncertain = [];
        private readonly List<string> _skipped = [];
        private readonly List<string> _protectedFields = [];
        private int _filled;
        private int _filledLines;
        private int _skippedExistingLines;
        private int _linesMissingUnit;

        public void Filled(string label, FieldOrigin origin)
        {
            _filled++;

            if (origin == FieldOrigin.DetectedUncertain)
            {
                _uncertain.Add(label);
            }
        }

        public void Skipped(string label) => _skipped.Add(label);

        public void Protected(string label) => _protectedFields.Add(label);

        public void FilledLines(int count, int missingUnit)
        {
            _filledLines = count;
            _linesMissingUnit = missingUnit;
        }

        public void SkippedExistingLines(int count) => _skippedExistingLines = count;

        public PrefillSummary ToSummary() => new(
            _filled, _uncertain, _skipped, _protectedFields,
            _filledLines, _skippedExistingLines, _linesMissingUnit);
    }
}

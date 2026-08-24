using System.Globalization;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Text;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Wie sicher eine Zeile der Übersicht ist.</summary>
public enum DetectionEntryKind
{
    /// <summary>Zweifelsfrei gelesen.</summary>
    Found,

    /// <summary>Gelesen, aber nicht eindeutig – der Anwender muss nachsehen.</summary>
    Uncertain,

    /// <summary>Nicht gefunden oder zu unsicher, um sie zu zeigen.</summary>
    Missing,
}

/// <summary>Eine Zeile der Übersicht „PDF analysiert“.</summary>
public sealed record DetectionEntry(DetectionEntryKind Kind, string Text);

/// <summary>
/// Fasst ein Erkennungsergebnis in Sätzen zusammen, die ein Anwender liest.
///
/// **Warum mit Werten:** Vorher stand dort nur „Rechnungsnummer erkannt“. Ob
/// die *richtige* Rechnungsnummer erkannt wurde, ließ sich erst zwei Schritte
/// später sehen. Im echten Testlauf wurde als Käufer „Währung: EUR“ erkannt –
/// mit dem Wert davor wäre das sofort aufgefallen.
///
/// **Ausnahme IBAN.** Sie erscheint nur maskiert. Die Übersicht steht offen im
/// Fenster, oft während einer Bildschirmübertragung; eine vollständige
/// Bankverbindung gehört dort nicht hin. Zum Prüfen genügt der Anfang.
///
/// Diese Klasse steht bewusst im Kern und nicht in der Oberfläche: Sie
/// entscheidet, **was** gemeldet wird, und ist damit ohne WPF prüfbar. Wie die
/// Zeilen aussehen – Zeichen, Farbe, Anordnung – entscheidet die Oberfläche.
/// </summary>
public static class DetectionOverview
{
    /// <summary>Beschreibt, was gefunden wurde – in Lesereihenfolge.</summary>
    public static IReadOnlyList<DetectionEntry> Describe(InvoiceDetectionResult detection)
    {
        ArgumentNullException.ThrowIfNull(detection);

        if (!detection.HasUsableText)
        {
            return
            [
                new DetectionEntry(
                    DetectionEntryKind.Missing,
                    "In dieser PDF wurde kein ausreichend verwertbarer Text gefunden. "
                    + "Die Rechnungsdaten müssen von Hand erfasst werden."),
            ];
        }

        return
        [
            .. DocumentEntries(detection),
            .. PartyEntries(detection),
            .. AmountEntries(detection),
            .. PaymentEntries(detection),
            LineItemEntry(detection),
        ];
    }

    /// <summary>
    /// Was die Positionserkennung gefunden hat – **nur die Anzahl**.
    ///
    /// Beschreibungen, Mengen und Preise gehören nicht hierher. Die Übersicht
    /// steht offen im Fenster, oft während einer Bildschirmübertragung; zum
    /// Prüfen genügt die Zahl. Die Werte selbst stehen in Schritt 2, wo sie
    /// ohnehin zu bestätigen sind.
    ///
    /// Diese Zeile beantwortet ausschließlich die Frage „was steht sicher in
    /// der PDF?“. Ob die Positionen tatsächlich in den Entwurf übernommen
    /// wurden, entscheidet erst die Vorbefüllung und meldet die Zusammenfassung
    /// in Schritt 2 – bei bereits erfassten Positionen sind das
    /// berechtigterweise zwei verschiedene Aussagen.
    /// </summary>
    private static DetectionEntry LineItemEntry(InvoiceDetectionResult detection)
    {
        if (detection.Lines.Count == 0)
        {
            return new DetectionEntry(
                DetectionEntryKind.Missing,
                "Rechnungspositionen nicht sicher erkannt. "
                + "Bitte erfassen Sie sie im nächsten Schritt von Hand.");
        }

        int missingUnit = detection.Lines.Count(line => line.UnitCode is null);

        return new DetectionEntry(
            DetectionEntryKind.Found,
            $"Rechnungspositionen erkannt: {detection.Lines.Count} – "
            + (missingUnit == 0
                ? "bitte im nächsten Schritt prüfen"
                : $"bei {Plural.Count(missingUnit, "Position", "Positionen")} ist keine "
                  + "Mengeneinheit angegeben. Bitte im nächsten Schritt ergänzen."));
    }

    private static IEnumerable<DetectionEntry> DocumentEntries(InvoiceDetectionResult d)
    {
        yield return Entry("Rechnungsnummer", d.InvoiceNumber, v => v);
        yield return Entry("Rechnungsdatum", d.IssueDate, Date);
        yield return Entry("Leistungsdatum", d.DeliveryDate, Date);
        yield return Entry("Leistungszeitraum von", d.BillingPeriodStart, Date);
        yield return Entry("Leistungszeitraum bis", d.BillingPeriodEnd, Date);
        yield return Entry("Fälligkeitsdatum", d.DueDate, Date);
        yield return Entry("Währung", d.Currency, v => v);
    }

    private static IEnumerable<DetectionEntry> PartyEntries(InvoiceDetectionResult d)
    {
        yield return Entry("Rechnungssteller", d.Seller.Name, v => v);
        yield return Entry("Empfänger", d.Buyer.Name, v => v);
        yield return Entry("Ort des Empfängers", d.Buyer.City, v => v);
        yield return Entry("Land des Empfängers", d.Buyer.Country, v => v);
        yield return Entry("USt-IdNr. des Empfängers", d.Buyer.VatId, v => v);
        yield return Entry("E-Mail des Empfängers", d.Buyer.Email, v => v);
    }

    private static IEnumerable<DetectionEntry> AmountEntries(InvoiceDetectionResult d)
    {
        yield return Entry("Nettobetrag", d.Totals.Net, Money);
        yield return Entry("Umsatzsteuer", d.Totals.Tax, Money);
        yield return Entry("Gesamtbetrag", d.Totals.Gross, Money);
        yield return Entry("Zahlbetrag", d.Totals.Payable, Money);
        yield return VatRateEntry(d.Totals.VatRates);
    }

    private static IEnumerable<DetectionEntry> PaymentEntries(InvoiceDetectionResult d)
    {
        yield return Entry("IBAN", d.Iban, Masked);
        yield return Entry("BIC", d.Bic, v => v);
    }

    /// <summary>
    /// Mehrere Steuersätze sind erlaubt und kommen vor. Sie stehen deshalb in
    /// einer Zeile beisammen; sobald einer davon unsicher ist, gilt die ganze
    /// Zeile als zu prüfen.
    /// </summary>
    private static DetectionEntry VatRateEntry(IReadOnlyList<DetectedValue<decimal>> rates)
    {
        DetectedValue<decimal>[] usable = [.. rates.Where(r => r.IsUsable)];

        if (usable.Length == 0)
        {
            return NotFound("Umsatzsteuersatz");
        }

        string text = string.Join(", ", usable.Select(r => Percent(r.Value)).Distinct(StringComparer.Ordinal));

        return usable.All(r => r.Confidence == DetectionConfidence.High)
            ? Found("Umsatzsteuersatz", text)
            : Uncertain("Umsatzsteuersatz", text);
    }

    /// <summary>
    /// Die eine Entscheidung je Angabe: gefunden, unsicher oder gar nicht da.
    ///
    /// Ein Wert unterhalb der Übernahmeschwelle gilt hier als nicht gefunden.
    /// Ihn samt Wert anzuzeigen, obwohl das Formular ihn nicht übernimmt, wäre
    /// irreführend.
    /// </summary>
    private static DetectionEntry Entry<T>(
        string label, DetectedValue<T>? detected, Func<T, string> format)
    {
        if (detected is not { IsUsable: true })
        {
            return NotFound(label);
        }

        string text = format(detected.Value);

        return detected.Confidence == DetectionConfidence.High
            ? Found(label, text)
            : Uncertain(label, text);
    }

    private static DetectionEntry Found(string label, string value)
        => new(DetectionEntryKind.Found, $"{label} erkannt: {value}");

    private static DetectionEntry Uncertain(string label, string value)
        => new(DetectionEntryKind.Uncertain, $"{label} erkannt: {value} – bitte prüfen");

    private static DetectionEntry NotFound(string label)
        => new(DetectionEntryKind.Missing, $"{label} nicht gefunden");

    /// <summary>
    /// Nur Anfang und Ende der IBAN. Mehr braucht niemand, um zu erkennen, ob
    /// die richtige Bankverbindung gelesen wurde.
    /// </summary>
    private static string Masked(string iban) => Iban.Mask(iban);

    private static string Date(DateOnly value)
        => value.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);

    private static string Money(decimal value)
        => value.ToString("N2", CultureInfo.CurrentCulture);

    private static string Percent(decimal value)
        => value.ToString("0.##", CultureInfo.CurrentCulture) + " %";
}

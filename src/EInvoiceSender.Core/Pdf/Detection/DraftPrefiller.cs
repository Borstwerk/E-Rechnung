using System.Globalization;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Was die Vorbefuellung getan hat – Grundlage der Anzeige.</summary>
/// <param name="FilledFields">Anzahl übernommener Felder.</param>
/// <param name="UncertainFields">Bezeichnungen der Felder, die zu prüfen sind.</param>
/// <param name="SkippedLowConfidence">Werte, die zu unsicher zur Übernahme waren.</param>
/// <param name="SkippedProtected">Felder, die bereits einen höherrangigen Wert trugen.</param>
public sealed record PrefillSummary(
    int FilledFields,
    IReadOnlyList<string> UncertainFields,
    IReadOnlyList<string> SkippedLowConfidence,
    IReadOnlyList<string> SkippedProtected);

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
/// * Ein unsicher gelesener Wert fuellt gar nichts.
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
        });

        return log.ToSummary();
    }

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

    /// <summary>Sammelt, was die Vorbefuellung getan und was sie gelassen hat.</summary>
    private sealed class PrefillLog
    {
        private readonly List<string> _uncertain = [];
        private readonly List<string> _skipped = [];
        private readonly List<string> _protectedFields = [];
        private int _filled;

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

        public PrefillSummary ToSummary() => new(_filled, _uncertain, _skipped, _protectedFields);
    }
}

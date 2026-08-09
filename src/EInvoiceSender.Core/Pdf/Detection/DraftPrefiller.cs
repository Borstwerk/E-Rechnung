using System.Globalization;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Was die Vorbefuellung getan hat – Grundlage der Anzeige.</summary>
/// <param name="FilledFields">Anzahl uebernommener Felder.</param>
/// <param name="UncertainFields">Bezeichnungen der Felder, die zu pruefen sind.</param>
/// <param name="SkippedLowConfidence">Werte, die zu unsicher zur Uebernahme waren.</param>
public sealed record PrefillSummary(
    int FilledFields,
    IReadOnlyList<string> UncertainFields,
    IReadOnlyList<string> SkippedLowConfidence);

/// <summary>
/// Traegt ein Erkennungsergebnis in das Eingabeformular ein.
///
/// Das ist die einzige Stelle, an der erkannte Werte den Weg ins Formular
/// finden – und damit die Stelle, an der die Vertrauensstufe wirkt:
///
/// * <see cref="DetectionConfidence.High"/> und
///   <see cref="DetectionConfidence.Medium"/> fuellen ein Feld und werden dort
///   gekennzeichnet.
/// * <see cref="DetectionConfidence.Low"/> fuellt **nichts**. Solche Werte
///   werden nur gezaehlt, damit die Oberflaeche sagen kann, dass etwas gefunden
///   wurde, das der Anwender selbst beurteilen muss.
///
/// Ein Feld, das der Anwender bereits ausgefuellt hat, wird nie ueberschrieben.
/// </summary>
public static class DraftPrefiller
{
    /// <summary>Traegt das Erkennungsergebnis in den Entwurf ein.</summary>
    public static PrefillSummary Apply(
        InvoiceDraft draft, InvoiceDetectionResult detection, CompanyTemplate? ownCompany = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(detection);

        var filled = 0;
        var uncertain = new List<string>();
        var skipped = new List<string>();

        draft.Prefill(d =>
        {
            void Text(string label, string property, DetectedValue<string>? value, Action<string> set)
            {
                if (value is null)
                {
                    return;
                }

                if (!value.IsUsable)
                {
                    skipped.Add(label);

                    return;
                }

                set(value.Value);
                d.MarkOrigin(property, OriginFor(value.Confidence, ownCompany, value));
                filled++;

                if (value.Confidence == DetectionConfidence.Medium)
                {
                    uncertain.Add(label);
                }
            }

            void Date(string label, string property, DetectedValue<DateOnly>? value, Action<DateOnly> set)
            {
                if (value is null)
                {
                    return;
                }

                if (!value.IsUsable)
                {
                    skipped.Add(label);

                    return;
                }

                set(value.Value);
                d.MarkOrigin(property, OriginFor(value.Confidence, ownCompany, null));
                filled++;

                if (value.Confidence == DetectionConfidence.Medium)
                {
                    uncertain.Add(label);
                }
            }

            // --- Dokument --------------------------------------------------
            if (string.IsNullOrWhiteSpace(d.InvoiceNumber))
            {
                Text("Rechnungsnummer", nameof(d.InvoiceNumber), detection.InvoiceNumber, v => d.InvoiceNumber = v);
            }

            Date("Rechnungsdatum", nameof(d.IssueDate), detection.IssueDate, v => d.IssueDate = v);
            Date("Leistungsdatum", nameof(d.DeliveryDate), detection.DeliveryDate, v => d.DeliveryDate = v);
            Date("Faelligkeitsdatum", nameof(d.DueDate), detection.DueDate, v => d.DueDate = v);
            Text("Waehrung", nameof(d.Currency), detection.Currency, v => d.Currency = v);

            // --- Verkaeufer ------------------------------------------------
            Text("Verkaeufer", nameof(d.SellerName), detection.Seller.Name, v => d.SellerName = v);
            Text("Strasse (Verkaeufer)", nameof(d.SellerStreet), detection.Seller.Street, v => d.SellerStreet = v);
            Text("PLZ (Verkaeufer)", nameof(d.SellerPostalCode), detection.Seller.PostalCode, v => d.SellerPostalCode = v);
            Text("Ort (Verkaeufer)", nameof(d.SellerCity), detection.Seller.City, v => d.SellerCity = v);
            Text("Land (Verkaeufer)", nameof(d.SellerCountry), detection.Seller.Country, v => d.SellerCountry = v);
            Text("USt-IdNr. (Verkaeufer)", nameof(d.SellerVatId), detection.Seller.VatId, v => d.SellerVatId = v);
            Text("Steuernummer", nameof(d.SellerTaxNumber), detection.Seller.TaxNumber, v => d.SellerTaxNumber = v);
            Text("E-Mail (Verkaeufer)", nameof(d.SellerEmail), detection.Seller.Email, v => d.SellerEmail = v);

            // --- Kaeufer ---------------------------------------------------
            Text("Kaeufer", nameof(d.BuyerName), detection.Buyer.Name, v => d.BuyerName = v);
            Text("Strasse (Kaeufer)", nameof(d.BuyerStreet), detection.Buyer.Street, v => d.BuyerStreet = v);
            Text("PLZ (Kaeufer)", nameof(d.BuyerPostalCode), detection.Buyer.PostalCode, v => d.BuyerPostalCode = v);
            Text("Ort (Kaeufer)", nameof(d.BuyerCity), detection.Buyer.City, v => d.BuyerCity = v);
            Text("USt-IdNr. (Kaeufer)", nameof(d.BuyerVatId), detection.Buyer.VatId, v => d.BuyerVatId = v);
            Text("E-Mail (Kaeufer)", nameof(d.BuyerEmail), detection.Buyer.Email, v => d.BuyerEmail = v);

            // --- Bankverbindung --------------------------------------------
            Text("IBAN", nameof(d.BankIban), detection.Iban, v => d.BankIban = v);
            Text("BIC", nameof(d.BankBic), detection.Bic, v => d.BankBic = v);

            // --- Positionen ------------------------------------------------
            // Bewusst nur bei ausreichender Sicherheit. Eine falsche Position
            // veraendert den Rechnungsbetrag - der schlimmstmoegliche Fehler,
            // den eine Vorbefuellung machen kann.
            if (detection.LinesConfidence >= DetectionConfidence.Medium && d.Lines.Count == 0)
            {
                foreach (DetectedLine line in detection.Lines)
                {
                    InvoiceLineDraft target = d.AddLine();
                    target.Name = line.Description;
                    target.Quantity = Format(line.Quantity) ?? target.Quantity;
                    target.Unit = line.Unit ?? target.Unit;
                    target.NetUnitPrice = Format(line.NetUnitPrice) ?? target.NetUnitPrice;
                    target.VatRate = Format(line.VatRate) ?? target.VatRate;
                    filled++;
                }

                if (detection.Lines.Count > 0)
                {
                    uncertain.Add($"{detection.Lines.Count} Position(en)");
                }
            }
            else if (detection.Lines.Count > 0)
            {
                skipped.Add($"{detection.Lines.Count} moegliche Position(en)");
            }
        });

        return new PrefillSummary(filled, uncertain, skipped);
    }

    /// <summary>
    /// Ein Wert, der wortgleich in der gespeicherten Vorlage steht, wird als
    /// "aus Vorlage" ausgewiesen. Das ist ehrlicher als "aus PDF erkannt": Die
    /// PDF hat ihn nur bestaetigt.
    /// </summary>
    private static FieldOrigin OriginFor(
        DetectionConfidence confidence, CompanyTemplate? ownCompany, DetectedValue<string>? value)
    {
        if (ownCompany is not null && value is not null && ComesFromTemplate(ownCompany, value.Value))
        {
            return FieldOrigin.Template;
        }

        return confidence == DetectionConfidence.High
            ? FieldOrigin.DetectedReliably
            : FieldOrigin.DetectedUncertain;
    }

    private static bool ComesFromTemplate(CompanyTemplate template, string value)
        => new[]
        {
            template.SellerName, template.SellerStreet, template.SellerPostalCode,
            template.SellerCity, template.SellerCountry, template.SellerVatId,
            template.SellerTaxNumber, template.SellerEmail, template.BankIban, template.BankBic,
        }.Any(t => !string.IsNullOrWhiteSpace(t)
                   && string.Equals(t, value, StringComparison.OrdinalIgnoreCase));

    private static string? Format(decimal? value)
        => value?.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
}

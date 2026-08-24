using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Der Weg der beiden neuen Verkäuferkennungen vom Formular ins Domänenmodell.
///
/// BR-CO-26 verlangt, dass der Empfänger den Rechnungssteller maschinell
/// identifizieren kann. Dafür genügt jede dieser drei Angaben: die
/// Verkäuferkennung (BT-29), die Registerkennung (BT-30) oder die USt-IdNr.
/// (BT-31). Bisher konnte der Anwender im Formular nur die dritte eintragen –
/// wer keine USt-IdNr. hat, kam nicht weiter, obwohl die Norm ihm zwei andere
/// Wege lässt.
///
/// Die Steuernummer (BT-32) ist ausdrücklich keiner davon.
/// </summary>
public sealed class SellerIdentificationDraftTests
{
    [Fact]
    public void DieRegisterkennungErreichtDenVerkäufer()
    {
        InvoiceDraft draft = Complete();
        draft.SellerLegalRegistrationId = "HRB 12345";

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.Equal("HRB 12345", invoice!.Seller.LegalRegistrationId);
    }

    [Fact]
    public void DieVerkäuferkennungErreichtDenVerkäufer()
    {
        InvoiceDraft draft = Complete();
        draft.SellerIdentifier = "LIEF-4711";

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.Equal("LIEF-4711", invoice!.Seller.SellerIdentifier);
    }

    /// <summary>
    /// Ein nicht ausgefülltes Feld ist keine Angabe. Als leerer Text
    /// weitergereicht, entstünde in der Datei ein leeres Pflichtelement –
    /// schlimmer als eine fehlende Angabe, weil es eine vortäuscht.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LeereKennungenErreichenDenVerkäuferGarNicht(string leer)
    {
        InvoiceDraft draft = Complete();
        draft.SellerLegalRegistrationId = leer;
        draft.SellerIdentifier = leer;

        draft.TryBuildInvoice(out Invoice? invoice);

        Assert.Null(invoice!.Seller.LegalRegistrationId);
        Assert.Null(invoice.Seller.SellerIdentifier);
    }

    /// <summary>Umgebende Leerzeichen gehören nicht in eine Kennung.</summary>
    [Fact]
    public void UmgebendeLeerzeichenWerdenEntfernt()
    {
        InvoiceDraft draft = Complete();
        draft.SellerLegalRegistrationId = "  HRB 12345  ";
        draft.SellerIdentifier = "  LIEF-4711  ";

        draft.TryBuildInvoice(out Invoice? invoice);

        Assert.Equal("HRB 12345", invoice!.Seller.LegalRegistrationId);
        Assert.Equal("LIEF-4711", invoice.Seller.SellerIdentifier);
    }

    /// <summary>
    /// **Der Fall, um den es geht.** Ein Kleinunternehmer ohne USt-IdNr., dem
    /// sein Kunde eine Lieferantennummer mitgeteilt hat, muss eine Rechnung
    /// erzeugen können. Die Steuernummer allein trägt ihn nicht durch – die
    /// Lieferantennummer schon.
    /// </summary>
    [Fact]
    public void OhneUstIdTrägtDieVerkäuferkennungDieRechnungDurch()
    {
        InvoiceDraft draft = Complete();
        draft.SellerVatId = string.Empty;
        draft.SellerTaxNumber = "079/123/45678";

        // Erst die Gegenprobe: Mit der Steuernummer allein ist der
        // Rechnungssteller nach BR-CO-26 nicht identifizierbar.
        draft.TryBuildInvoice(out Invoice? ohneKennung);
        Assert.Contains(
            Rules(ohneKennung!).Findings,
            f => f.NormRule == "BR-CO-26");

        draft.SellerIdentifier = "LIEF-4711";

        ValidationReport build = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(build.HasErrors, Describe(build));
        Assert.Null(invoice!.Seller.VatId);
        Assert.Equal("079/123/45678", invoice.Seller.TaxNumber);
        Assert.Equal("LIEF-4711", invoice.Seller.SellerIdentifier);

        Assert.False(Rules(invoice).HasErrors, Describe(Rules(invoice)));
    }

    /// <summary>Dasselbe über die Registerkennung.</summary>
    [Fact]
    public void OhneUstIdTrägtDieRegisterkennungDieRechnungDurch()
    {
        InvoiceDraft draft = Complete();
        draft.SellerVatId = string.Empty;
        draft.SellerTaxNumber = "079/123/45678";
        draft.SellerLegalRegistrationId = "HRB 12345";

        draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(Rules(invoice!).HasErrors, Describe(Rules(invoice!)));
    }

    /// <summary>
    /// **Keine Erkennung aus der PDF – und das ist die Absicht.** Beide
    /// Kennungen stehen auf Rechnungen an ganz verschiedenen Stellen, oft ohne
    /// Beschriftung, und lassen sich von Kundennummern, Auftragsnummern oder
    /// Bestellnummern nicht sicher unterscheiden. Eine geratene Kennung wäre
    /// schlimmer als keine: Sie ginge unbemerkt in eine Rechnung, die formal
    /// gültig aussieht und den falschen Absender ausweist.
    ///
    /// Der Test hält diese Entscheidung fest. Wer später eine Erkennung
    /// einbaut, kommt an ihm nicht vorbei.
    /// </summary>
    [Fact]
    public void KeineErkennungFülltDieBeidenKennungen()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Seller = new DetectedParty
            {
                Name = new DetectedValue<string>("Muster IT GmbH", DetectionConfidence.High),
                VatId = new DetectedValue<string>("DE123456789", DetectionConfidence.High),
                TaxNumber = new DetectedValue<string>("079/123/45678", DetectionConfidence.High),
            },
        });

        Assert.Equal("Muster IT GmbH", draft.SellerName);
        Assert.Equal(string.Empty, draft.SellerLegalRegistrationId);
        Assert.Equal(string.Empty, draft.SellerIdentifier);

        // Auch das Erkennungsergebnis selbst kennt die beiden Felder nicht.
        Assert.DoesNotContain(
            typeof(DetectedParty).GetProperties(),
            property => property.Name is "LegalRegistrationId" or "SellerIdentifier");

        Assert.DoesNotContain(
            Enum.GetNames<DetectedOwnCompanyFieldKind>(),
            name => name is "SellerLegalRegistrationId" or "SellerIdentifier");
    }

    // ------------------------------------------------------------- Hilfsmittel

    private static ValidationReport Rules(Invoice invoice)
        => new En16931RuleValidator().Validate(invoice, InvoiceCalculator.Calculate(invoice));

    private static InvoiceDraft Complete()
    {
        var draft = new InvoiceDraft
        {
            InvoiceNumber = "RE-2026-0815",
            IssueDate = new DateOnly(2026, 8, 9),
            SellerName = "Muster IT GmbH",
            SellerStreet = "Musterstraße 10",
            SellerPostalCode = "18055",
            SellerCity = "Rostock",
            SellerCountry = "DE",
            SellerVatId = "DE123456789",
            BuyerName = "Nordlicht Handel GmbH",
            BuyerStreet = "Hafenweg 3",
            BuyerPostalCode = "20095",
            BuyerCity = "Hamburg",
            BuyerCountry = "DE",
            BankAccountHolder = "Muster IT GmbH",
            BankIban = "DE89370400440532013000",
            PaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.",
            DueDate = new DateOnly(2026, 8, 23),
        };

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Systemadministration";
        line.Quantity = "4";
        line.Unit = "HUR";
        line.NetUnitPrice = "85,00";
        line.VatRate = "19";

        return draft;
    }

    private static string Describe(ValidationReport report)
        => string.Join(
            Environment.NewLine,
            report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));
}

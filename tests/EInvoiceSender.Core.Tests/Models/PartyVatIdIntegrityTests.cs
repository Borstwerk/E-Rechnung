using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Die USt-IdNr. von Verkäufer und Käufer darf auf dem Weg zur E-Rechnung
/// nirgends vertauscht, verloren oder überschrieben werden.
///
/// **Warum es diese Tests gibt:** In einer Windows-Abnahme stand im erzeugten
/// <c>factur-x.xml</c> beim Käufer die USt-IdNr. des Verkäufers. Die Suche
/// nach der Ursache zeigte eine Lücke im Testnetz: Kein einziger Test setzte
/// beide Kennungen gleichzeitig auf **verschiedene** Werte. Mit gleichen
/// Werten – oder mit nur einem gesetzten – wäre jede Vertauschung unsichtbar
/// geblieben.
///
/// Deshalb sind hier alle Werte ausdrücklich verschieden. Ein Test, der eine
/// Verwechslung nicht sehen könnte, prüft an dieser Stelle nichts.
///
/// Die USt-IdNr. ist keine Nebensache: Sie entscheidet über Reverse-Charge und
/// innergemeinschaftliche Lieferung. Eine vertauschte Kennung ergibt eine
/// formal gültige, steuerlich falsche Rechnung.
/// </summary>
public sealed class PartyVatIdIntegrityTests
{
    private const string SellerVat = "DE123456789";
    private const string BuyerVat = "DE987654321";

    [Fact]
    public void DerEntwurfÜbergibtBeideKennungenAnDieRichtigePartei()
    {
        InvoiceDraft draft = Complete();

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);

        Assert.Equal(SellerVat, invoice.Seller.VatId);
        Assert.Equal(BuyerVat, invoice.Buyer.VatId);
        Assert.NotEqual(invoice.Seller.VatId, invoice.Buyer.VatId);
    }

    /// <summary>
    /// **Die Probe, die den gemeldeten Befund gesehen hätte.** In der CII-Datei
    /// muss die Kennung des Verkäufers unter <c>SellerTradeParty</c> stehen und
    /// die des Käufers unter <c>BuyerTradeParty</c> – jede mit dem Schema
    /// <c>VA</c>, nicht als Steuernummer <c>FC</c>.
    /// </summary>
    [Fact]
    public void DieCiiDateiOrdnetBeideKennungenDerRichtigenParteiZu()
    {
        InvoiceDraft draft = Complete();
        draft.TryBuildInvoice(out Invoice? invoice);

        string xml = Encoding.UTF8.GetString(
            new CiiInvoiceWriter().Write(invoice!, InvoiceCalculator.Calculate(invoice!)));

        Assert.Equal(SellerVat, VatIdOf(xml, "SellerTradeParty"));
        Assert.Equal(BuyerVat, VatIdOf(xml, "BuyerTradeParty"));
    }

    /// <summary>
    /// Eine von Hand eingetragene Kennung überlebt jede spätere Vorbefüllung.
    ///
    /// Genau das ist der Ablauf aus der Abnahme: Firmenvorlage laden, PDF
    /// erkennen, Käuferkennung korrigieren, weiter. Was der Anwender getippt
    /// hat, gehört ihm – keine Erkennung und keine Vorlage darf es ersetzen.
    /// </summary>
    [Fact]
    public void EineVonHandKorrigierteKennungWirdNichtWiederÜberschrieben()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, DetectionWith(buyerVatId: "DE111111111"));

        Assert.Equal("DE111111111", draft.BuyerVatId);

        draft.BuyerVatId = BuyerVat;

        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.BuyerVatId)));

        // Weder eine erneute Erkennung noch eine Firmenvorlage darf daran rühren.
        DraftPrefiller.Apply(draft, DetectionWith(buyerVatId: "DE111111111"));
        CompanyTemplateApplier.Apply(draft, Template());

        Assert.Equal(BuyerVat, draft.BuyerVatId);
    }

    /// <summary>
    /// Die Firmenvorlage füllt die Verkäuferkennung, nicht die des Käufers.
    /// Eine Vorlage, die in ein Käuferfeld schriebe, wäre die gefährlichste
    /// Form dieses Fehlers – sie träfe jede Rechnung gleichermaßen.
    /// </summary>
    [Fact]
    public void DieFirmenvorlageFülltNurDieVerkäuferkennung()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template());

        Assert.Equal(SellerVat, draft.SellerVatId);
        Assert.Equal(string.Empty, draft.BuyerVatId);
    }

    /// <summary>
    /// Die Vorbefüllung aus der PDF trennt die beiden Parteien ebenso.
    /// </summary>
    [Fact]
    public void DieVorbefüllungTrenntVerkäuferUndKäufer()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, DetectionWith(BuyerVat, SellerVat));

        Assert.Equal(SellerVat, draft.SellerVatId);
        Assert.Equal(BuyerVat, draft.BuyerVatId);
    }

    /// <summary>
    /// **Der Ausgabeweg muss vor dem Schreiben abbrechen.** Ein Verkäufer, der
    /// nur eine Steuernummer trägt, ist nach BR-CO-26 nicht identifizierbar.
    ///
    /// Der Abbruch geschieht dabei ausdrücklich **nicht** im Entwurf: Das
    /// Domainobjekt entsteht, weil sich alle Angaben lesen lassen. Erst die
    /// EN-16931-Regelprüfung stoppt den Weg danach. Diese Arbeitsteilung ist
    /// gewollt – der Entwurf meldet, was er nicht lesen kann, das Regelwerk
    /// entscheidet, was fehlt.
    ///
    /// Warum das hier zusätzlich zur Regelprüfung steht: In einer
    /// Windows-Abnahme kam genau so eine Rechnung durch die eingebauten
    /// Prüfungen, wurde erzeugt, und erst der externe Validator beanstandete
    /// sie. Der Anwender soll das im Formular erfahren – nicht beim Empfänger.
    /// </summary>
    [Fact]
    public void NurEineSteuernummerWirdVomRegelwerkVorDerAusgabeGestoppt()
    {
        InvoiceDraft draft = Complete();
        draft.SellerVatId = string.Empty;
        draft.SellerTaxNumber = "079/123/45678";

        ValidationReport build = draft.TryBuildInvoice(out Invoice? invoice);

        // Der Entwurf liest die Angaben und baut das Domainobjekt. Ob eine
        // Pflichtangabe fehlt, entscheidet er nicht.
        Assert.False(build.HasErrors, Describe(build));
        Assert.NotNull(invoice);
        Assert.Null(invoice.Seller.VatId);
        Assert.Null(invoice.Seller.LegalRegistrationId);
        Assert.Equal("079/123/45678", invoice.Seller.TaxNumber);

        ValidationReport rules = new En16931RuleValidator().Validate(
            invoice, InvoiceCalculator.Calculate(invoice));

        Assert.True(rules.HasErrors);
        Assert.Contains(rules.Findings, f => f.NormRule == "BR-CO-26");
    }

    /// <summary>
    /// Mit hinterlegter Handelsregisternummer entsteht die Rechnung, und die
    /// Kennung steht als <c>SpecifiedLegalOrganization/ID</c> in der CII-Datei
    /// – dort, wo das CEN-Schematron sie für BR-CO-26 sucht.
    /// </summary>
    [Fact]
    public void EineHandelsregisternummerTrägtDieRechnungDurch()
    {
        InvoiceDraft draft = Complete();
        draft.SellerVatId = string.Empty;

        draft.TryBuildInvoice(out Invoice? invoice);

        Invoice withRegistration = invoice! with
        {
            Seller = invoice.Seller with { LegalRegistrationId = "HRB 12345" },
        };

        InvoiceTotals totals = InvoiceCalculator.Calculate(withRegistration);

        Assert.False(
            new En16931RuleValidator().Validate(withRegistration, totals).HasErrors);

        string xml = Encoding.UTF8.GetString(
            new CiiInvoiceWriter().Write(withRegistration, totals));

        Assert.Contains("<ram:SpecifiedLegalOrganization>", xml, StringComparison.Ordinal);
        Assert.Contains("HRB 12345", xml, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- Hilfsmittel

    /// <summary>
    /// Liest die USt-IdNr. einer Partei aus der CII-Datei – ausdrücklich nur
    /// den Abschnitt dieser Partei, damit ein Wert der anderen nicht
    /// versehentlich als Treffer durchgeht.
    /// </summary>
    private static string VatIdOf(string xml, string party)
    {
        int start = xml.IndexOf("<ram:" + party + ">", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{party} fehlt in der CII-Datei.");

        int end = xml.IndexOf("</ram:" + party + ">", start, StringComparison.Ordinal);
        Assert.True(end > start, $"{party} ist nicht geschlossen.");

        string section = xml[start..end];
        const string marker = "<ram:ID schemeID=\"VA\">";
        int id = section.IndexOf(marker, StringComparison.Ordinal);

        Assert.True(id >= 0, $"{party} trägt keine USt-IdNr. mit Schema VA.");

        int from = id + marker.Length;

        return section[from..section.IndexOf("</ram:ID>", from, StringComparison.Ordinal)];
    }

    private static CompanyTemplate Template() => new()
    {
        SellerName = "Muster IT GmbH",
        SellerStreet = "Musterstraße 10",
        SellerPostalCode = "18055",
        SellerCity = "Rostock",
        SellerCountry = "DE",
        SellerVatId = SellerVat,
    };

    private static InvoiceDetectionResult DetectionWith(
        string buyerVatId, string? sellerVatId = null) => new()
        {
            HasUsableText = true,
            Seller = sellerVatId is null
                ? new DetectedParty()
                : new DetectedParty
                {
                    VatId = new DetectedValue<string>(sellerVatId, DetectionConfidence.High),
                },
            Buyer = new DetectedParty
            {
                VatId = new DetectedValue<string>(buyerVatId, DetectionConfidence.High),
            },
        };

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
            SellerVatId = SellerVat,
            BuyerName = "Nordlicht Handel GmbH",
            BuyerStreet = "Hafenweg 3",
            BuyerPostalCode = "20095",
            BuyerCity = "Hamburg",
            BuyerCountry = "DE",
            BuyerVatId = BuyerVat,
            BankAccountHolder = "Muster IT GmbH",
            BankIban = "DE89370400440532013000",
            PaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.",
            DueDate = new DateOnly(2026, 8, 23),
        };

        Add(draft, "Systemadministration", "4", "HUR", "85,00");
        Add(draft, "Netzwerkkonfiguration", "2", "HUR", "95,00");
        Add(draft, "Technische Dokumentation", "1", "C62", "70,00");

        return draft;
    }

    private static void Add(
        InvoiceDraft draft, string name, string quantity, string unit, string price)
    {
        InvoiceLineDraft line = draft.AddLine();
        line.Name = name;
        line.Quantity = quantity;
        line.Unit = unit;
        line.NetUnitPrice = price;
        line.VatRate = "19";
    }

    private static string Describe(ValidationReport report)
        => string.Join(
            Environment.NewLine,
            report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));
}

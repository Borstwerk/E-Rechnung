using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using Xunit;

namespace EInvoiceSender.Core.Tests.Settings;

/// <summary>
/// Prüft die Übernahme geänderter Firmenvorgaben in ein laufendes Formular.
///
/// **Der Fall aus dem manuellen Test:** Rechnung offen, Schritt 2 offen,
/// Einstellungen geöffnet, Verkäuferdaten geändert, gespeichert, zurück – und
/// im Formular stand weiterhin die alte Anschrift.
///
/// Die Vorsicht dahinter war richtig: Mitten in einem Vorgang darf nichts
/// ungefragt überschrieben werden. Nur galt sie zu pauschal. Der Anwender wird
/// jetzt gefragt; sagt er ja, greift die Übernahme – aber ausschließlich für
/// Angaben aus der Vorlage und nur dort, wo er nicht selbst eingegriffen hat.
/// </summary>
public sealed class CompanyTemplateApplierTests
{
    private static CompanyTemplate Template { get; } = new()
    {
        SellerName = "Muster IT GmbH",
        SellerStreet = "Musterstraße 10",
        SellerPostalCode = "18055",
        SellerCity = "Rostock",
        SellerCountry = "DE",
        SellerEmail = "rechnung@example.invalid",
        SellerVatId = "DE123456789",
        SellerTaxNumber = "079/123/45678",
        BankAccountHolder = "Muster IT GmbH",
        BankIban = "DE89370400440532013000",
        BankBic = "COBADEFFXXX",
        DefaultCurrency = "EUR",
        DefaultPaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.",
        DefaultPaymentTermDays = 14,
    };

    [Theory]
    [InlineData(nameof(InvoiceDraft.SellerName), "Muster IT GmbH")]
    [InlineData(nameof(InvoiceDraft.SellerStreet), "Musterstraße 10")]
    [InlineData(nameof(InvoiceDraft.SellerPostalCode), "18055")]
    [InlineData(nameof(InvoiceDraft.SellerCity), "Rostock")]
    [InlineData(nameof(InvoiceDraft.SellerCountry), "DE")]
    [InlineData(nameof(InvoiceDraft.SellerEmail), "rechnung@example.invalid")]
    [InlineData(nameof(InvoiceDraft.SellerVatId), "DE123456789")]
    [InlineData(nameof(InvoiceDraft.SellerTaxNumber), "079/123/45678")]
    [InlineData(nameof(InvoiceDraft.BankAccountHolder), "Muster IT GmbH")]
    [InlineData(nameof(InvoiceDraft.BankIban), "DE89370400440532013000")]
    [InlineData(nameof(InvoiceDraft.BankBic), "COBADEFFXXX")]
    [InlineData(nameof(InvoiceDraft.Currency), "EUR")]
    public void DieVorlageFülltIhreEigenenFelder(string property, string expected)
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Equal(expected, typeof(InvoiceDraft).GetProperty(property)!.GetValue(draft));
    }

    /// <summary>
    /// Übernommene Werte stammen aus der Vorlage und werden auch so vermerkt.
    /// Vorher galten sie als Benutzereingabe – eine Behauptung, die niemand
    /// aufgestellt hatte, und der Grund, warum eine spätere Übernahme
    /// unmöglich war.
    /// </summary>
    [Fact]
    public void ÜbernommeneWerteGeltenAlsAusDerVorlage()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Equal(FieldOrigin.Template, draft.OriginOf(nameof(draft.SellerName)));
        Assert.Equal(FieldOrigin.Template, draft.OriginOf(nameof(draft.BankIban)));
    }

    /// <summary>
    /// Der Schutz, auf den es ankommt: Was der Anwender in diesem Vorgang
    /// selbst eingetragen hat, bleibt stehen.
    /// </summary>
    [Fact]
    public void EineEigeneEingabeBleibtStehen()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template);

        draft.SellerName = "Von Hand geändert";

        CompanyTemplateApplier.Apply(draft, Template with { SellerName = "Neuer Firmenname" });

        Assert.Equal("Von Hand geändert", draft.SellerName);
    }

    /// <summary>
    /// Die Gegenprobe: Ein Feld, das der Anwender nicht angefasst hat,
    /// aktualisiert sich sehr wohl.
    /// </summary>
    [Fact]
    public void EinUnangetastetesFeldWirdAktualisiert()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template);
        CompanyTemplateApplier.Apply(draft, Template with { SellerStreet = "Neue Straße 5" });

        Assert.Equal("Neue Straße 5", draft.SellerStreet);
    }

    /// <summary>
    /// Rechnungsspezifische Angaben gehören nicht zur Firmenvorlage und werden
    /// nie angefasst – auch dann nicht, wenn der Anwender die Übernahme
    /// ausdrücklich will.
    /// </summary>
    [Fact]
    public void RechnungsspezifischeAngabenBleibenUnberührt()
    {
        var draft = new InvoiceDraft
        {
            InvoiceNumber = "RE-2026-0815",
            BuyerName = "Nordlicht Handel GmbH",
            BuyerCity = "Hamburg",
            BuyerCountry = "DE",
            DeliveryDate = new DateOnly(2026, 8, 8),
        };

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Systemadministration";
        line.Quantity = "4";
        line.NetUnitPrice = "85,00";

        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Equal("RE-2026-0815", draft.InvoiceNumber);
        Assert.Equal("Nordlicht Handel GmbH", draft.BuyerName);
        Assert.Equal("Hamburg", draft.BuyerCity);
        Assert.Equal(new DateOnly(2026, 8, 8), draft.DeliveryDate);

        InvoiceLineDraft danach = Assert.Single(draft.Lines);
        Assert.Equal("Systemadministration", danach.Name);
        Assert.Equal("4", danach.Quantity);
        Assert.Equal("85,00", danach.NetUnitPrice);
    }

    /// <summary>
    /// Das Zahlungsziel ergibt sich aus dem Rechnungsdatum. Ein von Hand
    /// gesetztes Fälligkeitsdatum bleibt trotzdem stehen.
    /// </summary>
    [Fact]
    public void EinSelbstGesetztesFälligkeitsdatumBleibtStehen()
    {
        var draft = new InvoiceDraft
        {
            IssueDate = new DateOnly(2026, 8, 9),
            DueDate = new DateOnly(2026, 9, 30),
        };

        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Equal(new DateOnly(2026, 9, 30), draft.DueDate);
    }

    [Fact]
    public void OhneEigenesDatumErgibtSichDieFälligkeitAusDemZahlungsziel()
    {
        var draft = new InvoiceDraft { IssueDate = new DateOnly(2026, 8, 9) };

        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Equal(new DateOnly(2026, 8, 23), draft.DueDate);
    }

    /// <summary>Eine leere Vorlage überschreibt nichts mit Leere.</summary>
    [Fact]
    public void EineLeereVorlageLöschtNichts()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template);
        CompanyTemplateApplier.Apply(draft, new CompanyTemplate());

        Assert.Equal("Muster IT GmbH", draft.SellerName);
    }
}

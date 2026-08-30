using System.Globalization;
using EInvoiceSender.App.Presentation;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft die rein darstellende Aufbereitung des Prüffensters. Die fachlichen
/// Werte und Regelkennungen bleiben dabei unverändert im Core bestehen.
/// </summary>
public sealed class EInvoiceCheckDisplayFormattingTests
{
    [Fact]
    public void RechnungsdatumIstAuchUnterEnUsEinDeutschesDatumOhneUhrzeit()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            string text = EInvoiceCheckDisplayFormatter.FormatDate(new DateOnly(2026, 8, 10));

            Assert.Equal("10.08.2026", text);
            Assert.DoesNotContain(":", text, StringComparison.Ordinal);
            Assert.DoesNotContain("AM", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PM", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void GeldbetragVerwendetDeutschesZahlenformatUndRechnungswährung()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("600,00 EUR", EInvoiceCheckDisplayFormatter.FormatMoney(600m, "EUR"));
            Assert.Equal("714,50 USD", EInvoiceCheckDisplayFormatter.FormatMoney(714.5m, "USD"));
            Assert.DoesNotContain(
                "EUR",
                EInvoiceCheckDisplayFormatter.FormatMoney(714.5m, "USD"),
                StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NormregelBleibtMitVerständlicherTechnischerBeschriftungSichtbar()
    {
        ValidationFinding finding = ValidationFinding.Error(
            "APP-SEL-004",
            "Für die elektronische Rechnung fehlt eine eindeutige Verkäuferkennung.",
            "Seller.VatId",
            normRule: "BR-CO-26");

        string text = EInvoiceCheckDisplayFormatter.FormatTechnicalDetails(finding);

        Assert.Equal(
            "Technische Details: EN 16931 – Verkäuferidentifikation (BR-CO-26) "
            + "· interne Kennung APP-SEL-004",
            text);
        Assert.DoesNotContain(finding.Message, text, StringComparison.Ordinal);
    }

    [Fact]
    public void InterneKennungErfindetKeineNormregel()
    {
        ValidationFinding finding = ValidationFinding.Warning(
            "APP-CHK-099",
            "Die technische Bestandsaufnahme ist unvollständig.");

        string text = EInvoiceCheckDisplayFormatter.FormatTechnicalDetails(finding);

        Assert.Equal("Technische Details: Interne Kennung APP-CHK-099", text);
        Assert.DoesNotContain("EN 16931", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BR-", text, StringComparison.Ordinal);
    }
}

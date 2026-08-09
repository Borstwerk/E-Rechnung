using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.CodeLists;

/// <summary>
/// Prüft die reinen Nachschlagefunktionen der statischen Codelisten:
/// bekannte Werte, Normalisierung (Gross-/Kleinschreibung, Leerzeichen),
/// unbekannte Werte und die Nullbehandlung. Keine Geschäftsregeln.
/// </summary>
public sealed class CodeListTests
{
    // ----- CurrencyCodeList -----------------------------------------------

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("CHF")]
    public void CurrencyCodeList_IsValid_AcceptsKnownCodes(string code)
        => Assert.True(CurrencyCodeList.IsValid(code));

    [Fact]
    public void CurrencyCodeList_IsValid_IsCaseInsensitive()
        => Assert.True(CurrencyCodeList.IsValid("eur"));

    [Fact]
    public void CurrencyCodeList_IsValid_TrimsWhitespace()
        => Assert.True(CurrencyCodeList.IsValid("  EUR  "));

    [Fact]
    public void CurrencyCodeList_IsValid_RejectsUnknownCode()
        => Assert.False(CurrencyCodeList.IsValid("XXX"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CurrencyCodeList_IsValid_RejectsNullOrEmpty(string? code)
        => Assert.False(CurrencyCodeList.IsValid(code));

    [Fact]
    public void CurrencyCodeList_TryGetName_ReturnsGermanName()
    {
        bool found = CurrencyCodeList.TryGetName("eur", out string? name);

        Assert.True(found);
        Assert.Equal("Euro", name);
    }

    [Fact]
    public void CurrencyCodeList_TryGetName_FailsForUnknownCode()
    {
        bool found = CurrencyCodeList.TryGetName("XXX", out string? name);

        Assert.False(found);
        Assert.Null(name);
    }

    // ----- CountryCodeList --------------------------------------------------

    [Theory]
    [InlineData("DE")]
    [InlineData("FR")]
    [InlineData("US")]
    public void CountryCodeList_IsValid_AcceptsKnownCodes(string code)
        => Assert.True(CountryCodeList.IsValid(code));

    [Fact]
    public void CountryCodeList_IsValid_IsCaseInsensitive()
        => Assert.True(CountryCodeList.IsValid("de"));

    [Fact]
    public void CountryCodeList_IsValid_TrimsWhitespace()
        => Assert.True(CountryCodeList.IsValid(" DE "));

    [Fact]
    public void CountryCodeList_IsValid_RejectsUnknownCode()
        => Assert.False(CountryCodeList.IsValid("ZZ"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CountryCodeList_IsValid_RejectsNullOrEmpty(string? code)
        => Assert.False(CountryCodeList.IsValid(code));

    [Fact]
    public void CountryCodeList_TryGetName_ReturnsGermanName()
    {
        bool found = CountryCodeList.TryGetName("de", out string? name);

        Assert.True(found);
        Assert.Equal("Deutschland", name);
    }

    [Fact]
    public void CountryCodeList_TryGetName_FailsForUnknownCode()
    {
        bool found = CountryCodeList.TryGetName("ZZ", out string? name);

        Assert.False(found);
        Assert.Null(name);
    }

    [Fact]
    public void CountryCodeList_ContainsFullIsoRange()
    {
        // Die Liste ist als vollständig dokumentiert (rund 249 Codes) -
        // dieser Test soll ein versehentliches Zusammenstreichen erkennen.
        int total = CountAllCodes();
        Assert.True(total > 200, $"Erwartet mehr als 200 Ländercodes, gefunden: {total}.");
    }

    private static int CountAllCodes()
    {
        // ISO 3166-1 alpha-2 belegt nur zweistellige Großbuchstabenkombinationen.
        int count = 0;
        for (char first = 'A'; first <= 'Z'; first++)
        {
            for (char second = 'A'; second <= 'Z'; second++)
            {
                if (CountryCodeList.IsValid($"{first}{second}"))
                {
                    count++;
                }
            }
        }

        return count;
    }

    // ----- UnitCodeList ------------------------------------------------------

    [Theory]
    [InlineData("C62")]
    [InlineData("HUR")]
    [InlineData("KGM")]
    public void UnitCodeList_IsValid_AcceptsKnownCodes(string code)
        => Assert.True(UnitCodeList.IsValid(code));

    [Fact]
    public void UnitCodeList_IsValid_IsCaseInsensitive()
        => Assert.True(UnitCodeList.IsValid("hur"));

    [Fact]
    public void UnitCodeList_IsValid_TrimsWhitespace()
        => Assert.True(UnitCodeList.IsValid(" HUR "));

    [Fact]
    public void UnitCodeList_IsValid_RejectsUnknownCode()
        => Assert.False(UnitCodeList.IsValid("ZZZ"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnitCodeList_IsValid_RejectsNullOrEmpty(string? code)
        => Assert.False(UnitCodeList.IsValid(code));

    [Fact]
    public void UnitCodeList_TryGetName_ReturnsGermanName()
    {
        bool found = UnitCodeList.TryGetName("hur", out string? name);

        Assert.True(found);
        Assert.Equal("Stunde", name);
    }

    [Fact]
    public void UnitCodeList_TryGetName_FailsForUnknownCode()
    {
        bool found = UnitCodeList.TryGetName("ZZZ", out string? name);

        Assert.False(found);
        Assert.Null(name);
    }

    [Fact]
    public void UnitCodeList_CommonUnits_AreAllValid()
    {
        foreach ((string code, string _) in UnitCodeList.CommonUnits)
        {
            Assert.True(UnitCodeList.IsValid(code), $"CommonUnits-Code '{code}' ist nicht in UnitCodeList gültig.");
        }
    }

    // ----- DocumentCodeLists: InvoiceTypeCodes --------------------------------

    [Theory]
    [InlineData(380)]
    [InlineData(381)]
    [InlineData(384)]
    [InlineData(389)]
    public void InvoiceTypeCodes_IsValid_AcceptsKnownCodes(int code)
        => Assert.True(InvoiceTypeCodes.IsValid(code));

    [Fact]
    public void InvoiceTypeCodes_IsValid_RejectsUnknownCode()
        => Assert.False(InvoiceTypeCodes.IsValid(999));

    [Fact]
    public void InvoiceTypeCodes_TryGetName_ReturnsGermanName()
    {
        bool found = InvoiceTypeCodes.TryGetName(380, out string? name);

        Assert.True(found);
        Assert.Equal("Handelsrechnung", name);
    }

    [Fact]
    public void InvoiceTypeCodes_AllDomainEnumValues_AreValid()
    {
        foreach (InvoiceTypeCode value in Enum.GetValues<InvoiceTypeCode>())
        {
            int code = (int)value;
            Assert.True(InvoiceTypeCodes.IsValid(code), $"Domain-Enumwert InvoiceTypeCode.{value} ({code}) fehlt in InvoiceTypeCodes.");
        }
    }

    // ----- DocumentCodeLists: PaymentMeansCodes -------------------------------

    [Theory]
    [InlineData(30)]
    [InlineData(58)]
    public void PaymentMeansCodes_IsValid_AcceptsKnownCodes(int code)
        => Assert.True(PaymentMeansCodes.IsValid(code));

    [Fact]
    public void PaymentMeansCodes_IsValid_RejectsUnknownCode()
        => Assert.False(PaymentMeansCodes.IsValid(999));

    [Fact]
    public void PaymentMeansCodes_TryGetName_ReturnsGermanName()
    {
        bool found = PaymentMeansCodes.TryGetName(58, out string? name);

        Assert.True(found);
        Assert.Equal("SEPA-Überweisung", name);
    }

    [Fact]
    public void PaymentMeansCodes_AllDomainEnumValues_AreValid()
    {
        foreach (PaymentMeansCode value in Enum.GetValues<PaymentMeansCode>())
        {
            int code = (int)value;
            Assert.True(PaymentMeansCodes.IsValid(code), $"Domain-Enumwert PaymentMeansCode.{value} ({code}) fehlt in PaymentMeansCodes.");
        }
    }

    // ----- DocumentCodeLists: VatExemptionReasonCodes -------------------------

    [Theory]
    [InlineData("VATEX-EU-AE")]
    [InlineData("VATEX-EU-IC")]
    [InlineData("VATEX-EU-G")]
    public void VatExemptionReasonCodes_IsValid_AcceptsKnownCodes(string code)
        => Assert.True(VatExemptionReasonCodes.IsValid(code));

    [Fact]
    public void VatExemptionReasonCodes_IsValid_IsCaseSensitive()
        => Assert.False(VatExemptionReasonCodes.IsValid("vatex-eu-ae"));

    [Fact]
    public void VatExemptionReasonCodes_IsValid_TrimsWhitespace()
        => Assert.True(VatExemptionReasonCodes.IsValid(" VATEX-EU-AE "));

    [Fact]
    public void VatExemptionReasonCodes_IsValid_RejectsUnknownCode()
        => Assert.False(VatExemptionReasonCodes.IsValid("VATEX-EU-DOES-NOT-EXIST"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VatExemptionReasonCodes_IsValid_RejectsNullOrEmpty(string? code)
        => Assert.False(VatExemptionReasonCodes.IsValid(code));

    [Fact]
    public void VatExemptionReasonCodes_TryGetName_ReturnsGermanDescription()
    {
        bool found = VatExemptionReasonCodes.TryGetName("VATEX-EU-AE", out string? name);

        Assert.True(found);
        Assert.NotNull(name);
    }
}

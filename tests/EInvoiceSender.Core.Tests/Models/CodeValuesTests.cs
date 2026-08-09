using EInvoiceSender.Core.Models;
using Xunit;

namespace EInvoiceSender.Core.Tests.Values;

/// <summary>
/// Tests für <see cref="CurrencyCode"/>, <see cref="CountryCode"/>,
/// <see cref="UnitCode"/> und die Erweiterungen in <see cref="CodeValues"/>.
/// </summary>
public sealed class CodeValuesTests
{
    [Fact]
    public void CurrencyCode_TryParse_AkzeptiertKorrekteForm()
    {
        bool erfolg = CurrencyCode.TryParse("EUR", out CurrencyCode code);

        Assert.True(erfolg);
        Assert.Equal("EUR", code.Value);
    }

    [Fact]
    public void CurrencyCode_TryParse_TrimmtUndGroßbuchstaben()
    {
        bool erfolg = CurrencyCode.TryParse("  eur  ", out CurrencyCode code);

        Assert.True(erfolg);
        Assert.Equal("EUR", code.Value);
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    public void CurrencyCode_TryParse_LehntFalscheLängeAb(string eingabe)
    {
        bool erfolg = CurrencyCode.TryParse(eingabe, out CurrencyCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CurrencyCode_TryParse_LehntZiffernAb()
    {
        bool erfolg = CurrencyCode.TryParse("E1R", out CurrencyCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CurrencyCode_TryParse_LehntLeerAb()
    {
        bool erfolg = CurrencyCode.TryParse(string.Empty, out CurrencyCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CurrencyCode_TryParse_LehntNullAb()
    {
        bool erfolg = CurrencyCode.TryParse(null, out CurrencyCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CurrencyCode_Parse_WirftFormatExceptionBeiUngültigerEingabe()
    {
        Assert.Throws<FormatException>(() => CurrencyCode.Parse("ungültig"));
    }

    [Fact]
    public void CurrencyCode_Euro_IstEur()
    {
        Assert.Equal("EUR", CurrencyCode.Euro.Value);
    }

    [Fact]
    public void CountryCode_TryParse_AkzeptiertKorrekteForm()
    {
        bool erfolg = CountryCode.TryParse("DE", out CountryCode code);

        Assert.True(erfolg);
        Assert.Equal("DE", code.Value);
    }

    [Fact]
    public void CountryCode_TryParse_TrimmtUndGroßbuchstaben()
    {
        bool erfolg = CountryCode.TryParse(" de ", out CountryCode code);

        Assert.True(erfolg);
        Assert.Equal("DE", code.Value);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("DEU")]
    public void CountryCode_TryParse_LehntFalscheLängeAb(string eingabe)
    {
        bool erfolg = CountryCode.TryParse(eingabe, out CountryCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CountryCode_TryParse_LehntZiffernAb()
    {
        bool erfolg = CountryCode.TryParse("D1", out CountryCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CountryCode_TryParse_LehntLeerAb()
    {
        bool erfolg = CountryCode.TryParse(string.Empty, out CountryCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CountryCode_TryParse_LehntNullAb()
    {
        bool erfolg = CountryCode.TryParse(null, out CountryCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void CountryCode_Parse_WirftFormatExceptionBeiUngültigerEingabe()
    {
        Assert.Throws<FormatException>(() => CountryCode.Parse("123"));
    }

    [Fact]
    public void CountryCode_Germany_IstDe()
    {
        Assert.Equal("DE", CountryCode.Germany.Value);
    }

    [Fact]
    public void UnitCode_TryParse_AkzeptiertKorrekteForm()
    {
        bool erfolg = UnitCode.TryParse("HUR", out UnitCode code);

        Assert.True(erfolg);
        Assert.Equal("HUR", code.Value);
    }

    [Fact]
    public void UnitCode_TryParse_TrimmtUndGroßbuchstaben()
    {
        bool erfolg = UnitCode.TryParse(" hur ", out UnitCode code);

        Assert.True(erfolg);
        Assert.Equal("HUR", code.Value);
    }

    [Fact]
    public void UnitCode_TryParse_ErlaubtZiffern()
    {
        // C62 (Stück) enthält eine Ziffer, im Unterschied zu Currency/Country.
        bool erfolg = UnitCode.TryParse("C62", out UnitCode code);

        Assert.True(erfolg);
        Assert.Equal("C62", code.Value);
    }

    [Fact]
    public void UnitCode_TryParse_LehntZuLangeEingabeAb()
    {
        bool erfolg = UnitCode.TryParse("ABCD", out UnitCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void UnitCode_TryParse_LehntLeerAb()
    {
        bool erfolg = UnitCode.TryParse(string.Empty, out UnitCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void UnitCode_TryParse_LehntNullAb()
    {
        bool erfolg = UnitCode.TryParse(null, out UnitCode code);

        Assert.False(erfolg);
    }

    [Fact]
    public void UnitCode_Parse_WirftFormatExceptionBeiUngültigerEingabe()
    {
        Assert.Throws<FormatException>(() => UnitCode.Parse("ZU-LANG"));
    }

    [Fact]
    public void UnitCode_Piece_IstC62()
    {
        Assert.Equal("C62", UnitCode.Piece.Value);
    }

    [Fact]
    public void UnitCode_Hour_IstHur()
    {
        Assert.Equal("HUR", UnitCode.Hour.Value);
    }

    [Fact]
    public void UnitCode_Day_IstDay()
    {
        Assert.Equal("DAY", UnitCode.Day.Value);
    }

    [Theory]
    [InlineData(VatCategory.StandardRate, "S")]
    [InlineData(VatCategory.ZeroRated, "Z")]
    [InlineData(VatCategory.Exempt, "E")]
    [InlineData(VatCategory.ReverseCharge, "AE")]
    [InlineData(VatCategory.IntraCommunitySupply, "K")]
    [InlineData(VatCategory.ExportOutsideEu, "G")]
    [InlineData(VatCategory.OutsideScope, "O")]
    public void ToCode_LiefertDenUntdid5305CodeFürAlleWerte(VatCategory kategorie, string erwarteterCode)
    {
        Assert.Equal(erwarteterCode, kategorie.ToCode());
    }

    [Theory]
    [InlineData("S", VatCategory.StandardRate)]
    [InlineData("Z", VatCategory.ZeroRated)]
    [InlineData("E", VatCategory.Exempt)]
    [InlineData("AE", VatCategory.ReverseCharge)]
    [InlineData("K", VatCategory.IntraCommunitySupply)]
    [InlineData("G", VatCategory.ExportOutsideEu)]
    [InlineData("O", VatCategory.OutsideScope)]
    public void TryParseVatCategory_AkzeptiertAlleCodes(string code, VatCategory erwarteteKategorie)
    {
        bool erfolg = CodeValues.TryParseVatCategory(code, out VatCategory kategorie);

        Assert.True(erfolg);
        Assert.Equal(erwarteteKategorie, kategorie);
    }

    [Theory]
    [InlineData("s", VatCategory.StandardRate)]
    [InlineData("ae", VatCategory.ReverseCharge)]
    [InlineData(" k ", VatCategory.IntraCommunitySupply)]
    [InlineData(" o", VatCategory.OutsideScope)]
    public void TryParseVatCategory_AkzeptiertKleinschreibungUndLeerzeichen(string code, VatCategory erwarteteKategorie)
    {
        bool erfolg = CodeValues.TryParseVatCategory(code, out VatCategory kategorie);

        Assert.True(erfolg);
        Assert.Equal(erwarteteKategorie, kategorie);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseVatCategory_LehntUnbekanntenCodeAb(string? code)
    {
        bool erfolg = CodeValues.TryParseVatCategory(code, out VatCategory kategorie);

        Assert.False(erfolg);
        Assert.Equal(default, kategorie);
    }

    public static TheoryData<VatCategory> AlleVatCategoryWerte()
    {
        var daten = new TheoryData<VatCategory>();
        foreach (VatCategory wert in Enum.GetValues<VatCategory>())
        {
            daten.Add(wert);
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(AlleVatCategoryWerte))]
    public void Roundtrip_ToCodeUndTryParseVatCategorySindInvers(VatCategory kategorie)
    {
        // Enum.GetValues sorgt dafür, dass ein später hinzugefügter Wert diesen
        // Test bricht, falls ToCode/TryParseVatCategory nicht erweitert werden.
        string code = kategorie.ToCode();

        bool erfolg = CodeValues.TryParseVatCategory(code, out VatCategory zurückgelesen);

        Assert.True(erfolg);
        Assert.Equal(kategorie, zurückgelesen);
    }

    [Theory]
    [InlineData(VatCategory.StandardRate, true)]
    [InlineData(VatCategory.ZeroRated, false)]
    [InlineData(VatCategory.Exempt, false)]
    [InlineData(VatCategory.ReverseCharge, false)]
    [InlineData(VatCategory.IntraCommunitySupply, false)]
    [InlineData(VatCategory.ExportOutsideEu, false)]
    [InlineData(VatCategory.OutsideScope, false)]
    public void RequiresPositiveRate_NurStandardRateVerlangtPositivenSatz(VatCategory kategorie, bool erwartet)
    {
        Assert.Equal(erwartet, kategorie.RequiresPositiveRate());
    }

    [Theory]
    [InlineData(VatCategory.Exempt, true)]
    [InlineData(VatCategory.ReverseCharge, true)]
    [InlineData(VatCategory.IntraCommunitySupply, true)]
    [InlineData(VatCategory.ExportOutsideEu, true)]
    [InlineData(VatCategory.OutsideScope, true)]
    [InlineData(VatCategory.StandardRate, false)]
    [InlineData(VatCategory.ZeroRated, false)]
    public void RequiresExemptionReason_LiefertErwartetesErgebnisFürAlleKategorien(VatCategory kategorie, bool erwartet)
    {
        Assert.Equal(erwartet, kategorie.RequiresExemptionReason());
    }

    [Theory]
    [InlineData(InvoiceTypeCode.CreditNote, true)]
    [InlineData(InvoiceTypeCode.CommercialInvoice, false)]
    [InlineData(InvoiceTypeCode.CorrectedInvoice, false)]
    [InlineData(InvoiceTypeCode.SelfBilledInvoice, false)]
    public void IsCreditNote_NurCreditNoteIstWahr(InvoiceTypeCode typ, bool erwartet)
    {
        Assert.Equal(erwartet, typ.IsCreditNote());
    }
}

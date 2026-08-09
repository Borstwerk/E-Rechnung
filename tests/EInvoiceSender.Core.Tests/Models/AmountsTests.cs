using EInvoiceSender.Core.Models;
using Xunit;

namespace EInvoiceSender.Core.Tests.Money;

/// <summary>
/// Prueft die Rundungs- und Formatierungsregeln fuer Geldbetraege.
/// Die Rundung muss der XPath-Funktion <c>round()</c> des CEN-Schematron
/// entsprechen, also kaufmaennisch von der Null weg.
/// </summary>
public sealed class AmountsTests
{
    [Theory]
    [InlineData("1.005", "1.01")]
    [InlineData("1.004", "1.00")]
    [InlineData("2.675", "2.68")]
    [InlineData("-1.005", "-1.01")]
    [InlineData("-2.675", "-2.68")]
    [InlineData("0.005", "0.01")]
    [InlineData("0.004", "0.00")]
    [InlineData("19", "19.00")]
    public void RundetKaufmaennischVonDerNullWeg(string input, string expected)
    {
        decimal value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        decimal want = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(want, Amounts.Round(value));
    }

    [Fact]
    public void RundetAufFreiGewaehlteStellenzahl()
    {
        Assert.Equal(19.1235m, Amounts.Round(19.12345m, 4));
        Assert.Equal(19m, Amounts.Round(19.4m, 0));
        Assert.Equal(20m, Amounts.Round(19.5m, 0));
    }

    [Theory]
    [InlineData("1.00", 2, true)]
    [InlineData("1.5000", 2, true)]
    [InlineData("1.005", 2, false)]
    [InlineData("1.239", 2, false)]
    [InlineData("19", 2, true)]
    public void ErkenntZuVieleNachkommastellen(string input, int decimals, bool expected)
    {
        decimal value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Amounts.HasAtMostDecimals(value, decimals));
    }

    [Theory]
    [InlineData("100.00", "100.01", true)]
    [InlineData("100.00", "100.02", false)]
    [InlineData("100.00", "99.99", true)]
    [InlineData("100.00", "100.00", true)]
    public void VergleichtMitEinemCentToleranz(string a, string b, bool expected)
    {
        decimal left = decimal.Parse(a, System.Globalization.CultureInfo.InvariantCulture);
        decimal right = decimal.Parse(b, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Amounts.NearlyEqual(left, right));
    }

    [Theory]
    [InlineData("1", "1.00")]
    [InlineData("1.5", "1.50")]
    [InlineData("1234.567", "1234.57")]
    [InlineData("-0.5", "-0.50")]
    [InlineData("0", "0.00")]
    public void SchreibtBetraegeImmerMitPunktUndZweiStellen(string input, string expected)
    {
        decimal value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Amounts.ToXmlString(value));
    }

    [Theory]
    [InlineData("19", "19")]
    [InlineData("19.00", "19")]
    [InlineData("7", "7")]
    [InlineData("8.25", "8.25")]
    [InlineData("0", "0")]
    public void SchreibtSteuersaetzeOhneUnnoetigeNullen(string input, string expected)
    {
        decimal value = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Amounts.RateToXmlString(value));
    }

    [Fact]
    public void FormatierungIstUnabhaengigVonDerSystemkultur()
    {
        // Unter einer deutschen Kultur waere das Dezimaltrennzeichen ein Komma.
        // Die XML verlangt zwingend einen Punkt.
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");

            Assert.Equal("1234.56", Amounts.ToXmlString(1234.56m));
            Assert.Equal("8.25", Amounts.RateToXmlString(8.25m));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}

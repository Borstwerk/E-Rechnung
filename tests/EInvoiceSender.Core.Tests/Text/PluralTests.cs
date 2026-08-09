using EInvoiceSender.Core.Text;
using Xunit;

namespace EInvoiceSender.Core.Tests.Text;

/// <summary>
/// Sichert ab, dass angezeigte Zahlen und Hauptwörter zusammenpassen.
///
/// **Warum:** In der Oberfläche stand „1 Seite(n)“. Die Klammerform ist
/// Entwicklersprache und hat in einer Anwendung nichts zu suchen, die jemand
/// bedient.
/// </summary>
public sealed class PluralTests
{
    [Theory]
    [InlineData(1, "1 Seite")]
    [InlineData(2, "2 Seiten")]
    [InlineData(0, "0 Seiten")]
    public void ZahlUndHauptwortPassenZusammen(int count, string expected)
        => Assert.Equal(expected, Plural.Count(count, "Seite", "Seiten"));

    [Theory]
    [InlineData(1, "muss")]
    [InlineData(3, "müssen")]
    public void DasZeitwortRichtetSichNachDerZahl(int count, string expected)
        => Assert.Equal(expected, Plural.Word(count, "muss", "müssen"));

    /// <summary>
    /// Der eigentliche Zweck: In keiner Meldung darf eine Klammerform stehen
    /// bleiben.
    /// </summary>
    [Fact]
    public void EsEntstehtNieEineKlammerform()
    {
        string[] sätze =
        [
            Plural.Count(1, "Feld", "Felder"),
            Plural.Count(7, "Feld", "Felder"),
            Plural.Count(1, "Angabe", "Angaben"),
        ];

        Assert.DoesNotContain(sätze, s => s.Contains('(', StringComparison.Ordinal));
    }
}

using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Zugferd;
using Xunit;

namespace EInvoiceSender.Core.Tests.Zugferd;

/// <summary>
/// Die Verkäuferkennung (BT-29) auf dem Weg in die CII-Datei und zurück.
///
/// Sie ist die dritte Kennung, mit der BR-CO-26 den Rechnungssteller
/// maschinell identifizierbar macht – neben der Handelsregisternummer (BT-30)
/// und der USt-IdNr. (BT-31). Anders als beide steht sie im CII unmittelbar
/// als <c>SellerTradeParty/ram:ID</c>, also an einer Stelle, an der ein
/// falscher Platz oder ein erfundenes Schemakennzeichen die Datei sofort
/// unbrauchbar macht.
///
/// Geprüft wird deshalb dreierlei: dass sie an der richtigen Stelle steht,
/// dass sie ohne Schemakennzeichen geschrieben wird, und dass sie beim
/// Zurücklesen wieder dem Verkäufer zugeordnet wird – nicht dem Käufer.
/// </summary>
public sealed class SellerIdentifierTests
{
    private const string Identifier = "LIEF-4711";

    private static readonly CiiInvoiceWriter Writer = new();
    private static readonly CiiInvoiceReader Reader = new();

    /// <summary>
    /// Die dokumentierte Reihenfolge des <c>TradePartyType</c> lautet
    /// <c>ID, GlobalID, Name, …</c>. Steht die Kennung hinter dem Namen, ist
    /// die Datei nicht schemakonform – und zwar unabhängig davon, ob ein
    /// nachsichtiger Leser sie trotzdem findet.
    /// </summary>
    [Fact]
    public void DieVerkäuferkennungStehtVorDemNamen()
    {
        string block = SellerBlockOf(WriteXml(WithIdentifier(Identifier)));

        int id = block.IndexOf($"<ram:ID>{Identifier}</ram:ID>", StringComparison.Ordinal);
        int name = block.IndexOf("<ram:Name>", StringComparison.Ordinal);

        Assert.True(id >= 0, "Die Verkäuferkennung fehlt im Verkäuferblock.");
        Assert.True(name >= 0, "Der Name des Verkäufers fehlt im Verkäuferblock.");
        Assert.True(id < name, "Die Verkäuferkennung steht hinter dem Namen.");
    }

    /// <summary>
    /// Ein Schemakennzeichen behauptet, aus welcher Liste die Kennung stammt.
    /// Diese Anwendung fragt es nicht ab und kann es deshalb nicht wissen; es
    /// zu erfinden wäre eine erfundene Angabe in einer Rechnung.
    /// </summary>
    [Fact]
    public void DieVerkäuferkennungTrägtKeinSchemakennzeichen()
    {
        string block = SellerBlockOf(WriteXml(WithIdentifier(Identifier)));
        int id = block.IndexOf($"<ram:ID>{Identifier}</ram:ID>", StringComparison.Ordinal);

        Assert.True(id >= 0, "Die Verkäuferkennung fehlt im Verkäuferblock.");

        // Gegenprobe zur reinen Textsuche: Vor der Kennung darf überhaupt kein
        // ID-Element mit Schemakennzeichen auf derselben Ebene stehen.
        Assert.DoesNotContain($"schemeID=\"\">{Identifier}", block, StringComparison.Ordinal);
        Assert.DoesNotContain($"<ram:ID schemeID", block[..id], StringComparison.Ordinal);
    }

    /// <summary>
    /// Ohne Kennung darf kein leeres Element entstehen. Ein leeres
    /// <c>ram:ID</c> wäre für einen Prüfer keine fehlende Angabe, sondern eine
    /// leere – und damit ein anderer, schwerer zu findender Fehler.
    /// </summary>
    [Fact]
    public void OhneVerkäuferkennungEntstehtKeinLeeresElement()
    {
        string block = SellerBlockOf(WriteXml(WithIdentifier(null)));

        Assert.DoesNotContain("<ram:ID>", block, StringComparison.Ordinal);
        Assert.DoesNotContain("<ram:ID />", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Eine Kennung, die nur geschrieben wird, ist nicht nachgewiesen. Erst der
    /// Rückleseweg belegt, dass in der Datei steht, was gemeint war.
    /// </summary>
    [Fact]
    public void DasEchoLiestDieVerkäuferkennungZurück()
    {
        InvoiceEcho? echo = Reader.ReadEcho(WriteXml(WithIdentifier(Identifier)));

        Assert.NotNull(echo);
        Assert.Equal(Identifier, echo.SellerIdentifier);
    }

    /// <summary>Ohne Kennung meldet das Echo keine – und erfindet keine leere.</summary>
    [Fact]
    public void OhneVerkäuferkennungMeldetDasEchoKeine()
    {
        InvoiceEcho? echo = Reader.ReadEcho(WriteXml(WithIdentifier(null)));

        Assert.NotNull(echo);
        Assert.Null(echo.SellerIdentifier);
    }

    /// <summary>
    /// Eine Kennung aus lauter Leerzeichen ist keine Kennung. Sie wird wie eine
    /// fehlende behandelt – schreiben würde ein leeres Pflichtfeld ergeben.
    ///
    /// Diese Asymmetrie ist der Grund, weshalb der Echo-Vergleich in der
    /// Erzeugungskette leere Eingaben ebenso als „keine Kennung“ liest: Sonst
    /// meldete er einen Unterschied, den der Schreiber selbst erzeugt hat.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EineLeereVerkäuferkennungGiltAlsKeine(string identifier)
    {
        byte[] xml = WriteXml(WithIdentifier(identifier));

        Assert.DoesNotContain("<ram:ID>", SellerBlockOf(xml), StringComparison.Ordinal);
        Assert.Null(Reader.ReadEcho(xml)!.SellerIdentifier);
    }

    /// <summary>
    /// **Der eigentliche Zweck des Rückleseweges.** Weicht die Kennung in der
    /// Datei von der bestätigten ab, muss der Vergleich das sehen. Ein Echo,
    /// das die Kennung gar nicht liest, wäre bei jeder Verfälschung still.
    /// </summary>
    [Fact]
    public void EineVerfälschteVerkäuferkennungFälltImEchoAuf()
    {
        Invoice invoice = WithIdentifier(Identifier);

        string tampered = Encoding.UTF8.GetString(WriteXml(invoice)).Replace(
            $"<ram:ID>{Identifier}</ram:ID>",
            "<ram:ID>LIEF-0000</ram:ID>",
            StringComparison.Ordinal);

        InvoiceEcho? echo = Reader.ReadEcho(Encoding.UTF8.GetBytes(tampered));

        Assert.NotNull(echo);

        // Beides gehört hierher: Das Echo muss den verfälschten Wert wirklich
        // gelesen haben – nicht bloß irgendetwas anderes als das Erwartete.
        // Ein Echo, das die Kennung gar nicht liest, meldete sonst „ungleich“
        // und sähe damit richtig aus, ohne etwas zu prüfen.
        Assert.Equal("LIEF-0000", echo.SellerIdentifier);
        Assert.NotEqual(invoice.Seller.SellerIdentifier, echo.SellerIdentifier);
    }

    /// <summary>
    /// Der Leser darf die Kennung ausschließlich im Verkäuferblock suchen. Ein
    /// Leser, der irgendein <c>ram:ID</c> nimmt, läse je nach Datei die
    /// Rechnungsnummer oder eine Angabe des Käufers als Verkäuferkennung.
    /// </summary>
    [Fact]
    public void EineKennungImKäuferblockGiltNichtAlsVerkäuferkennung()
    {
        string xml = Encoding.UTF8.GetString(WriteXml(WithIdentifier(null))).Replace(
            "<ram:BuyerTradeParty>",
            "<ram:BuyerTradeParty><ram:ID>NICHT-DER-VERKÄUFER</ram:ID>",
            StringComparison.Ordinal);

        InvoiceEcho? echo = Reader.ReadEcho(Encoding.UTF8.GetBytes(xml));

        Assert.NotNull(echo);
        Assert.Null(echo.SellerIdentifier);
    }

    // ------------------------------------------------------------- Hilfsmittel

    private static Invoice WithIdentifier(string? identifier)
    {
        Invoice baseline = InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice;

        return baseline with
        {
            Seller = baseline.Seller with { SellerIdentifier = identifier },
        };
    }

    private static byte[] WriteXml(Invoice invoice)
        => Writer.Write(invoice, InvoiceCalculator.Calculate(invoice));

    /// <summary>
    /// Schneidet den Verkäuferblock heraus. Ohne diese Eingrenzung liefe jede
    /// Textsuche Gefahr, eine Angabe des Käufers als Treffer zu werten.
    /// </summary>
    private static string SellerBlockOf(byte[] xml)
    {
        string text = Encoding.UTF8.GetString(xml);

        int start = text.IndexOf("<ram:SellerTradeParty>", StringComparison.Ordinal);
        Assert.True(start >= 0, "Kein Verkäuferblock in der erzeugten Datei.");

        int end = text.IndexOf("</ram:SellerTradeParty>", start, StringComparison.Ordinal);
        Assert.True(end > start, "Der Verkäuferblock ist nicht geschlossen.");

        return text[start..end];
    }
}

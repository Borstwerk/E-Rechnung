using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Sichert die beiden neuen Verkäuferkennungen in der Oberfläche.
///
/// Die Trennung ist auch hier die Aussage: Die Registerkennung (BT-30) steht
/// im Formular **und** in den Einstellungen, weil sie Firmenstamm ist. Die
/// Verkäuferkennung (BT-29) steht nur im Formular – sie gehört zu einer
/// bestimmten Kundenbeziehung und hätte in den dauerhaften Vorgaben nichts
/// verloren.
///
/// Geprüft wird auf Quelltextebene: Das Anwendungsprojekt ist
/// <c>net10.0-windows</c> und lässt sich von den Prüfprojekten nicht
/// referenzieren.
/// </summary>
public sealed class SellerIdentificationFieldTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    // --------------------------------------------------------------- Schritt 2

    [Theory]
    [InlineData("SellerLegalRegistrationId")]
    [InlineData("SellerIdentifier")]
    public void DasFormularBindetBeideKennungenAnDenEntwurf(string property)
    {
        string[] bindings = [.. Load("InvoiceDataView.xaml").Descendants().Attributes()
            .Select(attribute => attribute.Value)];

        Assert.Contains(
            bindings,
            value => value.Contains(
                $"Draft.{property}, UpdateSourceTrigger=PropertyChanged", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ohne Herkunftshinweis sähe der Anwender nicht, ob ein Wert aus seiner
    /// Firmenvorlage stammt oder von ihm selbst – bei einer Kennung, die über
    /// die Gültigkeit der Rechnung entscheidet, ist das keine Kleinigkeit.
    /// </summary>
    [Theory]
    [InlineData("SellerLegalRegistrationId")]
    [InlineData("SellerIdentifier")]
    public void BeideKennungenZeigenIhreHerkunft(string property)
    {
        string[] bindings = [.. Load("InvoiceDataView.xaml").Descendants().Attributes()
            .Select(attribute => attribute.Value)];

        Assert.Contains(
            bindings,
            value => value.Contains("StaticResource FieldOrigin}", StringComparison.Ordinal)
                     && value.Contains($"ConverterParameter={property}", StringComparison.Ordinal));

        Assert.Contains(
            bindings,
            value => value.Contains("StaticResource FieldOriginVisible}", StringComparison.Ordinal)
                     && value.Contains($"ConverterParameter={property}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Registerkennung")]
    [InlineData("Lieferanten-/Kreditorennummer")]
    public void BeideKennungenTragenEineBeschriftung(string label)
    {
        Assert.Contains(
            Load("InvoiceDataView.xaml").Descendants(Presentation + "TextBlock"),
            text => text.Attribute("Text")?.Value == label);
    }

    /// <summary>
    /// Die visuelle Reihenfolge ist Teil der fachlichen Aussage: BT-32 steht
    /// bei den steuerlichen Angaben und ausdrücklich nicht in der Gruppe der
    /// drei Kennungen, die BR-CO-26 erfüllen. Die USt-IdNr. bleibt ebenfalls
    /// dort sichtbar; der Hilfetext der folgenden Gruppe erklärt ihre zweite
    /// Rolle, ohne ein zweites Eingabefeld zu erzeugen.
    /// </summary>
    [Fact]
    public void DasFormularTrenntSteuernummerVonVerkäuferidentifikation()
    {
        XElement seller = SellerCard();
        XElement taxes = Text(seller, "Steuerliche Angaben");
        XElement identification = Text(seller, "Verkäuferidentifikation für die E-Rechnung");

        int taxesRow = Row(taxes);
        int identificationRow = Row(identification);

        Assert.True(taxesRow < Row(Input(seller, "SellerVatId")));
        Assert.True(taxesRow < Row(Input(seller, "SellerTaxNumber")));
        Assert.True(Row(Input(seller, "SellerTaxNumber")) < identificationRow);
        Assert.True(identificationRow < Row(Input(seller, "SellerLegalRegistrationId")));
        Assert.True(identificationRow < Row(Input(seller, "SellerIdentifier")));
    }

    [Fact]
    public void DerHilfetextErklärtDieDreiIdentifikationswegeUndDieGrenzeDerSteuernummer()
    {
        XElement help = Assert.Single(
            SellerCard().Descendants(Presentation + "TextBlock"),
            text => text.Attribute("Text")?.Value.Contains(
                "Steuernummer allein reicht hierfür nicht aus", StringComparison.Ordinal) == true);

        string explanation = help.Attribute("Text")!.Value;

        Assert.Contains("USt-IdNr.", explanation, StringComparison.Ordinal);
        Assert.Contains("Registerkennung", explanation, StringComparison.Ordinal);
        Assert.Contains("Lieferanten-/Kreditorennummer", explanation, StringComparison.Ordinal);
        Assert.Contains("vom Kunden", explanation, StringComparison.Ordinal);
        Assert.Contains("zählt bereits", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void DieUStIdNrBleibtEinEinzigesFeldMitDemBisherigenBinding()
    {
        XElement field = Assert.Single(
            SellerCard().Descendants(Presentation + "TextBox"),
            box => box.Attributes().Any(attribute => attribute.Value.Contains(
                "Draft.SellerVatId, UpdateSourceTrigger=PropertyChanged",
                StringComparison.Ordinal)));

        Assert.Equal("Umsatzsteuer-Identifikationsnummer", field.Attribute("AutomationProperties.Name")?.Value);
    }

    [Theory]
    [InlineData("SellerVatId")]
    [InlineData("SellerTaxNumber")]
    [InlineData("SellerLegalRegistrationId")]
    [InlineData("SellerIdentifier")]
    public void DieBestehendenFachfelderBehaltenBindingUndHerkunft(string property)
    {
        XElement seller = SellerCard();
        _ = Input(seller, property);

        Assert.Contains(
            seller.Descendants(Presentation + "TextBlock"),
            hint => hint.Attribute("Text")?.Value.Contains(
                        $"ConverterParameter={property}", StringComparison.Ordinal) == true
                    && hint.Attribute("Visibility")?.Value.Contains(
                        $"ConverterParameter={property}", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// **Die Beschriftung allein genügt hier nicht.** „Lieferantennummer“ liest
    /// sich für viele wie eine Nummer, die man selbst vergibt – und eine selbst
    /// erfundene Kennung in BT-29 wäre genau das, was diese Anwendung nirgends
    /// tut: eine erfundene Angabe in einer Rechnung. Der Hinweis muss deshalb
    /// mit der Maus erreichbar sein, nicht nur für Hilfsmittel.
    ///
    /// Geprüft wird der ToolTip **an dem Bedienelement, das BT-29 bindet**.
    /// Irgendwo in der Datei nach dem Text zu suchen bewiese nichts: Er könnte
    /// an einem beliebigen anderen Feld hängen.
    /// </summary>
    [Fact]
    public void DasFeldFürDieVerkäuferkennungErklärtSichImToolTip()
    {
        XElement field = Assert.Single(
            Load("InvoiceDataView.xaml").Descendants(Presentation + "TextBox"),
            box => box.Attribute("Text")?.Value.Contains(
                "Draft.SellerIdentifier", StringComparison.Ordinal) == true);

        string? toolTip = field.Attribute("ToolTip")?.Value;

        Assert.False(
            string.IsNullOrWhiteSpace(toolTip),
            "Das Feld für die Verkäuferkennung (BT-29) trägt keinen ToolTip.");

        // Der Hinweis muss die drei Dinge sagen, um die es geht: dass der
        // Käufer die Nummer vergibt, wie sie auch heißen kann, und dass das
        // Feld ohne eine solche Mitteilung leer bleibt.
        Assert.Contains("Käufer", toolTip, StringComparison.Ordinal);
        Assert.Contains("Kreditorennummer", toolTip, StringComparison.Ordinal);
        Assert.Contains("mitgeteilt", toolTip, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Tastaturreihenfolge im Verkäuferblock muss aufsteigend und ohne
    /// Doppelung sein. Zwei Felder mit derselben Nummer springen unter Windows
    /// in einer Reihenfolge, die niemand vorhergesagt hat.
    /// </summary>
    [Fact]
    public void DieTastaturreihenfolgeImVerkäuferblockBleibtEindeutig()
    {
        int[] tabIndexes =
        [
            .. SellerCard()
                .Descendants()
                .Select(element => element.Attribute("TabIndex")?.Value)
                .Where(value => value is not null)
                .Select(value => int.Parse(value!, System.Globalization.CultureInfo.InvariantCulture)),
        ];

        Assert.Equal(tabIndexes.Length, tabIndexes.Distinct().Count());
        Assert.Equal([.. tabIndexes.Order()], tabIndexes);
    }

    // ------------------------------------------------------------ Einstellungen

    [Fact]
    public void DieEinstellungenFührenDieRegisterkennung()
    {
        XDocument window = Load("SettingsWindow.xaml");

        Assert.Contains(
            window.Descendants(Presentation + "TextBlock"),
            text => text.Attribute("Text")?.Value == "Registerkennung");

        Assert.Contains(
            window.Descendants().Attributes().Select(attribute => attribute.Value),
            value => value.Contains(
                "SellerLegalRegistrationId, UpdateSourceTrigger=PropertyChanged",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// **Der Negativfall.** Eine Lieferantennummer in den dauerhaften Vorgaben
    /// stünde bei der nächsten Rechnung an einen anderen Kunden wieder da –
    /// falsch, und ohne dass es jemandem auffiele.
    /// </summary>
    [Fact]
    public void DieEinstellungenKennenDieVerkäuferkennungNicht()
    {
        Assert.DoesNotContain(
            Load("SettingsWindow.xaml").Descendants().Attributes()
                .Select(attribute => attribute.Value),
            value => value.Contains("SellerIdentifier", StringComparison.Ordinal));

        Assert.DoesNotContain(
            "SellerIdentifier",
            Source("SettingsViewModel.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DasEinstellungsmodellLiestUndSchreibtDieRegisterkennung()
    {
        string source = Source("SettingsViewModel.cs");

        Assert.Contains(
            "SellerLegalRegistrationId = template.SellerLegalRegistrationId ?? string.Empty;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SellerLegalRegistrationId = Blank(SellerLegalRegistrationId),",
            source,
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- Hilfsmittel

    /// <summary>
    /// Der Verkäuferblock allein – der Käuferblock trägt eigene, absichtlich
    /// höhere Nummern, und beide zusammen zu prüfen sagte nichts über die
    /// Reihenfolge innerhalb einer Karte.
    /// </summary>
    private static XElement SellerCard()
        => Assert.Single(
            Load("InvoiceDataView.xaml").Descendants(Presentation + "Border"),
            border => border.Descendants(Presentation + "TextBlock").Any(
                text => text.Attribute("Text")?.Value == "Verkäufer (Sie)"));

    private static XElement Text(XElement seller, string value)
        => Assert.Single(
            seller.Descendants(Presentation + "TextBlock"),
            text => text.Attribute("Text")?.Value == value);

    private static XElement Input(XElement seller, string property)
        => Assert.Single(
            seller.Descendants(),
            element => element.Attributes().Any(attribute => attribute.Value.Contains(
                $"Draft.{property}, UpdateSourceTrigger=PropertyChanged",
                StringComparison.Ordinal)));

    private static int Row(XElement element)
        => int.Parse(
            element.Attribute("Grid.Row")?.Value ?? "0",
            System.Globalization.CultureInfo.InvariantCulture);

    private static XDocument Load(string fileName)
        => XDocument.Load(ProjectFiles.With(".xaml")
            .Single(path => Path.GetFileName(path) == fileName));

    private static string Source(string fileName)
        => File.ReadAllText(ProjectFiles.With(".cs")
            .Single(path => Path.GetFileName(path) == fileName));
}

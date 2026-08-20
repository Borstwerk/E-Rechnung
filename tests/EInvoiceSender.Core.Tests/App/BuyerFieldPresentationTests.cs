using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>Sichert die sichtbaren Buyer-Zusatzfelder in Schritt 2 und 3.</summary>
public sealed class BuyerFieldPresentationTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void SchrittZweiBindetLandVatUndMailWeiterhinDirektAnDenDraft()
    {
        XDocument view = Load("InvoiceDataView.xaml");
        string[] bindings =
        [
            .. view.Descendants()
                .Attributes()
                .Select(attribute => attribute.Value),
        ];

        Assert.Contains(bindings, value => value.Contains("Draft.BuyerCountry", StringComparison.Ordinal));
        Assert.Contains(bindings, value => value.Contains("Draft.BuyerVatId", StringComparison.Ordinal));
        Assert.Contains(bindings, value => value.Contains("Draft.BuyerEmail", StringComparison.Ordinal));
    }

    [Fact]
    public void SchrittDreiZeigtLandVatUndMailAlsKerndaten()
    {
        XDocument view = Load("ReviewView.xaml");
        XElement grid = Assert.Single(
            view.Descendants(Presentation + "UniformGrid"),
            element => element.Descendants(Presentation + "TextBlock")
                .Any(text => text.Attribute("Text")?.Value == "Käufer"));
        string content = grid.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("BuyerCountryText", content, StringComparison.Ordinal);
        Assert.Contains("BuyerVatIdText", content, StringComparison.Ordinal);
        Assert.Contains("BuyerEmailText", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ErgebnisVerwendetBuyerEmailFürDenBestehendenEmlEmpfängerweg()
    {
        string source = File.ReadAllText(ProjectFiles.With(".cs")
            .Single(path => Path.GetFileName(path) == "ResultViewModel.cs"));

        Assert.Contains("EmailRecipient = invoice.Buyer.Email ?? string.Empty", source,
            StringComparison.Ordinal);
        Assert.Contains(
            "To: string.IsNullOrWhiteSpace(EmailRecipient) ? [] : [EmailRecipient]",
            source,
            StringComparison.Ordinal);
    }

    private static XDocument Load(string fileName)
        => XDocument.Load(ProjectFiles.With(".xaml")
            .Single(path => Path.GetFileName(path) == fileName));
}

using System.Text.RegularExpressions;
using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Sichert die WPF-Verdrahtung des ausdrücklichen Speicherwegs. Die fachliche
/// Datenauswahl wird zur Laufzeit von <c>CompanyTemplateSavePlannerTests</c>
/// geprüft; hier geht es darum, dass kein anderer UI-Pfad schreibt.
/// </summary>
public sealed class CompanyTemplateSaveFlowTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void OberflächeBietetDieAusdrücklicheAktionUndErklärtDenPdfAusschluss()
    {
        XDocument view = XDocument.Load(ProjectFiles.With(".xaml")
            .Single(path => Path.GetFileName(path) == "InvoiceDataView.xaml"));
        XElement saveButton = Assert.Single(
            view.Descendants(Presentation + "Button"),
            element => element.Attribute("Command")?.Value
                .Contains("SaveOwnCompanyDataCommand", StringComparison.Ordinal) == true);
        string text = string.Join(' ', view.Descendants(Presentation + "TextBlock")
            .Select(element => element.Attribute("Text")?.Value));

        Assert.Equal("Als eigene Unternehmensdaten _speichern", saveButton.Attribute("Content")?.Value);
        Assert.Contains("Aus der PDF erkannte Angaben werden nicht übernommen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VorhandeneVorlageWirdInlineUndOhneMessageBoxBestätigt()
    {
        string xaml = Source("InvoiceDataView.xaml");
        string code = Source("InvoiceDataView.xaml.cs");

        Assert.Contains("HasCompanyTemplateOverwriteQuestion", xaml, StringComparison.Ordinal);
        Assert.Contains("ConfirmCompanyTemplateOverwriteCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CancelCompanyTemplateOverwriteCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModelHatGenauEinenStoreSchreibort()
    {
        string source = Source("InvoiceDataViewModel.cs");

        Match firstWrite = Regex.Match(source, @"\bSaveTemplateAsync\s*\(");
        Assert.True(firstWrite.Success);
        Assert.False(firstWrite.NextMatch().Success);
        Assert.Contains(
            "SaveTemplateAsync(plan.Candidate",
            source,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OnDraftPropertyChanged")]
    [InlineData("Reset")]
    [InlineData("CancelCompanyTemplateOverwrite")]
    public void PropertyChangeResetUndAbbruchSchreibenNicht(string method)
    {
        string body = MethodBody("InvoiceDataViewModel.cs", method);

        Assert.DoesNotContain("SaveTemplateAsync", body, StringComparison.Ordinal);
        Assert.DoesNotContain("PersistCompanyTemplateAsync", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SaveOwnCompanyDataAsync")]
    [InlineData("ConfirmCompanyTemplateOverwriteAsync")]
    public void BeideAusdrücklichenPfadeLesenVorDemPlanFrischAusDemStore(string method)
    {
        string body = MethodBody("InvoiceDataViewModel.cs", method);
        int load = body.IndexOf("LoadTemplateAsync", StringComparison.Ordinal);
        int plan = body.IndexOf("CompanyTemplateSavePlanner.Plan", StringComparison.Ordinal);

        Assert.True(load >= 0 && plan > load, $"{method} plant ohne unmittelbar vorher frisch zu laden.");
    }

    [Fact]
    public void IdentischerKandidatKehrtVorDemEinzigenSchreibwegZurück()
    {
        string body = MethodBody("InvoiceDataViewModel.cs", "SaveOwnCompanyDataAsync");
        int identical = body.IndexOf("!plan.IsChanged", StringComparison.Ordinal);
        int persist = body.IndexOf("PersistCompanyTemplateAsync", StringComparison.Ordinal);

        Assert.True(identical >= 0 && persist > identical);
        Assert.Contains("return;", body[identical..persist], StringComparison.Ordinal);
    }

    [Fact]
    public void ErfolgreichesSpeichernSynchronisiertNurDenSnapshot()
    {
        string body = MethodBody("MainViewModel.cs", "OnCompanyTemplateSaved");

        Assert.Contains("_appliedTemplate = template", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyTemplate", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ErzeugungsUndNavigationspfadeVerwendenDenNeuenPlannerNicht()
    {
        string main = Source("MainViewModel.cs");

        Assert.DoesNotContain("CompanyTemplateSavePlanner", main, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SaveOwnCompanyDataAsync",
            MethodBody("MainViewModel.cs", "GenerateAsync"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SaveOwnCompanyDataAsync",
            MethodBody("MainViewModel.cs", "GoForwardAsync"),
            StringComparison.Ordinal);
    }

    private static string MethodBody(string file, string method)
    {
        string source = Source(file);
        int start = source.IndexOf($" {method}(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{method} nicht in {file} gefunden.");

        int open = source.IndexOf('{', start);
        Assert.True(open >= 0, $"{method} in {file} hat keinen Rumpf.");

        int depth = 0;

        for (int index = open; index < source.Length; index++)
        {
            depth += source[index] switch { '{' => 1, '}' => -1, _ => 0 };

            if (depth == 0)
            {
                return source[open..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Der Rumpf von {method} endet nicht.");
    }

    private static string Source(string file)
        => File.ReadAllText(ProjectFiles.With(Path.GetExtension(file))
            .Single(path => Path.GetFileName(path) == file));
}

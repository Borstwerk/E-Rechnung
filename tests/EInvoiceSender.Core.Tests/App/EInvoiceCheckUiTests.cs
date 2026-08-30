using System.Xml.Linq;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Sichert die plattformübergreifend prüfbaren Verträge des WPF-Prüfmodus.
/// Die App selbst zielt auf Windows; deshalb werden Zusammensetzung und XAML
/// wie bei den übrigen Oberflächenwächtern am Quellbestand geprüft.
/// </summary>
public sealed class EInvoiceCheckUiTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void PrüfmodusIstVomErzeugungswizardGetrenntVerdrahtet()
    {
        string composition = ReadApp("App.xaml.cs");
        string mainWindow = ReadApp("Views", "MainWindow.xaml");
        string mainWindowCode = ReadApp("Views", "MainWindow.xaml.cs");
        string mainViewModel = ReadApp("ViewModels", "MainViewModel.cs");

        Assert.Contains("AddSingleton<IEInvoiceCheckService, EInvoiceCheckService>()", composition, StringComparison.Ordinal);
        Assert.Contains("AddTransient<EInvoiceCheckViewModel>()", composition, StringComparison.Ordinal);
        Assert.Contains("AddTransient<EInvoiceCheckWindow>()", composition, StringComparison.Ordinal);

        Assert.Contains("E-Rechnung _prüfen ...", mainWindow, StringComparison.Ordinal);
        Assert.Contains("OnCheckEInvoiceClicked", mainWindow, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<EInvoiceCheckWindow>()", mainWindowCode, StringComparison.Ordinal);

        Assert.DoesNotContain("IEInvoiceCheckService", mainViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("EInvoiceCheckWindow", mainViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void PrüffensterZeigtDasCoreErgebnisOhneKonformitätsbehauptung()
    {
        string viewModel = ReadApp("ViewModels", "EInvoiceCheckViewModel.cs");
        string window = ReadApp("Views", "Dialogs", "EInvoiceCheckWindow.xaml");
        string zusammen = viewModel + window;

        Assert.Contains("ShowFindings(ergebnis.Report)", viewModel, StringComparison.Ordinal);
        Assert.Contains("ergebnis.Completed", viewModel, StringComparison.Ordinal);
        Assert.Contains("ergebnis.Canceled", viewModel, StringComparison.Ordinal);
        Assert.Contains("ConfigureAwait(true)", viewModel, StringComparison.Ordinal);

        string[] bindungen =
        [
            "Result.SourceFileName",
            "Result.SourceSizeInBytes",
            "Result.SourceSha256",
            "Result.DocumentInfo.PdfVersion",
            "Result.DocumentInfo.EmbeddedFiles",
            "Result.InvoiceSummary.InvoiceNumber",
            "IssueDateText",
            "LineTotalText",
            "TaxBasisTotalText",
            "TaxTotalText",
            "GrandTotalText",
            "DuePayableAmountText",
            "Findings",
        ];

        foreach (string bindung in bindungen)
        {
            Assert.Contains(bindung, window, StringComparison.Ordinal);
        }

        Assert.Contains(
            "keine vollständige EN-16931- oder PDF/A-Konformitätsprüfung",
            window,
            StringComparison.Ordinal);

        string[] unzulässigeAussagen =
        [
            "ist gültig",
            "gültige E-Rechnung",
            "ist normkonform",
            "ist PDF/A-konform",
            "Prüfung bestanden",
        ];

        foreach (string aussage in unzulässigeAussagen)
        {
            Assert.DoesNotContain(aussage, zusammen, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("Succeeded", zusammen, StringComparison.Ordinal);
        Assert.DoesNotContain("IEInvoiceService", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IFileStorage", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ISettingsStore", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IEmailDraftService", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void WiederholungUndAbbruchHinterlassenKeinenAltenOderPersistiertenPfad()
    {
        string viewModel = ReadApp("ViewModels", "EInvoiceCheckViewModel.cs");
        string window = ReadApp("Views", "Dialogs", "EInvoiceCheckWindow.xaml");
        string windowCode = ReadApp("Views", "Dialogs", "EInvoiceCheckWindow.xaml.cs");

        int beginn = viewModel.IndexOf("BeginInspection(path);", StringComparison.Ordinal);
        int aufruf = viewModel.IndexOf(".CheckAsync(new CheckEInvoiceRequest(path)", StringComparison.Ordinal);

        Assert.True(beginn >= 0 && aufruf > beginn, "Der alte Anzeigestand muss vor dem Core-Aufruf geleert werden.");
        Assert.Contains("Result = null;", viewModel, StringComparison.Ordinal);
        Assert.Contains("ClearFindings();", viewModel, StringComparison.Ordinal);
        Assert.Contains("SelectedFileName = Path.GetFileName(path);", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("FilePath", viewModel, StringComparison.Ordinal);

        Assert.Contains("IncludeCancelCommand = true", viewModel, StringComparison.Ordinal);
        Assert.Contains("InspectCancelCommand", window, StringComparison.Ordinal);
        Assert.Contains("InspectCancelCommand.Execute(null)", windowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfUndCiiLeserWerdenNichtAlsZweitePipelineErzeugt()
    {
        string composition = ReadApp("App.xaml.cs");

        Assert.Contains(
            "(IPdfAttachmentReader)provider.GetRequiredService<IPdfAnalyzer>()",
            composition,
            StringComparison.Ordinal);
        Assert.Contains(
            "(ICiiInvoiceInspector)provider.GetRequiredService<IInvoiceXmlReader>()",
            composition,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IPdfAttachmentReader, PdfAnalyzer>", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<ICiiInvoiceInspector, CiiInvoiceReader>", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void EinLangerDateinameVerdrängtAbbruchUndFortschrittNicht()
    {
        XDocument window = XDocument.Load(AppPath("Views", "Dialogs", "EInvoiceCheckWindow.xaml"));
        XElement fileName = Assert.Single(
            window.Descendants(Presentation + "TextBlock"),
            element => (element.Attribute("Text")?.Value ?? string.Empty)
                .Contains("SelectedFileName", StringComparison.Ordinal));

        Assert.Equal("CharacterEllipsis", fileName.Attribute("TextTrimming")?.Value);
        Assert.Equal(Presentation + "Grid", fileName.Parent?.Name);
        Assert.Contains(
            fileName.Parent!.Elements(Presentation + "Grid.ColumnDefinitions")
                .Elements(Presentation + "ColumnDefinition"),
            column => column.Attribute("Width")?.Value == "*");
    }

    private static string ReadApp(params string[] parts)
        => File.ReadAllText(AppPath(parts));

    private static string AppPath(params string[] parts)
        => Path.Combine(
            TestPaths.RepositoryRoot,
            "src",
            "EInvoiceSender.App",
            Path.Combine(parts));
}

using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Zugferd;
using Xunit;

namespace EInvoiceSender.Core.Tests;

/// <summary>
/// Golden-Master-Tests der XML-Erzeugung.
///
/// Für jeden Fall aus <see cref="InvoiceScenarios"/> wird die XML erzeugt und
/// mit der abgelegten Sollfassung verglichen. Jede Änderung am Writer wird so
/// sichtbar, bevor sie unbemerkt in eine Rechnung gelangt.
///
/// Sollfassungen neu erzeugen (nur nach bewusster Änderung, und die Ausgabe
/// muss danach erneut mit dem CEN-Schematron geprüft werden):
///     UPDATE_GOLDEN_MASTERS=1 dotnet test tests/EInvoiceSender.Core.Tests
/// </summary>
public sealed class GoldenMasterTests
{
    private static readonly CiiInvoiceWriter Writer = new();

    [Theory]
    [MemberData(nameof(ScenarioKeys))]
    public void ErzeugteXmlEntsprichtDerSollfassung(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        string actual = GenerateXml(scenario);

        string expectedPath = Path.Combine(GoldenMasterDirectory, $"{key}.xml");

        if (ShouldUpdate)
        {
            Directory.CreateDirectory(GoldenMasterDirectory);
            File.WriteAllText(expectedPath, actual, new UTF8Encoding(false));
        }

        Assert.True(
            File.Exists(expectedPath),
            $"Sollfassung fehlt: {expectedPath}. Mit UPDATE_GOLDEN_MASTERS=1 erzeugen.");

        string expected = File.ReadAllText(expectedPath).ReplaceLineEndings("\n");

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(ScenarioKeys))]
    public void ErzeugteXmlIstZwischenLäufenIdentisch(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);

        Assert.Equal(GenerateXml(scenario), GenerateXml(scenario));
    }

    /// <summary>
    /// Schreibt alle Fälle in ein Ausgabeverzeichnis, damit die
    /// Gegenprüfung mit dem CEN-Schematron sie findet
    /// (build/validate-golden-masters.sh).
    /// </summary>
    [Fact]
    public void SchreibtAlleFälleFürDieGegenprüfung()
    {
        string validDirectory = Path.Combine(RepositoryRoot, "artifacts", "golden-masters", "valid");
        Directory.CreateDirectory(validDirectory);

        foreach (InvoiceScenario scenario in InvoiceScenarios.All.Where(s => s.ExpectedToBeValid))
        {
            File.WriteAllText(
                Path.Combine(validDirectory, $"{scenario.Key}.xml"),
                GenerateXml(scenario),
                new UTF8Encoding(false));
        }

        Assert.NotEmpty(Directory.GetFiles(validDirectory, "*.xml"));
    }

    public static TheoryData<string> ScenarioKeys()
    {
        var data = new TheoryData<string>();
        foreach (InvoiceScenario scenario in InvoiceScenarios.All)
        {
            data.Add(scenario.Key);
        }

        return data;
    }

    private static string GenerateXml(InvoiceScenario scenario)
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);
        byte[] xml = Writer.Write(scenario.Invoice, totals);

        return Encoding.UTF8.GetString(xml).ReplaceLineEndings("\n");
    }

    private static bool ShouldUpdate
        => Environment.GetEnvironmentVariable("UPDATE_GOLDEN_MASTERS") == "1";

    private static string GoldenMasterDirectory
        => Path.Combine(RepositoryRoot, "tests", "EInvoiceSender.Core.Tests", "GoldenMasters");

    /// <summary>
    /// Ermittelt das Repository-Wurzelverzeichnis vom Testausgabeverzeichnis aus.
    /// Sucht aufwärts nach der Solutiondatei, damit der Pfad unabhängig von
    /// der Build-Konfiguration stimmt.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException(
                       "Repository-Wurzel nicht gefunden – EInvoiceSender.sln fehlt oberhalb von "
                       + AppContext.BaseDirectory);
        }
    }
}

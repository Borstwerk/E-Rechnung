using System.Text;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Formats.Cii;
using EInvoiceSender.Formats.Tests.Scenarios;
using Xunit;

namespace EInvoiceSender.Formats.Tests;

/// <summary>
/// Erzeugt absichtlich fehlerhafte XML-Dateien fuer die Gegenpruefung.
///
/// Zweck: Eine Gegenpruefung, die nur gueltige Dateien kennt, beweist nichts –
/// sie koennte auch dann gruen sein, wenn der Validator gar nichts prueft.
/// Diese Faelle muessen vom CEN-Schematron beanstandet werden.
/// </summary>
public sealed class NegativeFixtureTests
{
    private static readonly CiiInvoiceWriter Writer = new();

    [Fact]
    public void SchreibtFehlerhafteFaelleFuerDieGegenpruefung()
    {
        string invalidDirectory = Path.Combine(RepositoryRoot, "artifacts", "golden-masters", "invalid");
        Directory.CreateDirectory(invalidDirectory);

        string valid = GenerateValidXml("01-dienstleistung-19");

        // Fall 11 der Spezifikation: absichtlich ungueltige XML.
        // Die Nettosumme (BT-109) passt nicht mehr zur Summe der Positionen –
        // das muss BR-CO-13 beanstanden.
        string brokenTotals = valid.Replace(
            "<ram:TaxBasisTotalAmount>950.00</ram:TaxBasisTotalAmount>",
            "<ram:TaxBasisTotalAmount>111.11</ram:TaxBasisTotalAmount>",
            StringComparison.Ordinal);

        Assert.NotEqual(valid, brokenTotals);
        Write(invalidDirectory, "90-falsche-nettosumme.xml", brokenTotals);

        // Fehlende Pflichtangabe: ohne Rechnungsnummer (BT-1) muss BR-02 greifen.
        string missingNumber = RemoveElement(valid, "ram:ID", "RE-2026-0001");
        Assert.NotEqual(valid, missingNumber);
        Write(invalidDirectory, "91-fehlende-rechnungsnummer.xml", missingNumber);

        // Falscher Steuerbetrag: BT-117 passt nicht zu Basis mal Satz (BR-CO-17).
        string brokenTax = valid.Replace(
            "<ram:CalculatedAmount>180.50</ram:CalculatedAmount>",
            "<ram:CalculatedAmount>99.99</ram:CalculatedAmount>",
            StringComparison.Ordinal);
        Assert.NotEqual(valid, brokenTax);
        Write(invalidDirectory, "92-falscher-steuerbetrag.xml", brokenTax);

        Assert.Equal(3, Directory.GetFiles(invalidDirectory, "*.xml").Length);
    }

    private static string GenerateValidXml(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);

        return Encoding.UTF8.GetString(Writer.Write(scenario.Invoice, totals)).ReplaceLineEndings("\n");
    }

    /// <summary>Entfernt die erste Zeile, die das gesuchte Element mit dem Wert enthaelt.</summary>
    private static string RemoveElement(string xml, string elementName, string value)
    {
        string needle = $"<{elementName}>{value}</{elementName}>";
        int index = xml.IndexOf(needle, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException($"'{needle}' nicht gefunden.");
        }

        int lineStart = xml.LastIndexOf('\n', index) + 1;
        int lineEnd = xml.IndexOf('\n', index) + 1;

        return xml.Remove(lineStart, lineEnd - lineStart);
    }

    private static void Write(string directory, string fileName, string content)
        => File.WriteAllText(Path.Combine(directory, fileName), content, new UTF8Encoding(false));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
        }
    }
}

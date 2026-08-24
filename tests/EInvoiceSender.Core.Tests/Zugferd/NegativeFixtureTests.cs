using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Zugferd;
using Xunit;

namespace EInvoiceSender.Core.Tests;

/// <summary>
/// Erzeugt absichtlich fehlerhafte XML-Dateien für die Gegenprüfung.
///
/// Zweck: Eine Gegenprüfung, die nur gültige Dateien kennt, beweist nichts –
/// sie könnte auch dann grün sein, wenn der Validator gar nichts prüft.
/// Diese Fälle müssen vom CEN-Schematron beanstandet werden.
/// </summary>
public sealed class NegativeFixtureTests
{
    private static readonly CiiInvoiceWriter Writer = new();

    [Fact]
    public void SchreibtFehlerhafteFälleFürDieGegenprüfung()
    {
        string invalidDirectory = Path.Combine(RepositoryRoot, "artifacts", "golden-masters", "invalid");
        Directory.CreateDirectory(invalidDirectory);

        string valid = GenerateValidXml("01-dienstleistung-19");

        // Fall 11 der Spezifikation: absichtlich ungültige XML.
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

        // Verkäufer ohne maschinell auswertbare Kennung: Die USt-IdNr.
        // (schemeID="VA") fällt weg, die Steuernummer (schemeID="FC") bleibt
        // stehen. Das muss BR-CO-26 beanstanden.
        //
        // **Warum gerade dieser Fall hier steht:** Genau er kam in einer
        // Windows-Abnahme durch die eingebauten Prüfungen und wurde erst vom
        // externen Validator beanstandet. Die interne Regel ließ die
        // Steuernummer als Ersatz gelten, das CEN-Schematron nicht. Diese
        // Datei hält den Unterschied künftig fest – sie ist die Gegenprobe
        // dazu, dass beide wieder auseinanderlaufen.
        string withoutVatRegistration = RemoveSellerVatRegistration(valid);

        Assert.NotEqual(valid, withoutVatRegistration);

        // Ausdrücklich nur im Verkäuferblock prüfen: Der Käufer trägt seine
        // eigene USt-IdNr., und die soll hier gerade stehen bleiben.
        string sellerBlock = SellerBlockOf(withoutVatRegistration);
        Assert.DoesNotContain("schemeID=\"VA\"", sellerBlock, StringComparison.Ordinal);
        Assert.Contains("schemeID=\"FC\"", sellerBlock, StringComparison.Ordinal);
        Assert.Contains("schemeID=\"VA\"", withoutVatRegistration, StringComparison.Ordinal);
        Write(invalidDirectory, "93-ohne-kennung.xml", withoutVatRegistration);

        Assert.Equal(4, Directory.GetFiles(invalidDirectory, "*.xml").Length);
    }

    private static string GenerateValidXml(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);

        return Encoding.UTF8.GetString(Writer.Write(scenario.Invoice, totals)).ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Entfernt die gesamte <c>SpecifiedTaxRegistration</c> mit dem gesuchten
    /// Schema – öffnendes Element, Kennung und schließendes Element.
    ///
    /// Nur die Kennungszeile herauszunehmen würde ein leeres
    /// <c>SpecifiedTaxRegistration</c> hinterlassen und damit einen anderen
    /// Fehler erzeugen als den, den diese Vorgabe zeigen soll.
    /// </summary>
    private static string RemoveSellerVatRegistration(string xml)
    {
        string[] lines = xml.Split('\n');

        int sellerStart = Array.FindIndex(
            lines, line => line.Contains("<ram:SellerTradeParty>", StringComparison.Ordinal));
        int sellerEnd = Array.FindIndex(
            lines, line => line.Contains("</ram:SellerTradeParty>", StringComparison.Ordinal));

        if (sellerStart < 0 || sellerEnd <= sellerStart)
        {
            throw new InvalidOperationException("Kein Verkäuferblock gefunden.");
        }

        int id = Array.FindIndex(
            lines,
            sellerStart,
            sellerEnd - sellerStart,
            line => line.Contains("<ram:ID schemeID=\"VA\">", StringComparison.Ordinal));

        if (id < 1)
        {
            throw new InvalidOperationException("Der Verkäufer trägt keine USt-IdNr.");
        }

        return string.Join('\n', lines.Where((_, index) => index < id - 1 || index > id + 1));
    }

    /// <summary>Schneidet den Verkäuferblock heraus, damit Prüfungen ihn nicht mit dem Käufer verwechseln.</summary>
    private static string SellerBlockOf(string xml)
    {
        int start = xml.IndexOf("<ram:SellerTradeParty>", StringComparison.Ordinal);
        int end = xml.IndexOf("</ram:SellerTradeParty>", start, StringComparison.Ordinal);

        return xml[start..end];
    }

    /// <summary>Entfernt die erste Zeile, die das gesuchte Element mit dem Wert enthält.</summary>
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
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
        }
    }
}

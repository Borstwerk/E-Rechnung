using System.Runtime.Versioning;
using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Dauerhafte Ende-zu-Ende-Konformitätstests für die fertige Rechnungsdatei.
///
/// Für jeden Golden Master wird die gesamte Kette geprüft:
/// XML erzeugen, mit dem CEN-Schematron prüfen, in die PDF einbetten, die
/// fertige Datei mit veraPDF und Mustang prüfen, erneut öffnen, die XML
/// extrahieren, auf Gleichheit prüfen und erneut gegen das Schematron laufen
/// lassen. Zusätzlich werden Anhangname, MIME-Typ, Dateibeziehung,
/// Profilkennung und die XMP-Metadaten geprüft.
///
/// Grundsatz: **Eine positive oberste Zusammenfassung genügt nie.** Der
/// Validator-Adapter bewertet jede Teilzusammenfassung einzeln, und diese Tests
/// prüfen zusätzlich, dass überhaupt eine Aussage vorliegt.
///
/// Die Laufzeit ist bewusst in Kauf genommen: Ohne die echten Referenzwerkzeuge
/// wäre die Aussage "normkonform" eine bloße Behauptung.
/// </summary>
[Collection(ExternalValidatorTestGroup.Name)]
// Der Rasterweg in dieser Klasse braucht PDFium.
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class EndToEndConformanceTests : IDisposable
{
    private readonly ExternalValidatorFixture _fixture;
    private readonly List<string> _temporaryFiles = [];
    private readonly CiiInvoiceWriter _writer = new();
    private readonly CiiInvoiceReader _reader = new();
    private readonly PdfAnalyzer _analyzer;
    private readonly PdfAInvoiceComposer _composer;

    public EndToEndConformanceTests(ExternalValidatorFixture fixture)
    {
        _fixture = fixture;
        _analyzer = new PdfAnalyzer(_reader, NullLogger<PdfAnalyzer>.Instance);
        _composer = new PdfAInvoiceComposer(_analyzer, NullLogger<PdfAInvoiceComposer>.Instance);
    }

    /// <summary>
    /// Die vollständige Kette für einen Golden Master.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValidScenarioKeys))]
    public async Task GesamteKetteIstNormkonform(string key)
    {
        IExternalDocumentValidator validator = _fixture.RequireValidator();
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);

        // --- 1. CII-XML erzeugen -------------------------------------------
        byte[] xml = _writer.Write(scenario.Invoice, totals);
        Assert.NotEmpty(xml);

        // --- 2. EN-16931-Schematron auf der erzeugten XML -------------------
        string xmlPath = Temp(xml, ".xml");
        ValidationReport xmlReport = await validator.ValidateAsync(xmlPath, TestContext.Current.CancellationToken);
        AssertPassed(xmlReport, $"Schematron auf der erzeugten XML ({key})");

        // --- 3. In die PDF einbetten ---------------------------------------
        string sourcePdfPath = Temp(TestPdfFactory.CreateSimplePdf());
        byte[] originalSource = await File.ReadAllBytesAsync(sourcePdfPath, TestContext.Current.CancellationToken);

        var request = new PdfACompositionRequest(
            SourcePdfPath: sourcePdfPath,
            InvoiceXml: xml,
            Title: $"Rechnung {scenario.Invoice.InvoiceNumber}",
            Author: scenario.Invoice.Seller.Name,
            Subject: $"Rechnung {scenario.Invoice.InvoiceNumber}",
            CreationDate: new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1)),
            Attachment: _writer.Attachment);

        CompositionResult composition = await _composer.ComposeAsync(request, TestContext.Current.CancellationToken);
        Assert.True(composition.Succeeded, Describe(composition.Report));
        Assert.NotNull(composition.PdfBytes);

        // Das Original darf sich unter keinen Umständen verändert haben.
        Assert.Equal(
            originalSource,
            await File.ReadAllBytesAsync(sourcePdfPath, TestContext.Current.CancellationToken));

        // --- 4./5. PDF/A-3b und Factur-X der fertigen Datei -----------------
        string resultPath = Temp(composition.PdfBytes, ".pdf");
        ValidationReport pdfReport = await validator.ValidateAsync(resultPath, TestContext.Current.CancellationToken);
        AssertPassed(pdfReport, $"veraPDF und Schematron auf der fertigen PDF ({key})");

        // --- 6. Erneut öffnen ---------------------------------------------
        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(resultPath, TestContext.Current.CancellationToken);
        Assert.Empty(reopened.UpgradeBlockers);
        Assert.Equal(scenario.Invoice.Lines.Count > 0, reopened.PageCount > 0);

        // --- 7. XML extrahieren und vergleichen -----------------------------
        Assert.True(reopened.HasExistingInvoiceXml, "Die eingebettete XML fehlt.");
        Assert.Equal(xml, reopened.ExistingInvoiceXml);

        // --- 8. Extrahierte XML erneut gegen das Schematron -----------------
        string extractedPath = Temp(reopened.ExistingInvoiceXml!, ".xml");
        ValidationReport extractedReport =
            await validator.ValidateAsync(extractedPath, TestContext.Current.CancellationToken);
        AssertPassed(extractedReport, $"Schematron auf der extrahierten XML ({key})");

        // --- 9. Anhangname, MIME-Typ, Dateibeziehung ------------------------
        EmbeddedFileInfo attachment = Assert.Single(reopened.EmbeddedFiles);
        Assert.Equal("factur-x.xml", attachment.FileName);
        Assert.Equal("text/xml", attachment.MimeType);
        Assert.Equal("/Alternative", attachment.Relationship);
        Assert.Equal(xml.Length, attachment.SizeInBytes);

        // --- 10. Profilkennung ---------------------------------------------
        Assert.Equal(CiiConstants.ProfileEn16931, reopened.ExistingInvoiceProfile);
        Assert.Equal(CiiConstants.ProfileEn16931, _reader.ReadProfileId(reopened.ExistingInvoiceXml!));

        // --- 11. XMP-Metadaten ---------------------------------------------
        Assert.Equal("3", reopened.DeclaredPdfAPart);
        Assert.Equal("B", reopened.DeclaredPdfAConformance);
        AssertXmpContents(composition.PdfBytes);

        // --- 12. Summen im Ergebnis stimmen mit der Berechnung überein -----
        InvoiceEcho? echo = _reader.ReadEcho(reopened.ExistingInvoiceXml!);
        Assert.NotNull(echo);
        Assert.Equal(scenario.Invoice.InvoiceNumber, echo.InvoiceNumber);
        Assert.Equal(totals.LineTotal, echo.LineTotal);
        Assert.Equal(totals.TaxBasisTotal, echo.TaxBasisTotal);
        Assert.Equal(totals.TaxTotal, echo.TaxTotal);
        Assert.Equal(totals.GrandTotal, echo.GrandTotal);
        Assert.Equal(totals.DuePayableAmount, echo.DuePayableAmount);
    }

    /// <summary>
    /// Dieselbe Kette, aber über den Rasterweg.
    ///
    /// **Das ist der Punkt, an dem sich entscheidet, ob der Rasterweg ein Weg
    /// ist oder eine Ausrede.** Eine sichtbare Kopie, die die Referenzwerkzeuge
    /// nicht bestehen würde, wäre kein Ausweg für den Anwender, sondern ein
    /// Versprechen, das erst beim Empfänger platzt. Deshalb läuft hier
    /// dasselbe Freigabegate wie über dem direkten Weg: veraPDF für PDF/A-3b,
    /// das CEN-Schematron für die Rechnungsdaten – auf der fertigen Datei und
    /// noch einmal auf der aus ihr herausgeholten XML.
    ///
    /// Die Ausgangsdatei ist eine, die den direkten Weg nicht gehen kann: ihre
    /// Schrift ist nicht eingebettet.
    /// </summary>
    [Fact]
    public async Task DerRasterwegBestehtDasselbeFreigabegate()
    {
        IExternalDocumentValidator validator = _fixture.RequireValidator();

        InvoiceScenario scenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);
        byte[] xml = _writer.Write(scenario.Invoice, totals);

        byte[] originalBytes = TestPdfFactory.CreatePdfWithNonEmbeddedFont();
        string sourcePdfPath = Temp(originalBytes);

        // Der direkte Weg lehnt diese Datei ab – das ist die Voraussetzung
        // dafür, dass der folgende Nachweis überhaupt etwas aussagt.
        PdfAnalysisResult vorher = await _analyzer.AnalyzeAsync(
            sourcePdfPath, TestContext.Current.CancellationToken);
        Assert.Contains(PdfUpgradeBlocker.FontsNotEmbedded, vorher.UpgradeBlockers);

        var request = new PdfACompositionRequest(
            SourcePdfPath: sourcePdfPath,
            InvoiceXml: xml,
            Title: $"Rechnung {scenario.Invoice.InvoiceNumber}",
            Author: scenario.Invoice.Seller.Name,
            Subject: $"Rechnung {scenario.Invoice.InvoiceNumber}",
            CreationDate: new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1)),
            Attachment: _writer.Attachment);

        CompositionResult composition = await PipelineParts.RasterComposer()
            .ComposeAsync(request, TestContext.Current.CancellationToken);

        Assert.True(composition.Succeeded, Describe(composition.Report));
        Assert.NotNull(composition.PdfBytes);

        // Das Original bleibt byteweise unberührt.
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(sourcePdfPath, TestContext.Current.CancellationToken));

        string resultPath = Temp(composition.PdfBytes, ".pdf");
        ValidationReport pdfReport = await validator.ValidateAsync(
            resultPath, TestContext.Current.CancellationToken);
        AssertPassed(pdfReport, "veraPDF und Schematron auf der gerasterten PDF");

        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(
            resultPath, TestContext.Current.CancellationToken);

        // Das Hindernis ist verschwunden, weil es die Schrift nicht mehr gibt.
        Assert.Empty(reopened.UpgradeBlockers);
        Assert.Equal(xml, reopened.ExistingInvoiceXml);
        Assert.Equal("3", reopened.DeclaredPdfAPart);
        Assert.Equal("B", reopened.DeclaredPdfAConformance);

        string extractedPath = Temp(reopened.ExistingInvoiceXml!, ".xml");
        ValidationReport extractedReport = await validator.ValidateAsync(
            extractedPath, TestContext.Current.CancellationToken);
        AssertPassed(extractedReport, "Schematron auf der aus der Rasterdatei geholten XML");

        AssertXmpContents(composition.PdfBytes);
    }

    /// <summary>
    /// Ein Bericht, der keine einzige Aussage enthält, darf nicht als bestanden
    /// gelten. Dieser Test sichert genau diese Eigenschaft des Adapters ab.
    /// </summary>
    [Fact]
    public async Task ValidatorMeldetFehlerBeiUnlesbaremBericht()
    {
        IExternalDocumentValidator validator = _fixture.RequireValidator();

        // Eine Datei, mit der Mustang nichts anfangen kann.
        string path = Temp(Encoding.UTF8.GetBytes("kein gültiges Dokument"), ".xml");

        ValidationReport report = await validator.ValidateAsync(path, TestContext.Current.CancellationToken);

        Assert.True(
            report.HasErrors,
            "Ein nicht auswertbares Ergebnis muss als Fehler gelten, nicht als Erfolg.");
    }

    /// <summary>
    /// Eine absichtlich verfälschte Datei muss beanstandet werden. Ohne diesen
    /// Nachweis könnte die gesamte Gegenprüfung wirkungslos sein, ohne dass es
    /// auffällt.
    /// </summary>
    [Fact]
    public async Task VerfälschteSummeWirdVomSchematronBeanstandet()
    {
        IExternalDocumentValidator validator = _fixture.RequireValidator();

        InvoiceScenario scenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);
        string xml = Encoding.UTF8.GetString(_writer.Write(scenario.Invoice, totals));

        string broken = xml.Replace(
            "<ram:TaxBasisTotalAmount>950.00</ram:TaxBasisTotalAmount>",
            "<ram:TaxBasisTotalAmount>1.00</ram:TaxBasisTotalAmount>",
            StringComparison.Ordinal);

        Assert.NotEqual(xml, broken);

        string path = Temp(Encoding.UTF8.GetBytes(broken), ".xml");
        ValidationReport report = await validator.ValidateAsync(path, TestContext.Current.CancellationToken);

        Assert.True(report.HasErrors, "Eine falsche Nettosumme muss beanstandet werden.");
    }

    /// <summary>
    /// Prüft die XMP-Angaben direkt in den Bytes der fertigen Datei.
    /// </summary>
    private static void AssertXmpContents(byte[] pdfBytes)
    {
        string text = Encoding.Latin1.GetString(pdfBytes);

        Assert.Contains("<pdfaid:part>3</pdfaid:part>", text, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:conformance>B</pdfaid:conformance>", text, StringComparison.Ordinal);
        Assert.Contains(
            "<fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>", text, StringComparison.Ordinal);
        Assert.Contains("<fx:DocumentType>INVOICE</fx:DocumentType>", text, StringComparison.Ordinal);
        Assert.Contains(
            "<fx:ConformanceLevel>EN 16931</fx:ConformanceLevel>", text, StringComparison.Ordinal);
        Assert.Contains(XmpMetadataBuilder.FacturXNamespace, text, StringComparison.Ordinal);

        // Das Erweiterungsschema muss deklariert sein, sonst ist die Datei
        // trotz korrekter Felder nicht PDF/A-konform.
        Assert.Contains("pdfaExtension:schemas", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verlangt einen bestandenen Bericht. Ein Bericht ohne jede Aussage gilt
    /// ausdrücklich nicht als bestanden.
    /// </summary>
    private static void AssertPassed(ValidationReport report, string step)
    {
        Assert.False(
            report.HasErrors,
            $"{step} fehlgeschlagen: {Describe(report)}");

        Assert.Contains(
            report.Findings,
            f => f.RuleId == "APP-EXT-000" && f.Severity == FindingSeverity.Information);
    }

    public static TheoryData<string> ValidScenarioKeys()
    {
        var data = new TheoryData<string>();

        foreach (InvoiceScenario scenario in InvoiceScenarios.All.Where(s => s.ExpectedToBeValid))
        {
            data.Add(scenario.Key);
        }

        return data;
    }

    private static string Describe(ValidationReport report)
        => report.Findings.Count == 0
            ? "(keine Befunde)"
            : string.Join(
                " | ",
                report.Findings.Select(f => $"{f.Severity} {f.RuleId}: {f.Message} {f.TechnicalDetail}"));

    private string Temp(byte[] content, string extension = ".pdf")
    {
        string path = TestPdfFactory.WriteToTempFile(content, extension);
        _temporaryFiles.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Aufräumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

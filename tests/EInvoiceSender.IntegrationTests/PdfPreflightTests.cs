using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Formats.Cii;
using EInvoiceSender.Infrastructure.PdfA;
using EInvoiceSender.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Prueft die Eingangspruefung (Preflight).
///
/// Zwei Eigenschaften stehen hier im Mittelpunkt:
/// 1. Das Urteil ist dreistufig und trifft in jedem Fall zu.
/// 2. Wird eine Datei abgelehnt, nennt die Meldung eine konkrete Einstellung,
///    die der Anwender in seinem Programm aendern kann. Eine Ablehnung ohne
///    Handlungsanweisung waere fuer ihn wertlos.
/// </summary>
public sealed class PdfPreflightTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfPreflightService _preflight;
    private readonly PdfAInvoiceComposer _composer;
    private readonly CiiInvoiceWriter _writer = new();

    public PdfPreflightTests()
    {
        var analyzer = new PdfAnalyzer(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);

        _preflight = new PdfPreflightService(analyzer, NullLogger<PdfPreflightService>.Instance);
        _composer = new PdfAInvoiceComposer(analyzer, NullLogger<PdfAInvoiceComposer>.Instance);
    }

    [Fact]
    public async Task EinfachePdfIstGeeignet()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf(pageCount: 3));

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.Suitable, report.Verdict);
        Assert.True(report.CanProceed);
        Assert.True(report.IsReadable);
        Assert.False(report.IsEncrypted);
        Assert.False(report.IsDamaged);
        Assert.False(report.HasDigitalSignature);
        Assert.True(report.AllFontsEmbedded);
        Assert.False(report.HasActiveContent);
        Assert.Equal(3, report.PageCount);
        Assert.False(report.HasExistingInvoice);
        Assert.Empty(report.EmbeddedFiles);
        Assert.NotEqual("unbekannt", report.PdfVersion);
        Assert.True(report.FileSizeInBytes > 0);
        Assert.False(report.Findings.HasErrors);
    }

    [Fact]
    public async Task PdfMitNichtEingebetteterSchriftIstNichtGeeignetUndNenntDieEinstellung()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        Assert.False(report.CanProceed);
        Assert.False(report.AllFontsEmbedded);

        ValidationFinding finding = FindError(report, "APP-PRE-011");

        // Die Meldung muss dem Anwender sagen, was er umstellen soll.
        Assert.Contains("Schriftarten einbetten", finding.Message, StringComparison.Ordinal);
        Assert.Contains("PDF/A", finding.Message, StringComparison.Ordinal);
        Assert.NotNull(finding.TechnicalDetail);
    }

    [Fact]
    public async Task BeschaedigtePdfIstNichtGeeignet()
    {
        string path = Temp(TestPdfFactory.CreateDamagedPdf());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        Assert.True(report.IsDamaged);

        ValidationFinding finding = FindError(report, "APP-PRE-013");
        Assert.Contains("neu", finding.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DateiOhnePdfInhaltWirdAbgelehntTrotzPdfEndung()
    {
        string path = Temp(TestPdfFactory.CreateNonPdf());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);

        ValidationFinding finding = FindError(report, "APP-PRE-004");
        Assert.Contains("Dateiendung", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LeereDateiWirdAbgelehnt()
    {
        string path = Temp([]);

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        FindError(report, "APP-PRE-002");
    }

    [Fact]
    public async Task FehlendeDateiWirdAbgelehnt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"gibt-es-nicht-{Guid.NewGuid():N}.pdf");

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        FindError(report, "APP-PRE-001");
    }

    [Fact]
    public async Task ZuGrosseDateiWirdAbgelehntUndNenntEinenAusweg()
    {
        var analyzer = new PdfAnalyzer(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);
        var strict = new PdfPreflightService(
            analyzer, NullLogger<PdfPreflightService>.Instance, maxFileSizeMegabytes: 1);

        // Eine Datei knapp ueber der Grenze.
        byte[] large = new byte[(1024 * 1024) + 1];
        "%PDF-1.4\n"u8.CopyTo(large);
        string path = Temp(large);

        PdfPreflightReport report = await strict.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);

        ValidationFinding finding = FindError(report, "APP-PRE-003");
        Assert.Contains("Bildaufloesung", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BereitsHybridePdfIstGeeignetAberMitWarnung()
    {
        // Eine fertige E-Rechnung erzeugen und erneut pruefen.
        string sourcePath = Temp(TestPdfFactory.CreateSimplePdf());

        InvoiceScenario scenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        var totals = Domain.Calculation.InvoiceCalculator.Calculate(scenario.Invoice);

        CompositionResult composed = await _composer.ComposeAsync(
            new PdfACompositionRequest(
                sourcePath,
                _writer.Write(scenario.Invoice, totals),
                "Rechnung",
                scenario.Invoice.Seller.Name,
                "Rechnung",
                DateTimeOffset.UnixEpoch,
                _writer.Attachment),
            TestContext.Current.CancellationToken);

        Assert.True(composed.Succeeded);

        string hybridPath = Temp(composed.PdfBytes!);
        PdfPreflightReport report = await _preflight.InspectAsync(hybridPath, TestContext.Current.CancellationToken);

        // Sie ist verarbeitbar – aber der Anwender wird ausdruecklich gewarnt.
        Assert.Equal(PreflightVerdict.SuitableWithWarnings, report.Verdict);
        Assert.True(report.CanProceed);
        Assert.True(report.HasExistingInvoice);
        Assert.Equal(CiiConstants.ProfileEn16931, report.ExistingInvoiceProfile);
        Assert.True(report.HasXmpMetadata);
        Assert.Equal("3B", report.DeclaredPdfALevel);

        ValidationFinding warning = Assert.Single(
            report.Findings.Findings,
            f => f.RuleId == "APP-PRE-020" && f.Severity == FindingSeverity.Warning);

        Assert.Contains("ersetzt", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreflightVeraendertDasOriginalNicht()
    {
        byte[] original = TestPdfFactory.CreateSimplePdf();
        string path = Temp(original);

        await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);
        await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task JedeAblehnungNenntEineHandlungUndEinTechnischesDetail()
    {
        // Alle Ablehnungswege durchlaufen und die Meldungsqualitaet pruefen.
        string[] paths =
        [
            Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFont()),
            Temp(TestPdfFactory.CreateDamagedPdf()),
            Temp(TestPdfFactory.CreateNonPdf()),
        ];

        foreach (string path in paths)
        {
            PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);

            foreach (ValidationFinding finding in report.Findings.Findings
                         .Where(f => f.Severity == FindingSeverity.Error))
            {
                Assert.StartsWith("APP-PRE-", finding.RuleId, StringComparison.Ordinal);

                // Verstaendlicher deutscher Satz, kein blosser Fehlercode.
                Assert.True(
                    finding.Message.Length > 40,
                    $"Meldung zu {finding.RuleId} ist zu knapp: {finding.Message}");
                Assert.EndsWith(".", finding.Message.TrimEnd(), StringComparison.Ordinal);
                Assert.NotNull(finding.TechnicalDetail);
            }
        }
    }

    private static ValidationFinding FindError(PdfPreflightReport report, string ruleId)
    {
        ValidationFinding? finding = report.Findings.Findings
            .FirstOrDefault(f => f.RuleId == ruleId && f.Severity == FindingSeverity.Error);

        Assert.True(
            finding is not null,
            $"Erwarteter Fehler {ruleId} fehlt. Gefunden: "
            + string.Join(", ", report.Findings.Findings.Select(f => $"{f.Severity} {f.RuleId}")));

        return finding!;
    }

    private string Temp(byte[] content)
    {
        string path = TestPdfFactory.WriteToTempFile(content);
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
                // Aufraeumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

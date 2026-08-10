using System.Runtime.Versioning;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Prüft den Rasterweg im Gesamtablauf.
///
/// **Worum es hier geht.** Der Rasterweg ist kein zweiter Ausgang, an dem die
/// Prüfungen milder wären. Er ist ein anderer Weg zur selben Datei: Am Ende
/// steht dieselbe PDF/A-3b mit derselben eingebetteten XML, und sie durchläuft
/// dieselbe Ergebnisprüfung. Was ihn unterscheidet, ist die sichtbare Seite –
/// und der Umstand, dass ihn niemand ohne ausdrückliche Zustimmung betritt.
///
/// Die Prüfungen hier decken drei Fragen ab:
///
/// 1. Kommt man ohne Zustimmung hinein? (Nein.)
/// 2. Bleibt mit Zustimmung eine Prüfung aus? (Nein.)
/// 3. Öffnet der Weg die Tür für Dateien, die aus anderen Gründen abgelehnt
///    gehören? (Nein.)
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class RasterFallbackRouteTests : IDisposable
{
    private readonly List<string> _temporaryPaths = [];
    private readonly CiiInvoiceWriter _writer = new();
    private readonly CiiInvoiceReader _reader = new();
    private readonly PdfAnalyzer _analyzer;
    private readonly string _outputDirectory;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 4, 1, 10, 0, 0, TimeSpan.FromHours(2));

    public RasterFallbackRouteTests()
    {
        _analyzer = new PdfAnalyzer(_reader, NullLogger<PdfAnalyzer>.Instance);
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"einvoice-raster-{Guid.NewGuid():N}");
        _temporaryPaths.Add(_outputDirectory);
    }

    // ------------------------------------------------- Die Zustimmung selbst

    /// <summary>
    /// Ohne Zustimmung entsteht nichts – auch keine halbe Datei, kein Bericht,
    /// kein Verzeichnis mit Resten.
    /// </summary>
    [Fact]
    public async Task OhneZustimmungEntstehtKeineDatei()
    {
        string source = TempPdf(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Null(result.ReportJsonFile);

        ValidationFinding finding = Assert.Single(
            result.Report.Findings, f => f.RuleId == "APP-USE-003");
        Assert.Contains("sichtbare Kopie", finding.Message, StringComparison.Ordinal);

        Assert.False(
            Directory.Exists(_outputDirectory) && Directory.GetFiles(_outputDirectory).Length > 0);
    }

    /// <summary>
    /// Die Zustimmung gilt nur, wo sie gebraucht wird. Eine geeignete Datei
    /// geht auch dann den direkten Weg, wenn der Auftrag die Zustimmung trägt –
    /// sonst wäre ein einmal gesetztes Häkchen ein stiller Qualitätsverlust für
    /// alle folgenden Rechnungen.
    /// </summary>
    [Fact]
    public async Task DieZustimmungAlleinSchicktNiemandenAufDenRasterweg()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source) with { RasterFallbackConfirmed = true },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.DoesNotContain(result.Report.Findings, f => f.RuleId == "APP-PDF-040");

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);

        Assert.Contains("Direkte Übernahme der Originalseiten", text, StringComparison.Ordinal);
    }

    // --------------------------------------------- Der Weg mit Zustimmung

    /// <summary>
    /// Mit Zustimmung entsteht eine vollständige, geprüfte E-Rechnung – und die
    /// Prüfungen dahinter sind dieselben wie auf dem direkten Weg: Die Datei
    /// wird erneut geöffnet, die XML herausgeholt und byteweise verglichen, die
    /// PDF/A-Kennzeichnung gelesen, die Prüfsumme gebildet.
    /// </summary>
    [Fact]
    public async Task MitZustimmungEntstehtEineVollständigGeprüfteERechnung()
    {
        byte[] original = TestPdfFactory.CreatePdfWithNonEmbeddedFont();
        string source = TempPdf(original);

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Consented(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.NotNull(result.OutputFile);
        Assert.Equal(9, result.CompletedSteps.Count);
        Assert.All(result.CompletedSteps, s => Assert.NotEqual(StepState.Failed, s.State));

        // Das Original ist byteweise unberührt.
        Assert.Equal(
            original,
            await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));

        // Die eingebettete XML ist im Ergebnis wiederzufinden – byteidentisch.
        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(
            result.OutputFile.FullPath, TestContext.Current.CancellationToken);

        Assert.NotNull(reopened.ExistingInvoiceXml);
        Assert.Equal(CiiConstants.ProfileEn16931, reopened.ExistingInvoiceProfile);
        Assert.Equal("3", reopened.DeclaredPdfAPart);
        Assert.Equal("B", reopened.DeclaredPdfAConformance);

        // Und das Ergebnis geht seinerseits den direkten Weg: Die gerasterten
        // Seiten tragen keine Schrift mehr, die nicht eingebettet wäre.
        Assert.DoesNotContain(PdfUpgradeBlocker.FontsNotEmbedded, reopened.UpgradeBlockers);
    }

    /// <summary>
    /// Der gegangene Weg steht im Prüfbericht – in beiden Fassungen.
    ///
    /// Ohne diese Angabe ließe sich später nicht mehr beantworten, ob der
    /// sichtbare Inhalt einer archivierten Datei aus dem Original stammt oder
    /// aus einem Abbild davon. Ansehen kann man ihr das nicht.
    /// </summary>
    [Fact]
    public async Task DerPrüfberichtNenntDenGegangenenWeg()
    {
        string source = TempPdf(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Consented(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);
        string json = await File.ReadAllTextAsync(
            result.ReportJsonFile!.FullPath, TestContext.Current.CancellationToken);

        foreach (string bericht in new[] { text, json })
        {
            Assert.Contains("Raster-Fallback", bericht, StringComparison.Ordinal);
            Assert.Contains("300 dpi", bericht, StringComparison.Ordinal);
        }

        // Der bewusst gewählte und erfolgreich gegangene Weg ist ein Hinweis,
        // kein Fehler und keine Warnung.
        ValidationFinding note = Assert.Single(
            result.Report.Findings, f => f.RuleId == "APP-PDF-040");

        Assert.Equal(FindingSeverity.Information, note.Severity);
        Assert.Contains("Original wurde nicht verändert", note.Message, StringComparison.Ordinal);
        Assert.False(result.Report.HasErrors);
    }

    /// <summary>
    /// Seitenzahl und sichtbare Geometrie bleiben erhalten, auch bei gemischten
    /// Formaten und gedrehten Seiten. Eine stillschweigend zurückgedrehte Seite
    /// wäre eine veränderte Rechnung.
    /// </summary>
    [Fact]
    public async Task SeitenzahlUndSeitenformateBleibenErhalten()
    {
        string source = TempPdf(TestPdfFactory.CreateMixedPageSizesPdf());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Consented(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));

        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(
            result.OutputFile!.FullPath, TestContext.Current.CancellationToken);

        Assert.Equal(3, reopened.PageCount);
    }

    /// <summary>
    /// Eine PDF, aus der sich kein Text lesen lässt – eine eingescannte
    /// Rechnung –, hält den Ablauf nicht auf. Sie wird verarbeitet, und die
    /// Erkennung sagt geradeheraus, dass sie nichts gefunden hat.
    ///
    /// **Hier wird ausdrücklich keine Texterkennung vorgetäuscht.** Es gibt
    /// keine OCR in dieser Anwendung, und ein Werkzeug, das aus einem Bild
    /// Beträge errät und sie ins Formular schreibt, wäre schlimmer als eines,
    /// das schweigt: Der Anwender bestätigt am Ende, dass die erfassten Daten
    /// mit der sichtbaren Rechnung übereinstimmen. Was er bestätigt, muss er
    /// selbst eingegeben oder wenigstens nachvollzogen haben.
    /// </summary>
    [Fact]
    public async Task EinePdfOhneLesbarenTextWirdVerarbeitetUndErfindetNichts()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        var detector = new InvoiceDataDetector(
            new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance),
            NullLogger<InvoiceDataDetector>.Instance);

        InvoiceDetectionResult detection = await detector.DetectAsync(
            source, null, TestContext.Current.CancellationToken);

        Assert.False(detection.HasUsableText);
        Assert.False(detection.HasAnything);

        DetectionEntry hinweis = Assert.Single(DetectionOverview.Describe(detection));
        Assert.Equal(DetectionEntryKind.Missing, hinweis.Kind);
        Assert.Contains("von Hand erfasst", hinweis.Text, StringComparison.Ordinal);

        // Und die Datei selbst ist trotzdem verarbeitbar.
        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
    }

    // ------------------------------------- Was der Weg NICHT mit hereinlässt

    /// <summary>
    /// Eine beschädigte Datei wird nicht „gerettet“. Was auf dem Bild landete,
    /// wäre ungewiss – und eine Rechnung, deren Inhalt man raten muss, wird
    /// nicht erzeugt.
    /// </summary>
    [Fact]
    public async Task EineBeschädigteDateiWirdAuchMitZustimmungNichtGerastert()
        => await AssertAbgelehnt(TestPdfFactory.CreateDamagedPdf());

    /// <summary>
    /// Ein Öffnungskennwort bleibt ein Öffnungskennwort. Es gibt nichts
    /// darzustellen.
    /// </summary>
    [Fact]
    public async Task EineKennwortgeschützteDateiWirdAuchMitZustimmungNichtGerastert()
        => await AssertAbgelehnt(TestPdfFactory.CreatePdfWithUserPassword());

    /// <summary>
    /// Ein Besitzerkennwort ebenso: Die Datei ließe sich darstellen, aber der
    /// Rechteinhaber hat festgelegt, was mit ihr geschehen darf.
    /// </summary>
    [Fact]
    public async Task EineRechtebeschränkteDateiWirdAuchMitZustimmungNichtGerastert()
        => await AssertAbgelehnt(TestPdfFactory.CreatePdfWithOwnerPassword());

    /// <summary>
    /// Aktive Inhalte gehören nicht in eine Rechnung. Dass sie beim Rastern
    /// verschwänden, ist kein Grund, sie zu übergehen.
    /// </summary>
    [Fact]
    public async Task EineDateiMitAktivemInhaltWirdAuchMitZustimmungNichtGerastert()
        => await AssertAbgelehnt(TestPdfFactory.CreatePdfWithNonEmbeddedFontAndJavaScript());

    private async Task AssertAbgelehnt(byte[] content)
    {
        string source = TempPdf(content);

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Consented(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.True(result.Report.HasErrors);

        // Und es wurde auch nicht heimlich gerastert und dann verworfen.
        Assert.DoesNotContain(result.Report.Findings, f => f.RuleId == "APP-PDF-040");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.Preflight, failed.Step);
    }

    // ------------------------------------------------------------------ Aufbau

    private EInvoiceService BuildUseCase()
        => new(
            PipelineParts.Preflight(_analyzer),
            new En16931RuleValidator(new FixedClock(FixedNow)),
            _writer,
            _reader,
            new PdfAInvoiceComposer(_analyzer, NullLogger<PdfAInvoiceComposer>.Instance),
            PipelineParts.RasterComposer(),
            _analyzer,
            new FileStorage(NullLogger<FileStorage>.Instance),
            new TemporaryWorkspaceFactory(),
            new StubClock(FixedNow),
            [],
            NullLogger<EInvoiceService>.Instance);

    private CreateEInvoiceRequest Request(string sourcePath)
        => new(
            SourcePdfPath: sourcePath,
            Invoice: InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice,
            ContentMatchConfirmed: true,
            OutputDirectory: _outputDirectory);

    private CreateEInvoiceRequest Consented(string sourcePath)
        => Request(sourcePath) with { RasterFallbackConfirmed = true };

    private static string Describe(CreateEInvoiceResult result)
        => string.Join(
            " | ",
            result.Report.Findings
                .Where(f => f.Severity == FindingSeverity.Error)
                .Select(f => $"{f.RuleId}: {f.Message} [{f.TechnicalDetail}]"));

    private string TempPdf(byte[] content)
    {
        string path = TestPdfFactory.WriteToTempFile(content);
        _temporaryPaths.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (string path in _temporaryPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Aufräumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

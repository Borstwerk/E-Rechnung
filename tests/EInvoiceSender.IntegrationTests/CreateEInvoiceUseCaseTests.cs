using System.Runtime.Versioning;
using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Diagnostics;
using EInvoiceSender.Core.Mail;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Ende-zu-Ende-Tests des Gesamtablaufs.
///
/// Abgedeckt sind Erfolg, Warnung, Validierungsfehler, beschädigte PDF,
/// nicht eingebettete Schrift, Zeitüberschreitung eines externen Werkzeugs und
/// Abbruch durch den Benutzer.
///
/// In jedem Fehlerfall wird geprüft, dass **keine Ausgabedatei** entsteht und
/// das **Original unverändert** bleibt. Eine halb fertige Datei wäre schlimmer
/// als gar keine, weil der Anwender sie für gültig halten könnte.
/// </summary>
// Die Kette schließt den Rasterweg ein, und der braucht PDFium. Die
// Angabe hält den Prüfer davon ab, Zielsysteme anzunehmen, auf denen
// diese Anwendung nie läuft.
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class CreateEInvoiceUseCaseTests : IDisposable
{
    private readonly List<string> _temporaryPaths = [];
    private readonly CiiInvoiceWriter _writer = new();
    private readonly CiiInvoiceReader _reader = new();
    private readonly PdfAnalyzer _analyzer;
    private readonly string _outputDirectory;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 4, 1, 10, 0, 0, TimeSpan.FromHours(2));

    public CreateEInvoiceUseCaseTests()
    {
        _analyzer = new PdfAnalyzer(_reader, NullLogger<PdfAnalyzer>.Instance);
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"einvoice-out-{Guid.NewGuid():N}");
        _temporaryPaths.Add(_outputDirectory);
    }

    [Fact]
    public async Task ErfolgreicherDurchlaufErzeugtDateiBerichtUndPrüfsumme()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        byte[] originalBytes = await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken);

        var progressMessages = new List<PipelineProgress>();
        var progress = new ImmediateProgress<PipelineProgress>(progressMessages.Add);

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), progress, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.NotNull(result.OutputFile);
        Assert.True(File.Exists(result.OutputFile.FullPath));

        // Prüfsumme, Zeitpunkt, Standard und Profil sind gesetzt.
        Assert.Equal(64, result.OutputFile.Sha256.Length);
        Assert.Equal(FixedNow, result.CreatedAt);
        Assert.Equal(CiiConstants.ProfileEn16931, result.ProfileId);
        Assert.Contains("EN 16931", result.StandardDescription, StringComparison.Ordinal);

        // Die Prüfsumme muss zur tatsächlich geschriebenen Datei passen.
        byte[] written = await File.ReadAllBytesAsync(
            result.OutputFile.FullPath, TestContext.Current.CancellationToken);
        Assert.Equal(FileStorage.ComputeSha256(written), result.OutputFile.Sha256);

        // Beide Berichte liegen vor.
        Assert.NotNull(result.ReportJsonFile);
        Assert.NotNull(result.ReportTextFile);

        string json = await File.ReadAllTextAsync(
            result.ReportJsonFile.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains(result.OutputFile.Sha256, json, StringComparison.Ordinal);
        Assert.Contains("\"profil\"", json, StringComparison.Ordinal);

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains("SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("Prüfwerkzeuge", text, StringComparison.Ordinal);

        // Das Original bleibt unverändert.
        Assert.Equal(
            originalBytes,
            await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));

        // Alle neun Schritte wurden gemeldet.
        Assert.Equal(9, result.CompletedSteps.Count);
        Assert.Contains(progressMessages, m => m.State == StepState.Running);
        Assert.All(result.CompletedSteps, s => Assert.NotEqual(StepState.Failed, s.State));
    }

    [Fact]
    public async Task OhneBestätigungWirdNichtsErzeugt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source) with { ContentMatchConfirmed = false },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-001");

        // Es darf nicht einmal ein Ausgabeverzeichnis mit Resten entstehen.
        Assert.False(
            Directory.Exists(_outputDirectory) && Directory.GetFiles(_outputDirectory).Length > 0);
    }

    [Fact]
    public async Task FehlerhafteRechnungsdatenVerhindernDieErzeugung()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        Invoice invalid = BaseInvoice() with { InvoiceNumber = "  " };

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source) with { Invoice = invalid },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-DOC-001");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.ValidateInvoiceData, failed.Step);
    }

    [Fact]
    public async Task UngültigeBuyerEmailStopptKontrolliertVorDerErzeugung()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        Invoice baseline = BaseInvoice();
        Invoice invalid = baseline with
        {
            Buyer = baseline.Buyer with { Email = "kaputt" },
        };

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source) with { Invoice = invalid },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, finding => finding.RuleId == "APP-BUY-004");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, step => step.State == StepState.Failed);
        Assert.Equal(PipelineStep.ValidateInvoiceData, failed.Step);
    }

    /// <summary>
    /// Eine nicht eingebettete Schrift hält den Vorgang weiterhin an – aber aus
    /// einem anderen Grund als früher: Es fehlt nicht der Weg, es fehlt die
    /// Zustimmung zu ihm.
    ///
    /// **Diese Sperre sitzt bewusst hier und nicht in der Oberfläche.** Wer den
    /// Kern unmittelbar aufruft, umgeht sonst genau die Entscheidung, um die es
    /// geht. Der Auftrag lässt sich als Datensatz zusammenstellen; die
    /// Zustimmung ist ein Feld darin, und ein nicht gesetztes Feld ist keine
    /// Zustimmung.
    /// </summary>
    [Fact]
    public async Task NichtEingebetteteSchriftBrauchtDieZustimmungZurSichtbarenKopie()
    {
        string source = TempPdf(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-003");

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.Preflight, failed.Step);
    }

    [Fact]
    public async Task BeschädigtePdfFührtZumAbbruch()
    {
        string source = TempPdf(TestPdfFactory.CreateDamagedPdf());

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.True(result.Report.HasErrors);
    }

    [Fact]
    public async Task BereitsHybridePdfBrauchtEineAusdrückicheBestätigung()
    {
        // Zuerst eine fertige E-Rechnung erzeugen ...
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        CreateEInvoiceResult first = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, Describe(first));

        // ... und diese ohne Bestätigung erneut verarbeiten.
        CreateEInvoiceResult second = await BuildUseCase().CreateAsync(
            Request(first.OutputFile!.FullPath),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(second.Succeeded);
        Assert.Contains(second.Report.Findings, f => f.RuleId == "APP-USE-002");

        // Mit Bestätigung geht es durch.
        CreateEInvoiceResult third = await BuildUseCase().CreateAsync(
            Request(first.OutputFile.FullPath) with { ExistingInvoiceReplacementConfirmed = true },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(third.Succeeded, Describe(third));
    }

    /// <summary>
    /// **Die Sperre hält auch bei einem unlesbaren Rechnungsanhang.**
    ///
    /// Der vorige Test verwendet eine wohlgeformte, lesbare E-Rechnung. Genau
    /// dieser Fall verdeckte eine Lücke: Solange die Anwesenheit einer
    /// vorhandenen Rechnung am erfolgreich gelesenen Profil hing, öffnete ein
    /// Anhang, der sich nicht auswerten lässt, die Sperre – und der Anwender
    /// hätte seine einzige Ausfertigung überschrieben, ohne gefragt worden zu
    /// sein.
    ///
    /// Geprüft werden die drei Gründe, aus denen der begrenzte Leser einen
    /// Anhang nicht auswertet: ungewöhnlich gepackt, zu groß, beschädigt.
    /// </summary>
    [Theory]
    [InlineData("gepackt")]
    [InlineData("zu-gross")]
    [InlineData("kaputt")]
    public async Task EinUnlesbarerRechnungsanhangBrauchtEbenfallsEineBestätigung(string fall)
    {
        string source = TempPdf(AttachedPdfFactory.Create(UnlesbarerAnhang(fall)));

        CreateEInvoiceResult ohneBestätigung = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(ohneBestätigung.Succeeded);
        Assert.Contains(ohneBestätigung.Report.Findings, f => f.RuleId == "APP-USE-002");

        // Mit ausdrücklicher Bestätigung läuft der bestehende Weg weiter.
        CreateEInvoiceResult mitBestätigung = await BuildUseCase().CreateAsync(
            Request(source) with { ExistingInvoiceReplacementConfirmed = true },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(mitBestätigung.Succeeded, Describe(mitBestätigung));
    }

    /// <summary>
    /// Ein Rechnungsanhang, den der begrenzte Leser nicht auswerten kann –
    /// jeweils aus einem anderen Grund.
    /// </summary>
    private static AttachedPdfFactory.Attachment UnlesbarerAnhang(string fall) => fall switch
    {
        "gepackt" => new AttachedPdfFactory.Attachment(
            "factur-x.xml", "<rsm:CrossIndustryInvoice/>"u8.ToArray(),
            Compress: true, WithDecodeParms: true),

        "zu-gross" => new AttachedPdfFactory.Attachment(
            "factur-x.xml", Füllung(SecureXml.MaxXmlSizeInBytes + 1024), Compress: true),

        _ => new AttachedPdfFactory.Attachment(
            "factur-x.xml", "<rsm:CrossIndustryInvoice><abgeschnitten"u8.ToArray()),
    };

    private static byte[] Füllung(int größe)
    {
        byte[] daten = new byte[größe];
        Array.Fill(daten, (byte)'A');

        return daten;
    }

    [Fact]
    public async Task ZeitüberschreitungDesExternenWerkzeugsVerhindertDieAusgabe()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.TimesOut))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-EXT-003");

        // Kein Rest im Ausgabeverzeichnis.
        Assert.True(
            !Directory.Exists(_outputDirectory) || Directory.GetFiles(_outputDirectory).Length == 0);
    }

    [Fact]
    public async Task BeanstandungDurchDenExternenValidatorVerhindertDieAusgabe()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.ReportsError))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.OutputFile);

        PipelineProgress failed = Assert.Single(
            result.CompletedSteps, s => s.State == StepState.Failed);
        Assert.Equal(PipelineStep.ExternalValidation, failed.Step);
    }

    /// <summary>
    /// Die ausgelieferte Anwendung richtet gar keinen externen Validator ein –
    /// sie soll ohne Java-Laufzeit arbeiten. Dieser Test fährt genau diese
    /// Zusammenstellung: kein Validator, vollständiger Ablauf.
    ///
    /// Er ist das automatisierte Gegenstück zur Abnahme auf einem sauberen
    /// Windows ohne Java: PDF laden, Rechnung erzeugen, speichern.
    /// </summary>
    [Fact]
    public async Task OhneJedenExternenValidatorEntstehtEineVollständigeDatei()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase()
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));
        Assert.NotNull(result.OutputFile);
        Assert.True(File.Exists(result.OutputFile!.FullPath));
        Assert.Empty(result.Validators);

        // Der Bericht behauptet nicht, es habe eine externe Prüfung gegeben.
        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);

        Assert.Contains("kein externer Validator", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FehlenderExternerValidatorWirdImBerichtAusgewiesen()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult result = await BuildUseCase(
                new StubValidator("Testvalidator", StubBehavior.NotAvailable))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        // Die Datei entsteht, aber der Bericht sagt deutlich, dass die
        // Gegenprüfung nicht stattgefunden hat.
        Assert.True(result.Succeeded, Describe(result));
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-030");

        ValidatorInfo info = Assert.Single(result.Validators);
        Assert.False(info.WasExecuted);

        string text = await File.ReadAllTextAsync(
            result.ReportTextFile!.FullPath, TestContext.Current.CancellationToken);
        Assert.Contains("NICHT AUSGEFÜHRT", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Der Nachweis, dass die Gegenprüfung die Verkäuferkennung wirklich
    /// prüft.** Ein Schreibfehler bei BT-29 fiele sonst erst beim Empfänger
    /// auf: Die Datei bliebe wohlgeformt, die Summen stimmten, und nur die
    /// Angabe, mit der der Empfänger den Rechnungssteller identifiziert, wäre
    /// eine andere als die bestätigte.
    ///
    /// Der Schreiber wird dafür nicht nachgebaut, sondern der echte mit einer
    /// veränderten Kennung gefüttert. So entsteht genau die Datei, die ein
    /// solcher Fehler erzeugen würde – gültig in allem übrigen.
    /// </summary>
    [Fact]
    public async Task EineVertauschteVerkäuferkennungStopptDieErzeugung()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        Invoice invoice = BaseInvoice();
        invoice = invoice with
        {
            Seller = invoice.Seller with { SellerIdentifier = "LIEF-4711" },
        };

        var request = new CreateEInvoiceRequest(
            SourcePdfPath: source,
            Invoice: invoice,
            ContentMatchConfirmed: true,
            OutputDirectory: _outputDirectory);

        CreateEInvoiceResult result = await BuildUseCase(
                new SellerIdentifierTamperingWriter(_writer, "LIEF-0000"))
            .CreateAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-014");

        // Eine halb fertige Datei wäre schlimmer als gar keine.
        Assert.Null(result.OutputFile);
        Assert.False(
            Directory.Exists(_outputDirectory) && Directory.GetFiles(_outputDirectory).Length > 0);
    }

    /// <summary>
    /// Gegenprobe: Ohne Verfälschung trägt derselbe Weg die Kennung durch bis
    /// in die erzeugte Datei. Ohne sie bewiese der Test oben nur, dass
    /// irgendetwas scheitert.
    /// </summary>
    [Fact]
    public async Task DieVerkäuferkennungStehtInDerErzeugtenDatei()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        Invoice invoice = BaseInvoice();
        invoice = invoice with
        {
            Seller = invoice.Seller with { SellerIdentifier = "LIEF-4711" },
        };

        CreateEInvoiceResult result = await BuildUseCase().CreateAsync(
            new CreateEInvoiceRequest(
                SourcePdfPath: source,
                Invoice: invoice,
                ContentMatchConfirmed: true,
                OutputDirectory: _outputDirectory),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));

        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(
            result.OutputFile!.FullPath, TestContext.Current.CancellationToken);

        InvoiceEcho? echo = _reader.ReadEcho(reopened.ExistingInvoiceXml!);

        Assert.NotNull(echo);
        Assert.Equal("LIEF-4711", echo.SellerIdentifier);
    }

    [Fact]
    public async Task BenutzerabbruchHinterlässtKeineDatei()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        using var cts = new CancellationTokenSource();

        // Beim ersten Fortschrittsereignis abbrechen.
        var progress = new ImmediateProgress<PipelineProgress>(_ => cts.Cancel());

        CreateEInvoiceResult result = await BuildUseCase()
            .CreateAsync(Request(source), progress, cts.Token);

        Assert.False(result.Succeeded);
        Assert.True(result.Canceled);
        Assert.Null(result.OutputFile);
        Assert.Contains(result.Report.Findings, f => f.RuleId == "APP-USE-090");

        Assert.True(
            !Directory.Exists(_outputDirectory) || Directory.GetFiles(_outputDirectory).Length == 0);
    }

    [Fact]
    public async Task VorhandeneDateiWirdNichtStillÜberschrieben()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());

        CreateEInvoiceResult first = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded, Describe(first));

        CreateEInvoiceResult second = await BuildUseCase().CreateAsync(
            Request(source), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(second.Succeeded, Describe(second));

        Assert.NotEqual(first.OutputFile!.FullPath, second.OutputFile!.FullPath);
        Assert.Contains("(2)", second.OutputFile.FullPath, StringComparison.Ordinal);
        Assert.True(File.Exists(first.OutputFile.FullPath));
    }

    /// <summary>
    /// Nach einem gescheiterten Lauf bleibt kein Arbeitsverzeichnis zurück.
    ///
    /// **Warum hier nicht gezählt wird.** Vorher zählte dieser Test alle
    /// <c>EInvoiceSender-*</c>-Verzeichnisse unter dem temporären Verzeichnis
    /// des Systems, vor und nach dem Lauf. Das war eine Aussage über den ganzen
    /// Rechner, nicht über diesen Vorgang: Jede andere Prüfklasse, die
    /// gleichzeitig lief, legte ihr eigenes – völlig berechtigtes –
    /// Arbeitsverzeichnis an und ließ die Zahlen auseinandergehen. Unter Windows
    /// fiel das zuerst auf, weil dort mehr parallel läuft.
    ///
    /// Die Frage lautet nicht „wie viele gibt es?“, sondern „ist **dieses**
    /// gelöscht?“. Also wird genau das verfolgt: Die Fabrik merkt sich, was sie
    /// ausgegeben hat, und danach wird dieser eine Pfad geprüft. Das ist
    /// unabhängig von allem, was sonst auf dem Rechner geschieht – und braucht
    /// weder eine Wartezeit noch das Abschalten der Parallelität.
    /// </summary>
    [Fact]
    public async Task TemporäreDateienWerdenAuchImFehlerfallEntfernt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        var factory = new RecordingWorkspaceFactory();

        CreateEInvoiceResult result = await BuildUseCase(
                factory, new StubValidator("Testvalidator", StubBehavior.ReportsError))
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);

        // Ohne diese Zusicherung wäre der Rest wertlos: Wurde gar kein
        // Arbeitsverzeichnis angelegt, ist auch keines übrig geblieben.
        string workspace = Assert.Single(factory.Created);

        Assert.False(
            Directory.Exists(workspace),
            $"Das Arbeitsverzeichnis {workspace} ist nach dem gescheiterten Lauf noch da.");
    }

    /// <summary>
    /// Auch der geglückte Lauf lässt nichts zurück. Der Fehlerfall allein
    /// belegt das nicht – beim Speichern kommen weitere Zwischendateien hinzu.
    /// </summary>
    [Fact]
    public async Task TemporäreDateienWerdenAuchNachErfolgEntfernt()
    {
        string source = TempPdf(TestPdfFactory.CreateSimplePdf());
        var factory = new RecordingWorkspaceFactory();

        CreateEInvoiceResult result = await BuildUseCase(factory)
            .CreateAsync(Request(source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, Describe(result));

        string workspace = Assert.Single(factory.Created);

        Assert.False(Directory.Exists(workspace));
    }

    /// <summary>
    /// Maßgeblicher Datenschutztest des persistierten Ergebnisses. Er fährt
    /// den echten Erzeugungsweg und den E-Mail-Entwurf mit bewusst markanten
    /// Nutzdaten und durchsucht danach jede tatsächlich geschriebene
    /// Diagnosezeile.
    /// </summary>
    [Fact]
    public async Task PersistenteDiagnoselogsEnthaltenKeineRechnungsOderKundendaten()
    {
        string logDirectory = TempDirectory("PRIVACY-LOG-DIRECTORY-9F3");
        string sourceDirectory = TempDirectory("PRIVACY-INPUT-DIRECTORY-9F3");
        string outputDirectory = TempDirectory("PRIVACY-OUTPUT-DIRECTORY-9F3");
        string draftDirectory = TempDirectory("PRIVACY-DRAFT-DIRECTORY-9F3");
        string sourcePath = Path.Combine(sourceDirectory, "PRIVACY-INPUT-FILENAME-9F3.pdf");

        byte[] sourcePdf =
        [
            .. TestPdfFactory.CreateSimplePdf(),
            .. Encoding.UTF8.GetBytes("\n% PRIVACY-PDF-CONTENT-9F3\n"),
        ];
        await File.WriteAllBytesAsync(sourcePath, sourcePdf, TestContext.Current.CancellationToken);

        Invoice invoice = PrivacyInvoice();

        using (var provider = new LocalFileLoggerProvider(new DiagnosticLogOptions(logDirectory)))
        using (ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder
                   .SetMinimumLevel(LogLevel.Information)
                   .AddProvider(provider)))
        {
            CreateEInvoiceResult result = await BuildUseCase(loggerFactory).CreateAsync(
                new CreateEInvoiceRequest(
                    SourcePdfPath: sourcePath,
                    Invoice: invoice,
                    ContentMatchConfirmed: true,
                    OutputDirectory: outputDirectory),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded, Describe(result));

            byte[] attachment = await File.ReadAllBytesAsync(
                result.OutputFile!.FullPath,
                TestContext.Current.CancellationToken);
            var mail = new EmlDraftService(
                loggerFactory.CreateLogger<EmlDraftService>(),
                draftDirectory);
            EmailDraftResult draft = await mail.CreateDraftAsync(
                new EmailDraft(
                    From: "PRIVACY-SELLER-MAIL-9F3@example.invalid",
                    FromDisplayName: "PRIVACY-SELLER-NAME-9F3 GmbH",
                    To: ["PRIVACY-BUYER-MAIL-9F3@example.invalid"],
                    Subject: "PRIVACY-INVOICE-NUMBER-9F3",
                    Body: "PRIVACY-MAIL-BODY-9F3",
                    Attachments:
                    [
                        new EmailAttachment(
                            "PRIVACY-OUTPUT-FILENAME-9F3.pdf",
                            "application/pdf",
                            attachment),
                    ]),
                TestContext.Current.CancellationToken);

            Assert.True(draft.Succeeded, draft.Message);
        }

        string[] logFiles = Directory.GetFiles(logDirectory, "diagnose-*.log");
        string persisted = string.Join('\n', logFiles.Select(File.ReadAllText));

        // Positiver Beleg, dass der echte Ablauf tatsächlich in genau diese
        // Dateien geschrieben hat und der Test nicht bloß ein leeres Log prüft.
        Assert.Contains("event=2020", persisted, StringComparison.Ordinal);
        Assert.Contains("event=5001", persisted, StringComparison.Ordinal);
        Assert.Contains("event=6001", persisted, StringComparison.Ordinal);
        Assert.Contains("event=7001", persisted, StringComparison.Ordinal);

        string[] forbiddenMarkers =
        [
            "PRIVACY-SELLER-NAME-9F3",
            "PRIVACY-BUYER-NAME-9F3",
            "PRIVACY-INVOICE-NUMBER-9F3",
            "PRIVACY-SELLER-MAIL-9F3",
            "PRIVACY-BUYER-MAIL-9F3",
            "PRIVACY-STREET-9F3",
            "DE89370400440532013000",
            "DE89 3704 0044 0532 0130 00",
            "MARKDEFFXXX",
            "DE999999999",
            "99/888/77777",
            "PRIVACY-POSITION-9F3",
            "PRIVACY-PDF-CONTENT-9F3",
            "PRIVACY-XML-CONTENT-9F3",
            "PRIVACY-MAIL-BODY-9F3",
            "PRIVACY-INPUT-DIRECTORY-9F3",
            "PRIVACY-INPUT-FILENAME-9F3",
            "PRIVACY-OUTPUT-DIRECTORY-9F3",
            "PRIVACY-OUTPUT-FILENAME-9F3",
            "PRIVACY-DRAFT-DIRECTORY-9F3",
        ];

        foreach (string marker in forbiddenMarkers)
        {
            Assert.DoesNotContain(marker, persisted, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ Aufbau

    private EInvoiceService BuildUseCase(params IExternalDocumentValidator[] validators)
        => BuildUseCase(new TemporaryWorkspaceFactory(), validators);

    private EInvoiceService BuildUseCase(IInvoiceXmlWriter writer)
        => BuildUseCase(new TemporaryWorkspaceFactory(), writer);

    private EInvoiceService BuildUseCase(
        ITemporaryWorkspaceFactory workspaceFactory, params IExternalDocumentValidator[] validators)
        => BuildUseCase(workspaceFactory, _writer, validators);

    private EInvoiceService BuildUseCase(
        ITemporaryWorkspaceFactory workspaceFactory,
        IInvoiceXmlWriter writer,
        params IExternalDocumentValidator[] validators)
        => new(
            PipelineParts.Preflight(_analyzer),
            new En16931RuleValidator(new FixedClock(FixedNow)),
            writer,
            _reader,
            new PdfAInvoiceComposer(_analyzer, NullLogger<PdfAInvoiceComposer>.Instance),
            PipelineParts.RasterComposer(),
            _analyzer,
            new FileStorage(NullLogger<FileStorage>.Instance),
            workspaceFactory,
            new StubClock(FixedNow),
            validators,
            NullLogger<EInvoiceService>.Instance);

    private EInvoiceService BuildUseCase(ILoggerFactory loggerFactory)
    {
        var analyzer = new PdfAnalyzer(_reader, loggerFactory.CreateLogger<PdfAnalyzer>());
        var renderProbe = new PdfiumRenderProbe(loggerFactory.CreateLogger<PdfiumRenderProbe>());
        var preflight = new PdfPreflightService(
            analyzer,
            renderProbe,
            loggerFactory.CreateLogger<PdfPreflightService>());
        var directComposer = new PdfAInvoiceComposer(
            analyzer,
            loggerFactory.CreateLogger<PdfAInvoiceComposer>());
        var rasterBuilder = new RasterizedPdfBuilder(
            loggerFactory.CreateLogger<RasterizedPdfBuilder>());
        var rasterComposer = new RasterFallbackComposer(
            rasterBuilder,
            loggerFactory.CreateLogger<RasterFallbackComposer>());

        return new EInvoiceService(
            preflight,
            new En16931RuleValidator(new FixedClock(FixedNow)),
            _writer,
            _reader,
            directComposer,
            rasterComposer,
            analyzer,
            new FileStorage(loggerFactory.CreateLogger<FileStorage>()),
            new TemporaryWorkspaceFactory(),
            new StubClock(FixedNow),
            [],
            loggerFactory.CreateLogger<EInvoiceService>());
    }

    private CreateEInvoiceRequest Request(string sourcePath)
        => new(
            SourcePdfPath: sourcePath,
            Invoice: BaseInvoice(),
            ContentMatchConfirmed: true,
            OutputDirectory: _outputDirectory);

    private static Invoice BaseInvoice()
        => InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice;

    private static Invoice PrivacyInvoice()
    {
        Invoice baseline = BaseInvoice();

        return baseline with
        {
            InvoiceNumber = "PRIVACY-INVOICE-NUMBER-9F3",
            Seller = baseline.Seller with
            {
                Name = "PRIVACY-SELLER-NAME-9F3 GmbH",
                Address = baseline.Seller.Address with { Street = "PRIVACY-STREET-9F3 42" },
                Email = "PRIVACY-SELLER-MAIL-9F3@example.invalid",
                VatId = "DE999999999",
                TaxNumber = "99/888/77777",
            },
            Buyer = baseline.Buyer with
            {
                Name = "PRIVACY-BUYER-NAME-9F3 AG",
                Address = baseline.Buyer.Address with { Street = "PRIVACY-STREET-9F3 84" },
                Email = "PRIVACY-BUYER-MAIL-9F3@example.invalid",
            },
            Lines =
            [
                baseline.Lines[0] with
                {
                    Name = "PRIVACY-POSITION-9F3",
                    Description = "PRIVACY-PDF-CONTENT-9F3",
                },
            ],
            Payment = baseline.Payment! with
            {
                BankAccount = new BankAccount(
                    "PRIVACY-SELLER-NAME-9F3 GmbH",
                    Iban.Parse("DE89370400440532013000"),
                    "MARKDEFFXXX"),
                Reference = "PRIVACY-INVOICE-NUMBER-9F3",
            },
            Note = "PRIVACY-XML-CONTENT-9F3",
        };
    }

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

    private string TempDirectory(string name)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
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
                else if (File.Exists(path))
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

/// <summary>Zeitquelle mit festem Wert.</summary>
internal sealed class StubClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now => now;
}

/// <summary>
/// Schreibt mit dem echten Schreiber, tauscht aber vorher die Verkäuferkennung
/// aus. Das bildet einen Schreibfehler bei BT-29 nach, ohne den Schreiber
/// nachzubauen: Die erzeugte Datei ist in allem übrigen die richtige.
/// </summary>
internal sealed class SellerIdentifierTamperingWriter(
    IInvoiceXmlWriter inner, string? identifier) : IInvoiceXmlWriter
{
    public string ProfileId => inner.ProfileId;

    public string FormatDescription => inner.FormatDescription;

    public InvoiceAttachmentDescriptor Attachment => inner.Attachment;

    public byte[] Write(Invoice invoice, InvoiceTotals totals)
        => inner.Write(
            invoice with { Seller = invoice.Seller with { SellerIdentifier = identifier } },
            totals);
}

/// <summary>
/// Reicht echte Arbeitsverzeichnisse durch und schreibt mit, welche das waren.
///
/// Damit lässt sich nach dem Lauf die einzig richtige Frage stellen: Ist
/// **dieses** Verzeichnis verschwunden? Eine Zählung aller Verzeichnisse im
/// temporären Verzeichnis des Systems beantwortet sie nicht – sie beantwortet
/// eine Frage über den ganzen Rechner, und die hat mit dem Vorgang nichts zu
/// tun.
///
/// Das Verhalten bleibt unverändert: Es wird das echte
/// <see cref="TemporaryWorkspace"/> verwendet, nur eben beobachtet. Eine
/// Attrappe prüfte am Ende die Attrappe.
/// </summary>
internal sealed class RecordingWorkspaceFactory : ITemporaryWorkspaceFactory
{
    private readonly TemporaryWorkspaceFactory _inner = new();
    private readonly List<string> _created = [];
    private readonly Lock _gate = new();

    /// <summary>Die Pfade aller ausgegebenen Arbeitsverzeichnisse.</summary>
    public IReadOnlyList<string> Created
    {
        get
        {
            lock (_gate)
            {
                return [.. _created];
            }
        }
    }

    public ITemporaryWorkspace Create()
    {
        ITemporaryWorkspace workspace = _inner.Create();

        lock (_gate)
        {
            _created.Add(workspace.Path);
        }

        return workspace;
    }
}

/// <summary>Zeitquelle mit festem Wert für den Regelvalidator.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Verhalten des Ersatzvalidators im Test.</summary>
internal enum StubBehavior
{
    /// <summary>Meldet ein bestandenes Ergebnis.</summary>
    Passes,

    /// <summary>Beanstandet die Datei.</summary>
    ReportsError,

    /// <summary>Meldet eine Zeitüberschreitung.</summary>
    TimesOut,

    /// <summary>Ist auf diesem System nicht eingerichtet.</summary>
    NotAvailable,
}

/// <summary>
/// Ersatzvalidator für die Ablauftests. Bildet die vier Ausgänge nach, die
/// ein echter externer Validator haben kann, ohne dass Java nötig ist.
/// </summary>
internal sealed class StubValidator(string name, StubBehavior behavior) : IExternalDocumentValidator
{
    public string Name => name;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(behavior != StubBehavior.NotAvailable);

    public Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>("1.0-test");

    public Task<ValidationReport> ValidateAsync(
        string filePath, CancellationToken cancellationToken = default)
    {
        var report = new ValidationReportBuilder();

        switch (behavior)
        {
            case StubBehavior.Passes:
                report.Information("APP-EXT-000", "Die externe Prüfung wurde bestanden.");
                break;

            case StubBehavior.ReportsError:
                report.Error(
                    "APP-EXT-010",
                    "Die erzeugte Datei entspricht nicht dem Standard PDF/A-3.",
                    technicalDetail: "Testfall");
                break;

            case StubBehavior.TimesOut:
                report.Error(
                    "APP-EXT-003",
                    "Die externe Prüfung wurde abgebrochen, weil sie zu lange gedauert hat. "
                    + "Die Datei gilt damit als ungeprüft.",
                    technicalDetail: "Testfall");
                break;

            case StubBehavior.NotAvailable:
            default:
                break;
        }

        return Task.FromResult(report.Build());
    }
}

/// <summary>
/// Liefert Fortschrittsmeldungen sofort und im meldenden Thread aus.
///
/// <see cref="Progress{T}"/> ist hier untauglich: Es stellt jede Meldung über
/// den Synchronisierungskontext zu – im Test also über den Threadpool. Die
/// Meldung trifft damit irgendwann ein, möglicherweise erst nach dem Ende des
/// Tests. Zwei Folgefehler wurden dadurch beobachtet:
///
/// - Eine Zusicherung prüft die Meldungsliste, bevor sie gefüllt ist. Der
///   Test schlägt dann sporadisch fehl, ohne dass sich am Programm etwas
///   geändert hätte.
/// - Ein Rückruf ruft <c>Cancel</c> auf einer <c>CancellationTokenSource</c>
///   auf, die das <c>using</c> des Tests bereits entsorgt hat. Das Ergebnis
///   ist eine unbeobachtete <c>ObjectDisposedException</c> auf einem
///   Threadpool-Thread, die xunit als "Catastrophic failure" meldet und die
///   den gesamten Testlauf rot färbt.
///
/// Im Test ist die sofortige Zustellung außerdem das, was gemeint ist:
/// "beim ersten Fortschrittsereignis abbrechen" soll genau dann geschehen und
/// nicht irgendwann später.
///
/// In der Oberfläche bleibt <see cref="Progress{T}"/> richtig – dort ist das
/// Zustellen über den Kontext genau der Zweck, weil die Meldung auf dem
/// Oberflächen-Thread ankommen muss.
/// </summary>
internal sealed class ImmediateProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

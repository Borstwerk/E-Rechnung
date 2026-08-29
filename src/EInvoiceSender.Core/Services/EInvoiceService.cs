using System.Diagnostics;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Reports;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using EInvoiceSender.Core.Validation;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Services;

/// <summary>
/// Der vollständige Ablauf von der vorhandenen PDF bis zur gespeicherten
/// E-Rechnung.
///
/// Die Reihenfolge ist verbindlich; sie steht in docs/ARCHITECTURE.md,
/// Abschnitt "Der Ablauf". Wichtige Eigenschaften:
///
/// * **Nichts wird erzeugt ohne die Bestätigung des Benutzers**, dass die
///   strukturierten Daten mit der sichtbaren PDF übereinstimmen. Die Sperre
///   sitzt hier, nicht in der Oberfläche.
/// * **Keine halb fertige Ausgabedatei.** Gespeichert wird erst, wenn alle
///   verpflichtenden Prüfungen bestanden sind.
/// * **Das Original bleibt unverändert.** Es wird ausschließlich gelesen.
/// * **Temporäre Dateien verschwinden immer**, auch bei Fehler oder Abbruch.
/// * **Ein Abbruch durch den Benutzer ist jederzeit möglich** und hinterlässt
///   keine Datei.
///
/// Der Anwendungsfall enthält selbst keine Fachlogik: Er ruft die Ports in der
/// richtigen Reihenfolge auf und entscheidet anhand der Befunde, ob es
/// weitergeht.
/// </summary>
public sealed partial class EInvoiceService : IEInvoiceService
{
    private readonly IPdfPreflightService _preflight;
    private readonly IBusinessRuleValidator _ruleValidator;
    private readonly IInvoiceXmlWriter _xmlWriter;
    private readonly IInvoiceXmlReader _xmlReader;
    private readonly IPdfAInvoiceComposer _composer;
    private readonly IPdfARasterFallbackComposer _rasterComposer;
    private readonly IPdfAnalyzer _analyzer;
    private readonly IFileStorage _fileStorage;
    private readonly ITemporaryWorkspaceFactory _workspaceFactory;
    private readonly IClock _clock;
    private readonly IReadOnlyList<IExternalDocumentValidator> _externalValidators;
    private readonly ILogger<EInvoiceService> _logger;

    private const int TotalSteps = 9;

    public EInvoiceService(
        IPdfPreflightService preflight,
        IBusinessRuleValidator ruleValidator,
        IInvoiceXmlWriter xmlWriter,
        IInvoiceXmlReader xmlReader,
        IPdfAInvoiceComposer composer,
        IPdfARasterFallbackComposer rasterComposer,
        IPdfAnalyzer analyzer,
        IFileStorage fileStorage,
        ITemporaryWorkspaceFactory workspaceFactory,
        IClock clock,
        IEnumerable<IExternalDocumentValidator> externalValidators,
        ILogger<EInvoiceService> logger)
    {
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        _ruleValidator = ruleValidator ?? throw new ArgumentNullException(nameof(ruleValidator));
        _xmlWriter = xmlWriter ?? throw new ArgumentNullException(nameof(xmlWriter));
        _xmlReader = xmlReader ?? throw new ArgumentNullException(nameof(xmlReader));
        _composer = composer ?? throw new ArgumentNullException(nameof(composer));
        _rasterComposer = rasterComposer ?? throw new ArgumentNullException(nameof(rasterComposer));
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        _workspaceFactory = workspaceFactory ?? throw new ArgumentNullException(nameof(workspaceFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _externalValidators = [.. externalValidators ?? throw new ArgumentNullException(nameof(externalValidators))];
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<PdfPreflightReport> AnalyzePdfAsync(
        string pdfPath, CancellationToken cancellationToken = default)
        => _preflight.InspectAsync(pdfPath, cancellationToken);

    /// <inheritdoc />
    public ValidationReport ValidateInvoice(Invoice invoice)
        => _ruleValidator.Validate(invoice, InvoiceCalculator.Calculate(invoice));

    /// <inheritdoc />
    public async Task<CreateEInvoiceResult> CreateAsync(
        CreateEInvoiceRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var context = new CreationContext(request, progress, _clock.Now, cancellationToken);

        // Die Bestätigung wird vor allem anderen geprüft - noch vor dem
        // Anlegen eines Arbeitsverzeichnisses. Sie ist keine Formalie der
        // Oberfläche, sondern Voraussetzung des Vorgangs.
        if (!UserConfirmedContentMatch(context))
        {
            return LogFailedAndReturn(context, stopwatch);
        }

        using ITemporaryWorkspace workspace = _workspaceFactory.Create();
        context.Workspace = workspace;

        try
        {
            if (!await SourcePdfIsSuitableAsync(context).ConfigureAwait(false))
            {
                return LogFailedAndReturn(context, stopwatch);
            }

            if (!InvoiceDataIsValid(context))
            {
                return LogFailedAndReturn(context, stopwatch);
            }

            CreateStructuredInvoice(context);

            if (!StructuredInvoiceMatchesInput(context))
            {
                return LogFailedAndReturn(context, stopwatch);
            }

            if (!await ComposePdfAAsync(context).ConfigureAwait(false))
            {
                return LogFailedAndReturn(context, stopwatch);
            }

            if (!await EmbeddedInvoiceIsReadableAsync(context).ConfigureAwait(false))
            {
                return LogFailedAndReturn(context, stopwatch);
            }

            if (!await ReferenceValidatorsAcceptAsync(context).ConfigureAwait(false))
            {
                return LogFailedAndReturn(context, stopwatch);
            }

            BuildChecksum(context);

            CreateEInvoiceResult result = await SaveResultAsync(context).ConfigureAwait(false);
            LogCompleted(
                _logger,
                result.OutputFile?.SizeInBytes ?? 0,
                stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            // Der Benutzer hat abgebrochen. Es bleibt keine Datei zurück, das
            // Arbeitsverzeichnis wird beim Verwerfen gelöscht.
            context.Report.Information(
                "APP-USE-090",
                "Der Vorgang wurde abgebrochen. Es wurde keine Datei erzeugt.");

            LogCanceled(_logger, stopwatch.ElapsedMilliseconds);

            return Failed(context, canceled: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Report.Error(
                "APP-USE-091",
                "Die Datei konnte nicht gespeichert werden. Prüfen Sie, ob das "
                + "Ausgabeverzeichnis beschreibbar ist und genügend Platz frei ist.",
                "OutputDirectory",
                $"{ex.GetType().Name}: {ex.Message}");

            LogTechnicalFailure(_logger, stopwatch.ElapsedMilliseconds, ex);
            return Failed(context);
        }
    }

    private CreateEInvoiceResult LogFailedAndReturn(
        CreationContext context,
        Stopwatch stopwatch)
    {
        LogFailed(_logger, stopwatch.ElapsedMilliseconds);
        return Failed(context);
    }

    // ===================================================== Die neun Schritte

    private static bool UserConfirmedContentMatch(CreationContext context)
    {
        if (context.Request.ContentMatchConfirmed)
        {
            return true;
        }

        context.Report.Error(
            "APP-USE-001",
            "Die Erzeugung wurde nicht gestartet, weil die Bestätigung fehlt, dass die "
            + "erfassten Rechnungsdaten mit der sichtbaren PDF-Rechnung übereinstimmen.",
            "ContentMatchConfirmed");

        return false;
    }

    private async Task<bool> SourcePdfIsSuitableAsync(CreationContext context)
    {
        context.Begin(PipelineStep.Preflight, 1, "PDF wird geprüft");

        PdfPreflightReport preflight = await _preflight
            .InspectAsync(context.Request.SourcePdfPath, context.CancellationToken)
            .ConfigureAwait(false);

        context.Report.AddRange(preflight.Findings);

        // Die Prüfung läuft hier ein zweites Mal, und das bleibt so. Zwischen
        // der Auswahl in Schritt 1 und dem Erzeugen liegt Zeit, in der die Datei
        // ausgetauscht worden sein kann. Was die Oberfläche vor einer Minute
        // gesehen hat, ist kein Nachweis über die Datei von jetzt.
        switch (preflight.Route)
        {
            case PdfProcessingRoute.Rejected:
                context.Fail(PipelineStep.Preflight, 1, "Die PDF-Datei ist nicht geeignet");

                return false;

            // Der Rasterweg ohne Zustimmung ist kein Sonderfall, den die
            // Oberfläche schon abfangen wird – hier ist die Sperre.
            case PdfProcessingRoute.RasterFallback when !context.Request.RasterFallbackConfirmed:
                context.Report.Error(
                    "APP-USE-003",
                    "Die gewählte PDF-Datei lässt sich nur über eine sichtbare Kopie "
                    + "verarbeiten. Bestätigen Sie ausdrücklich, dass die sichtbare Kopie "
                    + "verwendet werden soll, oder wählen Sie eine PDF-Datei mit "
                    + "eingebetteten Schriftarten.",
                    "SourcePdfPath",
                    "Zustimmung zum Rasterweg fehlt.");

                context.Fail(PipelineStep.Preflight, 1, "Zustimmung zur sichtbaren Kopie fehlt");

                return false;

            case PdfProcessingRoute.RasterFallback:
            case PdfProcessingRoute.Direct:
            default:
                context.Route = preflight.Route;
                break;
        }

        // Eine bereits eingebettete Rechnung wird nie stillschweigend ersetzt.
        if (preflight.HasExistingInvoice && !context.Request.ExistingInvoiceReplacementConfirmed)
        {
            context.Report.Error(
                "APP-USE-002",
                "Die gewählte PDF-Datei enthält bereits eine Rechnung. Bestätigen Sie "
                + "ausdrücklich, dass diese ersetzt werden soll, oder wählen Sie die "
                + "ursprüngliche PDF-Rechnung aus.",
                "SourcePdfPath",
                $"Vorhandenes Profil: {preflight.ExistingInvoiceProfile ?? "unbekannt"}");

            context.Fail(PipelineStep.Preflight, 1, "Bestätigung zum Ersetzen fehlt");

            return false;
        }

        context.Succeed(
            PipelineStep.Preflight, 1, "PDF geprüft",
            withWarnings: preflight.Verdict == PreflightVerdict.SuitableWithWarnings);

        return true;
    }

    private bool InvoiceDataIsValid(CreationContext context)
    {
        context.Begin(PipelineStep.ValidateInvoiceData, 2, "Rechnungsdaten werden geprüft");

        context.Totals = InvoiceCalculator.Calculate(context.Request.Invoice);

        ValidationReport ruleReport = _ruleValidator.Validate(context.Request.Invoice, context.Totals);
        context.Report.AddRange(ruleReport);

        if (ruleReport.HasErrors)
        {
            context.Fail(
                PipelineStep.ValidateInvoiceData, 2,
                $"{ruleReport.ErrorCount} Problem(e) in den Rechnungsdaten");

            return false;
        }

        context.Succeed(
            PipelineStep.ValidateInvoiceData, 2, "Rechnungsdaten geprüft",
            withWarnings: ruleReport.HasWarnings);

        return true;
    }

    private void CreateStructuredInvoice(CreationContext context)
    {
        context.Begin(PipelineStep.CreateXml, 3, "Rechnungsdaten werden umgewandelt");

        context.Xml = _xmlWriter.Write(context.Request.Invoice, context.Totals!);

        context.Succeed(PipelineStep.CreateXml, 3, "Rechnungsdaten umgewandelt");
    }

    private bool StructuredInvoiceMatchesInput(CreationContext context)
    {
        context.Begin(PipelineStep.VerifyXml, 4, "Erzeugte Daten werden gegengeprüft");

        if (!VerifyXmlEcho(context.Xml!, context.Request, context.Totals!, context.Report))
        {
            context.Fail(
                PipelineStep.VerifyXml, 4,
                "Die erzeugten Daten stimmen nicht mit der Eingabe überein");

            return false;
        }

        context.Succeed(PipelineStep.VerifyXml, 4, "Erzeugte Daten gegengeprüft");

        return true;
    }

    private async Task<bool> ComposePdfAAsync(CreationContext context)
    {
        context.Begin(PipelineStep.ComposePdfA, 5, "E-Rechnung wird erzeugt");

        Invoice invoice = context.Request.Invoice;

        var compositionRequest = new PdfACompositionRequest(
            SourcePdfPath: context.Request.SourcePdfPath,
            InvoiceXml: context.Xml!,
            Title: $"Rechnung {invoice.InvoiceNumber}",
            Author: invoice.Seller.Name,
            Subject: $"Rechnung {invoice.InvoiceNumber} an {invoice.Buyer.Name}",
            CreationDate: context.StartedAt,
            Attachment: _xmlWriter.Attachment);

        // Der Weg steht seit der Eingangsprüfung fest und wird hier nur noch
        // befolgt. Direkt heißt: Seiten übernehmen. Raster heißt: Seiten neu
        // aufbauen. Beide enden in denselben PDF/A-Bestandteilen und derselben
        // Ergebnisprüfung.
        IPdfAInvoiceComposer composer = context.Route == PdfProcessingRoute.RasterFallback
            ? _rasterComposer
            : _composer;

        CompositionResult composition = await composer
            .ComposeAsync(compositionRequest, context.CancellationToken).ConfigureAwait(false);

        context.Report.AddRange(composition.Report);

        if (!composition.Succeeded || composition.PdfBytes is null)
        {
            context.Fail(PipelineStep.ComposePdfA, 5, "Die E-Rechnung konnte nicht erzeugt werden");

            return false;
        }

        context.ResultPdf = composition.PdfBytes;
        context.Succeed(
            PipelineStep.ComposePdfA, 5,
            context.Route == PdfProcessingRoute.RasterFallback
                ? "E-Rechnung als sichtbare Kopie erzeugt"
                : "E-Rechnung erzeugt");

        return true;
    }

    private async Task<bool> EmbeddedInvoiceIsReadableAsync(CreationContext context)
    {
        context.Begin(PipelineStep.ReopenAndExtract, 6, "Ergebnis wird erneut geöffnet");

        context.WorkingCopy = await context.Workspace!
            .WriteAsync("ergebnis.pdf", context.ResultPdf!, context.CancellationToken)
            .ConfigureAwait(false);

        bool readable = await VerifyResultAsync(
                context.WorkingCopy, context.Xml!, context.Request, context.Totals!,
                context.Report, context.CancellationToken)
            .ConfigureAwait(false);

        if (!readable)
        {
            context.Fail(
                PipelineStep.ReopenAndExtract, 6,
                "Die erzeugte Datei ließ sich nicht korrekt auslesen");

            return false;
        }

        context.Succeed(PipelineStep.ReopenAndExtract, 6, "Ergebnis geprüft");

        return true;
    }

    private async Task<bool> ReferenceValidatorsAcceptAsync(CreationContext context)
    {
        context.Begin(PipelineStep.ExternalValidation, 7, "Zusätzliche Prüfung läuft");

        bool rejected = await RunExternalValidatorsAsync(
                context.WorkingCopy!, context.Report, context.Validators, context.CancellationToken)
            .ConfigureAwait(false);

        if (rejected)
        {
            context.Fail(
                PipelineStep.ExternalValidation, 7,
                "Die zusätzliche Prüfung hat die Datei beanstandet");

            return false;
        }

        // Ein nicht eingerichteter Validator wird als übersprungen ausgewiesen,
        // nie als bestanden. Sonst sähe eine ungeprüfte Datei aus wie eine
        // geprüfte.
        bool anyExecuted = context.Validators.Any(v => v.WasExecuted);

        context.ReportStep(
            PipelineStep.ExternalValidation,
            anyExecuted ? StepState.Succeeded : StepState.Skipped,
            7,
            anyExecuted ? "Zusätzliche Prüfung bestanden" : "Kein externer Validator eingerichtet");

        return true;
    }

    private static void BuildChecksum(CreationContext context)
    {
        context.Begin(PipelineStep.BuildReport, 8, "Bericht wird erstellt");

        context.Checksum = ComputeChecksum(context.ResultPdf!);

        context.Succeed(PipelineStep.BuildReport, 8, "Bericht erstellt");
    }

    private async Task<CreateEInvoiceResult> SaveResultAsync(CreationContext context)
    {
        context.Begin(PipelineStep.Save, 9, "Datei wird gespeichert");

        CreateEInvoiceRequest request = context.Request;

        string fileName = string.IsNullOrWhiteSpace(request.OutputFileName)
            ? SafeFileName.BuildOutputFileName(request.Invoice.InvoiceNumber, request.Invoice.Buyer.Name)
            : request.OutputFileName;

        StoredFile stored = await _fileStorage.WriteAsync(
            request.OutputDirectory, fileName, context.ResultPdf!,
            request.OverwriteBehavior, context.CancellationToken).ConfigureAwait(false);

        ValidationReport finalReport = context.Report.Build();

        StoredFile? jsonFile = null;
        StoredFile? textFile = null;

        if (request.WriteReportFiles)
        {
            (jsonFile, textFile) = await WriteReportsAsync(
                    request, stored, context.Checksum!, finalReport, context.StartedAt,
                    context.Validators, context.Route, context.CancellationToken)
                .ConfigureAwait(false);
        }

        context.Succeed(PipelineStep.Save, 9, "Datei gespeichert");

        return new CreateEInvoiceResult(
            Succeeded: true,
            OutputFile: stored,
            ReportJsonFile: jsonFile,
            ReportTextFile: textFile,
            Report: finalReport,
            CompletedSteps: context.Steps,
            CreatedAt: context.StartedAt,
            StandardDescription: _xmlWriter.FormatDescription,
            ProfileId: _xmlWriter.ProfileId,
            Validators: context.Validators);
    }

    /// <summary>
    /// Der Zustand eines einzelnen Erzeugungsvorgangs.
    ///
    /// Bündelt das, was früher als ein Dutzend lokaler Variablen durch die
    /// lange Methode gereicht wurde. Die Zwischenergebnisse sind bewusst
    /// veränderlich: Sie entstehen der Reihe nach, und jeder Schritt setzt
    /// voraus, dass der vorige erfolgreich war.
    /// </summary>
    private sealed class CreationContext(
        CreateEInvoiceRequest request,
        IProgress<PipelineProgress>? progress,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        public CreateEInvoiceRequest Request { get; } = request;

        public DateTimeOffset StartedAt { get; } = startedAt;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public ValidationReportBuilder Report { get; } = new();

        public List<PipelineProgress> Steps { get; } = [];

        public List<ValidatorInfo> Validators { get; } = [];

        /// <summary>
        /// Der Weg, den die Eingangsprüfung freigegeben hat. Er steht bewusst
        /// hier und nicht als lokale Variable in einer Methode: Erzeugung und
        /// Bericht müssen denselben Wert lesen, sonst könnte der Bericht einen
        /// anderen Weg ausweisen als den gegangenen.
        /// </summary>
        public PdfProcessingRoute Route { get; set; } = PdfProcessingRoute.Direct;

        public ITemporaryWorkspace? Workspace { get; set; }

        public InvoiceTotals? Totals { get; set; }

        public byte[]? Xml { get; set; }

        public byte[]? ResultPdf { get; set; }

        public string? WorkingCopy { get; set; }

        public string? Checksum { get; set; }

        public void Begin(PipelineStep step, int number, string description)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ReportStep(step, StepState.Running, number, description);
        }

        public void Succeed(PipelineStep step, int number, string description, bool withWarnings = false)
            => ReportStep(
                step,
                withWarnings ? StepState.SucceededWithWarnings : StepState.Succeeded,
                number,
                description);

        public void Fail(PipelineStep step, int number, string description)
            => ReportStep(step, StepState.Failed, number, description);

        /// <summary>
        /// Meldet einen Schritt. "Läuft gerade" geht nur an die Oberfläche,
        /// nicht in die Ergebnisliste – dort stehen die neun abgeschlossenen
        /// Schritte.
        /// </summary>
        public void ReportStep(PipelineStep step, StepState state, int number, string description)
        {
            var message = new PipelineProgress(step, state, number, TotalSteps, description);

            if (state != StepState.Running)
            {
                Steps.Add(message);
            }

            progress?.Report(message);
        }
    }

    /// <summary>
    /// Liest die erzeugte XML zurück und vergleicht sie mit dem, was erzeugt
    /// werden sollte. Fängt Fehler im Writer ab, bevor eine Datei entsteht.
    /// </summary>
    private bool VerifyXmlEcho(
        byte[] xml,
        CreateEInvoiceRequest request,
        InvoiceTotals totals,
        ValidationReportBuilder report)
    {
        InvoiceEcho? echo = _xmlReader.ReadEcho(xml);

        if (echo is null)
        {
            report.Error(
                "APP-USE-010",
                "Die erzeugten Rechnungsdaten konnten nicht zurückgelesen werden.",
                "Xml");

            return false;
        }

        bool ok = true;

        if (echo.ProfileId != _xmlWriter.ProfileId)
        {
            report.Error(
                "APP-USE-011",
                "Die erzeugten Rechnungsdaten tragen nicht die erwartete Profilkennung.",
                "Xml",
                $"Erwartet {_xmlWriter.ProfileId}, gelesen {echo.ProfileId}");
            ok = false;
        }

        if (echo.InvoiceNumber != request.Invoice.InvoiceNumber)
        {
            report.Error(
                "APP-USE-012",
                "Die Rechnungsnummer in den erzeugten Daten weicht von der Eingabe ab.",
                "Xml",
                $"Erwartet {request.Invoice.InvoiceNumber}, gelesen {echo.InvoiceNumber}");
            ok = false;
        }

        // BT-29: Die Verkäuferkennung ist eine der drei Angaben, mit denen der
        // Empfänger den Rechnungssteller maschinell identifiziert. Ginge sie
        // beim Schreiben verloren oder landete sie an der falschen Stelle,
        // beanstandete das erst der Empfänger – hier fällt es vorher auf.
        //
        // Leer und nicht vorhanden sind dabei dasselbe: Der Schreiber lässt
        // eine leere Kennung weg, also darf der Vergleich sie nicht vermissen.
        string? expectedSellerIdentifier =
            string.IsNullOrWhiteSpace(request.Invoice.Seller.SellerIdentifier)
                ? null
                : request.Invoice.Seller.SellerIdentifier;

        if (echo.SellerIdentifier != expectedSellerIdentifier)
        {
            report.Error(
                "APP-USE-014",
                "Die Verkäuferkennung in den erzeugten Daten weicht von der Eingabe ab.",
                "Xml",
                $"Erwartet {expectedSellerIdentifier}, gelesen {echo.SellerIdentifier}");
            ok = false;
        }

        foreach ((string label, decimal expected, decimal? actual) in new[]
                 {
                     ("Summe der Positionen", totals.LineTotal, echo.LineTotal),
                     ("Nettosumme", totals.TaxBasisTotal, echo.TaxBasisTotal),
                     ("Gesamtsteuer", totals.TaxTotal, echo.TaxTotal),
                     ("Bruttosumme", totals.GrandTotal, echo.GrandTotal),
                     ("offener Zahlbetrag", totals.DuePayableAmount, echo.DuePayableAmount),
                 })
        {
            if (actual != expected)
            {
                report.Error(
                    "APP-USE-013",
                    $"Der Betrag '{label}' in den erzeugten Daten weicht von der Berechnung ab.",
                    "Xml",
                    $"Erwartet {expected}, gelesen {actual}");
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>
    /// Öffnet die erzeugte Datei erneut, holt die XML heraus und vergleicht sie
    /// mit der erzeugten. Das ist der Nachweis, dass die Einbettung wirklich
    /// funktioniert hat und nicht nur scheinbar.
    /// </summary>
    private async Task<bool> VerifyResultAsync(
        string filePath,
        byte[] expectedXml,
        CreateEInvoiceRequest request,
        InvoiceTotals totals,
        ValidationReportBuilder report,
        CancellationToken cancellationToken)
    {
        PdfAnalysisResult reopened = await _analyzer
            .AnalyzeAsync(filePath, cancellationToken).ConfigureAwait(false);

        // Hier geht es tatsächlich um den Inhalt: Die soeben erzeugte Datei
        // muss die Rechnungsdaten nicht nur tragen, sondern lesbar tragen.
        if (!reopened.HasReadableExistingInvoiceXml || reopened.ExistingInvoiceXml is null)
        {
            report.Error(
                "APP-USE-020",
                "In der erzeugten Datei wurden die Rechnungsdaten nicht wiedergefunden.",
                "Output");

            return false;
        }

        if (!reopened.ExistingInvoiceXml.AsSpan().SequenceEqual(expectedXml))
        {
            report.Error(
                "APP-USE-021",
                "Die in der Datei enthaltenen Rechnungsdaten stimmen nicht mit den "
                + "erzeugten überein.",
                "Output",
                $"Erzeugt {expectedXml.Length} Bytes, gelesen {reopened.ExistingInvoiceXml.Length} Bytes");

            return false;
        }

        if (reopened.DeclaredPdfAPart != "3" || reopened.DeclaredPdfAConformance != "B")
        {
            report.Error(
                "APP-USE-022",
                "Die erzeugte Datei trägt nicht die erwartete PDF/A-3B-Kennzeichnung.",
                "Output",
                $"Gelesen: Teil {reopened.DeclaredPdfAPart}, Stufe {reopened.DeclaredPdfAConformance}");

            return false;
        }

        // Die extrahierte XML wird erneut fachlich gegengeprüft.
        return VerifyXmlEcho(reopened.ExistingInvoiceXml, request, totals, report);
    }

    /// <summary>
    /// Führt alle eingerichteten externen Validatoren aus.
    /// Liefert true, wenn mindestens einer die Datei beanstandet hat.
    /// </summary>
    private async Task<bool> RunExternalValidatorsAsync(
        string filePath,
        ValidationReportBuilder report,
        List<ValidatorInfo> validators,
        CancellationToken cancellationToken)
    {
        bool anyFailed = false;

        foreach (IExternalDocumentValidator validator in _externalValidators)
        {
            bool available = await validator.IsAvailableAsync(cancellationToken).ConfigureAwait(false);

            if (!available)
            {
                validators.Add(new ValidatorInfo(
                    validator.Name, null, WasExecuted: false,
                    "Auf diesem Rechner nicht eingerichtet."));

                // Ausdrücklich als Warnung im Bericht: Der Benutzer soll wissen,
                // dass die verbindliche Gegenprüfung nicht stattgefunden hat.
                report.Warning(
                    "APP-USE-030",
                    $"Die zusätzliche Prüfung mit '{validator.Name}' wurde übersprungen, "
                    + "weil das Werkzeug nicht eingerichtet ist. Die Datei wurde nur mit den "
                    + "eingebauten Prüfungen kontrolliert.",
                    "Validation");

                continue;
            }

            string? version = await validator.GetVersionAsync(cancellationToken).ConfigureAwait(false);
            ValidationReport result = await validator
                .ValidateAsync(filePath, cancellationToken).ConfigureAwait(false);

            report.AddRange(result);
            validators.Add(new ValidatorInfo(validator.Name, version, WasExecuted: true));

            if (result.HasErrors)
            {
                anyFailed = true;
            }
        }

        return anyFailed;
    }

    private async Task<(StoredFile? Json, StoredFile? Text)> WriteReportsAsync(
        CreateEInvoiceRequest request,
        StoredFile stored,
        string checksum,
        ValidationReport report,
        DateTimeOffset createdAt,
        IReadOnlyList<ValidatorInfo> validators,
        PdfProcessingRoute route,
        CancellationToken cancellationToken)
    {
        string stem = Path.GetFileNameWithoutExtension(stored.FullPath);

        byte[] json = ValidationReportWriter.ToJson(
            request, stored, checksum, report, createdAt,
            _xmlWriter.FormatDescription, _xmlWriter.ProfileId, validators, route);

        byte[] text = ValidationReportWriter.ToText(
            request, stored, checksum, report, createdAt,
            _xmlWriter.FormatDescription, _xmlWriter.ProfileId, validators, route);

        StoredFile jsonFile = await _fileStorage.WriteAsync(
            request.OutputDirectory, $"{stem}-Prüfbericht.json", json,
            OverwriteBehavior.Overwrite, cancellationToken).ConfigureAwait(false);

        StoredFile textFile = await _fileStorage.WriteAsync(
            request.OutputDirectory, $"{stem}-Prüfbericht.txt", text,
            OverwriteBehavior.Overwrite, cancellationToken).ConfigureAwait(false);

        return (jsonFile, textFile);
    }

    private static string ComputeChecksum(byte[] content)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();


    private CreateEInvoiceResult Failed(CreationContext context, bool canceled = false)
        => new(
            Succeeded: false,
            OutputFile: null,
            ReportJsonFile: null,
            ReportTextFile: null,
            Report: context.Report.Build(),
            CompletedSteps: context.Steps,
            CreatedAt: context.StartedAt,
            StandardDescription: _xmlWriter.FormatDescription,
            ProfileId: _xmlWriter.ProfileId,
            Validators: context.Validators,
            Canceled: canceled);

    [LoggerMessage(
        EventId = 6001, Level = LogLevel.Information,
        Message = "E-Rechnung technisch erzeugt: {ByteCount} Bytes, {Milliseconds} ms")]
    private static partial void LogCompleted(
        ILogger logger, long byteCount, long milliseconds);

    [LoggerMessage(
        EventId = 6002, Level = LogLevel.Information,
        Message = "Erzeugung technisch abgebrochen: {Milliseconds} ms")]
    private static partial void LogCanceled(ILogger logger, long milliseconds);

    [LoggerMessage(
        EventId = 6003, Level = LogLevel.Information,
        Message = "Erzeugung fachlich oder technisch nicht abgeschlossen: {Milliseconds} ms")]
    private static partial void LogFailed(ILogger logger, long milliseconds);

    [LoggerMessage(
        EventId = 6004, Level = LogLevel.Error,
        Message = "Technischer Dateifehler während der Erzeugung nach {Milliseconds} ms")]
    private static partial void LogTechnicalFailure(
        ILogger logger, long milliseconds, Exception exception);
}

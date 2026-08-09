using EInvoiceSender.Core.Reports;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using EInvoiceSender.Core.Validation;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Services;

/// <summary>
/// Der vollstaendige Ablauf von der vorhandenen PDF bis zur gespeicherten
/// E-Rechnung.
///
/// Die Reihenfolge ist verbindlich und in docs/SPECIFICATION.md, Abschnitt 8
/// festgelegt. Wichtige Eigenschaften:
///
/// * **Nichts wird erzeugt ohne die Bestaetigung des Benutzers**, dass die
///   strukturierten Daten mit der sichtbaren PDF uebereinstimmen. Die Sperre
///   sitzt hier, nicht in der Oberflaeche.
/// * **Keine halb fertige Ausgabedatei.** Gespeichert wird erst, wenn alle
///   verpflichtenden Pruefungen bestanden sind.
/// * **Das Original bleibt unveraendert.** Es wird ausschliesslich gelesen.
/// * **Temporaere Dateien verschwinden immer**, auch bei Fehler oder Abbruch.
/// * **Ein Abbruch durch den Benutzer ist jederzeit moeglich** und hinterlaesst
///   keine Datei.
///
/// Der Anwendungsfall enthaelt selbst keine Fachlogik: Er ruft die Ports in der
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

        var report = new ValidationReportBuilder();
        var steps = new List<PipelineProgress>();
        var validators = new List<ValidatorInfo>();
        DateTimeOffset startedAt = _clock.Now;

        // Die Bestaetigung wird vor allem anderen geprueft. Sie ist keine
        // Formalie der Oberflaeche, sondern Voraussetzung des Vorgangs.
        if (!request.ContentMatchConfirmed)
        {
            report.Error(
                "APP-USE-001",
                "Die Erzeugung wurde nicht gestartet, weil die Bestaetigung fehlt, dass die "
                + "erfassten Rechnungsdaten mit der sichtbaren PDF-Rechnung uebereinstimmen.",
                "ContentMatchConfirmed");

            return Failed(report, steps, startedAt, validators);
        }

        using ITemporaryWorkspace workspace = _workspaceFactory.Create();

        try
        {
            // --- 1. Eingangspruefung ---------------------------------------
            Report(progress, steps, PipelineStep.Preflight, StepState.Running, 1, "PDF wird geprueft");

            PdfPreflightReport preflight = await _preflight
                .InspectAsync(request.SourcePdfPath, cancellationToken).ConfigureAwait(false);

            report.AddRange(preflight.Findings);

            if (!preflight.CanProceed)
            {
                Report(progress, steps, PipelineStep.Preflight, StepState.Failed, 1,
                    "Die PDF-Datei ist nicht geeignet");

                return Failed(report, steps, startedAt, validators);
            }

            // Eine bereits eingebettete Rechnung wird nie stillschweigend ersetzt.
            if (preflight.HasExistingInvoice && !request.ExistingInvoiceReplacementConfirmed)
            {
                report.Error(
                    "APP-USE-002",
                    "Die gewaehlte PDF-Datei enthaelt bereits eine Rechnung. Bestaetigen Sie "
                    + "ausdruecklich, dass diese ersetzt werden soll, oder waehlen Sie die "
                    + "urspruengliche PDF-Rechnung aus.",
                    "SourcePdfPath",
                    $"Vorhandenes Profil: {preflight.ExistingInvoiceProfile}");

                Report(progress, steps, PipelineStep.Preflight, StepState.Failed, 1,
                    "Bestaetigung zum Ersetzen fehlt");

                return Failed(report, steps, startedAt, validators);
            }

            Report(progress, steps, PipelineStep.Preflight,
                preflight.Verdict == PreflightVerdict.SuitableWithWarnings
                    ? StepState.SucceededWithWarnings
                    : StepState.Succeeded,
                1, "PDF geprueft");

            cancellationToken.ThrowIfCancellationRequested();

            // --- 2. Rechnungsdaten pruefen ---------------------------------
            Report(progress, steps, PipelineStep.ValidateInvoiceData, StepState.Running, 2,
                "Rechnungsdaten werden geprueft");

            InvoiceTotals totals = InvoiceCalculator.Calculate(request.Invoice);
            ValidationReport ruleReport = _ruleValidator.Validate(request.Invoice, totals);
            report.AddRange(ruleReport);

            if (ruleReport.HasErrors)
            {
                Report(progress, steps, PipelineStep.ValidateInvoiceData, StepState.Failed, 2,
                    $"{ruleReport.ErrorCount} Problem(e) in den Rechnungsdaten");

                return Failed(report, steps, startedAt, validators);
            }

            Report(progress, steps, PipelineStep.ValidateInvoiceData,
                ruleReport.HasWarnings ? StepState.SucceededWithWarnings : StepState.Succeeded,
                2, "Rechnungsdaten geprueft");

            cancellationToken.ThrowIfCancellationRequested();

            // --- 3. XML erzeugen -------------------------------------------
            Report(progress, steps, PipelineStep.CreateXml, StepState.Running, 3,
                "Rechnungsdaten werden umgewandelt");

            byte[] xml = _xmlWriter.Write(request.Invoice, totals);

            Report(progress, steps, PipelineStep.CreateXml, StepState.Succeeded, 3,
                "Rechnungsdaten umgewandelt");

            // --- 4. XML gegenpruefen ---------------------------------------
            Report(progress, steps, PipelineStep.VerifyXml, StepState.Running, 4,
                "Erzeugte Daten werden gegengeprueft");

            if (!VerifyXmlEcho(xml, request, totals, report))
            {
                Report(progress, steps, PipelineStep.VerifyXml, StepState.Failed, 4,
                    "Die erzeugten Daten stimmen nicht mit der Eingabe ueberein");

                return Failed(report, steps, startedAt, validators);
            }

            Report(progress, steps, PipelineStep.VerifyXml, StepState.Succeeded, 4,
                "Erzeugte Daten gegengeprueft");

            cancellationToken.ThrowIfCancellationRequested();

            // --- 5. PDF/A-3 erzeugen ---------------------------------------
            Report(progress, steps, PipelineStep.ComposePdfA, StepState.Running, 5,
                "E-Rechnung wird erzeugt");

            var compositionRequest = new PdfACompositionRequest(
                SourcePdfPath: request.SourcePdfPath,
                InvoiceXml: xml,
                Title: $"Rechnung {request.Invoice.InvoiceNumber}",
                Author: request.Invoice.Seller.Name,
                Subject: $"Rechnung {request.Invoice.InvoiceNumber} an {request.Invoice.Buyer.Name}",
                CreationDate: startedAt,
                Attachment: _xmlWriter.Attachment);

            CompositionResult composition = await _composer
                .ComposeAsync(compositionRequest, cancellationToken).ConfigureAwait(false);

            report.AddRange(composition.Report);

            if (!composition.Succeeded || composition.PdfBytes is null)
            {
                Report(progress, steps, PipelineStep.ComposePdfA, StepState.Failed, 5,
                    "Die E-Rechnung konnte nicht erzeugt werden");

                return Failed(report, steps, startedAt, validators);
            }

            byte[] resultPdf = composition.PdfBytes;

            Report(progress, steps, PipelineStep.ComposePdfA, StepState.Succeeded, 5,
                "E-Rechnung erzeugt");

            cancellationToken.ThrowIfCancellationRequested();

            // --- 6. Erneut oeffnen und auslesen ----------------------------
            Report(progress, steps, PipelineStep.ReopenAndExtract, StepState.Running, 6,
                "Ergebnis wird erneut geoeffnet");

            string workingCopy = await workspace
                .WriteAsync("ergebnis.pdf", resultPdf, cancellationToken).ConfigureAwait(false);

            if (!await VerifyResultAsync(workingCopy, xml, request, totals, report, cancellationToken)
                    .ConfigureAwait(false))
            {
                Report(progress, steps, PipelineStep.ReopenAndExtract, StepState.Failed, 6,
                    "Die erzeugte Datei liess sich nicht korrekt auslesen");

                return Failed(report, steps, startedAt, validators);
            }

            Report(progress, steps, PipelineStep.ReopenAndExtract, StepState.Succeeded, 6,
                "Ergebnis geprueft");

            cancellationToken.ThrowIfCancellationRequested();

            // --- 7. Externe Validatoren ------------------------------------
            Report(progress, steps, PipelineStep.ExternalValidation, StepState.Running, 7,
                "Zusaetzliche Pruefung laeuft");

            bool externalFailed = await RunExternalValidatorsAsync(
                workingCopy, report, validators, cancellationToken).ConfigureAwait(false);

            if (externalFailed)
            {
                Report(progress, steps, PipelineStep.ExternalValidation, StepState.Failed, 7,
                    "Die zusaetzliche Pruefung hat die Datei beanstandet");

                return Failed(report, steps, startedAt, validators);
            }

            Report(progress, steps, PipelineStep.ExternalValidation,
                validators.Any(v => v.WasExecuted) ? StepState.Succeeded : StepState.Skipped,
                7,
                validators.Any(v => v.WasExecuted)
                    ? "Zusaetzliche Pruefung bestanden"
                    : "Kein externer Validator eingerichtet");

            cancellationToken.ThrowIfCancellationRequested();

            // --- 8. Bericht erstellen --------------------------------------
            Report(progress, steps, PipelineStep.BuildReport, StepState.Running, 8,
                "Bericht wird erstellt");

            string checksum = ComputeChecksum(resultPdf);

            Report(progress, steps, PipelineStep.BuildReport, StepState.Succeeded, 8,
                "Bericht erstellt");

            // --- 9. Speichern ----------------------------------------------
            Report(progress, steps, PipelineStep.Save, StepState.Running, 9, "Datei wird gespeichert");

            string fileName = string.IsNullOrWhiteSpace(request.OutputFileName)
                ? SafeFileName.BuildOutputFileName(
                    request.Invoice.InvoiceNumber, request.Invoice.Buyer.Name)
                : request.OutputFileName;

            StoredFile stored = await _fileStorage.WriteAsync(
                request.OutputDirectory, fileName, resultPdf,
                request.OverwriteBehavior, cancellationToken).ConfigureAwait(false);

            ValidationReport finalReport = report.Build();

            StoredFile? jsonFile = null;
            StoredFile? textFile = null;

            if (request.WriteReportFiles)
            {
                (jsonFile, textFile) = await WriteReportsAsync(
                    request, stored, checksum, finalReport, startedAt, validators, cancellationToken)
                    .ConfigureAwait(false);
            }

            Report(progress, steps, PipelineStep.Save, StepState.Succeeded, 9, "Datei gespeichert");

            LogCompleted(_logger, request.Invoice.InvoiceNumber, stored.SizeInBytes, checksum[..12]);

            return new CreateEInvoiceResult(
                Succeeded: true,
                OutputFile: stored,
                ReportJsonFile: jsonFile,
                ReportTextFile: textFile,
                Report: finalReport,
                CompletedSteps: steps,
                CreatedAt: startedAt,
                StandardDescription: _xmlWriter.FormatDescription,
                ProfileId: _xmlWriter.ProfileId,
                Validators: validators);
        }
        catch (OperationCanceledException)
        {
            // Der Benutzer hat abgebrochen. Es bleibt keine Datei zurueck, das
            // Arbeitsverzeichnis wird im finally geloescht.
            report.Information(
                "APP-USE-090",
                "Der Vorgang wurde abgebrochen. Es wurde keine Datei erzeugt.");

            LogCanceled(_logger, request.Invoice.InvoiceNumber);

            return Failed(report, steps, startedAt, validators, canceled: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            report.Error(
                "APP-USE-091",
                "Die Datei konnte nicht gespeichert werden. Pruefen Sie, ob das "
                + "Ausgabeverzeichnis beschreibbar ist und genuegend Platz frei ist.",
                "OutputDirectory",
                $"{ex.GetType().Name}: {ex.Message}");

            return Failed(report, steps, startedAt, validators);
        }
    }

    /// <summary>
    /// Liest die erzeugte XML zurueck und vergleicht sie mit dem, was erzeugt
    /// werden sollte. Faengt Fehler im Writer ab, bevor eine Datei entsteht.
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
                "Die erzeugten Rechnungsdaten konnten nicht zurueckgelesen werden.",
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
    /// Oeffnet die erzeugte Datei erneut, holt die XML heraus und vergleicht sie
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

        if (!reopened.HasExistingInvoiceXml || reopened.ExistingInvoiceXml is null)
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
                + "erzeugten ueberein.",
                "Output",
                $"Erzeugt {expectedXml.Length} Bytes, gelesen {reopened.ExistingInvoiceXml.Length} Bytes");

            return false;
        }

        if (reopened.DeclaredPdfAPart != "3" || reopened.DeclaredPdfAConformance != "B")
        {
            report.Error(
                "APP-USE-022",
                "Die erzeugte Datei traegt nicht die erwartete PDF/A-3B-Kennzeichnung.",
                "Output",
                $"Gelesen: Teil {reopened.DeclaredPdfAPart}, Stufe {reopened.DeclaredPdfAConformance}");

            return false;
        }

        // Die extrahierte XML wird erneut fachlich gegengeprueft.
        return VerifyXmlEcho(reopened.ExistingInvoiceXml, request, totals, report);
    }

    /// <summary>
    /// Fuehrt alle eingerichteten externen Validatoren aus.
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

                // Ausdruecklich als Warnung im Bericht: Der Benutzer soll wissen,
                // dass die verbindliche Gegenpruefung nicht stattgefunden hat.
                report.Warning(
                    "APP-USE-030",
                    $"Die zusaetzliche Pruefung mit '{validator.Name}' wurde uebersprungen, "
                    + "weil das Werkzeug nicht eingerichtet ist. Die Datei wurde nur mit den "
                    + "eingebauten Pruefungen kontrolliert.",
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
        CancellationToken cancellationToken)
    {
        string stem = Path.GetFileNameWithoutExtension(stored.FullPath);

        byte[] json = ValidationReportWriter.ToJson(
            request, stored, checksum, report, createdAt,
            _xmlWriter.FormatDescription, _xmlWriter.ProfileId, validators);

        byte[] text = ValidationReportWriter.ToText(
            request, stored, checksum, report, createdAt,
            _xmlWriter.FormatDescription, _xmlWriter.ProfileId, validators);

        StoredFile jsonFile = await _fileStorage.WriteAsync(
            request.OutputDirectory, $"{stem}-Pruefbericht.json", json,
            OverwriteBehavior.Overwrite, cancellationToken).ConfigureAwait(false);

        StoredFile textFile = await _fileStorage.WriteAsync(
            request.OutputDirectory, $"{stem}-Pruefbericht.txt", text,
            OverwriteBehavior.Overwrite, cancellationToken).ConfigureAwait(false);

        return (jsonFile, textFile);
    }

    private static string ComputeChecksum(byte[] content)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

    private static void Report(
        IProgress<PipelineProgress>? progress,
        List<PipelineProgress> steps,
        PipelineStep step,
        StepState state,
        int index,
        string description)
    {
        var message = new PipelineProgress(step, state, index, TotalSteps, description);

        if (state != StepState.Running)
        {
            steps.Add(message);
        }

        progress?.Report(message);
    }

    private CreateEInvoiceResult Failed(
        ValidationReportBuilder report,
        IReadOnlyList<PipelineProgress> steps,
        DateTimeOffset startedAt,
        IReadOnlyList<ValidatorInfo> validators,
        bool canceled = false)
        => new(
            Succeeded: false,
            OutputFile: null,
            ReportJsonFile: null,
            ReportTextFile: null,
            Report: report.Build(),
            CompletedSteps: steps,
            CreatedAt: startedAt,
            StandardDescription: _xmlWriter.FormatDescription,
            ProfileId: _xmlWriter.ProfileId,
            Validators: validators,
            Canceled: canceled);

    [LoggerMessage(
        EventId = 6001, Level = LogLevel.Information,
        Message = "E-Rechnung erzeugt: Nummer {InvoiceNumber}, {ByteCount} Bytes, Pruefsumme {ChecksumPrefix}")]
    private static partial void LogCompleted(
        ILogger logger, string invoiceNumber, long byteCount, string checksumPrefix);

    [LoggerMessage(
        EventId = 6002, Level = LogLevel.Information,
        Message = "Erzeugung abgebrochen: Nummer {InvoiceNumber}")]
    private static partial void LogCanceled(ILogger logger, string invoiceNumber);
}

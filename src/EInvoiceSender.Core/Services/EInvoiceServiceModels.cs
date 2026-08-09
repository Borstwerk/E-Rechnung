using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Services;

/// <summary>Die einzelnen Schritte des Erzeugungsablaufs, in ihrer Reihenfolge.</summary>
public enum PipelineStep
{
    /// <summary>Die Eingangs-PDF wird geprueft.</summary>
    Preflight,

    /// <summary>Die erfassten Rechnungsdaten werden geprueft.</summary>
    ValidateInvoiceData,

    /// <summary>Die Rechnungs-XML wird erzeugt.</summary>
    CreateXml,

    /// <summary>Die erzeugte XML wird gegengeprueft.</summary>
    VerifyXml,

    /// <summary>Die PDF/A-3-Datei mit eingebetteter XML wird erzeugt.</summary>
    ComposePdfA,

    /// <summary>Die fertige Datei wird erneut geoeffnet und ausgelesen.</summary>
    ReopenAndExtract,

    /// <summary>Externe Validatoren pruefen die fertige Datei.</summary>
    ExternalValidation,

    /// <summary>Der Validierungsbericht wird erstellt.</summary>
    BuildReport,

    /// <summary>Das Ergebnis wird gespeichert.</summary>
    Save,
}

/// <summary>Zustand eines Schrittes.</summary>
public enum StepState
{
    /// <summary>Der Schritt laeuft gerade.</summary>
    Running,

    /// <summary>Der Schritt wurde erfolgreich beendet.</summary>
    Succeeded,

    /// <summary>Der Schritt lief mit Warnungen durch.</summary>
    SucceededWithWarnings,

    /// <summary>Der Schritt ist fehlgeschlagen; der Ablauf endet.</summary>
    Failed,

    /// <summary>Der Schritt wurde uebersprungen.</summary>
    Skipped,
}

/// <summary>
/// Fortschrittsmeldung fuer die Oberflaeche.
/// </summary>
/// <param name="Step">Betroffener Schritt.</param>
/// <param name="State">Zustand.</param>
/// <param name="Index">Nummer des Schrittes, beginnend bei 1.</param>
/// <param name="TotalSteps">Gesamtzahl der Schritte.</param>
/// <param name="Description">Deutscher Text zur Anzeige.</param>
public sealed record PipelineProgress(
    PipelineStep Step,
    StepState State,
    int Index,
    int TotalSteps,
    string Description);

/// <summary>
/// Auftrag zur Erzeugung einer E-Rechnung.
/// </summary>
/// <param name="SourcePdfPath">Die vorhandene PDF-Rechnung. Wird nie veraendert.</param>
/// <param name="Invoice">Die erfassten und bestaetigten Rechnungsdaten.</param>
/// <param name="ContentMatchConfirmed">
/// Bestaetigung des Benutzers, dass die strukturierten Daten mit der sichtbaren
/// PDF uebereinstimmen. **Ohne diese Bestaetigung wird nichts erzeugt** – die
/// Sperre sitzt im Anwendungsfall, nicht nur in der Oberflaeche
/// (docs/SPECIFICATION.md, Abschnitt 7).
/// </param>
/// <param name="OutputDirectory">Zielverzeichnis fuer die Ergebnisdateien.</param>
/// <param name="OutputFileName">
/// Gewuenschter Dateiname. Leer bedeutet: aus Rechnungsnummer und Empfaenger
/// bilden.
/// </param>
/// <param name="OverwriteBehavior">Verhalten bei bereits vorhandener Zieldatei.</param>
/// <param name="ExistingInvoiceReplacementConfirmed">
/// Bestaetigung, dass eine bereits in der PDF enthaltene Rechnungs-XML ersetzt
/// werden darf. Ohne sie bricht der Ablauf ab, statt still zu ersetzen.
/// </param>
/// <param name="WriteReportFiles">
/// Sollen der maschinenlesbare und der menschenlesbare Bericht als Dateien
/// abgelegt werden?
/// </param>
public sealed record CreateEInvoiceRequest(
    string SourcePdfPath,
    Invoice Invoice,
    bool ContentMatchConfirmed,
    string OutputDirectory,
    string? OutputFileName = null,
    OverwriteBehavior OverwriteBehavior = OverwriteBehavior.CreateNumberedCopy,
    bool ExistingInvoiceReplacementConfirmed = false,
    bool WriteReportFiles = true);

/// <summary>
/// Angaben zu einem beteiligten Pruefwerkzeug, fuer den Bericht.
/// </summary>
/// <param name="Name">Name des Werkzeugs.</param>
/// <param name="Version">Ermittelte Version, sofern bekannt.</param>
/// <param name="WasExecuted">Wurde es tatsaechlich ausgefuehrt?</param>
/// <param name="Note">Erlaeuterung, falls es nicht ausgefuehrt wurde.</param>
public sealed record ValidatorInfo(string Name, string? Version, bool WasExecuted, string? Note = null);

/// <summary>
/// Das Ergebnis eines Erzeugungslaufs.
/// </summary>
/// <param name="Succeeded">Wurde eine gueltige Datei erzeugt und gespeichert?</param>
/// <param name="OutputFile">Die gespeicherte E-Rechnung, falls erfolgreich.</param>
/// <param name="ReportJsonFile">Der maschinenlesbare Bericht, falls geschrieben.</param>
/// <param name="ReportTextFile">Die menschenlesbare Zusammenfassung, falls geschrieben.</param>
/// <param name="Report">Alle Befunde des gesamten Laufs.</param>
/// <param name="CompletedSteps">Zustand je Schritt, fuer die Anzeige.</param>
/// <param name="CreatedAt">Erzeugungszeitpunkt.</param>
/// <param name="StandardDescription">Verwendeter Standard und Profil.</param>
/// <param name="ProfileId">Verwendete Profilkennung.</param>
/// <param name="Validators">Beteiligte Pruefwerkzeuge mit Version.</param>
/// <param name="Canceled">Wurde der Lauf vom Benutzer abgebrochen?</param>
public sealed record CreateEInvoiceResult(
    bool Succeeded,
    StoredFile? OutputFile,
    StoredFile? ReportJsonFile,
    StoredFile? ReportTextFile,
    ValidationReport Report,
    IReadOnlyList<PipelineProgress> CompletedSteps,
    DateTimeOffset CreatedAt,
    string StandardDescription,
    string ProfileId,
    IReadOnlyList<ValidatorInfo> Validators,
    bool Canceled = false);

/// <summary>
/// Der zentrale Dienst des Kerns.
///
/// Die Oberflaeche kennt nur diese eine Schnittstelle. Alles Weitere –
/// Eingangspruefung, Regelwerk, XML-Erzeugung, PDF/A-Aufwertung, Berichte –
/// liegt dahinter und wird von <c>EInvoiceService</c> zusammengefuehrt.
///
/// Die drei Methoden entsprechen den drei Fragen, die die Oberflaeche stellt:
/// Kann ich diese PDF verwenden? Sind die erfassten Daten in Ordnung? Und:
/// bitte erzeuge die E-Rechnung.
/// </summary>
public interface IEInvoiceService
{
    /// <summary>Prueft, ob eine PDF-Datei als Grundlage taugt.</summary>
    Task<PdfPreflightReport> AnalyzePdfAsync(
        string pdfPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prueft die erfassten Rechnungsdaten gegen das Regelwerk EN 16931.
    ///
    /// Laeuft ohne Datei- und Prozesszugriff und ist deshalb waehrend der
    /// Eingabe jederzeit aufrufbar.
    /// </summary>
    ValidationReport ValidateInvoice(Invoice invoice);

    /// <summary>Erzeugt und prueft die E-Rechnung.</summary>
    Task<CreateEInvoiceResult> CreateAsync(
        CreateEInvoiceRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

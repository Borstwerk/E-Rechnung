using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Services;

/// <summary>
/// Beschreibt eine bereits im PDF eingebettete Datei.
/// </summary>
/// <param name="FileName">Name des Anhangs, z. B. <c>factur-x.xml</c>.</param>
/// <param name="Relationship">Wert von <c>/AFRelationship</c>, falls gesetzt.</param>
/// <param name="MimeType">Angegebener MIME-Typ.</param>
/// <param name="SizeInBytes">Groesse des entpackten Anhangs.</param>
public sealed record EmbeddedFileInfo(
    string FileName,
    string? Relationship,
    string? MimeType,
    long SizeInBytes);

/// <summary>
/// Grund, warum ein PDF nicht zu PDF/A-3 aufgewertet werden kann.
/// Jeder Wert hat eine eigene, verstaendliche Erklaerung in der Oberflaeche.
/// </summary>
public enum PdfUpgradeBlocker
{
    /// <summary>Die Datei ist verschluesselt oder passwortgeschuetzt.</summary>
    Encrypted,

    /// <summary>Mindestens eine verwendete Schrift ist nicht eingebettet.</summary>
    FontsNotEmbedded,

    /// <summary>Die Datei enthaelt JavaScript oder eine startende Aktion.</summary>
    ActiveContent,

    /// <summary>Die Datei ist beschaedigt und nicht vollstaendig lesbar.</summary>
    Damaged,

    /// <summary>Die Datei enthaelt eine digitale Signatur, die durch die Aenderung ungueltig wuerde.</summary>
    DigitallySigned,
}

/// <summary>
/// Ergebnis der Voranalyse einer PDF-Datei.
/// </summary>
/// <param name="PageCount">Anzahl der Seiten.</param>
/// <param name="PdfVersion">Im Header angegebene PDF-Version, z. B. "1.7".</param>
/// <param name="IsEncrypted">Ist die Datei verschluesselt?</param>
/// <param name="DeclaredPdfAPart">Aus dem XMP gelesener PDF/A-Teil, falls vorhanden.</param>
/// <param name="DeclaredPdfAConformance">Aus dem XMP gelesene Konformitaetsstufe, falls vorhanden.</param>
/// <param name="EmbeddedFiles">Bereits vorhandene Anhaenge.</param>
/// <param name="ExistingInvoiceXml">
/// Inhalt einer bereits eingebetteten Rechnungs-XML, falls vorhanden. Wird
/// benoetigt, um den Benutzer vor einer erneuten Verarbeitung zu warnen.
/// </param>
/// <param name="ExistingInvoiceProfile">Profil-URN der bereits eingebetteten Rechnung.</param>
/// <param name="UpgradeBlockers">Gruende, die eine PDF/A-3-Aufwertung verhindern.</param>
public sealed record PdfAnalysisResult(
    int PageCount,
    string PdfVersion,
    bool IsEncrypted,
    string? DeclaredPdfAPart,
    string? DeclaredPdfAConformance,
    IReadOnlyList<EmbeddedFileInfo> EmbeddedFiles,
    byte[]? ExistingInvoiceXml,
    string? ExistingInvoiceProfile,
    IReadOnlyList<PdfUpgradeBlocker> UpgradeBlockers)
{
    /// <summary>Enthaelt die Datei bereits eine Rechnungs-XML?</summary>
    public bool HasExistingInvoiceXml => ExistingInvoiceXml is { Length: > 0 };

    /// <summary>Kann die Datei zu PDF/A-3 aufgewertet werden?</summary>
    public bool CanBeUpgraded => UpgradeBlockers.Count == 0;
}

/// <summary>
/// Liest und untersucht PDF-Dateien. Fuehrt keine Aenderungen durch.
/// </summary>
public interface IPdfAnalyzer
{
    /// <summary>
    /// Untersucht eine PDF-Datei. Wirft nicht bei beschaedigten Dateien, sondern
    /// meldet den Zustand ueber <see cref="PdfAnalysisResult.UpgradeBlockers"/>.
    /// </summary>
    Task<PdfAnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prueft anhand der Dateisignatur, ob es sich tatsaechlich um ein PDF handelt.
    /// Die Dateiendung allein reicht als Nachweis nicht aus.
    /// </summary>
    Task<bool> LooksLikePdfAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Auftrag zur Erzeugung der ZUGFeRD-Datei.
/// </summary>
/// <param name="SourcePdfPath">Pfad der unveraenderten Original-PDF.</param>
/// <param name="InvoiceXml">Die erzeugte, bereits gepruefte Rechnungs-XML.</param>
/// <param name="Title">Dokumenttitel fuer die PDF-Metadaten.</param>
/// <param name="Author">Autor fuer die PDF-Metadaten, ueblicherweise der Verkaeufer.</param>
/// <param name="Subject">Betreff fuer die PDF-Metadaten.</param>
/// <param name="CreationDate">Erzeugungszeitpunkt, wird in XMP und Dokumentinfo geschrieben.</param>
/// <param name="Attachment">
/// Wie die XML einzubetten ist. Kommt vom XML-Writer, damit die PDF-Schicht
/// nichts ueber Rechnungsformate wissen muss.
/// </param>
public sealed record PdfACompositionRequest(
    string SourcePdfPath,
    byte[] InvoiceXml,
    string Title,
    string Author,
    string Subject,
    DateTimeOffset CreationDate,
    InvoiceAttachmentDescriptor Attachment);

/// <summary>
/// Erzeugt aus einer Original-PDF und der Rechnungs-XML eine PDF/A-3-Datei mit
/// eingebetteter XML. Die Original-PDF wird dabei niemals veraendert.
/// </summary>
public interface IPdfAInvoiceComposer
{
    /// <summary>
    /// Erzeugt die Ausgabedatei im Arbeitsspeicher.
    /// Schlaegt fehl, wenn das Original nicht aufwertbar ist – in dem Fall wird
    /// kein Ergebnis geliefert, sondern ein Bericht mit der Ursache.
    /// </summary>
    Task<CompositionResult> ComposeAsync(
        PdfACompositionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Ergebnis der PDF/A-3-Erzeugung.</summary>
/// <param name="Succeeded">Konnte die Datei erzeugt werden?</param>
/// <param name="PdfBytes">Die erzeugte Datei; nur bei Erfolg gesetzt.</param>
/// <param name="Report">Befunde der Erzeugung, auch bei Erfolg (Warnungen).</param>
public sealed record CompositionResult(
    bool Succeeded,
    byte[]? PdfBytes,
    ValidationReport Report)
{
    /// <summary>Erzeugt ein Fehlerergebnis mit den angegebenen Befunden.</summary>
    public static CompositionResult Failed(ValidationReport report) => new(false, null, report);

    /// <summary>Erzeugt ein Erfolgsergebnis.</summary>
    public static CompositionResult Success(byte[] pdfBytes, ValidationReport report)
        => new(true, pdfBytes, report);
}

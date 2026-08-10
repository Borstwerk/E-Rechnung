using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Services;

/// <summary>
/// Beschreibt eine bereits im PDF eingebettete Datei.
/// </summary>
/// <param name="FileName">Name des Anhangs, z. B. <c>factur-x.xml</c>.</param>
/// <param name="Relationship">Wert von <c>/AFRelationship</c>, falls gesetzt.</param>
/// <param name="MimeType">Angegebener MIME-Typ.</param>
/// <param name="SizeInBytes">Größe des entpackten Anhangs.</param>
public sealed record EmbeddedFileInfo(
    string FileName,
    string? Relationship,
    string? MimeType,
    long SizeInBytes);

/// <summary>
/// Grund, warum ein PDF nicht zu PDF/A-3 aufgewertet werden kann.
/// Jeder Wert hat eine eigene, verständliche Erklärung in der Oberfläche.
/// </summary>
public enum PdfUpgradeBlocker
{
    /// <summary>Die Datei ist verschlüsselt oder passwortgeschützt.</summary>
    Encrypted,

    /// <summary>Mindestens eine verwendete Schrift ist nicht eingebettet.</summary>
    FontsNotEmbedded,

    /// <summary>Die Datei enthält JavaScript oder eine startende Aktion.</summary>
    ActiveContent,

    /// <summary>Die Datei ist beschädigt und nicht vollständig lesbar.</summary>
    Damaged,

    /// <summary>Die Datei enthält eine digitale Signatur, die durch die Änderung ungültig würde.</summary>
    DigitallySigned,

    /// <summary>
    /// Die Datei lässt sich zwar ohne Kennwort öffnen, trägt aber ein
    /// Verschlüsselungswörterbuch mit Rechteeinschränkungen – ein
    /// Besitzerkennwort.
    ///
    /// Das ist ausdrücklich etwas anderes als <see cref="Encrypted"/>: Zum Lesen
    /// wird kein Kennwort verlangt, weshalb PDFsharp das Dokument anstandslos
    /// öffnet und nichts meldet. Der Rechteinhaber hat aber festgelegt, was mit
    /// dem Dokument geschehen darf, und daran ändert die Anwendung nichts
    /// stillschweigend.
    /// </summary>
    RightsRestricted,
}

/// <summary>
/// Der Weg, auf dem aus der Ausgangsdatei eine E-Rechnung entsteht.
///
/// Der Wert beantwortet genau eine Frage: **Wie** kann diese Datei verarbeitet
/// werden? Er sagt nichts darüber, ob der Benutzer den gewählten Weg
/// gutgeheißen hat – das steht getrennt in der Zustimmung am
/// Erzeugungsauftrag.
/// </summary>
public enum PdfProcessingRoute
{
    /// <summary>
    /// Die Seiten des Originals werden unverändert übernommen und um die
    /// fehlenden PDF/A-3-Bestandteile ergänzt. Der bevorzugte Weg: Er erhält
    /// den Text der Rechnung als Text.
    /// </summary>
    Direct,

    /// <summary>
    /// Der direkte Weg ist versperrt, aber die Seiten lassen sich darstellen.
    /// Dann kann die Anwendung örtlich eine sichtbare Kopie aus den gerenderten
    /// Seiten aufbauen. Das kostet den durchsuchbaren Text und setzt deshalb
    /// die ausdrückliche Zustimmung des Benutzers voraus.
    /// </summary>
    RasterFallback,

    /// <summary>
    /// Es gibt keinen Weg. Der Bericht nennt den Grund und was der Benutzer
    /// tun kann.
    /// </summary>
    Rejected,
}

/// <summary>
/// Ergebnis der Voranalyse einer PDF-Datei.
/// </summary>
/// <param name="PageCount">Anzahl der Seiten.</param>
/// <param name="PdfVersion">Im Header angegebene PDF-Version, z. B. "1.7".</param>
/// <param name="IsEncrypted">Ist die Datei verschlüsselt?</param>
/// <param name="DeclaredPdfAPart">Aus dem XMP gelesener PDF/A-Teil, falls vorhanden.</param>
/// <param name="DeclaredPdfAConformance">Aus dem XMP gelesene Konformitätsstufe, falls vorhanden.</param>
/// <param name="EmbeddedFiles">Bereits vorhandene Anhänge.</param>
/// <param name="ExistingInvoiceXml">
/// Inhalt einer bereits eingebetteten Rechnungs-XML, falls vorhanden. Wird
/// benötigt, um den Benutzer vor einer erneuten Verarbeitung zu warnen.
/// </param>
/// <param name="ExistingInvoiceProfile">Profil-URN der bereits eingebetteten Rechnung.</param>
/// <param name="UpgradeBlockers">Gründe, die eine PDF/A-3-Aufwertung verhindern.</param>
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
    /// <summary>Enthält die Datei bereits eine Rechnungs-XML?</summary>
    public bool HasExistingInvoiceXml => ExistingInvoiceXml is { Length: > 0 };

    /// <summary>Kann die Datei zu PDF/A-3 aufgewertet werden?</summary>
    public bool CanBeUpgraded => UpgradeBlockers.Count == 0;
}

/// <summary>
/// Liest und untersucht PDF-Dateien. Führt keine Änderungen durch.
/// </summary>
public interface IPdfAnalyzer
{
    /// <summary>
    /// Untersucht eine PDF-Datei. Wirft nicht bei beschädigten Dateien, sondern
    /// meldet den Zustand über <see cref="PdfAnalysisResult.UpgradeBlockers"/>.
    /// </summary>
    Task<PdfAnalysisResult> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prüft anhand der Dateisignatur, ob es sich tatsächlich um ein PDF handelt.
    /// Die Dateiendung allein reicht als Nachweis nicht aus.
    /// </summary>
    Task<bool> LooksLikePdfAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Die festen Kenngrößen des Rasterwegs.
///
/// Sie stehen hier und nicht beim Erzeuger, weil auch der Prüfbericht sie nennt.
/// Zwei Stellen mit derselben Zahl wären zwei Stellen, die auseinanderlaufen
/// können – und ein Bericht, der eine andere Auflösung ausweist als die
/// verwendete, wäre schlimmer als gar keine Angabe.
/// </summary>
public static class RasterFallback
{
    /// <summary>
    /// Die Auflösung des Rasterwegs. 300 dpi ist der übliche Wert für
    /// Druckbilder mit kleiner Schrift; belegt ist er durch die Messreihe in
    /// docs/SPIKE-RASTER-FALLBACK.md. Bewusst fest und nicht einstellbar.
    /// </summary>
    public const int Dpi = 300;
}

/// <summary>
/// Ergebnis der Darstellbarkeitsprüfung.
/// </summary>
/// <param name="CanRender">Ließen sich alle Seiten darstellen?</param>
/// <param name="PageCount">Anzahl der geprüften Seiten.</param>
/// <param name="Reason">
/// Warum es nicht ging, in technischer Form für den Bericht. Nur gesetzt,
/// wenn <paramref name="CanRender"/> falsch ist.
/// </param>
public sealed record PdfRenderProbeResult(bool CanRender, int PageCount, string? Reason)
{
    /// <summary>Alle Seiten ließen sich darstellen.</summary>
    public static PdfRenderProbeResult Renderable(int pageCount) => new(true, pageCount, null);

    /// <summary>Die Darstellung schlug fehl.</summary>
    public static PdfRenderProbeResult NotRenderable(string reason) => new(false, 0, reason);
}

/// <summary>
/// Stellt fest, ob sich die Seiten einer PDF-Datei überhaupt darstellen lassen.
///
/// Das ist die Voraussetzung des Rasterwegs, und sie wird nachgewiesen und nicht
/// vermutet: Die Prüfung stellt jede Seite tatsächlich dar, nur in grober
/// Auflösung. Ein Dokument, dessen fünfte Seite den Renderer stolpern lässt,
/// darf nicht als „geht schon“ angeboten werden.
///
/// Die Datei wird ausschließlich gelesen.
/// </summary>
public interface IPdfRenderProbe
{
    /// <summary>Stellt alle Seiten probeweise dar.</summary>
    Task<PdfRenderProbeResult> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Auftrag zur Erzeugung der ZUGFeRD-Datei.
/// </summary>
/// <param name="SourcePdfPath">Pfad der unveränderten Original-PDF.</param>
/// <param name="InvoiceXml">Die erzeugte, bereits geprüfte Rechnungs-XML.</param>
/// <param name="Title">Dokumenttitel für die PDF-Metadaten.</param>
/// <param name="Author">Autor für die PDF-Metadaten, üblicherweise der Verkäufer.</param>
/// <param name="Subject">Betreff für die PDF-Metadaten.</param>
/// <param name="CreationDate">Erzeugungszeitpunkt, wird in XMP und Dokumentinfo geschrieben.</param>
/// <param name="Attachment">
/// Wie die XML einzubetten ist. Kommt vom XML-Writer, damit die PDF-Schicht
/// nichts über Rechnungsformate wissen muss.
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
/// eingebetteter XML. Die Original-PDF wird dabei niemals verändert.
/// </summary>
public interface IPdfAInvoiceComposer
{
    /// <summary>
    /// Erzeugt die Ausgabedatei im Arbeitsspeicher.
    /// Schlägt fehl, wenn das Original nicht aufwertbar ist – in dem Fall wird
    /// kein Ergebnis geliefert, sondern ein Bericht mit der Ursache.
    /// </summary>
    Task<CompositionResult> ComposeAsync(
        PdfACompositionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Erzeugt dasselbe Ergebnis wie <see cref="IPdfAInvoiceComposer"/>, nur auf dem
/// Rasterweg: Die sichtbaren Seiten werden neu aus den dargestellten
/// Seitenbildern aufgebaut.
///
/// Bewusst ein eigener Anschluss statt eines Schalters am vorhandenen: Der
/// direkte Weg bleibt damit unangetastet, und wer die Erzeugung liest, sieht an
/// der Typangabe, welcher Weg gemeint ist. Eine Auswahlhierarchie mit Fabrik und
/// Strategie wäre für zwei Wege zu viel Gerüst.
/// </summary>
public interface IPdfARasterFallbackComposer : IPdfAInvoiceComposer;

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

using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;

namespace EInvoiceSender.Core.Checking;

/// <summary>
/// Auftrag, eine fertige E-Rechnung zu prüfen.
/// </summary>
/// <param name="SourcePath">
/// Pfad der zu prüfenden Datei. Sie wird ausschließlich gelesen.
/// </param>
public sealed record CheckEInvoiceRequest(string SourcePath);

/// <summary>
/// Die technische Bestandsaufnahme der geprüften Datei: was tatsächlich darin
/// steht.
/// </summary>
/// <remarks>
/// Alle Angaben sind <b>Feststellungen</b>, keine Bewertungen. Insbesondere
/// sind <see cref="DeclaredPdfAPart"/> und
/// <see cref="DeclaredPdfAConformance"/> genau das, was ihr Name sagt: eine
/// Deklaration im XMP der Datei. Ob die Datei PDF/A tatsächlich einhält,
/// entscheidet ein Referenzvalidator wie veraPDF – und der ist hier nicht
/// gelaufen.
/// </remarks>
/// <param name="PdfVersion">Im Header angegebene PDF-Version.</param>
/// <param name="PageCount">Anzahl der Seiten.</param>
/// <param name="DeclaredPdfAPart">Deklarierter PDF/A-Teil, falls vorhanden.</param>
/// <param name="DeclaredPdfAConformance">Deklarierte Konformitätsstufe, falls vorhanden.</param>
/// <param name="EmbeddedFiles">Alle eingebetteten Dateien, auch nicht rechnungsartige.</param>
/// <param name="InvoiceAttachmentName">Name des ausgewerteten Rechnungsanhangs.</param>
/// <param name="InvoiceAttachmentKind">Erkanntes Format des Rechnungsanhangs.</param>
/// <param name="InvoiceAttachmentSizeInBytes">Größe des Rechnungsanhangs.</param>
/// <param name="ProfileId">Profilkennung aus der eingebetteten XML.</param>
/// <param name="IsEncrypted">Trägt die Datei ein Verschlüsselungswörterbuch?</param>
/// <param name="IsDigitallySigned">
/// Enthält die Datei Signaturfelder? Eine reine Feststellung: Bei einer
/// bereits fertigen Rechnung ist eine Signatur nichts Verdächtiges, sondern
/// eher ein Zeichen von Sorgfalt.
/// </param>
public sealed record CheckedDocumentInfo(
    string PdfVersion,
    int PageCount,
    string? DeclaredPdfAPart,
    string? DeclaredPdfAConformance,
    IReadOnlyList<EmbeddedFileInfo> EmbeddedFiles,
    string? InvoiceAttachmentName,
    CheckedAttachmentKind? InvoiceAttachmentKind,
    long? InvoiceAttachmentSizeInBytes,
    string? ProfileId,
    bool IsEncrypted,
    bool IsDigitallySigned);

/// <summary>
/// Ergebnis der Prüfung einer fertigen E-Rechnung.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es gibt hier bewusst kein <c>Succeeded</c>.</b> Ein solches Feld würde
/// unweigerlich als „die Rechnung ist gültig“ gelesen – von der Oberfläche
/// wie von jedem, der den Code später anfasst. Diese Aussage trifft dieser
/// Slice nicht und darf sie nicht treffen: Er nimmt auf, was in der Datei
/// steht, und führt weder die EN-16931-Regelprüfung noch veraPDF aus.
/// </para>
/// <para>
/// <see cref="Completed"/> beantwortet allein: Ist die Bestandsaufnahme bis
/// zum Ende gelaufen? Sie kann vollständig gelaufen sein und trotzdem lauter
/// Fehlerbefunde enthalten – etwa wenn die Datei gar keine Rechnungsdaten
/// trägt.
/// </para>
/// </remarks>
/// <param name="Completed">
/// Wurde die Bestandsaufnahme vollständig durchgeführt? <see langword="false"/>,
/// wenn sie vorzeitig abgebrochen ist, weil es nichts weiter zu untersuchen
/// gab – fehlende Datei, keine PDF, unlesbare PDF, mehrdeutige Anhänge.
/// <b>Keine Aussage über die Gültigkeit der Rechnung.</b>
/// </param>
/// <param name="Canceled">Hat der Anwender die Prüfung abgebrochen?</param>
/// <param name="SourceFileName">Dateiname der geprüften Datei.</param>
/// <param name="SourceSizeInBytes">Größe der geprüften Datei.</param>
/// <param name="SourceSha256">
/// SHA-256 der geprüften Datei in Kleinbuchstaben. Dokumentiert, worauf sich
/// der Bericht bezieht, und macht nachprüfbar, dass die Datei unverändert
/// geblieben ist.
/// </param>
/// <param name="DocumentInfo">Die technische Bestandsaufnahme; null, wenn nichts zu lesen war.</param>
/// <param name="InvoiceSummary">
/// Die Kerndaten der eingebetteten Rechnung; null, wenn keine auswertbare
/// gefunden wurde.
/// </param>
/// <param name="Report">Alle Befunde der Prüfung.</param>
public sealed record CheckEInvoiceResult(
    bool Completed,
    bool Canceled,
    string SourceFileName,
    long SourceSizeInBytes,
    string? SourceSha256,
    CheckedDocumentInfo? DocumentInfo,
    CiiInvoiceSummary? InvoiceSummary,
    ValidationReport Report);

/// <summary>
/// Prüft eine bereits fertige E-Rechnung.
///
/// **Ein eigener Anschluss neben <see cref="IEInvoiceService"/>, mit Absicht.**
/// Jener Dienst erzeugt Rechnungen: Er nimmt Eingaben entgegen, prüft sie gegen
/// die Produktgrenzen von BorstWerk und schreibt eine neue Datei. Dieser hier
/// tut das Gegenteil – er nimmt eine fremde, fertige Datei und schreibt nichts.
/// Beides in einen Dienst zu legen hieße, eine Schnittstelle zu bauen, deren
/// eine Hälfte für die andere nie zutrifft; spätestens beim Lesen des Codes
/// wäre unklar, welche Methoden eine Datei anfassen und welche nicht.
///
/// **Die Quelldatei wird ausschließlich gelesen.** Keine Reparatur, keine
/// Änderung, keine neue PDF, kein Austausch der eingebetteten XML.
/// </summary>
public interface IEInvoiceCheckService
{
    /// <summary>
    /// Nimmt auf, was in der Datei steht. Wirft nicht bei fehlerhaften
    /// Dateien – eine unbrauchbare Eingabe ist der Betriebsfall dieses
    /// Dienstes und gehört als Befund in den Bericht.
    /// </summary>
    Task<CheckEInvoiceResult> CheckAsync(
        CheckEInvoiceRequest request, CancellationToken cancellationToken = default);
}

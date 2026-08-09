namespace EInvoiceSender.Core.Services;

/// <summary>Ein Anhang eines E-Mail-Entwurfs.</summary>
/// <param name="FileName">Dateiname, wie er im Mailprogramm erscheinen soll.</param>
/// <param name="MimeType">MIME-Typ, z. B. <c>application/pdf</c>.</param>
/// <param name="Content">Inhalt des Anhangs.</param>
public sealed record EmailAttachment(string FileName, string MimeType, ReadOnlyMemory<byte> Content);

/// <summary>
/// Beschreibung eines vorzubereitenden E-Mail-Entwurfs.
/// </summary>
/// <param name="From">Absenderadresse, üblicherweise aus den Firmendaten.</param>
/// <param name="FromDisplayName">Anzeigename des Absenders.</param>
/// <param name="To">Empfängeradressen. Mindestens eine ist erforderlich.</param>
/// <param name="Subject">Betreff.</param>
/// <param name="Body">Nachrichtentext als reiner Text.</param>
/// <param name="Attachments">Anhänge, üblicherweise die fertige ZUGFeRD-Datei.</param>
public sealed record EmailDraft(
    string? From,
    string? FromDisplayName,
    IReadOnlyList<string> To,
    string Subject,
    string Body,
    IReadOnlyList<EmailAttachment> Attachments);

/// <summary>Ergebnis der Entwurfserzeugung.</summary>
/// <param name="Succeeded">Konnte der Entwurf erzeugt werden?</param>
/// <param name="DraftFilePath">Pfad der erzeugten Entwurfsdatei, falls vorhanden.</param>
/// <param name="FallbackUri">
/// Ersatzweise verwendbarer <c>mailto:</c>-Verweis ohne Anhang. Wird immer
/// mitgeliefert, damit der Benutzer auch bei einem störrischen Mailprogramm
/// weiterkommt.
/// </param>
/// <param name="Message">Erläuterung für den Anwender.</param>
public sealed record EmailDraftResult(
    bool Succeeded,
    string? DraftFilePath,
    Uri? FallbackUri,
    string Message);

/// <summary>
/// Bereitet E-Mail-Entwürfe vor. Diese Schnittstelle versendet nichts –
/// der Versand bleibt ausdrücklich beim Benutzer.
///
/// Weitere Anbieter (Outlook, Graph, SMTP) können später ergänzt werden,
/// ohne dass die Rechnungslogik angefasst werden muss.
/// </summary>
public interface IEmailDraftService
{
    /// <summary>Name des Verfahrens für Anzeige und Protokoll.</summary>
    string Name { get; }

    /// <summary>
    /// Erzeugt den Entwurf und legt ihn so ab, dass der Benutzer ihn öffnen kann.
    /// </summary>
    Task<EmailDraftResult> CreateDraftAsync(
        EmailDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baut einen <c>mailto:</c>-Verweis ohne Anhang. Nach RFC 6068 werden
    /// Anhänge dort nicht unterstützt; der Verweis dient nur als Rückfallweg.
    /// </summary>
    Uri BuildMailtoUri(EmailDraft draft);
}

namespace EInvoiceSender.Application.Abstractions;

/// <summary>Ein Anhang eines E-Mail-Entwurfs.</summary>
/// <param name="FileName">Dateiname, wie er im Mailprogramm erscheinen soll.</param>
/// <param name="MimeType">MIME-Typ, z. B. <c>application/pdf</c>.</param>
/// <param name="Content">Inhalt des Anhangs.</param>
public sealed record EmailAttachment(string FileName, string MimeType, ReadOnlyMemory<byte> Content);

/// <summary>
/// Beschreibung eines vorzubereitenden E-Mail-Entwurfs.
/// </summary>
/// <param name="From">Absenderadresse, ueblicherweise aus den Firmendaten.</param>
/// <param name="FromDisplayName">Anzeigename des Absenders.</param>
/// <param name="To">Empfaengeradressen. Mindestens eine ist erforderlich.</param>
/// <param name="Subject">Betreff.</param>
/// <param name="Body">Nachrichtentext als reiner Text.</param>
/// <param name="Attachments">Anhaenge, ueblicherweise die fertige ZUGFeRD-Datei.</param>
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
/// mitgeliefert, damit der Benutzer auch bei einem stoerrischen Mailprogramm
/// weiterkommt.
/// </param>
/// <param name="Message">Erlaeuterung fuer den Anwender.</param>
public sealed record EmailDraftResult(
    bool Succeeded,
    string? DraftFilePath,
    Uri? FallbackUri,
    string Message);

/// <summary>
/// Bereitet E-Mail-Entwuerfe vor. Diese Schnittstelle versendet nichts –
/// der Versand bleibt ausdruecklich beim Benutzer.
///
/// Weitere Anbieter (Outlook, Graph, SMTP) koennen spaeter ergaenzt werden,
/// ohne dass die Rechnungslogik angefasst werden muss.
/// </summary>
public interface IEmailDraftService
{
    /// <summary>Name des Verfahrens fuer Anzeige und Protokoll.</summary>
    string Name { get; }

    /// <summary>
    /// Erzeugt den Entwurf und legt ihn so ab, dass der Benutzer ihn oeffnen kann.
    /// </summary>
    Task<EmailDraftResult> CreateDraftAsync(
        EmailDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Baut einen <c>mailto:</c>-Verweis ohne Anhang. Nach RFC 6068 werden
    /// Anhaenge dort nicht unterstuetzt; der Verweis dient nur als Rueckfallweg.
    /// </summary>
    Uri BuildMailtoUri(EmailDraft draft);
}

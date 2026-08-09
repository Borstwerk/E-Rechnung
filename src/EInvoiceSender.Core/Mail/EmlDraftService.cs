using System.Globalization;
using System.Text;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Storage;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace EInvoiceSender.Core.Mail;

/// <summary>
/// Erzeugt einen E-Mail-Entwurf als RFC-5322-konforme <c>.eml</c>-Datei.
///
/// **Diese Klasse versendet nichts.** Sie legt eine Datei ab, die der Benutzer
/// in seinem Mailprogramm öffnet, kontrolliert und selbst absendet
/// (docs/KNOWN-LIMITATIONS.md, Abschnitt "E-Mail").
///
/// Bewusste Festlegungen und ihre Gründe:
/// * <c>X-Unsent: 1</c> – veranlasst Outlook und Thunderbird, die Datei als
///   noch nicht gesendeten Entwurf zu öffnen statt als empfangene Nachricht.
/// * **Keine <c>Message-ID</c>** – mit gesetzter Kennung verweigert Outlook
///   berichtetermaßen das wiederholte Öffnen derselben Datei.
/// * **Reiner Text statt HTML** – für das „neue Outlook" ist ein Verlust von
///   Anhängen bei HTML-Nachrichten berichtet worden.
///
/// **Nicht behauptet wird die vollständige Verträglichkeit mit dem neuen
/// Outlook.** Sie ist aus der Entwicklungsumgebung heraus nicht prüfbar und
/// muss auf einem echten Windows-11-System verifiziert werden. Deshalb liefert
/// jeder Aufruf zusätzlich einen Rückfallweg mit.
/// </summary>
public sealed partial class EmlDraftService : IEmailDraftService
{
    private readonly ILogger<EmlDraftService> _logger;
    private readonly string _draftDirectory;

    /// <summary>
    /// Erzeugt den Dienst.
    /// </summary>
    /// <param name="draftDirectory">
    /// Verzeichnis für die Entwurfsdateien. Vorgabe ist ein Unterverzeichnis
    /// im lokalen Anwendungsdatenordner des Benutzers.
    /// </param>
    /// <param name="logger">Protokollierung.</param>
    public EmlDraftService(ILogger<EmlDraftService> logger, string? draftDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _draftDirectory = draftDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EInvoiceSender",
            "Entwürfe");
    }

    /// <inheritdoc />
    public string Name => "E-Mail-Entwurf als Datei (.eml)";

    /// <inheritdoc />
    public async Task<EmailDraftResult> CreateDraftAsync(
        EmailDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Uri fallback = BuildMailtoUri(draft);

        if (draft.To.Count == 0)
        {
            return new EmailDraftResult(
                Succeeded: false,
                DraftFilePath: null,
                FallbackUri: fallback,
                Message: "Es ist keine Empfängeradresse hinterlegt. "
                         + "Tragen Sie eine Adresse ein oder verwenden Sie die Rechnung "
                         + "direkt aus dem Ausgabeordner.");
        }

        try
        {
            MimeMessage message = BuildMessage(draft);

            Directory.CreateDirectory(_draftDirectory);

            string fileName = BuildFileName(draft);
            string path = Path.Combine(_draftDirectory, fileName);

            // Atomar schreiben, damit das Mailprogramm nie eine halb
            // geschriebene Datei zu sehen bekommt.
            string temporaryPath = path + ".tmp";

            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 32768, useAsync: true))
            {
                await message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);

            LogDraftCreated(_logger, fileName, draft.Attachments.Count);

            return new EmailDraftResult(
                Succeeded: true,
                DraftFilePath: path,
                FallbackUri: fallback,
                Message: "Der E-Mail-Entwurf wurde erzeugt. Er wird jetzt in Ihrem "
                         + "Mailprogramm geöffnet. Bitte prüfen Sie ihn und senden Sie ihn "
                         + "selbst ab.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            LogDraftFailed(_logger, ex.GetType().Name);

            // Ein misslungener Entwurf darf den Anwender nicht blockieren:
            // Die Rechnung ist bereits erzeugt und gespeichert.
            return new EmailDraftResult(
                Succeeded: false,
                DraftFilePath: null,
                FallbackUri: fallback,
                Message: "Der E-Mail-Entwurf konnte nicht erzeugt werden. "
                         + "Die fertige Rechnung ist davon nicht betroffen – Sie finden sie "
                         + "im Ausgabeordner und können sie von Hand anhängen.");
        }
    }

    /// <inheritdoc />
    public Uri BuildMailtoUri(EmailDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // Nach RFC 6068 kennt mailto keinen Anhangsparameter. Der Verweis
        // enthält deshalb nur Empfänger, Betreff und Text; der Anhang muss
        // vom Benutzer hinzugefügt werden.
        var builder = new StringBuilder("mailto:");

        // Das At-Zeichen darf NICHT maskiert werden: Es trennt in einer
        // mailto-Adresse den lokalen Teil vom Rechnernamen. Maskiert man es,
        // lässt sich die URI nicht mehr auswerten – genau daran ist eine
        // frühere Fassung gescheitert.
        builder.AppendJoin(',', draft.To.Select(EscapeAddress));

        var parameters = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(draft.Subject))
        {
            parameters.Add("subject=" + Uri.EscapeDataString(draft.Subject));
        }

        if (!string.IsNullOrWhiteSpace(draft.Body))
        {
            parameters.Add("body=" + Uri.EscapeDataString(draft.Body));
        }

        if (parameters.Count > 0)
        {
            builder.Append('?').AppendJoin('&', parameters);
        }

        return new Uri(builder.ToString());
    }

    /// <summary>
    /// Maskiert eine E-Mail-Adresse für die Verwendung in einem
    /// <c>mailto:</c>-Verweis. Lokaler Teil und Rechnername werden getrennt
    /// maskiert, das trennende At-Zeichen bleibt unverändert.
    /// </summary>
    private static string EscapeAddress(string address)
    {
        string trimmed = address.Trim();
        int at = trimmed.LastIndexOf('@');

        if (at <= 0 || at == trimmed.Length - 1)
        {
            return Uri.EscapeDataString(trimmed);
        }

        return Uri.EscapeDataString(trimmed[..at]) + "@" + Uri.EscapeDataString(trimmed[(at + 1)..]);
    }

    private static MimeMessage BuildMessage(EmailDraft draft)
    {
        var message = new MimeMessage();

        if (!string.IsNullOrWhiteSpace(draft.From))
        {
            message.From.Add(new MailboxAddress(draft.FromDisplayName ?? string.Empty, draft.From));
        }

        foreach (string recipient in draft.To)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = draft.Subject;
        message.Date = DateTimeOffset.Now;

        // Der Kennsatz, der aus der Datei einen Entwurf macht.
        message.Headers.Add("X-Unsent", "1");

        // Bewusst KEINE Message-ID: siehe Klassenkommentar. MimeKit erzeugt
        // beim Schreiben von sich aus keine, solange der Kopf entfernt bleibt.
        message.Headers.Remove(HeaderId.MessageId);

        var body = new TextPart(TextFormat.Plain)
        {
            Text = draft.Body ?? string.Empty,
        };

        if (draft.Attachments.Count == 0)
        {
            message.Body = body;

            return message;
        }

        var multipart = new Multipart("mixed") { body };

        foreach (EmailAttachment attachment in draft.Attachments)
        {
            ContentType contentType = ContentType.Parse(attachment.MimeType);

            var part = new MimePart(contentType.MediaType, contentType.MediaSubtype)
            {
                Content = new MimeContent(new MemoryStream(attachment.Content.ToArray())),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = attachment.FileName,
            };

            multipart.Add(part);
        }

        message.Body = multipart;

        return message;
    }

    /// <summary>
    /// Baut den Dateinamen des Entwurfs. Enthält einen Zeitstempel, damit
    /// mehrere Entwürfe nebeneinander bestehen können.
    /// </summary>
    private static string BuildFileName(EmailDraft draft)
    {
        string stem = draft.Attachments.Count > 0
            ? Path.GetFileNameWithoutExtension(draft.Attachments[0].FileName)
            : "Rechnung";

        string safeStem = SafeFileName.Sanitize(stem, 60);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        return string.Create(CultureInfo.InvariantCulture, $"{safeStem}-{timestamp}.eml");
    }

    [LoggerMessage(
        EventId = 7001, Level = LogLevel.Information,
        Message = "E-Mail-Entwurf erzeugt: {FileName}, {AttachmentCount} Anhang/Anhänge")]
    private static partial void LogDraftCreated(ILogger logger, string fileName, int attachmentCount);

    [LoggerMessage(
        EventId = 7002, Level = LogLevel.Warning,
        Message = "E-Mail-Entwurf konnte nicht erzeugt werden ({Reason}).")]
    private static partial void LogDraftFailed(ILogger logger, string reason);
}

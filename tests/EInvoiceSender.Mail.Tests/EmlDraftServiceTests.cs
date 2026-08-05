using System.Text;
using EInvoiceSender.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace EInvoiceSender.Mail.Tests;

/// <summary>
/// Prueft die Erzeugung des E-Mail-Entwurfs.
///
/// Die Kopfzeilen sind hier keine Kosmetik: <c>X-Unsent</c> entscheidet
/// darueber, ob Outlook die Datei als Entwurf oder als empfangene Nachricht
/// oeffnet, und eine gesetzte <c>Message-ID</c> verhindert das wiederholte
/// Oeffnen. Beides wird deshalb ausdruecklich geprueft.
/// </summary>
public sealed class EmlDraftServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly EmlDraftService _service;

    public EmlDraftServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"eml-test-{Guid.NewGuid():N}");
        _service = new EmlDraftService(NullLogger<EmlDraftService>.Instance, _directory);
    }

    [Fact]
    public async Task EntwurfEnthaeltEmpfaengerBetreffTextUndAnhang()
    {
        EmailDraft draft = BuildDraft();

        EmailDraftResult result = await _service.CreateDraftAsync(
            draft, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.DraftFilePath);
        Assert.True(File.Exists(result.DraftFilePath));
        Assert.EndsWith(".eml", result.DraftFilePath, StringComparison.Ordinal);

        MimeMessage message = await MimeMessage.LoadAsync(
            result.DraftFilePath, TestContext.Current.CancellationToken);

        Assert.Equal("Rechnung RE-2026-0001", message.Subject);
        Assert.Contains(
            message.To.Mailboxes,
            m => m.Address == "einkauf@example.invalid");
        Assert.Contains(
            message.From.Mailboxes,
            m => m.Address == "rechnung@example.invalid");

        Assert.Contains("Sehr geehrte", message.TextBody, StringComparison.Ordinal);

        MimePart attachment = Assert.Single(message.Attachments.OfType<MimePart>());
        Assert.Equal("RE-2026-0001_Beispielkunde_ZUGFeRD.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType.MimeType);
    }

    [Fact]
    public async Task EntwurfTraegtDenKennsatzXUnsent()
    {
        EmailDraftResult result = await _service.CreateDraftAsync(
            BuildDraft(), TestContext.Current.CancellationToken);

        string raw = await File.ReadAllTextAsync(
            result.DraftFilePath!, TestContext.Current.CancellationToken);

        Assert.Contains("X-Unsent: 1", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntwurfEnthaeltKeineMessageId()
    {
        EmailDraftResult result = await _service.CreateDraftAsync(
            BuildDraft(), TestContext.Current.CancellationToken);

        string raw = await File.ReadAllTextAsync(
            result.DraftFilePath!, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("Message-Id:", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NachrichtentextIstReinerTextOhneHtml()
    {
        // Fuer das neue Outlook ist ein Verlust von Anhaengen bei HTML-Koerpern
        // berichtet worden. Der Entwurf muss deshalb reiner Text bleiben.
        EmailDraftResult result = await _service.CreateDraftAsync(
            BuildDraft(), TestContext.Current.CancellationToken);

        MimeMessage message = await MimeMessage.LoadAsync(
            result.DraftFilePath!, TestContext.Current.CancellationToken);

        Assert.Null(message.HtmlBody);
        Assert.NotNull(message.TextBody);
    }

    [Fact]
    public async Task UmlauteBleibenErhalten()
    {
        EmailDraft draft = BuildDraft() with
        {
            Subject = "Rechnung für Müller & Söhne – Grüße",
            Body = "Sehr geehrte Damen und Herren,\n\nanbei die Rechnung über 1.190,00 €.\n",
        };

        EmailDraftResult result = await _service.CreateDraftAsync(
            draft, TestContext.Current.CancellationToken);

        MimeMessage message = await MimeMessage.LoadAsync(
            result.DraftFilePath!, TestContext.Current.CancellationToken);

        Assert.Equal(draft.Subject, message.Subject);
        Assert.Contains("Müller & Söhne", message.Subject, StringComparison.Ordinal);
        Assert.Contains("1.190,00 €", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OhneEmpfaengerWirdKeinEntwurfErzeugtAberEinRueckfallwegGeliefert()
    {
        EmailDraft draft = BuildDraft() with { To = [] };

        EmailDraftResult result = await _service.CreateDraftAsync(
            draft, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.DraftFilePath);
        Assert.NotNull(result.FallbackUri);

        // Die Meldung muss dem Anwender einen Weg nennen.
        Assert.Contains("Ausgabeordner", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MailtoVerweisEnthaeltBetreffUndTextAberKeinenAnhang()
    {
        Uri uri = _service.BuildMailtoUri(BuildDraft());

        Assert.Equal("mailto", uri.Scheme);
        Assert.Contains("einkauf@example.invalid", uri.OriginalString, StringComparison.Ordinal);
        Assert.Contains("subject=", uri.OriginalString, StringComparison.Ordinal);
        Assert.Contains("body=", uri.OriginalString, StringComparison.Ordinal);

        // RFC 6068 kennt keinen Anhangsparameter.
        Assert.DoesNotContain("attach", uri.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MailtoVerweisMaskiertSonderzeichen()
    {
        EmailDraft draft = BuildDraft() with
        {
            Subject = "Rechnung & Mahnung? Ja!",
            Body = "Zeile 1\nZeile 2 mit ü",
        };

        Uri uri = _service.BuildMailtoUri(draft);

        // Ein unmaskiertes & wuerde den Verweis zerreissen.
        Assert.DoesNotContain("Rechnung & Mahnung", uri.OriginalString, StringComparison.Ordinal);
        Assert.Contains("%26", uri.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MehrereEntwuerfeUeberschreibenSichNicht()
    {
        EmailDraftResult first = await _service.CreateDraftAsync(
            BuildDraft(), TestContext.Current.CancellationToken);

        await Task.Delay(1100, TestContext.Current.CancellationToken);

        EmailDraftResult second = await _service.CreateDraftAsync(
            BuildDraft(), TestContext.Current.CancellationToken);

        Assert.NotEqual(first.DraftFilePath, second.DraftFilePath);
        Assert.True(File.Exists(first.DraftFilePath!));
        Assert.True(File.Exists(second.DraftFilePath!));
    }

    [Fact]
    public async Task EsBleibtKeineTemporaereDateiZurueck()
    {
        await _service.CreateDraftAsync(BuildDraft(), TestContext.Current.CancellationToken);

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    private static EmailDraft BuildDraft()
        => new(
            From: "rechnung@example.invalid",
            FromDisplayName: "Musterbetrieb Beispiel GmbH",
            To: ["einkauf@example.invalid"],
            Subject: "Rechnung RE-2026-0001",
            Body: "Sehr geehrte Damen und Herren,\n\nanbei erhalten Sie die Rechnung.\n",
            Attachments:
            [
                new EmailAttachment(
                    "RE-2026-0001_Beispielkunde_ZUGFeRD.pdf",
                    "application/pdf",
                    Encoding.ASCII.GetBytes("%PDF-1.7 Testinhalt")),
            ]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufraeumen darf einen Testlauf nicht scheitern lassen.
        }
    }
}

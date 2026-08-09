using System.IO;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.App.Services;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 5: Das Ergebnis zeigen und den E-Mail-Entwurf vorbereiten.
///
/// Versendet wird nichts. Die Anwendung legt einen Entwurf an und oeffnet ihn;
/// abschicken bleibt eine bewusste Handlung des Anwenders.
/// </summary>
public sealed partial class ResultViewModel(
    IEmailDraftService emailDraftService,
    IShellService shell) : StepViewModel
{
    private readonly IEmailDraftService _emailDraftService = emailDraftService;
    private readonly IShellService _shell = shell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyPropertyChangedFor(nameof(DirectoryPath))]
    [NotifyPropertyChangedFor(nameof(FileSizeText))]
    [NotifyPropertyChangedFor(nameof(Sha256Text))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateEmailDraftCommand))]
    private CreateEInvoiceResult? _result;

    [ObservableProperty]
    private string _emailRecipient = string.Empty;

    [ObservableProperty]
    private string _emailSubject = string.Empty;

    [ObservableProperty]
    private string _emailBody = string.Empty;

    [ObservableProperty]
    private string _emailDraftPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Liegt ein Ergebnis vor?</summary>
    public bool HasResult => Result?.OutputFile is not null;

    /// <summary>Dateiname der erzeugten E-Rechnung.</summary>
    public string FileName => Result?.OutputFile is { } f ? Path.GetFileName(f.FullPath) : string.Empty;

    /// <summary>Speicherort.</summary>
    public string DirectoryPath =>
        Result?.OutputFile is { } f ? Path.GetDirectoryName(f.FullPath) ?? string.Empty : string.Empty;

    /// <summary>Dateigroesse.</summary>
    public string FileSizeText => Result?.OutputFile is { } f
        ? string.Create(
            CultureInfo.CurrentCulture,
            $"{(f.SizeInBytes / 1024.0).ToString("N0", CultureInfo.CurrentCulture)} kB")
        : string.Empty;

    /// <summary>Pruefsumme der erzeugten Datei.</summary>
    public string Sha256Text => Result?.OutputFile?.Sha256 ?? string.Empty;

    /// <summary>Der verwendete Standard.</summary>
    public string StandardText => Result?.StandardDescription ?? string.Empty;

    /// <summary>Die verwendete Profilkennung.</summary>
    public string ProfileText => Result?.ProfileId ?? string.Empty;

    /// <summary>
    /// Die eingesetzten Pruefwerkzeuge, jeweils mit dem Hinweis, ob sie
    /// tatsaechlich gelaufen sind. Ein nicht ausgefuehrter Referenzvalidator
    /// darf nie wie ein bestandener aussehen.
    /// </summary>
    public IReadOnlyList<string> ValidatorLines => Result is null
        ? []
        : [.. Result.Validators.Select(v => v.WasExecuted
            ? $"{v.Name} {v.Version}: ausgefuehrt"
            : $"{v.Name}: NICHT AUSGEFUEHRT{(v.Note is null ? string.Empty : $" – {v.Note}")}")];

    /// <summary>Uebernimmt das Ergebnis und schlaegt die E-Mail-Felder vor.</summary>
    public void Show(CreateEInvoiceResult result, Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(invoice);

        Result = result;
        ShowFindings(result.Report);

        if (string.IsNullOrWhiteSpace(EmailRecipient))
        {
            EmailRecipient = invoice.Buyer.Email ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(EmailSubject))
        {
            EmailSubject = $"Rechnung {invoice.InvoiceNumber}";
        }

        if (string.IsNullOrWhiteSpace(EmailBody))
        {
            EmailBody =
                "Sehr geehrte Damen und Herren,\n\n"
                + $"anbei erhalten Sie die Rechnung {invoice.InvoiceNumber} "
                + "als elektronische Rechnung.\n\n"
                + "Mit freundlichen Gruessen\n"
                + invoice.Seller.Name;
        }

        OnPropertyChanged(nameof(StandardText));
        OnPropertyChanged(nameof(ProfileText));
        OnPropertyChanged(nameof(ValidatorLines));
    }

    /// <summary>Legt den E-Mail-Entwurf an und oeffnet ihn.</summary>
    [RelayCommand(CanExecute = nameof(HasResult))]
    public async Task CreateEmailDraftAsync(CancellationToken cancellationToken = default)
    {
        if (Result?.OutputFile is not { } outputFile)
        {
            return;
        }

        byte[] content = await File.ReadAllBytesAsync(outputFile.FullPath, cancellationToken)
            .ConfigureAwait(true);

        var draft = new EmailDraft(
            From: null,
            FromDisplayName: null,
            To: string.IsNullOrWhiteSpace(EmailRecipient) ? [] : [EmailRecipient],
            Subject: EmailSubject,
            Body: EmailBody,
            Attachments: [new EmailAttachment(Path.GetFileName(outputFile.FullPath), "application/pdf", content)]);

        EmailDraftResult draftResult = await _emailDraftService
            .CreateDraftAsync(draft, cancellationToken).ConfigureAwait(true);

        EmailDraftPath = draftResult.DraftFilePath ?? string.Empty;
        StatusMessage = draftResult.Message;

        if (draftResult.Succeeded && !string.IsNullOrEmpty(EmailDraftPath))
        {
            await _shell.OpenFileAsync(EmailDraftPath, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            // Immer ein Rueckfallweg: mailto oeffnet den Mailclient ohne Anhang,
            // die Datei liegt im Ausgabeordner bereit.
            await _shell.OpenUriAsync(_emailDraftService.BuildMailtoUri(draft), cancellationToken)
                .ConfigureAwait(true);
        }
    }

    /// <summary>Oeffnet den Ausgabeordner im Explorer.</summary>
    [RelayCommand(CanExecute = nameof(HasResult))]
    public async Task OpenOutputFolderAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(DirectoryPath))
        {
            await _shell.OpenFolderAsync(DirectoryPath, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Setzt den Schritt auf den Anfangszustand zurueck.</summary>
    public void Reset()
    {
        Result = null;
        EmailRecipient = string.Empty;
        EmailSubject = string.Empty;
        EmailBody = string.Empty;
        EmailDraftPath = string.Empty;
        StatusMessage = string.Empty;
        ClearFindings();
    }
}

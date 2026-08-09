using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 4: Die E-Rechnung erzeugen und prüfen lassen.
///
/// Der Kern meldet jeden der neun Arbeitsschritte einzeln zurück. Die
/// Oberfläche zeigt sie mit, damit ein längerer Lauf nachvollziehbar bleibt
/// und ein Abbruch jederzeit möglich ist.
/// </summary>
public sealed partial class GenerationViewModel(IEInvoiceService service) : StepViewModel, IDisposable
{
    private readonly IEInvoiceService _service = service;
    private CancellationTokenSource? _running;

    /// <summary>Fortschrittsmeldungen des Kerns.</summary>
    public ObservableCollection<StepProgressViewModel> Progress { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private CreateEInvoiceResult? _result;

    /// <summary>Führt die Erzeugung aus und liefert das Ergebnis.</summary>
    public async Task<CreateEInvoiceResult> RunAsync(CreateEInvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _running?.Dispose();
        _running = new CancellationTokenSource();

        Progress.Clear();
        ClearFindings();
        IsRunning = true;

        try
        {
            // Progress<T> ist hier richtig: Es stellt die Meldungen über den
            // erfassten Synchronisierungskontext zu, also auf dem
            // Oberflächen-Thread.
            var progress = new Progress<PipelineProgress>(OnProgress);

            Result = await _service.CreateAsync(request, progress, _running.Token)
                .ConfigureAwait(true);

            ShowFindings(Result.Report);

            return Result;
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Bricht eine laufende Erzeugung ab.</summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel() => _running?.Cancel();

    private bool CanCancel() => IsRunning;

    private void OnProgress(PipelineProgress message)
    {
        StepProgressViewModel? existing = Progress.FirstOrDefault(p => p.Step == message.Step);

        if (existing is null)
        {
            Progress.Add(new StepProgressViewModel(message));
        }
        else
        {
            existing.Update(message);
        }
    }

    /// <summary>Setzt den Schritt auf den Anfangszustand zurück.</summary>
    public void Reset()
    {
        Progress.Clear();
        Result = null;
        ClearFindings();
    }

    /// <summary>Gibt die Abbruchquelle frei.</summary>
    public void Dispose()
    {
        _running?.Dispose();
        _running = null;
    }
}

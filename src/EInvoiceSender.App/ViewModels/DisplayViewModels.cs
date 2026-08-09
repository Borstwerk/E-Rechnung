using CommunityToolkit.Mvvm.ComponentModel;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.App.ViewModels;

/// <summary>Ein Befund, aufbereitet fuer die Anzeige.</summary>
public sealed class FindingViewModel(ValidationFinding finding)
{
    /// <summary>Der zugrunde liegende Befund.</summary>
    public ValidationFinding Finding { get; } = finding;

    /// <summary>Verstaendliche Meldung.</summary>
    public string Message => Finding.Message;

    /// <summary>Technische Angaben fuer den aufklappbaren Bereich.</summary>
    public string TechnicalDetail => Finding.BuildTechnicalSummary();

    /// <summary>Betroffenes Feld.</summary>
    public string FieldPath => Finding.FieldPath;

    /// <summary>
    /// Schweregrad als Wort. Fehler werden **nicht nur** durch Farbe
    /// gekennzeichnet – das waere fuer farbfehlsichtige Anwender unbrauchbar.
    /// </summary>
    public string SeverityLabel => Finding.Severity switch
    {
        FindingSeverity.Error => "Fehler",
        FindingSeverity.Warning => "Warnung",
        _ => "Hinweis",
    };

    /// <summary>Zeichen zur zusaetzlichen, farbunabhaengigen Kennzeichnung.</summary>
    public string SeverityGlyph => Finding.Severity switch
    {
        FindingSeverity.Error => "✕",
        FindingSeverity.Warning => "!",
        _ => "i",
    };

    /// <summary>Schweregrad fuer Vorlagenauswahl.</summary>
    public FindingSeverity Severity => Finding.Severity;
}

/// <summary>Der Zustand eines Ablaufschrittes in der Fortschrittsanzeige.</summary>
public sealed partial class StepProgressViewModel : ObservableObject
{
    public StepProgressViewModel(PipelineProgress progress)
    {
        Step = progress.Step;
        _description = progress.Description;
        _state = progress.State;
    }

    /// <summary>Der Schritt.</summary>
    public PipelineStep Step { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    private StepState _state;

    [ObservableProperty]
    private string _description;

    /// <summary>Zustand als Wort, nicht nur als Farbe.</summary>
    public string StateLabel => State switch
    {
        StepState.Running => "laeuft",
        StepState.Succeeded => "erledigt",
        StepState.SucceededWithWarnings => "erledigt, mit Hinweisen",
        StepState.Failed => "fehlgeschlagen",
        StepState.Skipped => "uebersprungen",
        _ => string.Empty,
    };

    /// <summary>Uebernimmt eine neue Meldung zu diesem Schritt.</summary>
    public void Update(PipelineProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        State = progress.State;
        Description = progress.Description;
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Gemeinsame Grundlage der fünf Schrittansichten.
///
/// Enthält nur, was wirklich jeder Schritt braucht: die Liste der Befunde und
/// die immer gleiche Art, sie anzuzeigen. Fachlogik steht in
/// <c>EInvoiceSender.Core</c>.
///
/// **Regel für jedes await in einem ViewModel: <c>ConfigureAwait(true)</c>.**
/// Die Fortsetzung nach dem await muss auf den Oberflächen-Thread
/// zurückkehren, sonst meldet das ViewModel aus einem Threadpool-Thread an
/// gebundene Bedienelemente und WPF wirft "Der aufrufende Thread kann nicht auf
/// dieses Objekt zugreifen". Bewacht wird die Regel von
/// <c>UiThreadAffinityTests</c>.
/// </summary>
public abstract partial class StepViewModel : ObservableObject
{
    /// <summary>Befunde des Schrittes, Fehler zuerst.</summary>
    public ObservableCollection<FindingViewModel> Findings { get; } = [];

    /// <summary>Gibt es mindestens einen Befund?</summary>
    public bool HasFindings => Findings.Count > 0;

    /// <summary>
    /// Zeigt einen Prüfbericht an. Fehler stehen oben, danach Warnungen, dann
    /// Hinweise – der Anwender soll zuerst sehen, was ihn wirklich aufhält.
    /// </summary>
    protected void ShowFindings(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        Findings.Clear();

        foreach (ValidationFinding finding in report.Findings.OrderByDescending(f => f.Severity))
        {
            Findings.Add(new FindingViewModel(finding));
        }

        OnPropertyChanged(nameof(HasFindings));
    }

    /// <summary>Leert die Befundliste.</summary>
    protected void ClearFindings()
    {
        Findings.Clear();
        OnPropertyChanged(nameof(HasFindings));
    }
}

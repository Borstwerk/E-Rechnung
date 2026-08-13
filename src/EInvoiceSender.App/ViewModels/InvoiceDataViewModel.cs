using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using EInvoiceSender.Core.Text;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 2: Die strukturierten Rechnungsdaten erfassen.
///
/// Das ViewModel hält nur das Formular und die Auswahllisten. Gelesen,
/// gerechnet und geprüft wird im Kern: <see cref="InvoiceDraft"/> wandelt die
/// Eingaben in eine <see cref="Invoice"/>, <see cref="InvoiceCalculator"/>
/// rechnet, und <see cref="IEInvoiceService.ValidateInvoice"/> prüft gegen
/// EN 16931.
/// </summary>
public sealed partial class InvoiceDataViewModel : StepViewModel
{
    private readonly IEInvoiceService _service;
    private readonly ISettingsStore _settingsStore;
    private CompanyTemplateSavePlan? _pendingCompanyTemplateSave;

    public InvoiceDataViewModel(IEInvoiceService service, ISettingsStore settingsStore)
    {
        _service = service;
        _settingsStore = settingsStore;

        // Die Summen sollen von selbst stimmen. Eine Positionsänderung meldet
        // sich, sobald die Zelle bestätigt ist – nicht bei jedem Tastendruck.
        // Mehr Ereignisse braucht es dafür nicht.
        Draft.Lines.CollectionChanged += OnLinesChanged;
        Draft.PropertyChanged += OnDraftPropertyChanged;
    }

    /// <summary>
    /// Meldet eine ausdrücklich gespeicherte Vorlage an die Ablaufsteuerung.
    /// Der laufende Entwurf wird dabei nicht erneut mit der Vorlage befüllt.
    /// </summary>
    public event Action<CompanyTemplate>? CompanyTemplateSaved;

    /// <summary>Das Eingabeformular.</summary>
    public InvoiceDraft Draft { get; } = new();

    /// <summary>
    /// Hält die Summen aktuell, wenn Positionen hinzukommen oder wegfallen,
    /// und hört auf jede vorhandene Position.
    /// </summary>
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (InvoiceLineDraft line in e.OldItems?.OfType<InvoiceLineDraft>() ?? [])
        {
            line.PropertyChanged -= OnLineChanged;
        }

        // Beim Zurücksetzen meldet die Sammlung keine alten Einträge. Deshalb
        // wird für jede vorhandene Position neu angemeldet; ein doppeltes
        // Abmelden davor ist unschädlich und hier die einfachste Absicherung.
        foreach (InvoiceLineDraft line in Draft.Lines)
        {
            line.PropertyChanged -= OnLineChanged;
            line.PropertyChanged += OnLineChanged;
        }

        RecalculateTotals();
    }

    /// <summary>
    /// Eine bestätigte Änderung an einer Position. Die Nummer zählt nicht: Sie
    /// wird beim Neunummerieren gesetzt und ändert an den Summen nichts.
    /// </summary>
    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InvoiceLineDraft.Number))
        {
            return;
        }

        RecalculateTotals();
    }

    private void OnDraftPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InvoiceDraft.Origins))
        {
            return;
        }

        OnPropertyChanged(nameof(ShowsCompanyTemplateSaveOffer));
        SaveOwnCompanyDataCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private InvoiceTotals? _totals;

    /// <summary>
    /// Was die Vorbefüllung aus der PDF übernommen hat. Leer, solange nichts
    /// erkannt wurde.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrefillMessage))]
    [NotifyPropertyChangedFor(nameof(HasPrefillMessage))]
    private PrefillSummary? _prefill;

    /// <summary>Ein Satz darüber, was vorausgefüllt wurde.</summary>
    public string PrefillMessage
    {
        get
        {
            if (Prefill is not { FilledFields: > 0 } summary)
            {
                return string.Empty;
            }

            int count = summary.FilledFields;

            string text = $"{Plural.Count(count, "Feld", "Felder")} "
                          + $"{Plural.Word(count, "wurde", "wurden")} aus der PDF vorausgefüllt.";

            if (summary.UncertainFields.Count > 0)
            {
                text += $" Bitte prüfen Sie besonders: {string.Join(", ", summary.UncertainFields)}.";
            }

            return text + " Jeder Wert lässt sich überschreiben.";
        }
    }

    /// <summary>Gibt es einen Hinweis zur Vorbefüllung?</summary>
    public bool HasPrefillMessage => PrefillMessage.Length > 0;

    /// <summary>
    /// Wird angeboten, sobald mindestens ein erlaubtes Unternehmensfeld vom
    /// Anwender selbst bearbeitet wurde. PDF-Erkennung allein reicht nie aus.
    /// </summary>
    public bool ShowsCompanyTemplateSaveOffer => CompanyTemplateSavePlanner.HasManualInput(Draft);

    /// <summary>Status der ausdrücklichen Vorlagenspeicherung.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompanyTemplateSaveMessage))]
    private string _companyTemplateSaveMessage = string.Empty;

    /// <summary>Gibt es eine Status- oder Validierungsmeldung?</summary>
    public bool HasCompanyTemplateSaveMessage => CompanyTemplateSaveMessage.Length > 0;

    /// <summary>Wartet eine vorhandene Firmenvorlage auf Bestätigung?</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOwnCompanyDataCommand))]
    private bool _hasCompanyTemplateOverwriteQuestion;

    /// <summary>
    /// Übernimmt ein Erkennungsergebnis in das Formular.
    ///
    /// Die Vertrauensstufen entscheiden dabei, was übernommen wird – siehe
    /// <see cref="DraftPrefiller"/>. Nichts davon geht ungeprüft weiter: Der
    /// Weg führt immer über die Bestätigung in Schritt 3.
    /// </summary>
    public void ApplyDetection(InvoiceDetectionResult detection, CompanyTemplate? ownCompany)
    {
        ArgumentNullException.ThrowIfNull(detection);

        if (!detection.HasUsableText)
        {
            return;
        }

        Prefill = DraftPrefiller.Apply(Draft, detection, ownCompany);
        RecalculateTotals();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveLineCommand))]
    private InvoiceLineDraft? _selectedLine;

    /// <summary>Auswahlliste der Währungen.</summary>
    public static IReadOnlyList<CodeChoice> Currencies { get; } =
        [.. CurrencyCodeList.All.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Auswahlliste der Länder.</summary>
    public static IReadOnlyList<CodeChoice> Countries { get; } =
        [.. CountryCodeList.All.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Auswahlliste der gebräuchlichen Mengeneinheiten.</summary>
    public static IReadOnlyList<CodeChoice> Units { get; } =
        [.. UnitCodeList.CommonUnits.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Auswahlliste der Steuerkategorien.</summary>
    public static IReadOnlyList<VatCategoryChoice> VatCategories { get; } =
    [
        new(VatCategory.StandardRate, "S – Regelbesteuerung"),
        new(VatCategory.ZeroRated, "Z – Nullsatz"),
        new(VatCategory.Exempt, "E – steuerbefreit"),
        new(VatCategory.ReverseCharge, "AE – Reverse Charge"),
        new(VatCategory.IntraCommunitySupply, "K – innergemeinschaftliche Lieferung"),
        new(VatCategory.ExportOutsideEu, "G – Ausfuhrlieferung"),
        new(VatCategory.OutsideScope, "O – nicht steuerbar"),
    ];

    /// <summary>Fügt eine Position hinzu.</summary>
    [RelayCommand]
    public void AddLine()
    {
        SelectedLine = Draft.AddLine();
        RecalculateTotals();
    }

    /// <summary>Entfernt die gewählte Position.</summary>
    [RelayCommand(CanExecute = nameof(CanRemoveLine))]
    public void RemoveLine()
    {
        if (SelectedLine is null)
        {
            return;
        }

        Draft.Lines.Remove(SelectedLine);
        SelectedLine = Draft.Lines.Count > 0 ? Draft.Lines[^1] : null;
        Draft.RenumberLines();
        RecalculateTotals();
    }

    private bool CanRemoveLine() => SelectedLine is not null;

    /// <summary>
    /// Berechnet die Summen aus dem aktuellen Formularstand neu.
    ///
    /// Gerechnet wird **nur** über die Positionen. Vorher lief das über den Bau
    /// einer vollständigen Rechnung, und die Anzeige blieb leer, solange
    /// irgendeine andere Angabe fehlte – Rechnungsnummer, Käufer, Land. Der
    /// Anwender trug drei Positionen ein, drückte „Summen neu berechnen“ und
    /// sah nichts. Erst „Weiter“ brachte die Zahlen zum Vorschein, weil dort
    /// eine vollständige Rechnung entstand.
    ///
    /// Beanstandet wird hier nichts: Rechnen und Prüfen sind getrennt.
    /// </summary>
    [RelayCommand]
    public void RecalculateTotals() => Totals = Draft.TryCalculateTotals();

    /// <summary>
    /// Prüft die erfassten Daten und zeigt die Befunde an. Liefert die
    /// gebaute Rechnung, wenn keine Fehler vorliegen, sonst <c>null</c>.
    /// </summary>
    public Invoice? Validate(out string statusMessage)
    {
        ValidationReport buildReport = Draft.TryBuildInvoice(out Invoice? invoice);

        if (buildReport.HasErrors || invoice is null)
        {
            ShowFindings(buildReport);
            statusMessage = "Einige Eingaben konnten nicht gelesen werden.";

            return null;
        }

        Totals = InvoiceCalculator.Calculate(invoice);

        ValidationReport ruleReport = _service.ValidateInvoice(invoice);
        ShowFindings(buildReport.Concat(ruleReport));

        statusMessage = ruleReport.HasErrors
            ? $"{Plural.Count(ruleReport.ErrorCount, "Angabe", "Angaben")} "
              + $"{Plural.Word(ruleReport.ErrorCount, "muss", "müssen")} noch korrigiert werden."
            : "Die Angaben sind vollständig und in sich stimmig.";

        return ruleReport.HasErrors ? null : invoice;
    }

    /// <summary>
    /// Setzt den Schritt auf den Anfangszustand zurück.
    ///
    /// Aufgerufen beim Beginn einer neuen Rechnung. Danach steht hier nichts
    /// mehr aus dem vorigen Vorgang: kein Formularinhalt, keine Positionen,
    /// keine Summen, keine Befunde, keine ausgewählte Zeile und kein Hinweis
    /// auf eine Vorbefüllung.
    /// </summary>
    public void Reset()
    {
        Draft.Reset();
        Prefill = null;
        SelectedLine = null;
        Totals = null;
        ChangedTemplate = null;
        _pendingCompanyTemplateSave = null;
        HasCompanyTemplateOverwriteQuestion = false;
        CompanyTemplateSaveMessage = string.Empty;
        ClearFindings();
    }

    /// <summary>
    /// Plant die Speicherung aus einer unmittelbar zuvor frisch geladenen
    /// Vorlage. Ohne diesen ausdrücklichen Befehl findet kein Schreibzugriff statt.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveOwnCompanyData))]
    public async Task SaveOwnCompanyDataAsync(CancellationToken cancellationToken = default)
    {
        ClearPendingCompanyTemplateSave();

        try
        {
            CompanyTemplate existing = await _settingsStore.LoadTemplateAsync(cancellationToken)
                .ConfigureAwait(true);
            CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(Draft, existing);

            if (!plan.HasManualInput)
            {
                CompanyTemplateSaveMessage = plan.Errors[0];
                return;
            }

            if (!plan.IsChanged)
            {
                CompanyTemplateSaveMessage = "Diese Unternehmensdaten sind bereits gespeichert.";
                return;
            }

            if (plan.Errors.Count > 0)
            {
                CompanyTemplateSaveMessage = string.Join(" ", plan.Errors);
                return;
            }

            if (plan.RequiresConfirmation)
            {
                _pendingCompanyTemplateSave = plan;
                HasCompanyTemplateOverwriteQuestion = true;
                CompanyTemplateSaveMessage = string.Empty;
                return;
            }

            await PersistCompanyTemplateAsync(plan, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CompanyTemplateSaveMessage =
                "Die Unternehmensdaten konnten nicht lokal gespeichert werden.";
        }
    }

    private bool CanSaveOwnCompanyData()
        => ShowsCompanyTemplateSaveOffer && !HasCompanyTemplateOverwriteQuestion;

    /// <summary>
    /// Bestätigt die Änderung einer vorhandenen Vorlage. Vor dem Schreiben wird
    /// erneut frisch gelesen; eine zwischenzeitliche Änderung bricht sicher ab.
    /// </summary>
    [RelayCommand]
    public async Task ConfirmCompanyTemplateOverwriteAsync(
        CancellationToken cancellationToken = default)
    {
        if (_pendingCompanyTemplateSave is not { } pending)
        {
            return;
        }

        try
        {
            CompanyTemplate current = await _settingsStore.LoadTemplateAsync(cancellationToken)
                .ConfigureAwait(true);

            if (current != pending.Existing)
            {
                ClearPendingCompanyTemplateSave();
                CompanyTemplateSaveMessage = "Die gespeicherte Vorlage wurde zwischenzeitlich geändert. "
                                             + "Bitte prüfen und speichern Sie erneut.";
                return;
            }

            CompanyTemplateSavePlan freshPlan = CompanyTemplateSavePlanner.Plan(Draft, current);

            if (!freshPlan.CanSave)
            {
                ClearPendingCompanyTemplateSave();
                CompanyTemplateSaveMessage = freshPlan.IsChanged
                    ? string.Join(" ", freshPlan.Errors)
                    : "Diese Unternehmensdaten sind bereits gespeichert.";
                return;
            }

            await PersistCompanyTemplateAsync(freshPlan, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ClearPendingCompanyTemplateSave();
            CompanyTemplateSaveMessage =
                "Die Unternehmensdaten konnten nicht lokal gespeichert werden.";
        }
    }

    /// <summary>Verwirft die ausstehende Bestätigung ohne Schreibzugriff.</summary>
    [RelayCommand]
    public void CancelCompanyTemplateOverwrite()
    {
        ClearPendingCompanyTemplateSave();
        CompanyTemplateSaveMessage = "Die gespeicherte Firmenvorlage wurde nicht geändert.";
    }

    private async Task PersistCompanyTemplateAsync(
        CompanyTemplateSavePlan plan, CancellationToken cancellationToken)
    {
        await _settingsStore.SaveTemplateAsync(plan.Candidate, cancellationToken)
            .ConfigureAwait(true);

        ClearPendingCompanyTemplateSave();
        CompanyTemplateSaveMessage = plan.Warnings.Count == 0
            ? "Die Unternehmensdaten wurden lokal gespeichert."
            : "Die Unternehmensdaten wurden lokal gespeichert. Hinweis: "
              + string.Join(" ", plan.Warnings);
        CompanyTemplateSaved?.Invoke(plan.Candidate);
    }

    private void ClearPendingCompanyTemplateSave()
    {
        _pendingCompanyTemplateSave = null;
        HasCompanyTemplateOverwriteQuestion = false;
        SaveOwnCompanyDataCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Übernimmt eine gespeicherte Firmenvorlage in das Formular.
    ///
    /// Die Entscheidung, welches Feld dabei angefasst wird, trifft
    /// <see cref="CompanyTemplateApplier"/>: Was der Anwender selbst geändert
    /// hat, bleibt stehen.
    /// </summary>
    public void ApplyTemplate(CompanyTemplate template)
        => CompanyTemplateApplier.Apply(Draft, template);

    /// <summary>
    /// Eine in den Einstellungen geänderte Firmenvorlage, über die der Anwender
    /// noch entscheiden muss. Leer, solange nichts zu entscheiden ist.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTemplateQuestion))]
    private CompanyTemplate? _changedTemplate;

    /// <summary>Steht eine Rückfrage zur geänderten Firmenvorlage an?</summary>
    public bool HasTemplateQuestion => ChangedTemplate is not null;

    /// <summary>
    /// Legt eine geänderte Firmenvorlage zur Entscheidung vor.
    ///
    /// Mitten in einem Vorgang wird nichts ungefragt ausgetauscht – im
    /// Formular können längst eigene Angaben stehen. Der Anwender entscheidet,
    /// ob die neuen Verkäuferdaten schon für diese Rechnung gelten sollen.
    /// </summary>
    public void AskAboutChangedTemplate(CompanyTemplate template)
        => ChangedTemplate = template;

    /// <summary>Übernimmt die geänderte Vorlage für die laufende Rechnung.</summary>
    [RelayCommand]
    public void ApplyChangedTemplate()
    {
        if (ChangedTemplate is { } template)
        {
            ApplyTemplate(template);
            RecalculateTotals();
        }

        ChangedTemplate = null;
    }

    /// <summary>Lässt die geänderte Vorlage erst für die nächste Rechnung gelten.</summary>
    [RelayCommand]
    public void KeepCurrentTemplate() => ChangedTemplate = null;
}

/// <summary>Ein Eintrag einer Codeliste, wie ihn ein Auswahlfeld anzeigt.</summary>
public sealed record CodeChoice(string Code, string Name)
{
    /// <summary>Anzeigetext: Code und Klartext, damit beides erkennbar bleibt.</summary>
    public string Display => $"{Code} – {Name}";
}

/// <summary>Eine Steuerkategorie, wie sie ein Auswahlfeld anzeigt.</summary>
public sealed record VatCategoryChoice(VatCategory Category, string Display);

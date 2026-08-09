using System.Collections.Specialized;
using System.ComponentModel;
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

    public InvoiceDataViewModel(IEInvoiceService service)
    {
        _service = service;

        // Die Summen sollen von selbst stimmen. Eine Positionsänderung meldet
        // sich, sobald die Zelle bestätigt ist – nicht bei jedem Tastendruck.
        // Mehr Ereignisse braucht es dafür nicht.
        Draft.Lines.CollectionChanged += OnLinesChanged;
    }

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
        ClearFindings();
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

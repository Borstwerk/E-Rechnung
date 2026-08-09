using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 2: Die strukturierten Rechnungsdaten erfassen.
///
/// Das ViewModel haelt nur das Formular und die Auswahllisten. Gelesen,
/// gerechnet und geprueft wird im Kern: <see cref="InvoiceDraft"/> wandelt die
/// Eingaben in eine <see cref="Invoice"/>, <see cref="InvoiceCalculator"/>
/// rechnet, und <see cref="IEInvoiceService.ValidateInvoice"/> prueft gegen
/// EN 16931.
/// </summary>
public sealed partial class InvoiceDataViewModel(IEInvoiceService service) : StepViewModel
{
    private readonly IEInvoiceService _service = service;

    /// <summary>Das Eingabeformular.</summary>
    public InvoiceDraft Draft { get; } = new();

    [ObservableProperty]
    private InvoiceTotals? _totals;

    [ObservableProperty]
    private InvoiceLineDraft? _selectedLine;

    /// <summary>Auswahlliste der Waehrungen.</summary>
    public static IReadOnlyList<CodeChoice> Currencies { get; } =
        [.. CurrencyCodeList.All.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Auswahlliste der Laender.</summary>
    public static IReadOnlyList<CodeChoice> Countries { get; } =
        [.. CountryCodeList.All.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Auswahlliste der gebraeuchlichen Mengeneinheiten.</summary>
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

    /// <summary>Fuegt eine Position hinzu.</summary>
    [RelayCommand]
    public void AddLine()
    {
        SelectedLine = Draft.AddLine();
        RecalculateTotals();
    }

    /// <summary>Entfernt die gewaehlte Position.</summary>
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

    partial void OnSelectedLineChanged(InvoiceLineDraft? value)
        => RemoveLineCommand.NotifyCanExecuteChanged();

    /// <summary>Berechnet die Summen aus dem aktuellen Formularstand neu.</summary>
    [RelayCommand]
    public void RecalculateTotals()
    {
        ValidationReport report = Draft.TryBuildInvoice(out Invoice? invoice);

        Totals = report.HasErrors || invoice is null
            ? null
            : InvoiceCalculator.Calculate(invoice);
    }

    /// <summary>
    /// Prueft die erfassten Daten und zeigt die Befunde an. Liefert die
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
            ? $"{ruleReport.ErrorCount} Angabe(n) muessen noch korrigiert werden."
            : "Die Angaben sind vollstaendig und in sich stimmig.";

        return ruleReport.HasErrors ? null : invoice;
    }

    /// <summary>Uebernimmt eine gespeicherte Firmenvorlage in das Formular.</summary>
    public void ApplyTemplate(CompanyTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        Draft.SellerName = template.SellerName ?? Draft.SellerName;
        Draft.SellerStreet = template.SellerStreet ?? Draft.SellerStreet;
        Draft.SellerPostalCode = template.SellerPostalCode ?? Draft.SellerPostalCode;
        Draft.SellerCity = template.SellerCity ?? Draft.SellerCity;
        Draft.SellerCountry = template.SellerCountry ?? Draft.SellerCountry;
        Draft.SellerEmail = template.SellerEmail ?? Draft.SellerEmail;
        Draft.SellerVatId = template.SellerVatId ?? Draft.SellerVatId;
        Draft.SellerTaxNumber = template.SellerTaxNumber ?? Draft.SellerTaxNumber;
        Draft.BankAccountHolder = template.BankAccountHolder ?? Draft.BankAccountHolder;
        Draft.BankIban = template.BankIban ?? Draft.BankIban;
        Draft.BankBic = template.BankBic ?? Draft.BankBic;
        Draft.PaymentTerms = template.DefaultPaymentTerms ?? Draft.PaymentTerms;
        Draft.Currency = template.DefaultCurrency ?? Draft.Currency;

        if (template.DefaultPaymentTermDays > 0 && Draft.IssueDate is { } issue)
        {
            Draft.DueDate = issue.AddDays(template.DefaultPaymentTermDays);
        }
    }
}

/// <summary>Ein Eintrag einer Codeliste, wie ihn ein Auswahlfeld anzeigt.</summary>
public sealed record CodeChoice(string Code, string Name)
{
    /// <summary>Anzeigetext: Code und Klartext, damit beides erkennbar bleibt.</summary>
    public string Display => $"{Code} – {Name}";
}

/// <summary>Eine Steuerkategorie, wie sie ein Auswahlfeld anzeigt.</summary>
public sealed record VatCategoryChoice(VatCategory Category, string Display);

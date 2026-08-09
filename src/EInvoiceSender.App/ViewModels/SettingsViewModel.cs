using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Die dauerhaft gespeicherten Vorgaben: Firmenangaben, Bankverbindung und
/// Standardtexte.
///
/// Zwei Zusagen gelten hier ausdruecklich:
///
/// - **Kein stilles Speichern.** Gespeichert wird nur auf Knopfdruck. Bank- und
///   Steuerdaten sollen nicht unbemerkt auf der Platte landen.
/// - **Die IBAN wird unter Windows per DPAPI geschuetzt.** Steht DPAPI nicht
///   zur Verfuegung, wird sie **gar nicht** gespeichert, statt im Klartext.
///   Der Hinweistext sagt das dem Anwender.
/// </summary>
public sealed partial class SettingsViewModel(ISettingsStore store) : ObservableObject
{
    private readonly ISettingsStore _store = store;

    [ObservableProperty] private string _sellerName = string.Empty;
    [ObservableProperty] private string _sellerStreet = string.Empty;
    [ObservableProperty] private string _sellerPostalCode = string.Empty;
    [ObservableProperty] private string _sellerCity = string.Empty;
    [ObservableProperty] private string _sellerCountry = "DE";
    [ObservableProperty] private string _sellerEmail = string.Empty;
    [ObservableProperty] private string _sellerVatId = string.Empty;
    [ObservableProperty] private string _sellerTaxNumber = string.Empty;
    [ObservableProperty] private string _bankAccountHolder = string.Empty;
    [ObservableProperty] private string _bankIban = string.Empty;
    [ObservableProperty] private string _bankBic = string.Empty;
    [ObservableProperty] private string _defaultCurrency = "EUR";
    [ObservableProperty] private int _defaultPaymentTermDays = 14;
    [ObservableProperty] private string _defaultPaymentTerms = string.Empty;
    [ObservableProperty] private string _defaultEmailSubject = string.Empty;
    [ObservableProperty] private string _defaultEmailBody = string.Empty;
    [ObservableProperty] private string _lastOutputDirectory = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Auswahlliste der Waehrungen.</summary>
    public static IReadOnlyList<CodeChoice> Currencies { get; } =
        [.. CurrencyCodeList.All.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Auswahlliste der Laender.</summary>
    public static IReadOnlyList<CodeChoice> Countries { get; } =
        [.. CountryCodeList.All.Select(e => new CodeChoice(e.Code, e.Name))];

    /// <summary>Kann die IBAN auf diesem System geschuetzt abgelegt werden?</summary>
    public bool SupportsProtectedStorage => _store.SupportsProtectedStorage;

    /// <summary>Hinweis zum Umgang mit der IBAN auf diesem System.</summary>
    public string BankStorageHint => SupportsProtectedStorage
        ? "Die IBAN wird mit dem Windows-Datenschutz (DPAPI) verschluesselt abgelegt und "
          + "ist nur unter Ihrem Benutzerkonto lesbar."
        : "Auf diesem System steht der Windows-Datenschutz (DPAPI) nicht zur Verfuegung. "
          + "Die IBAN wird deshalb bewusst NICHT gespeichert – sie muss je Rechnung erfasst werden.";

    /// <summary>Laedt die gespeicherten Vorgaben.</summary>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            CompanyTemplate template = await _store.LoadTemplateAsync(cancellationToken)
                .ConfigureAwait(true);

            Apply(template);
            StatusMessage = string.Empty;
        }
        catch (IOException exception)
        {
            StatusMessage = "Die gespeicherten Vorgaben konnten nicht gelesen werden. "
                            + "Die Felder bleiben leer; Speichern legt sie neu an. "
                            + $"Technische Angabe: {exception.Message}";
        }
    }

    /// <summary>Speichert die Vorgaben.</summary>
    [RelayCommand]
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.SaveTemplateAsync(ToTemplate(), cancellationToken).ConfigureAwait(true);

            StatusMessage = SupportsProtectedStorage
                ? "Die Vorgaben wurden gespeichert."
                : "Die Vorgaben wurden gespeichert – ohne die IBAN, siehe Hinweis oben.";
        }
        catch (IOException exception)
        {
            StatusMessage = "Die Vorgaben konnten nicht gespeichert werden. "
                            + $"Technische Angabe: {exception.Message}";
        }
    }

    /// <summary>Setzt alle Felder auf den Auslieferungszustand zurueck.</summary>
    [RelayCommand]
    public void Reset()
    {
        Apply(new CompanyTemplate());
        StatusMessage = "Die Felder wurden zurueckgesetzt. Erst 'Speichern' uebernimmt das dauerhaft.";
    }

    private void Apply(CompanyTemplate template)
    {
        SellerName = template.SellerName ?? string.Empty;
        SellerStreet = template.SellerStreet ?? string.Empty;
        SellerPostalCode = template.SellerPostalCode ?? string.Empty;
        SellerCity = template.SellerCity ?? string.Empty;
        SellerCountry = template.SellerCountry ?? "DE";
        SellerEmail = template.SellerEmail ?? string.Empty;
        SellerVatId = template.SellerVatId ?? string.Empty;
        SellerTaxNumber = template.SellerTaxNumber ?? string.Empty;
        BankAccountHolder = template.BankAccountHolder ?? string.Empty;
        BankIban = template.BankIban ?? string.Empty;
        BankBic = template.BankBic ?? string.Empty;
        DefaultCurrency = template.DefaultCurrency ?? "EUR";
        DefaultPaymentTermDays = template.DefaultPaymentTermDays;
        DefaultPaymentTerms = template.DefaultPaymentTerms ?? string.Empty;
        DefaultEmailSubject = template.DefaultEmailSubject ?? string.Empty;
        DefaultEmailBody = template.DefaultEmailBody ?? string.Empty;
        LastOutputDirectory = template.LastOutputDirectory ?? string.Empty;
    }

    private CompanyTemplate ToTemplate() => new()
    {
        SellerName = Blank(SellerName),
        SellerStreet = Blank(SellerStreet),
        SellerPostalCode = Blank(SellerPostalCode),
        SellerCity = Blank(SellerCity),
        SellerCountry = Blank(SellerCountry),
        SellerEmail = Blank(SellerEmail),
        SellerVatId = Blank(SellerVatId),
        SellerTaxNumber = Blank(SellerTaxNumber),
        BankAccountHolder = Blank(BankAccountHolder),
        BankIban = Blank(BankIban),
        BankBic = Blank(BankBic),
        DefaultCurrency = Blank(DefaultCurrency),
        DefaultPaymentTermDays = DefaultPaymentTermDays,
        DefaultPaymentTerms = Blank(DefaultPaymentTerms),
        DefaultEmailSubject = Blank(DefaultEmailSubject),
        DefaultEmailBody = Blank(DefaultEmailBody),
        LastOutputDirectory = Blank(LastOutputDirectory),
    };

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

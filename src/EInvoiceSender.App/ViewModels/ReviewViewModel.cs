using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 3: Die sichtbare PDF neben den erfassten Daten zeigen und die
/// Uebereinstimmung ausdruecklich bestaetigen lassen.
///
/// Dieser Schritt traegt den fachlichen Kern der ganzen Anwendung: Die
/// strukturierten Daten sind vom Menschen eingegeben, nicht aus der PDF
/// gelesen. Ob beides zusammenpasst, kann nur der Mensch entscheiden – deshalb
/// die Pflichtbestaetigung. Ohne sie sperrt der Kern die Erzeugung, nicht bloss
/// die Oberflaeche.
/// </summary>
public sealed partial class ReviewViewModel : StepViewModel
{
    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private Invoice? _invoice;

    [ObservableProperty]
    private InvoiceTotals? _totals;

    /// <summary>
    /// Bestaetigung, dass die erfassten Daten mit der sichtbaren PDF
    /// uebereinstimmen.
    /// </summary>
    [ObservableProperty]
    private bool _contentMatchConfirmed;

    /// <summary>
    /// Bestaetigung, eine bereits eingebettete Rechnung zu ersetzen. Wird nur
    /// abgefragt, wenn die gewaehlte PDF schon strukturierte Daten enthaelt.
    /// </summary>
    [ObservableProperty]
    private bool _existingInvoiceReplacementConfirmed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsReplacementQuestion))]
    private bool _sourceAlreadyContainsInvoice;

    /// <summary>Muss die Ersetzungsfrage ueberhaupt gestellt werden?</summary>
    public bool ShowsReplacementQuestion => SourceAlreadyContainsInvoice;

    /// <summary>
    /// Wohin die erzeugte Datei geschrieben wird.
    ///
    /// Steht bewusst in diesem Schritt und nicht nur in den Einstellungen: Der
    /// Anwender soll den Ordner unmittelbar vor dem Erzeugen sehen und aendern
    /// koennen. Die Vorgabe ist ein Unterordner der eigenen Dokumente – damit
    /// geht nie ein leerer Pfad in den Kern.
    /// </summary>
    [ObservableProperty]
    private string _outputDirectory = DefaultOutputDirectory;

    /// <summary>Die Vorgabe, solange nichts gespeichert ist.</summary>
    public static string DefaultOutputDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EInvoiceSender");

    /// <summary>Verkaeufer, einzeilig.</summary>
    public string SellerText => Invoice is null
        ? string.Empty
        : $"{Invoice.Seller.Name}, {Invoice.Seller.Address.PostalCode} {Invoice.Seller.Address.City}";

    /// <summary>Kaeufer, einzeilig.</summary>
    public string BuyerText => Invoice is null
        ? string.Empty
        : $"{Invoice.Buyer.Name}, {Invoice.Buyer.Address.PostalCode} {Invoice.Buyer.Address.City}";

    /// <summary>Rechnungsnummer.</summary>
    public string InvoiceNumberText => Invoice?.InvoiceNumber ?? string.Empty;

    /// <summary>Rechnungsdatum.</summary>
    public string IssueDateText => Invoice is null
        ? string.Empty
        : Invoice.IssueDate.ToString("d", CultureInfo.CurrentCulture);

    /// <summary>Nettobetrag.</summary>
    public string NetText => Money(Totals?.TaxBasisTotal);

    /// <summary>Steuerbetrag.</summary>
    public string TaxText => Money(Totals?.TaxTotal);

    /// <summary>Bruttobetrag.</summary>
    public string GrossText => Money(Totals?.GrandTotal);

    /// <summary>Zahlbetrag.</summary>
    public string PayableText => Money(Totals?.DuePayableAmount);

    /// <summary>Uebernimmt den Stand aus den vorigen Schritten.</summary>
    public void Show(Invoice invoice, InvoiceTotals totals, ImageSource? preview, bool alreadyHybrid)
    {
        Invoice = invoice;
        Totals = totals;
        PreviewImage = preview;
        SourceAlreadyContainsInvoice = alreadyHybrid;

        OnPropertyChanged(nameof(SellerText));
        OnPropertyChanged(nameof(BuyerText));
        OnPropertyChanged(nameof(InvoiceNumberText));
        OnPropertyChanged(nameof(IssueDateText));
        OnPropertyChanged(nameof(NetText));
        OnPropertyChanged(nameof(TaxText));
        OnPropertyChanged(nameof(GrossText));
        OnPropertyChanged(nameof(PayableText));
    }

    /// <summary>Setzt den Schritt auf den Anfangszustand zurueck.</summary>
    public void Reset()
    {
        ContentMatchConfirmed = false;
        ExistingInvoiceReplacementConfirmed = false;
        SourceAlreadyContainsInvoice = false;
        Invoice = null;
        Totals = null;
        PreviewImage = null;
        ClearFindings();
    }

    private string Money(decimal? value) => value is null
        ? string.Empty
        : string.Create(
            CultureInfo.CurrentCulture,
            $"{value.Value.ToString("N2", CultureInfo.CurrentCulture)} {Invoice?.Currency.Value}");
}

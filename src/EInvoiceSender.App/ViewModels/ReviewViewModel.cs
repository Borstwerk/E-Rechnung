using System.Globalization;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;

namespace EInvoiceSender.App.ViewModels;

/// <summary>
/// Schritt 3: Die sichtbare PDF neben den erfassten Daten zeigen und die
/// Übereinstimmung ausdrücklich bestätigen lassen.
///
/// Dieser Schritt trägt den fachlichen Kern der ganzen Anwendung: Die
/// strukturierten Daten sind vom Menschen eingegeben, nicht aus der PDF
/// gelesen. Ob beides zusammenpasst, kann nur der Mensch entscheiden – deshalb
/// die Pflichtbestätigung. Ohne sie sperrt der Kern die Erzeugung, nicht bloß
/// die Oberfläche.
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
    /// Bestätigung, dass die erfassten Daten mit der sichtbaren PDF
    /// übereinstimmen.
    /// </summary>
    [ObservableProperty]
    private bool _contentMatchConfirmed;

    /// <summary>
    /// Bestätigung, eine bereits eingebettete Rechnung zu ersetzen. Wird nur
    /// abgefragt, wenn die gewählte PDF schon strukturierte Daten enthält.
    /// </summary>
    [ObservableProperty]
    private bool _existingInvoiceReplacementConfirmed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsReplacementQuestion))]
    private bool _sourceAlreadyContainsInvoice;

    /// <summary>Muss die Ersetzungsfrage überhaupt gestellt werden?</summary>
    public bool ShowsReplacementQuestion => SourceAlreadyContainsInvoice;

    /// <summary>
    /// Wohin die erzeugte Datei geschrieben wird.
    ///
    /// Steht bewusst in diesem Schritt und nicht nur in den Einstellungen: Der
    /// Anwender soll den Ordner unmittelbar vor dem Erzeugen sehen und ändern
    /// können. Die Vorgabe ist ein Unterordner der eigenen Dokumente – damit
    /// geht nie ein leerer Pfad in den Kern.
    /// </summary>
    [ObservableProperty]
    private string _outputDirectory = DefaultOutputDirectory;

    /// <summary>Die Vorgabe, solange nichts gespeichert ist.</summary>
    public static string DefaultOutputDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EInvoiceSender");

    /// <summary>Verkäufer, einzeilig.</summary>
    public string SellerText => Invoice is null
        ? string.Empty
        : $"{Invoice.Seller.Name}, {Invoice.Seller.Address.PostalCode} {Invoice.Seller.Address.City}";

    /// <summary>Käufer, einzeilig.</summary>
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

    /// <summary>Übernimmt den Stand aus den vorigen Schritten.</summary>
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

    /// <summary>Das Ergebnis des Abgleichs mit dem PDF-Betrag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTotalsComparison))]
    [NotifyPropertyChangedFor(nameof(TotalsComparisonGlyph))]
    private TotalsComparison? _totalsComparison;

    /// <summary>Liegt ein Abgleich vor?</summary>
    public bool HasTotalsComparison => TotalsComparison is not null;

    /// <summary>Zeichen zur farbunabhängigen Kennzeichnung des Abgleichs.</summary>
    public string TotalsComparisonGlyph => TotalsComparison switch
    {
        { WasPerformed: true, Matches: true } => "\u2713",
        { WasPerformed: true } => "!",
        _ => "i",
    };

    /// <summary>Übernimmt das Ergebnis des Summenabgleichs.</summary>
    public void ShowTotalsComparison(TotalsComparison comparison) => TotalsComparison = comparison;

    /// <summary>Setzt den Schritt auf den Anfangszustand zurück.</summary>
    public void Reset()
    {
        ContentMatchConfirmed = false;
        ExistingInvoiceReplacementConfirmed = false;
        SourceAlreadyContainsInvoice = false;
        Invoice = null;
        Totals = null;
        TotalsComparison = null;
        PreviewImage = null;
        ClearFindings();
    }

    private string Money(decimal? value) => value is null
        ? string.Empty
        : string.Create(
            CultureInfo.CurrentCulture,
            $"{value.Value.ToString("N2", CultureInfo.CurrentCulture)} {Invoice?.Currency.Value}");
}

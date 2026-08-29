using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Checking;

/// <summary>
/// Wie ein rechnungsartiger Anhang für diesen Prüfmodus einzuordnen ist.
/// </summary>
public enum CheckedAttachmentKind
{
    /// <summary>
    /// ZUGFeRD beziehungsweise Factur-X – das Format, das dieser Prüfmodus
    /// auswerten kann.
    /// </summary>
    FacturX,

    /// <summary>
    /// XRechnung. Erkannt, aber in dieser Fassung nicht ausgewertet: Das
    /// Format ist UBL oder CII in einer eigenen Ausprägung mit eigenen
    /// Geschäftsregeln.
    /// </summary>
    XRechnung,

    /// <summary>
    /// Order-X. Erkannt, aber kein Rechnungsformat – es beschreibt
    /// Bestellungen.
    /// </summary>
    OrderX,
}

/// <summary>
/// Ein rechnungsartiger Anhang, eingeordnet allein nach seinem Namen.
/// </summary>
/// <remarks>
/// <b>Bewusst ohne Inhalt.</b> Die Einordnung geschieht auf den Metadaten, die
/// die PDF-Analyse ohnehin schon gelesen hat. Erst wenn feststeht, dass genau
/// ein auswertbarer Anhang vorliegt, wird dessen Inhalt entpackt – und dann
/// begrenzt. Alles andere hieße, für einen Anhang zu bezahlen, den niemand
/// ansehen wird.
/// </remarks>
/// <param name="File">Die Metadaten des Anhangs.</param>
/// <param name="Kind">Das erkannte Format.</param>
public sealed record CheckedAttachment(EmbeddedFileInfo File, CheckedAttachmentKind Kind)
{
    /// <summary>Kann dieser Prüfmodus den Anhang auswerten?</summary>
    public bool IsSupported => Kind == CheckedAttachmentKind.FacturX;
}

/// <summary>
/// Ordnet die Anhänge einer PDF nach ihrem Dateinamen ein.
/// </summary>
/// <remarks>
/// <para>
/// Bewusst eine eigene Liste und nicht
/// <see cref="InvoiceAttachmentDescriptor.KnownInvoiceFileNames"/>: Jene Liste
/// beantwortet für die Erzeugung die Frage „hängt hier schon irgendetwas
/// Rechnungsartiges dran, vor dem ich warnen muss?“ und wirft dafür alle
/// Formate in einen Topf. Genau diese Zusammenfassung wäre hier der Fehler –
/// der Prüfmodus muss „Factur-X, kann ich lesen“ von „XRechnung, erkenne ich,
/// lese ich aber nicht“ unterscheiden, weil der Anwender daraus verschiedene
/// Schlüsse zieht.
/// </para>
/// <para>
/// Verglichen wird ohne Rücksicht auf Gross- und Kleinschreibung. Die
/// Schreibweise <c>ZUGFeRD-invoice.xml</c> ist historisch belegt und kommt in
/// Dateien aus ZUGFeRD 1.0 vor; sie ist derselbe Anhang wie
/// <c>zugferd-invoice.xml</c> und darf nicht als zweiter zählen.
/// </para>
/// </remarks>
public static class CheckedAttachmentNames
{
    private static readonly Dictionary<string, CheckedAttachmentKind> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["factur-x.xml"] = CheckedAttachmentKind.FacturX,
            ["zugferd-invoice.xml"] = CheckedAttachmentKind.FacturX,
            ["xrechnung.xml"] = CheckedAttachmentKind.XRechnung,
            ["order-x.xml"] = CheckedAttachmentKind.OrderX,
        };

    /// <summary>
    /// Die Anhangsnamen, die dieser Prüfmodus auswerten kann.
    /// </summary>
    public static IReadOnlyList<string> SupportedFileNames { get; } =
        [.. Known.Where(e => e.Value == CheckedAttachmentKind.FacturX)
                 .Select(e => e.Key)
                 .OrderBy(name => name, StringComparer.Ordinal)];

    /// <summary>
    /// Die Anhangsnamen, die erkannt, aber nicht ausgewertet werden.
    /// </summary>
    public static IReadOnlyList<string> RecognisedButUnsupportedFileNames { get; } =
        [.. Known.Where(e => e.Value != CheckedAttachmentKind.FacturX)
                 .Select(e => e.Key)
                 .OrderBy(name => name, StringComparer.Ordinal)];

    /// <summary>
    /// Ordnet einen Dateinamen ein. Liefert <see langword="false"/>, wenn der
    /// Name nach keinem bekannten Rechnungsformat aussieht – das ist dann ein
    /// gewöhnlicher Anhang und für die Prüfung ohne Belang.
    /// </summary>
    public static bool TryClassify(string? fileName, out CheckedAttachmentKind kind)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            kind = default;
            return false;
        }

        return Known.TryGetValue(fileName.Trim(), out kind);
    }

    /// <summary>
    /// Wählt aus allen Anhängen die rechnungsartigen heraus, in der
    /// Reihenfolge, in der sie in der Datei stehen – allein anhand der Namen,
    /// ohne einen Inhalt anzufassen.
    /// </summary>
    public static IReadOnlyList<CheckedAttachment> SelectInvoiceLike(
        IEnumerable<EmbeddedFileInfo> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return
        [
            .. from file in files
               let classified = TryClassify(file.FileName, out CheckedAttachmentKind kind)
                   ? new CheckedAttachment(file, kind)
                   : null
               where classified is not null
               select classified,
        ];
    }

    /// <summary>Lesbare Bezeichnung für den Bericht.</summary>
    public static string Describe(CheckedAttachmentKind kind) => kind switch
    {
        CheckedAttachmentKind.FacturX => "ZUGFeRD/Factur-X",
        CheckedAttachmentKind.XRechnung => "XRechnung",
        CheckedAttachmentKind.OrderX => "Order-X",
        _ => "unbekannt",
    };
}

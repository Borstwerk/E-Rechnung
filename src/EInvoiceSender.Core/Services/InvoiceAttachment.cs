namespace EInvoiceSender.Core.Services;

/// <summary>
/// Beschreibt, wie die Rechnungs-XML im PDF eingebettet werden muss.
///
/// Diese Angaben sind formatabhaengig (ZUGFeRD verlangt andere Werte als
/// Order-X), gehoeren aber nicht in die PDF-Schicht. Deshalb liefert sie der
/// XML-Writer, und die PDF-Schicht setzt sie nur um. So bleibt
/// <c>EInvoiceSender.Infrastructure</c> frei von Wissen ueber Rechnungsformate.
/// </summary>
/// <param name="FileName">Dateiname des Anhangs, z. B. <c>factur-x.xml</c>.</param>
/// <param name="MimeType">MIME-Typ, z. B. <c>text/xml</c>.</param>
/// <param name="Relationship">
/// Wert fuer <c>/AFRelationship</c> ohne fuehrenden Schraegstrich,
/// z. B. <c>Alternative</c>.
/// </param>
/// <param name="Description">Beschreibungstext, den ein PDF-Betrachter anzeigt.</param>
public sealed record InvoiceAttachmentDescriptor(
    string FileName,
    string MimeType,
    string Relationship,
    string Description)
{
    /// <summary>
    /// Dateinamen, unter denen in einer fremden PDF eine Rechnungs-XML zu
    /// erwarten ist. Historische Namen sind enthalten, damit auch aeltere
    /// Hybridrechnungen erkannt und der Benutzer gewarnt werden kann.
    /// </summary>
    public static IReadOnlyList<string> KnownInvoiceFileNames { get; } =
    [
        "factur-x.xml",
        "zugferd-invoice.xml",
        "ZUGFeRD-invoice.xml",
        "xrechnung.xml",
        "order-x.xml",
    ];

    /// <summary>
    /// Prueft, ob ein Anhangsname auf eine Rechnungs-XML hindeutet.
    /// </summary>
    public static bool LooksLikeInvoiceFile(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && KnownInvoiceFileNames.Any(known
               => string.Equals(fileName, known, StringComparison.OrdinalIgnoreCase));
}

using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Services;

/// <summary>
/// Erzeugt die strukturierte Rechnungs-XML aus dem Domaenenmodell.
/// </summary>
public interface IInvoiceXmlWriter
{
    /// <summary>Kennung des erzeugten Profils, z. B. <c>urn:cen.eu:en16931:2017</c>.</summary>
    string ProfileId { get; }

    /// <summary>Menschenlesbare Bezeichnung des Formats fuer den Bericht.</summary>
    string FormatDescription { get; }

    /// <summary>Wie die erzeugte XML im PDF einzubetten ist.</summary>
    InvoiceAttachmentDescriptor Attachment { get; }

    /// <summary>
    /// Erzeugt die XML als UTF-8-Bytes. Die Summen werden nicht neu berechnet,
    /// sondern in der uebergebenen, bereits geprueften Form geschrieben – so
    /// kann die geschriebene Datei nicht von der geprueften abweichen.
    /// </summary>
    byte[] Write(Invoice invoice, InvoiceTotals totals);
}

/// <summary>
/// Liest eine Rechnungs-XML zurueck. Wird fuer die Gegenpruefung des Ergebnisses
/// und fuer die Erkennung bereits hybrider PDFs benoetigt.
/// </summary>
public interface IInvoiceXmlReader
{
    /// <summary>
    /// Liest die Profilkennung aus einer Rechnungs-XML, ohne das gesamte
    /// Dokument abzubilden. Liefert null, wenn es keine Rechnungs-XML ist.
    /// </summary>
    string? ReadProfileId(byte[] xml);

    /// <summary>
    /// Liest die Kerndaten zurueck, die fuer die Gegenpruefung noetig sind:
    /// Rechnungsnummer, Datum, Waehrung und die Summen.
    /// </summary>
    InvoiceEcho? ReadEcho(byte[] xml);
}

/// <summary>
/// Die aus einer erzeugten XML zurueckgelesenen Kerndaten. Dient dem Nachweis,
/// dass das Ergebnis das enthaelt, was erzeugt werden sollte.
/// </summary>
/// <param name="ProfileId">Gelesene Profilkennung.</param>
/// <param name="InvoiceNumber">Rechnungsnummer (BT-1).</param>
/// <param name="IssueDate">Rechnungsdatum (BT-2).</param>
/// <param name="TypeCode">Rechnungsart (BT-3).</param>
/// <param name="Currency">Waehrung (BT-5).</param>
/// <param name="LineTotal">Summe der Positionen (BT-106).</param>
/// <param name="TaxBasisTotal">Nettosumme (BT-109).</param>
/// <param name="TaxTotal">Gesamtsteuer (BT-110).</param>
/// <param name="GrandTotal">Bruttosumme (BT-112).</param>
/// <param name="DuePayableAmount">Offener Zahlbetrag (BT-115).</param>
/// <param name="LineCount">Anzahl der Positionen.</param>
public sealed record InvoiceEcho(
    string? ProfileId,
    string? InvoiceNumber,
    DateOnly? IssueDate,
    string? TypeCode,
    string? Currency,
    decimal? LineTotal,
    decimal? TaxBasisTotal,
    decimal? TaxTotal,
    decimal? GrandTotal,
    decimal? DuePayableAmount,
    int LineCount);

/// <summary>
/// Prueft die fachlichen Regeln nach EN 16931 auf dem Domaenenmodell.
/// Liefert deutsche Meldungen mit technischer Regel-ID.
/// </summary>
public interface IBusinessRuleValidator
{
    /// <summary>Prueft die Rechnung samt berechneter Summen.</summary>
    ValidationReport Validate(Invoice invoice, InvoiceTotals totals);
}

/// <summary>
/// Prueft die erzeugte XML strukturell: Wohlgeformtheit, erwartete Elemente,
/// Profilkennung und Uebereinstimmung der Summen mit dem Modell.
/// </summary>
public interface IXmlStructureValidator
{
    /// <summary>Prueft die erzeugte XML gegen die Erwartung.</summary>
    ValidationReport Validate(byte[] xml, Invoice invoice, InvoiceTotals totals);
}

/// <summary>
/// Prueft die Struktur einer erzeugten PDF/A-3-Datei mit Bordmitteln:
/// OutputIntent, XMP, Anhang, Dateibeziehung, Metadaten, Verschluesselung.
///
/// Das ist ausdruecklich eine Teilmenge dessen, was ein Referenzvalidator wie
/// veraPDF prueft. Der Bericht weist das aus – siehe ADR-0004.
/// </summary>
public interface IPdfAStructureValidator
{
    /// <summary>Prueft die erzeugte Datei.</summary>
    Task<ValidationReport> ValidateAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liest den eingebetteten Anhang wieder aus. Liefert null, wenn keiner
    /// gefunden wurde.
    /// </summary>
    Task<byte[]?> ExtractInvoiceXmlAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
}

/// <summary>
/// Adapter fuer einen externen Validator (z. B. Mustang-CLI mit CEN-Schematron
/// und veraPDF). Optional: Ist keiner eingerichtet, laeuft die Anwendung mit
/// ihren eigenen Pruefungen weiter und weist das im Bericht aus.
/// </summary>
public interface IExternalDocumentValidator
{
    /// <summary>Name des Werkzeugs fuer den Bericht.</summary>
    string Name { get; }

    /// <summary>
    /// Ist der Validator auf diesem System einsatzbereit? Prueft Verfuegbarkeit
    /// der Laufzeit und der Programmdatei, ohne eine Pruefung auszufuehren.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Ermittelte Version des Werkzeugs, falls verfuegbar.</summary>
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Prueft eine fertige Datei und liefert uebersetzte Befunde.</summary>
    Task<ValidationReport> ValidateAsync(string filePath, CancellationToken cancellationToken = default);
}

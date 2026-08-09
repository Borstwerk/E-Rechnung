using System.Globalization;
using System.Xml.Linq;
using System.Xml;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Zugferd;

/// <summary>
/// Liest die Kerndaten einer CII-Rechnungs-XML zurueck.
///
/// Zweck ist nicht, das Dokument vollstaendig abzubilden, sondern zwei Fragen zu
/// beantworten:
/// 1. Welches Profil hat diese Datei? (Erkennung bereits hybrider PDFs)
/// 2. Enthaelt die erzeugte Datei tatsaechlich das, was erzeugt werden sollte?
///    (Gegenpruefung des Ergebnisses nach dem erneuten Oeffnen)
///
/// Der Baum wird ueber <see cref="XDocument"/> aufgebaut, aber ausschliesslich
/// mit dem abgesicherten Leser aus <see cref="SecureXml"/>: DTD-Verarbeitung ist
/// verboten, externe Verweise werden nicht aufgeloest, und Groesse wie Tiefe
/// sind begrenzt. Eingehende XML aus fremden PDFs ist nicht vertrauenswuerdig.
///
/// Bewusst nicht ueber einen vorwaertsgerichteten <see cref="XmlReader"/>
/// implementiert: Dort verschiebt <c>ReadElementContentAsString</c> die Position
/// bereits auf den Folgeknoten, was in einer Leseschleife lautlos Elemente
/// ueberspringt. Dieser Fehler ist in der Entwicklung genau so aufgetreten.
/// </summary>
public sealed class CiiInvoiceReader : IInvoiceXmlReader
{
    private static readonly XNamespace Rsm = CiiConstants.NsRsm;
    private static readonly XNamespace Ram = CiiConstants.NsRam;
    private static readonly XNamespace Udt = CiiConstants.NsUdt;

    /// <inheritdoc />
    public string? ReadProfileId(byte[] xml)
    {
        XDocument? document = TryLoad(xml);

        return document?.Root?
            .Element(Rsm + "ExchangedDocumentContext")?
            .Element(Ram + "GuidelineSpecifiedDocumentContextParameter")?
            .Element(Ram + "ID")?.Value;
    }

    /// <inheritdoc />
    public InvoiceEcho? ReadEcho(byte[] xml)
    {
        XDocument? document = TryLoad(xml);
        XElement? root = document?.Root;

        if (root is null || root.Name != Rsm + CiiConstants.RootElement)
        {
            return null;
        }

        string? profileId = root
            .Element(Rsm + "ExchangedDocumentContext")?
            .Element(Ram + "GuidelineSpecifiedDocumentContextParameter")?
            .Element(Ram + "ID")?.Value;

        XElement? exchangedDocument = root.Element(Rsm + "ExchangedDocument");
        XElement? transaction = root.Element(Rsm + "SupplyChainTradeTransaction");
        XElement? settlement = transaction?.Element(Ram + "ApplicableHeaderTradeSettlement");
        XElement? summation = settlement?.Element(Ram + "SpecifiedTradeSettlementHeaderMonetarySummation");

        if (profileId is null && exchangedDocument is null)
        {
            return null;
        }

        return new InvoiceEcho(
            ProfileId: profileId,
            InvoiceNumber: exchangedDocument?.Element(Ram + "ID")?.Value,
            IssueDate: ReadDate(exchangedDocument?.Element(Ram + "IssueDateTime")),
            TypeCode: exchangedDocument?.Element(Ram + "TypeCode")?.Value,
            Currency: settlement?.Element(Ram + "InvoiceCurrencyCode")?.Value,
            LineTotal: ReadDecimal(summation?.Element(Ram + "LineTotalAmount")),
            TaxBasisTotal: ReadDecimal(summation?.Element(Ram + "TaxBasisTotalAmount")),
            TaxTotal: ReadDecimal(summation?.Element(Ram + "TaxTotalAmount")),
            GrandTotal: ReadDecimal(summation?.Element(Ram + "GrandTotalAmount")),
            DuePayableAmount: ReadDecimal(summation?.Element(Ram + "DuePayableAmount")),
            LineCount: transaction?.Elements(Ram + "IncludedSupplyChainTradeLineItem").Count() ?? 0);
    }

    /// <summary>
    /// Laedt das Dokument abgesichert. Liefert null statt einer Ausnahme, wenn
    /// die Eingabe keine brauchbare XML ist – der Aufrufer soll dem Anwender
    /// eine verstaendliche Meldung zeigen, keinen Stapelabzug.
    /// </summary>
    private static XDocument? TryLoad(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        try
        {
            using XmlReader reader = SecureXml.CreateReader(xml);
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            // Groessenbegrenzung aus SecureXml.
            return null;
        }
    }

    /// <summary>Liest einen Betrag in invarianter Schreibweise.</summary>
    private static decimal? ReadDecimal(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return decimal.TryParse(
            element.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : null;
    }

    /// <summary>
    /// Liest ein Datum aus einem <c>udt:DateTimeString</c> im Format 102
    /// (<c>JJJJMMTT</c>). Eine andere Formatangabe wird nicht geraten, sondern
    /// als "kein Datum" gemeldet.
    /// </summary>
    private static DateOnly? ReadDate(XElement? container)
    {
        XElement? dateElement = container?.Element(Udt + "DateTimeString");

        if (dateElement is null
            || (string?)dateElement.Attribute("format") != CiiConstants.DateFormatCode)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            dateElement.Value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
    }
}

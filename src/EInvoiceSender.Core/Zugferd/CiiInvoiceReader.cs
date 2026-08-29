using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;

namespace EInvoiceSender.Core.Zugferd;

/// <summary>
/// Liest die Kerndaten einer CII-Rechnungs-XML zurück.
///
/// Zweck ist nicht, das Dokument vollständig abzubilden, sondern zwei Fragen zu
/// beantworten:
/// 1. Welches Profil hat diese Datei? (Erkennung bereits hybrider PDFs)
/// 2. Enthält die erzeugte Datei tatsächlich das, was erzeugt werden sollte?
///    (Gegenprüfung des Ergebnisses nach dem erneuten Öffnen)
///
/// Der Baum wird über <see cref="XDocument"/> aufgebaut, aber ausschließlich
/// mit dem abgesicherten Leser aus <see cref="SecureXml"/>: DTD-Verarbeitung ist
/// verboten, externe Verweise werden nicht aufgelöst, und Größe wie Tiefe
/// sind begrenzt. Eingehende XML aus fremden PDFs ist nicht vertrauenswürdig.
///
/// Bewusst nicht über einen vorwärtsgerichteten <see cref="XmlReader"/>
/// implementiert: Dort verschiebt <c>ReadElementContentAsString</c> die Position
/// bereits auf den Folgeknoten, was in einer Leseschleife lautlos Elemente
/// überspringt. Dieser Fehler ist in der Entwicklung genau so aufgetreten.
/// </summary>
public sealed class CiiInvoiceReader : IInvoiceXmlReader, ICiiInvoiceInspector
{
    private static readonly XNamespace Rsm = CiiConstants.NsRsm;
    private static readonly XNamespace Ram = CiiConstants.NsRam;
    private static readonly XNamespace Udt = CiiConstants.NsUdt;

    /// <inheritdoc />
    public string? ReadProfileId(byte[] xml)
    {
        XDocument? document = TryLoad(xml, out _);

        return document?.Root?
            .Element(Rsm + "ExchangedDocumentContext")?
            .Element(Ram + "GuidelineSpecifiedDocumentContextParameter")?
            .Element(Ram + "ID")?.Value;
    }

    /// <inheritdoc />
    public InvoiceEcho? ReadEcho(byte[] xml)
    {
        XDocument? document = TryLoad(xml, out _);

        if (ReadCore(document?.Root) is not { } core)
        {
            return null;
        }

        return new InvoiceEcho(
            ProfileId: core.ProfileId,
            InvoiceNumber: core.InvoiceNumber,
            IssueDate: core.IssueDate,
            TypeCode: core.TypeCode,
            Currency: core.Currency,
            SellerIdentifier: core.SellerIdentifier,
            LineTotal: core.LineTotal,
            TaxBasisTotal: core.TaxBasisTotal,
            TaxTotal: core.TaxTotal,
            GrandTotal: core.GrandTotal,
            DuePayableAmount: core.DuePayableAmount,
            LineCount: core.LineCount);
    }

    /// <inheritdoc />
    public CiiInspection Inspect(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        if (xml.Length == 0)
        {
            return CiiInspection.Failed(CiiStructureStatus.Empty);
        }

        XDocument? document = TryLoad(xml, out CiiStructureStatus failure);

        if (document is null)
        {
            return CiiInspection.Failed(failure);
        }

        if (ReadCore(document.Root) is not { } core)
        {
            // Wohlgeformt, aber nicht das erwartete Dokument. Das ist etwas
            // anderes als "kaputt" und bekommt deshalb einen eigenen Befund.
            return CiiInspection.Failed(CiiStructureStatus.NotCii);
        }

        return CiiInspection.Read(core);
    }

    /// <summary>
    /// Die gemeinsame Navigation durch das CII-Dokument.
    ///
    /// Sie steht bewusst genau einmal hier: Die Gegenprüfung der Erzeugung und
    /// die Bestandsaufnahme einer fremden Rechnung lesen dieselben Felder an
    /// denselben Stellen. Zwei Fassungen derselben Elementpfade wären zwei
    /// Fassungen, die auseinanderlaufen können – und dann läse der Prüfmodus
    /// etwas anderes als die Erzeugung, ohne dass es jemandem auffiele.
    ///
    /// Liefert <see langword="null"/> unter genau den Bedingungen, unter denen
    /// <see cref="ReadEcho"/> das schon immer tat: kein Dokument, falsches
    /// Wurzelelement, oder weder Profilkennung noch Dokumentangaben.
    /// </summary>
    private static CiiInvoiceSummary? ReadCore(XElement? root)
    {
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
        XElement? agreement = transaction?.Element(Ram + "ApplicableHeaderTradeAgreement");
        XElement? settlement = transaction?.Element(Ram + "ApplicableHeaderTradeSettlement");
        XElement? summation = settlement?.Element(Ram + "SpecifiedTradeSettlementHeaderMonetarySummation");

        if (profileId is null && exchangedDocument is null)
        {
            return null;
        }

        return new CiiInvoiceSummary(
            ProfileId: profileId,
            InvoiceNumber: exchangedDocument?.Element(Ram + "ID")?.Value,
            IssueDate: ReadDate(exchangedDocument?.Element(Ram + "IssueDateTime")),
            TypeCode: exchangedDocument?.Element(Ram + "TypeCode")?.Value,
            Currency: settlement?.Element(Ram + "InvoiceCurrencyCode")?.Value,
            // BT-29 ausdrücklich nur unterhalb von SellerTradeParty: Ein
            // ram:ID gibt es im Dokument mehrfach – als Rechnungsnummer, in
            // den Steuerregistrierungen und möglicherweise beim Käufer. Ein
            // Leser ohne diese Eingrenzung läse je nach Datei etwas anderes.
            SellerIdentifier: agreement?
                .Element(Ram + "SellerTradeParty")?
                .Element(Ram + "ID")?.Value,
            LineTotal: ReadDecimal(summation?.Element(Ram + "LineTotalAmount")),
            TaxBasisTotal: ReadDecimal(summation?.Element(Ram + "TaxBasisTotalAmount")),
            TaxTotal: ReadDecimal(summation?.Element(Ram + "TaxTotalAmount")),
            GrandTotal: ReadDecimal(summation?.Element(Ram + "GrandTotalAmount")),
            DuePayableAmount: ReadDecimal(summation?.Element(Ram + "DuePayableAmount")),
            LineCount: transaction?.Elements(Ram + "IncludedSupplyChainTradeLineItem").Count() ?? 0);
    }

    /// <summary>
    /// Lädt das Dokument abgesichert. Liefert null statt einer Ausnahme, wenn
    /// die Eingabe keine brauchbare XML ist – der Aufrufer soll dem Anwender
    /// eine verständliche Meldung zeigen, keinen Stapelabzug.
    ///
    /// <paramref name="failure"/> hält fest, <b>warum</b> es nicht ging. Für
    /// die Erzeugung ist das gleichgültig, für die Prüfung einer fremden Datei
    /// ist es die halbe Auskunft.
    /// </summary>
    private static XDocument? TryLoad(byte[] xml, out CiiStructureStatus failure)
    {
        ArgumentNullException.ThrowIfNull(xml);

        failure = CiiStructureStatus.Cii;

        try
        {
            using XmlReader reader = SecureXml.CreateReader(xml);
            return XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            // Auch der Fall, in dem der abgesicherte Leser eine DTD ablehnt:
            // Für den Anwender ist die Datei damit nicht lesbar.
            failure = CiiStructureStatus.NotWellFormed;
            return null;
        }
        catch (InvalidDataException)
        {
            // Größenbegrenzung aus SecureXml.
            failure = CiiStructureStatus.TooLarge;
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

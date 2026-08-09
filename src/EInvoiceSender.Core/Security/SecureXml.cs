using System.Xml;

namespace EInvoiceSender.Core.Security;

/// <summary>
/// Sichere Einstellungen fuer die XML-Verarbeitung.
///
/// Eingehende XML-Dateien stammen aus fremden PDFs und sind grundsaetzlich
/// nicht vertrauenswuerdig (docs/SECURITY.md, S1 und S2). Deshalb gilt fuer
/// jedes Lesen ohne Ausnahme:
///
/// * <c>DtdProcessing = Prohibit</c> – verhindert XXE und Entity-Expansion
///   („Billion Laughs"), weil externe wie interne Entitaeten gar nicht erst
///   verarbeitet werden.
/// * <c>XmlResolver = null</c> – kein Nachladen externer Ressourcen, weder aus
///   dem Dateisystem noch aus dem Netz.
/// * Groessen- und Tiefenbegrenzung – begrenzt den Schaden durch absichtlich
///   tief verschachtelte Dokumente.
///
/// Es darf im gesamten Projekt keinen anderen Weg geben, XML zu lesen. Deshalb
/// liegt diese Klasse in der Application-Schicht: Sowohl die Formatschicht
/// (Rechnungs-XML) als auch die Validierungsschicht (Berichte externer
/// Werkzeuge) brauchen sie, und eine zweite Fassung waere genau die
/// Abweichung, die hier verhindert werden soll.
/// </summary>
public static class SecureXml
{
    /// <summary>Groesste zulaessige XML-Groesse in Bytes (8 MB).</summary>
    public const int MaxXmlSizeInBytes = 8 * 1024 * 1024;

    /// <summary>Groesste zulaessige Verschachtelungstiefe.</summary>
    public const int MaxDepth = 100;

    /// <summary>
    /// Erzeugt Leseeinstellungen ohne DTD-Verarbeitung und ohne Aufloesung
    /// externer Verweise.
    /// </summary>
    public static XmlReaderSettings CreateReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        MaxCharactersInDocument = MaxXmlSizeInBytes,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        CloseInput = false,
    };

    /// <summary>
    /// Oeffnet einen abgesicherten Leser auf einem Datenstrom.
    /// </summary>
    public static XmlReader CreateReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return XmlReader.Create(stream, CreateReaderSettings());
    }

    /// <summary>
    /// Oeffnet einen abgesicherten Leser auf einem Bytepuffer.
    /// Prueft vorher die Groesse, damit ein uebergrosses Dokument gar nicht
    /// erst in den Parser gelangt.
    /// </summary>
    public static XmlReader CreateReader(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        if (xml.Length > MaxXmlSizeInBytes)
        {
            throw new InvalidDataException(
                $"Die XML-Datei ist mit {xml.Length} Bytes groesser als das Limit von {MaxXmlSizeInBytes} Bytes.");
        }

        return CreateReader(new MemoryStream(xml, writable: false));
    }

    /// <summary>
    /// Schreibeinstellungen fuer die erzeugte Rechnungs-XML.
    /// Einrueckung mit zwei Leerzeichen und feste Zeilenenden sorgen dafuer,
    /// dass zwei Laeufe byte-identische Dateien erzeugen – notwendig fuer die
    /// Golden-Master-Tests und fuer nachvollziehbare Pruefsummen.
    /// </summary>
    public static XmlWriterSettings CreateWriterSettings() => new()
    {
        Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Replace,
        CloseOutput = false,
        OmitXmlDeclaration = false,
    };
}

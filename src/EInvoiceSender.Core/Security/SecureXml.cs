using System.Xml;

namespace EInvoiceSender.Core.Security;

/// <summary>
/// Sichere Einstellungen für die XML-Verarbeitung.
///
/// Eingehende XML-Dateien stammen aus fremden PDFs und sind grundsätzlich
/// nicht vertrauenswürdig (docs/SECURITY.md, S1 und S2). Deshalb gilt für
/// jedes Lesen ohne Ausnahme:
///
/// * <c>DtdProcessing = Prohibit</c> – verhindert XXE und Entity-Expansion
///   („Billion Laughs"), weil externe wie interne Entitäten gar nicht erst
///   verarbeitet werden.
/// * <c>XmlResolver = null</c> – kein Nachladen externer Ressourcen, weder aus
///   dem Dateisystem noch aus dem Netz.
/// * Größen- und Tiefenbegrenzung – begrenzt den Schaden durch absichtlich
///   tief verschachtelte Dokumente.
///
/// Es darf im gesamten Projekt keinen anderen Weg geben, XML zu lesen. Deshalb
/// liegt diese Klasse in der Application-Schicht: Sowohl die Formatschicht
/// (Rechnungs-XML) als auch die Validierungsschicht (Berichte externer
/// Werkzeuge) brauchen sie, und eine zweite Fassung wäre genau die
/// Abweichung, die hier verhindert werden soll.
/// </summary>
public static class SecureXml
{
    /// <summary>Größte zulässige XML-Größe in Bytes (8 MB).</summary>
    public const int MaxXmlSizeInBytes = 8 * 1024 * 1024;

    /// <summary>Größte zulässige Verschachtelungstiefe.</summary>
    public const int MaxDepth = 100;

    /// <summary>
    /// Erzeugt Leseeinstellungen ohne DTD-Verarbeitung und ohne Auflösung
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
    /// Öffnet einen abgesicherten Leser auf einem Datenstrom.
    /// </summary>
    public static XmlReader CreateReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return XmlReader.Create(stream, CreateReaderSettings());
    }

    /// <summary>
    /// Öffnet einen abgesicherten Leser auf einem Bytepuffer.
    /// Prüft vorher die Größe, damit ein übergroßes Dokument gar nicht
    /// erst in den Parser gelangt.
    /// </summary>
    public static XmlReader CreateReader(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        if (xml.Length > MaxXmlSizeInBytes)
        {
            throw new InvalidDataException(
                $"Die XML-Datei ist mit {xml.Length} Bytes größer als das Limit von {MaxXmlSizeInBytes} Bytes.");
        }

        return CreateReader(new MemoryStream(xml, writable: false));
    }

    /// <summary>
    /// Schreibeinstellungen für die erzeugte Rechnungs-XML.
    /// Einrückung mit zwei Leerzeichen und feste Zeilenenden sorgen dafür,
    /// dass zwei Läufe byte-identische Dateien erzeugen – notwendig für die
    /// Golden-Master-Tests und für nachvollziehbare Prüfsummen.
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

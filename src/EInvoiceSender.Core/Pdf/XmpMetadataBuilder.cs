using System.Globalization;
using System.Security;
using System.Text;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Baut das XMP-Metadatenpaket fuer eine ZUGFeRD-/Factur-X-Datei.
///
/// Ohne dieses Paket ist die Datei weder PDF/A-konform noch als Hybridrechnung
/// erkennbar. Erforderlich sind drei Bestandteile:
///
/// 1. <c>pdfaid:part</c> und <c>pdfaid:conformance</c> – kennzeichnen PDF/A-3b.
/// 2. Die Deklaration des Factur-X-Erweiterungsschemas im
///    <c>pdfaExtension</c>-Container. PDF/A verlangt, dass jedes verwendete
///    Fremdschema dort beschrieben ist; fehlt die Deklaration, ist das Dokument
///    nicht konform.
/// 3. Die eigentlichen <c>fx</c>-Felder mit Profil und Dateiname.
///
/// Die exakten Zeichenketten sind in docs/STANDARDS.md, Abschnitt 2.4 belegt.
/// </summary>
public static class XmpMetadataBuilder
{
    /// <summary>Namensraum des Factur-X-Erweiterungsschemas.</summary>
    public const string FacturXNamespace = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";

    /// <summary>Praefix des Factur-X-Erweiterungsschemas.</summary>
    public const string FacturXPrefix = "fx";

    /// <summary>Wert von <c>fx:DocumentType</c> fuer eine Rechnung.</summary>
    public const string DocumentType = "INVOICE";

    /// <summary>Wert von <c>fx:Version</c>.</summary>
    public const string FacturXVersion = "1.0";

    /// <summary>Wert von <c>fx:ConformanceLevel</c> fuer das Profil EN 16931.</summary>
    public const string ConformanceLevel = "EN 16931";

    /// <summary>PDF/A-Teil.</summary>
    public const string PdfAPart = "3";

    /// <summary>PDF/A-Konformitaetsstufe.</summary>
    public const string PdfAConformance = "B";

    /// <summary>
    /// Kennung des XMP-Pakets. Dieser Wert ist in der XMP-Spezifikation fest
    /// vorgegeben und darf nicht veraendert werden.
    /// </summary>
    private const string PacketId = "W5M0MpCehiHzreSzNTczkc9d";

    /// <summary>
    /// Erzeugt das vollstaendige XMP-Paket als UTF-8-Bytes.
    /// </summary>
    /// <param name="title">Dokumenttitel (<c>dc:title</c>).</param>
    /// <param name="author">Verfasser, ueblicherweise der Verkaeufer (<c>dc:creator</c>).</param>
    /// <param name="subject">Kurzbeschreibung (<c>dc:description</c>).</param>
    /// <param name="producer">Erzeugendes Programm (<c>pdf:Producer</c>).</param>
    /// <param name="creationDate">Erzeugungszeitpunkt.</param>
    /// <param name="embeddedFileName">Name der eingebetteten XML.</param>
    public static byte[] Build(
        string title,
        string author,
        string subject,
        string producer,
        DateTimeOffset creationDate,
        string embeddedFileName)
    {
        string timestamp = creationDate.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

        var builder = new StringBuilder(4096);

        // Das Paket beginnt mit einer Byte-Order-Mark als Kodierungshinweis –
        // so schreibt es die XMP-Spezifikation vor.
        builder.Append("<?xpacket begin=\"﻿\" id=\"").Append(PacketId).Append("\"?>\n");
        builder.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\" x:xmptk=\"EInvoiceSender\">\n");
        builder.Append("  <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");

        // Dublin Core
        builder.Append("    <rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
        builder.Append("      <dc:format>application/pdf</dc:format>\n");
        AppendAltText(builder, "dc:title", title);
        builder.Append("      <dc:creator>\n        <rdf:Seq>\n          <rdf:li>")
               .Append(Escape(author))
               .Append("</rdf:li>\n        </rdf:Seq>\n      </dc:creator>\n");
        AppendAltText(builder, "dc:description", subject);
        builder.Append("    </rdf:Description>\n");

        // Basis-XMP
        builder.Append("    <rdf:Description rdf:about=\"\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">\n");
        builder.Append("      <xmp:CreatorTool>").Append(Escape(producer)).Append("</xmp:CreatorTool>\n");
        builder.Append("      <xmp:CreateDate>").Append(timestamp).Append("</xmp:CreateDate>\n");
        builder.Append("      <xmp:ModifyDate>").Append(timestamp).Append("</xmp:ModifyDate>\n");
        builder.Append("    </rdf:Description>\n");

        // PDF-Erzeuger
        builder.Append("    <rdf:Description rdf:about=\"\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">\n");
        builder.Append("      <pdf:Producer>").Append(Escape(producer)).Append("</pdf:Producer>\n");
        builder.Append("    </rdf:Description>\n");

        // PDF/A-Kennzeichnung
        builder.Append("    <rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">\n");
        builder.Append(CultureInfo.InvariantCulture, $"      <pdfaid:part>{PdfAPart}</pdfaid:part>\n");
        builder.Append(
            CultureInfo.InvariantCulture,
            $"      <pdfaid:conformance>{PdfAConformance}</pdfaid:conformance>\n");
        builder.Append("    </rdf:Description>\n");

        AppendExtensionSchema(builder);

        // Die eigentlichen Factur-X-Felder
        builder.Append("    <rdf:Description rdf:about=\"\" xmlns:")
               .Append(FacturXPrefix).Append("=\"").Append(FacturXNamespace).Append("\">\n");
        builder.Append("      <fx:DocumentType>").Append(DocumentType).Append("</fx:DocumentType>\n");
        builder.Append("      <fx:DocumentFileName>").Append(Escape(embeddedFileName))
               .Append("</fx:DocumentFileName>\n");
        builder.Append("      <fx:Version>").Append(FacturXVersion).Append("</fx:Version>\n");
        builder.Append("      <fx:ConformanceLevel>").Append(ConformanceLevel)
               .Append("</fx:ConformanceLevel>\n");
        builder.Append("    </rdf:Description>\n");

        builder.Append("  </rdf:RDF>\n");
        builder.Append("</x:xmpmeta>\n");

        // Auffuellbereich, damit ein spaeteres Werkzeug das Paket an Ort und
        // Stelle aendern kann, ohne die Datei umzubauen.
        for (int i = 0; i < 20; i++)
        {
            builder.Append("                                                                          \n");
        }

        builder.Append("<?xpacket end=\"w\"?>");

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(builder.ToString());
    }

    /// <summary>
    /// Schreibt die Deklaration des Factur-X-Erweiterungsschemas.
    /// PDF/A verlangt fuer jedes fremde Schema eine solche Beschreibung samt
    /// aller verwendeten Eigenschaften.
    /// </summary>
    private static void AppendExtensionSchema(StringBuilder builder)
    {
        builder.Append("""
                <rdf:Description rdf:about="" xmlns:pdfaExtension="http://www.aiim.org/pdfa/ns/extension/" xmlns:pdfaSchema="http://www.aiim.org/pdfa/ns/schema#" xmlns:pdfaProperty="http://www.aiim.org/pdfa/ns/property#">
                  <pdfaExtension:schemas>
                    <rdf:Bag>
                      <rdf:li rdf:parseType="Resource">
                        <pdfaSchema:schema>Factur-X PDFA Extension Schema</pdfaSchema:schema>
                        <pdfaSchema:namespaceURI>urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#</pdfaSchema:namespaceURI>
                        <pdfaSchema:prefix>fx</pdfaSchema:prefix>
                        <pdfaSchema:property>
                          <rdf:Seq>
                            <rdf:li rdf:parseType="Resource">
                              <pdfaProperty:name>DocumentFileName</pdfaProperty:name>
                              <pdfaProperty:valueType>Text</pdfaProperty:valueType>
                              <pdfaProperty:category>external</pdfaProperty:category>
                              <pdfaProperty:description>Name of the embedded XML invoice file</pdfaProperty:description>
                            </rdf:li>
                            <rdf:li rdf:parseType="Resource">
                              <pdfaProperty:name>DocumentType</pdfaProperty:name>
                              <pdfaProperty:valueType>Text</pdfaProperty:valueType>
                              <pdfaProperty:category>external</pdfaProperty:category>
                              <pdfaProperty:description>INVOICE</pdfaProperty:description>
                            </rdf:li>
                            <rdf:li rdf:parseType="Resource">
                              <pdfaProperty:name>Version</pdfaProperty:name>
                              <pdfaProperty:valueType>Text</pdfaProperty:valueType>
                              <pdfaProperty:category>external</pdfaProperty:category>
                              <pdfaProperty:description>The actual version of the standard applying to the embedded XML invoice file</pdfaProperty:description>
                            </rdf:li>
                            <rdf:li rdf:parseType="Resource">
                              <pdfaProperty:name>ConformanceLevel</pdfaProperty:name>
                              <pdfaProperty:valueType>Text</pdfaProperty:valueType>
                              <pdfaProperty:category>external</pdfaProperty:category>
                              <pdfaProperty:description>The conformance level of the embedded XML invoice file</pdfaProperty:description>
                            </rdf:li>
                          </rdf:Seq>
                        </pdfaSchema:property>
                      </rdf:li>
                    </rdf:Bag>
                  </pdfaExtension:schemas>
                </rdf:Description>

        """.ReplaceLineEndings("\n"));
    }

    private static void AppendAltText(StringBuilder builder, string elementName, string value)
    {
        builder.Append("      <").Append(elementName).AppendLine(">");
        builder.Append("        <rdf:Alt>\n");
        builder.Append("          <rdf:li xml:lang=\"x-default\">").Append(Escape(value)).Append("</rdf:li>\n");
        builder.Append("        </rdf:Alt>\n");
        builder.Append("      </").Append(elementName).AppendLine(">");
    }

    /// <summary>
    /// Maskiert Text fuer die Einbettung in XML. Die Werte stammen aus
    /// Benutzereingaben (Firmenname, Betreff) und duerfen das Paket nicht
    /// zerstoeren koennen.
    /// </summary>
    private static string Escape(string? value)
        => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
}

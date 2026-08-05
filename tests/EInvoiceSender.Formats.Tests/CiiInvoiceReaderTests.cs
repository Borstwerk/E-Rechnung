using System.Text;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.Xml;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Formats.Cii;
using EInvoiceSender.TestSupport;
using Xunit;

namespace EInvoiceSender.Formats.Tests;

/// <summary>
/// Prueft das Zurueclesen der erzeugten XML. Das ist der Nachweis, dass das
/// Ergebnis das enthaelt, was erzeugt werden sollte – und zugleich der Schutz
/// vor bosartigen XML-Dateien aus fremden PDFs.
/// </summary>
public sealed class CiiInvoiceReaderTests
{
    private static readonly CiiInvoiceWriter Writer = new();
    private static readonly CiiInvoiceReader Reader = new();

    [Theory]
    [MemberData(nameof(ScenarioKeys))]
    public void GelesenesEchoStimmtMitDenErzeugtenWertenUeberein(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);
        byte[] xml = Writer.Write(scenario.Invoice, totals);

        InvoiceEcho? echo = Reader.ReadEcho(xml);

        Assert.NotNull(echo);
        Assert.Equal(CiiConstants.ProfileEn16931, echo.ProfileId);
        Assert.Equal(scenario.Invoice.InvoiceNumber, echo.InvoiceNumber);
        Assert.Equal(scenario.Invoice.IssueDate, echo.IssueDate);
        Assert.Equal("380", echo.TypeCode);
        Assert.Equal("EUR", echo.Currency);
        Assert.Equal(scenario.Invoice.Lines.Count, echo.LineCount);

        Assert.Equal(totals.LineTotal, echo.LineTotal);
        Assert.Equal(totals.TaxBasisTotal, echo.TaxBasisTotal);
        Assert.Equal(totals.TaxTotal, echo.TaxTotal);
        Assert.Equal(totals.GrandTotal, echo.GrandTotal);
        Assert.Equal(totals.DuePayableAmount, echo.DuePayableAmount);
    }

    [Theory]
    [MemberData(nameof(ScenarioKeys))]
    public void ProfilkennungWirdErkannt(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);

        Assert.Equal(
            CiiConstants.ProfileEn16931,
            Reader.ReadProfileId(Writer.Write(scenario.Invoice, totals)));
    }

    [Fact]
    public void UnbekannteProfilkennungWirdUnveraendertGemeldet()
    {
        byte[] xml = Bytes("""
            <?xml version="1.0" encoding="utf-8"?>
            <rsm:CrossIndustryInvoice
                xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
                xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
              <rsm:ExchangedDocumentContext>
                <ram:GuidelineSpecifiedDocumentContextParameter>
                  <ram:ID>urn:factur-x.eu:1p0:minimum</ram:ID>
                </ram:GuidelineSpecifiedDocumentContextParameter>
              </rsm:ExchangedDocumentContext>
            </rsm:CrossIndustryInvoice>
            """);

        Assert.Equal("urn:factur-x.eu:1p0:minimum", Reader.ReadProfileId(xml));
        Assert.Equal("MINIMUM", CiiConstants.DescribeProfile(Reader.ReadProfileId(xml)));
    }

    [Fact]
    public void NichtWohlgeformteXmlLiefertNullStattAusnahme()
    {
        byte[] xml = Bytes("<rsm:CrossIndustryInvoice><nicht geschlossen");

        Assert.Null(Reader.ReadProfileId(xml));
        Assert.Null(Reader.ReadEcho(xml));
    }

    [Fact]
    public void FremdesXmlOhneRechnungsdatenLiefertNull()
    {
        byte[] xml = Bytes("<?xml version=\"1.0\"?><irgendwas><anderes>Text</anderes></irgendwas>");

        Assert.Null(Reader.ReadProfileId(xml));
        Assert.Null(Reader.ReadEcho(xml));
    }

    /// <summary>
    /// Sicherheitstest: Eine XML mit externer Entitaet darf keine lokale Datei
    /// einlesen. Bei aktivem <c>DtdProcessing = Prohibit</c> scheitert bereits
    /// das Parsen, und der Leser meldet lediglich "keine Rechnung".
    /// </summary>
    [Fact]
    public void Sicherheitstest_ExterneEntitaetWirdNichtAufgeloest()
    {
        byte[] xml = Bytes("""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE angriff [
              <!ENTITY geheim SYSTEM "file:///etc/passwd">
            ]>
            <rsm:CrossIndustryInvoice
                xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
                xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
              <rsm:ExchangedDocumentContext>
                <ram:GuidelineSpecifiedDocumentContextParameter>
                  <ram:ID>&geheim;</ram:ID>
                </ram:GuidelineSpecifiedDocumentContextParameter>
              </rsm:ExchangedDocumentContext>
            </rsm:CrossIndustryInvoice>
            """);

        string? profile = Reader.ReadProfileId(xml);

        Assert.Null(profile);
    }

    /// <summary>
    /// Sicherheitstest gegen Entity-Expansion („Billion Laughs"). Auch dieser
    /// Angriff scheitert an der abgeschalteten DTD-Verarbeitung, statt den
    /// Arbeitsspeicher zu fuellen.
    /// </summary>
    [Fact]
    public void Sicherheitstest_EntityExpansionWirdVerhindert()
    {
        byte[] xml = Bytes("""
            <?xml version="1.0"?>
            <!DOCTYPE lol [
              <!ENTITY lol "lol">
              <!ENTITY lol1 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
              <!ENTITY lol2 "&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;&lol1;">
              <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
            ]>
            <root>&lol3;</root>
            """);

        Assert.Null(Reader.ReadEcho(xml));
    }

    [Fact]
    public void UebergrosseXmlWirdAbgelehnt()
    {
        // Der Leser darf ein absichtlich riesiges Dokument nicht erst vollstaendig
        // in den Parser lassen.
        byte[] xml = new byte[SecureXml.MaxXmlSizeInBytes + 1];
        Array.Fill(xml, (byte)'a');

        Assert.Null(Reader.ReadProfileId(xml));
        Assert.Null(Reader.ReadEcho(xml));
    }

    public static TheoryData<string> ScenarioKeys()
    {
        var data = new TheoryData<string>();
        foreach (InvoiceScenario scenario in InvoiceScenarios.All)
        {
            data.Add(scenario.Key);
        }

        return data;
    }

    private static byte[] Bytes(string content) => Encoding.UTF8.GetBytes(content);
}

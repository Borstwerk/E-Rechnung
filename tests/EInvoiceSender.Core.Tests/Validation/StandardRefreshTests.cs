using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Xunit;

namespace EInvoiceSender.Core.Tests.Validation;

/// <summary>
/// ER-030-STD-01a und STD-01b: Standardstand 0.3.0 und die fachliche Trennung
/// zwischen Normgültigkeit und BorstWerk-Unterstützung.
///
/// **Der eigentliche Fehler, um den es hier geht.** Die Codelisten dieser
/// Anwendung sind kuratierte Auswahlen – bewusst klein, damit niemand einen
/// Code wählt, dessen Bedeutung hier niemand geprüft hat. Das Regelwerk
/// behandelte einen Code außerhalb dieser Auswahl aber als Normverstoß. Das
/// sind zwei verschiedene Aussagen:
///
/// - „nach dem gepinnten EN-16931-Codebestand ungültig“ – eine Aussage über
///   die Norm,
/// - „von BorstWerk nicht angeboten“ – eine Aussage über dieses Programm.
///
/// Die zweite als die erste auszugeben ist besonders für den späteren
/// Prüfmodus (ER-030-CHK-01) gefährlich: Eine fremde, gültige E-Rechnung
/// dürfte nie als normwidrig gelten, nur weil BorstWerk den Wert selbst nicht
/// zur Auswahl stellt.
/// </summary>
public sealed class StandardRefreshTests
{
    // ------------------------------------------------- STD-01a Standardstand

    /// <summary>
    /// Der Bericht nennt den Standard, den diese Anwendung erzeugt. Bleibt er
    /// auf 2.3/1.07/D16B stehen, behauptet jede erzeugte Datei einen älteren
    /// Stand als den tatsächlich abgeglichenen.
    /// </summary>
    [Fact]
    public void DieFormatbeschreibungNenntDenAktuellenStandardstand()
    {
        string beschreibung = CiiConstants.FormatDescription;

        Assert.Contains("ZUGFeRD 2.5.2", beschreibung, StringComparison.Ordinal);
        Assert.Contains("Factur-X 1.09.2", beschreibung, StringComparison.Ordinal);
        Assert.Contains("EN 16931", beschreibung, StringComparison.Ordinal);
        Assert.Contains("D22B", beschreibung, StringComparison.Ordinal);
        Assert.Contains("PDF/A-3b", beschreibung, StringComparison.Ordinal);

        // Der alte Stand darf nicht daneben stehen bleiben.
        Assert.DoesNotContain("D16B", beschreibung, StringComparison.Ordinal);
        Assert.DoesNotContain("1.07", beschreibung, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Was sich ausdrücklich nicht ändern darf.** Profilkennung und
    /// Namensräume sind über ZUGFeRD 2.1 bis 2.5 hinweg unverändert. Sie
    /// „mitzuziehen“ wäre der naheliegende und falsche Reflex bei einem
    /// Versionssprung – jede Datei würde damit unlesbar.
    /// </summary>
    [Fact]
    public void ProfilkennungUndNamensräumeBleibenUnverändert()
    {
        Assert.Equal("urn:cen.eu:en16931:2017", CiiConstants.ProfileEn16931);
        Assert.Equal(
            "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100", CiiConstants.NsRsm);
        Assert.Equal(
            "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100",
            CiiConstants.NsRam);
        Assert.Equal(
            "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100", CiiConstants.NsUdt);
        Assert.Equal(
            "urn:un:unece:uncefact:data:standard:QualifiedDataType:100", CiiConstants.NsQdt);
    }

    /// <summary>Einbettung und PDF/A-Kennzeichnung bleiben unverändert.</summary>
    [Fact]
    public void EinbettungUndPdfAKennzeichnungBleibenUnverändert()
    {
        Assert.Equal("factur-x.xml", CiiConstants.EmbeddedFileName);
        Assert.Equal("text/xml", CiiConstants.EmbeddedMimeType);
        Assert.Equal("Alternative", CiiConstants.EmbeddedRelationship);
    }

    /// <summary>
    /// **fx:Version ist keine Versionsnummer des Standards.** Sie bezeichnet
    /// die Fassung des XMP-Extension-Schemas und lautet unverändert „1.0“.
    /// Sie auf 1.09.2 zu setzen wäre der zweite naheliegende Fehler eines
    /// Versionssprungs.
    /// </summary>
    [Fact]
    public void DieXmpAngabenBleibenUnverändert()
    {
        Assert.Equal(
            "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#",
            XmpMetadataBuilder.FacturXNamespace);
        Assert.Equal("INVOICE", XmpMetadataBuilder.DocumentType);
        Assert.Equal("1.0", XmpMetadataBuilder.FacturXVersion);
        Assert.Equal("EN 16931", XmpMetadataBuilder.ConformanceLevel);
    }

    /// <summary>
    /// Die erzeugte XML trägt weiterhin genau die alte Profilkennung. Der
    /// Standardsprung ist eine Aussage über die abgeglichene Fassung, keine
    /// Änderung am Erzeugnis.
    /// </summary>
    [Fact]
    public void DieErzeugteXmlTrägtWeiterhinDieUnveränderteProfilkennung()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        byte[] xml = new CiiInvoiceWriter().Write(
            szenario.Invoice, InvoiceCalculator.Calculate(szenario.Invoice));

        Assert.Equal(CiiConstants.ProfileEn16931, new CiiInvoiceReader().ReadProfileId(xml));
    }

    // ------------------------------------------------- STD-01b Währungscodes

    /// <summary>
    /// XCG (Karibischer Gulden) ist im Codebestand v17b neu hinzugekommen und
    /// löst ANG ab.
    /// </summary>
    [Fact]
    public void XcgIstNachAktuellemStandGültigUndWirdAngeboten()
    {
        Assert.True(CurrencyCodeList.IsOffered("XCG"));
        Assert.False(CurrencyCodeList.IsWithdrawnFromEn16931("XCG"));
        Assert.Contains(CurrencyCodeList.All, e => e.Code == "XCG");
    }

    /// <summary>
    /// BGN und ANG sind mit v17b aus dem EN-16931-Codebestand entfernt. Sie
    /// werden nicht mehr angeboten – und, was hier der Punkt ist, sie gelten
    /// nicht als bloß „unbekannt“, sondern als nachweislich zurückgezogen.
    /// </summary>
    [Theory]
    [InlineData("BGN")]
    [InlineData("ANG")]
    [InlineData("HRK")]
    public void ZurückgezogeneWährungenSindNichtMehrGültig(string code)
    {
        Assert.False(CurrencyCodeList.IsOffered(code));
        Assert.True(CurrencyCodeList.IsWithdrawnFromEn16931(code));
        Assert.DoesNotContain(CurrencyCodeList.All, e => e.Code == code);
    }

    /// <summary>
    /// **Der Kern der Trennung.** Ein Code, den BorstWerk nicht anbietet, ist
    /// deshalb nicht normwidrig. Über ihn trifft diese Anwendung schlicht
    /// keine Aussage – die volle ISO-4217-Liste ist hier nicht abgebildet.
    /// </summary>
    [Theory]
    [InlineData("KZT")]
    [InlineData("PEN")]
    [InlineData("NGN")]
    public void NichtAngeboteneWährungenGeltenNichtAlsZurückgezogen(string code)
    {
        Assert.False(CurrencyCodeList.IsOffered(code));
        Assert.False(CurrencyCodeList.IsWithdrawnFromEn16931(code));
    }

    /// <summary>Die Auswahl der Oberfläche bleibt bewusst begrenzt.</summary>
    [Fact]
    public void DieAngeboteneWährungsauswahlBleibtEineTeilmenge()
    {
        // Rund 180 Währungen sind nach ISO 4217 aktiv. Die Auswahl hier ist
        // eine kuratierte Teilmenge und soll es bleiben.
        Assert.InRange(CurrencyCodeList.All.Count, 20, 80);
        Assert.Contains(CurrencyCodeList.All, e => e.Code == "EUR");
    }

    /// <summary>Der Regelfall bleibt unberührt.</summary>
    [Fact]
    public void EuroBleibtUnverändertGültig()
    {
        Assert.True(CurrencyCodeList.IsOffered("EUR"));
        Assert.False(CurrencyCodeList.IsWithdrawnFromEn16931("EUR"));
        Assert.True(CurrencyCodeList.TryGetName("eur", out string? name));
        Assert.Equal("Euro", name);
    }

    // ------------------------------------- STD-01b Regelwerk: Währungsbefund

    /// <summary>
    /// Eine zurückgezogene Währung ist ein echter Normbefund und bleibt ein
    /// Fehler.
    /// </summary>
    [Fact]
    public void EineZurückgezogeneWährungBleibtEinFehler()
    {
        ValidationReport bericht = Prüfe(MitWährung("BGN"));

        ValidationFinding befund = Assert.Single(
            bericht.Findings, f => f.RuleId == "APP-DOC-008");

        Assert.Equal(FindingSeverity.Error, befund.Severity);
        Assert.Equal("BR-05", befund.NormRule);
    }

    /// <summary>
    /// **Der eigentliche Fix.** Eine nicht angebotene, aber nirgends als
    /// zurückgezogen belegte Währung darf kein Normverstoß mehr sein. Sie ist
    /// ein Hinweis – und der Befund sagt ausdrücklich, wer das entscheidet.
    /// </summary>
    [Fact]
    public void EineNichtAngeboteneWährungIstNurNochEinHinweis()
    {
        ValidationReport bericht = Prüfe(MitWährung("KZT"));

        Assert.DoesNotContain(bericht.Findings, f => f.RuleId == "APP-DOC-008");

        ValidationFinding befund = Assert.Single(
            bericht.Findings, f => f.RuleId == "APP-DOC-011");

        Assert.Equal(FindingSeverity.Warning, befund.Severity);

        // Kein Normverweis: Über die Normgültigkeit sagt dieser Befund nichts.
        Assert.Null(befund.NormRule);
        Assert.Contains("nicht", befund.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------ STD-01b Mengeneinheiten

    /// <summary>
    /// Die Erstellungsauswahl bleibt begrenzt – das war vorher so und bleibt
    /// bewusst so.
    /// </summary>
    [Fact]
    public void DieAngeboteneEinheitenauswahlBleibtEineTeilmenge()
    {
        Assert.True(UnitCodeList.IsSupported("HUR"));
        Assert.True(UnitCodeList.IsSupported("C62"));
        Assert.False(UnitCodeList.IsSupported("FASS"));
        Assert.InRange(UnitCodeList.CommonUnits.Count, 10, 60);
    }

    /// <summary>
    /// **Nicht unterstützt ist nicht ungültig.** Die Erzeugung wird weiterhin
    /// angehalten – aber der Befund behauptet nicht mehr, der Code verstoße
    /// gegen BR-23. Rec. 20/21 kennt mehrere hundert Codes; welche davon
    /// gültig sind, entscheidet hier niemand.
    /// </summary>
    [Fact]
    public void EineNichtUnterstützteEinheitIstKeinNormverstoß()
    {
        Invoice rechnung = BasisRechnung();
        rechnung = rechnung with
        {
            Lines = [rechnung.Lines[0] with { Unit = UnitCode.Parse("FTK") }],
        };

        ValidationFinding befund = Assert.Single(
            Prüfe(rechnung).Findings, f => f.RuleId == "APP-LIN-005");

        // Die Erzeugung wird weiterhin angehalten.
        Assert.Equal(FindingSeverity.Error, befund.Severity);

        // Aber ohne Normverweis, und der Text sagt, wessen Grenze das ist.
        Assert.Null(befund.NormRule);
        Assert.Contains("unterstützt", befund.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------ STD-01a Rechnungsart

    /// <summary>
    /// **Ein falscher Normverweis, belegt am Primärartefakt.** `APP-DOC-007`
    /// trug bisher `BR-CO-03`. Diese Regel lautet im EN-16931-Schematron des
    /// gepinnten Prüfwerkzeugs aber: „Value added tax point date (BT-7) and
    /// Value added tax point date code (BT-8) are mutually exclusive.“ Sie hat
    /// mit der Rechnungsart nichts zu tun.
    ///
    /// Ersetzt wird sie durch keinen anderen Verweis. Über die Zugehörigkeit
    /// zu UNTDID 1001 wacht `BR-CL-01`; die Liste hier ist nur die Teilmenge,
    /// die diese Anwendung beherrscht. Es gilt dasselbe wie bei Währung und
    /// Einheit: nicht unterstützt ist nicht ungültig.
    /// </summary>
    [Fact]
    public void DieRechnungsartTrägtKeinenFalschenNormverweisMehr()
    {
        Invoice rechnung = BasisRechnung() with { TypeCode = (InvoiceTypeCode)999 };

        ValidationFinding befund = Assert.Single(
            Prüfe(rechnung).Findings, f => f.RuleId == "APP-DOC-007");

        Assert.Equal(FindingSeverity.Error, befund.Severity);
        Assert.Null(befund.NormRule);
        Assert.Contains("BR-CL-01", befund.TechnicalDetail ?? string.Empty, StringComparison.Ordinal);
    }

    // ------------------------------------------------- Unveränderte Zusagen

    /// <summary>
    /// BR-CO-26 bleibt vollständig unberührt: Verkäuferkennung, Registerkennung
    /// oder USt-IdNr. – die Steuernummer allein genügt weiterhin nicht.
    /// </summary>
    [Fact]
    public void DasVerhaltenBeiBrCo26BleibtUnverändert()
    {
        Invoice nurSteuernummer = BasisRechnung();
        nurSteuernummer = nurSteuernummer with
        {
            Seller = nurSteuernummer.Seller with
            {
                VatId = null,
                LegalRegistrationId = null,
                SellerIdentifier = null,
                TaxNumber = "079/123/45678",
            },
        };

        Assert.Contains(
            Prüfe(nurSteuernummer).Findings,
            f => f.RuleId == "APP-SEL-004" && f.NormRule == "BR-CO-26");

        // Jede der drei zulässigen Kennungen trägt die Rechnung weiterhin durch.
        foreach (SellerParty verkäufer in new[]
                 {
                     nurSteuernummer.Seller with { VatId = "DE123456789" },
                     nurSteuernummer.Seller with { LegalRegistrationId = "HRB 12345" },
                     nurSteuernummer.Seller with { SellerIdentifier = "LIEF-4711" },
                 })
        {
            Assert.DoesNotContain(
                Prüfe(nurSteuernummer with { Seller = verkäufer }).Findings,
                f => f.RuleId == "APP-SEL-004");
        }
    }

    // ------------------------------------------------------------ Hilfsmittel

    private static Invoice BasisRechnung()
        => InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice;

    private static Invoice MitWährung(string code)
        => BasisRechnung() with { Currency = CurrencyCode.Parse(code) };

    private static ValidationReport Prüfe(Invoice invoice)
        => new En16931RuleValidator().Validate(invoice, InvoiceCalculator.Calculate(invoice));
}

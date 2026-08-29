using System.Security.Cryptography;
using System.Text;
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
    /// **Der Bestand selbst, nicht bloß Stichproben.** Elf Kontrollwerte
    /// belegen, dass die Trennung funktioniert – sie belegen nicht, dass die
    /// übrigen 167 Kennungen richtig übernommen wurden. Genau dort sitzt aber
    /// der gefährliche Fehler: Eine ausgelassene Kennung lehnt stillschweigend
    /// gültige Rechnungen ab, eine hinzuerfundene lässt ungültige durch, und
    /// beides fällt niemandem auf, bis es beim Empfänger auffällt.
    ///
    /// Deshalb wird hier der vollständige übernommene Bestand gegen die
    /// Prüfsumme der Primärquelle gerechnet: ZUGFeRD 2.5.2 / Factur-X 1.09.2,
    /// „EN16931 code lists values v17b“, veröffentlicht 2026-04-16, anzuwenden
    /// ab 2026-05-15, Liste Currency mit 178 Codes. Kanonische Form: Codes
    /// alphabetisch sortiert, je Code ein Zeilenvorschub.
    /// </summary>
    [Fact]
    public void DerÜbernommeneWährungsbestandEntsprichtDerPrimärquelle()
    {
        Assert.Equal(178, CurrencyCodeList.NormCodes.Count);

        string kanonisch = string.Concat(
            CurrencyCodeList.NormCodes
                .OrderBy(c => c, StringComparer.Ordinal)
                .Select(c => c + "\n"));

        string prüfsumme = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(kanonisch)));

        Assert.Equal(
            "fb4f5fb74e80a59d37ace3a95b0ad1063db70a6e2fa582772e2c865e0f3610b2",
            prüfsumme);
    }

    /// <summary>
    /// Nach EN 16931 gültige Kennungen. <c>XXX</c> (keine Währung) und
    /// <c>STN</c> gehören ausdrücklich dazu – <c>STN</c> steht neben dem
    /// zurückgezogenen <c>STD</c> und trennt sauber, wer den Bestand kennt und
    /// wer rät.
    /// </summary>
    [Theory]
    [InlineData("EUR")]
    [InlineData("XCG")]
    [InlineData("KZT")]
    [InlineData("PEN")]
    [InlineData("XXX")]
    [InlineData("STN")]
    public void NormgültigeWährungenWerdenAlsGültigErkannt(string code)
        => Assert.True(CurrencyCodeList.IsValidPerEn16931(code));

    /// <summary>
    /// **Der Kern der Rückgabe.** Zurückgezogene Kennungen (<c>ANG</c>,
    /// <c>BGN</c>, <c>HRK</c>, <c>STD</c>) und ein frei erfundener Code
    /// (<c>XYZ</c>) fallen in dieselbe Klasse: nach dem abgeglichenen Stand
    /// ungültig. Eine Negativliste bekannter Rückzüge könnte <c>XYZ</c> nicht
    /// erfassen – die positive Prüfung gegen den vollständigen Bestand schon.
    /// </summary>
    [Theory]
    [InlineData("ANG")]
    [InlineData("BGN")]
    [InlineData("HRK")]
    [InlineData("XYZ")]
    [InlineData("STD")]
    public void NichtNormgültigeWährungenWerdenAlsUngültigErkannt(string code)
    {
        Assert.False(CurrencyCodeList.IsValidPerEn16931(code));
        Assert.False(CurrencyCodeList.IsOffered(code));
        Assert.DoesNotContain(CurrencyCodeList.All, e => e.Code == code);
    }

    /// <summary>
    /// **Angebot und Norm sind wirklich unabhängig.** <c>XCG</c> ist nach
    /// v17b gültig – und trotzdem nicht im Angebot, weil es keinen fachlichen
    /// Grund gibt, es anzubieten. Dass ein Code neu in der Norm ist, ist für
    /// sich genommen kein Grund, ihn in die Auswahl zu nehmen. Fiele beides
    /// wieder zusammen, wäre die Trennung nur behauptet.
    /// </summary>
    [Fact]
    public void XcgIstNormgültigAberNichtImAngebot()
    {
        Assert.True(CurrencyCodeList.IsValidPerEn16931("XCG"));
        Assert.False(CurrencyCodeList.IsOffered("XCG"));
        Assert.DoesNotContain(CurrencyCodeList.All, e => e.Code == "XCG");

        ValidationReport bericht = Prüfe(MitWährung("XCG"));

        Assert.DoesNotContain(bericht.Findings, f => f.RuleId == "APP-DOC-008");
        Assert.Contains(bericht.Findings, f => f.RuleId == "APP-DOC-011");
    }

    /// <summary>
    /// Die Auswahl der Oberfläche bleibt eine echte Teilmenge des Normbestands
    /// – und zwar in beide Richtungen geprüft: klein genug, um kuratiert zu
    /// bleiben, und ohne einen einzigen Eintrag, den die Norm nicht kennt.
    /// Letzteres ist die eigentliche Absicherung: Ein angebotener Code, der
    /// normwidrig ist, würde eine unbrauchbare Rechnung erzeugen, ohne dass
    /// die Oberfläche warnt.
    /// </summary>
    [Fact]
    public void DasAngebotIstEineEchteTeilmengeDesNormbestands()
    {
        Assert.InRange(CurrencyCodeList.All.Count, 20, 80);
        Assert.Contains(CurrencyCodeList.All, e => e.Code == "EUR");

        foreach ((string code, _) in CurrencyCodeList.All)
        {
            Assert.True(
                CurrencyCodeList.IsValidPerEn16931(code),
                $"Angeboten, aber nicht im EN-16931-Codebestand v17b: {code}");
        }
    }

    /// <summary>
    /// Die Rückzugserläuterungen dürfen dem Bestand nie widersprechen: Was
    /// hier als zurückgezogen erklärt wird, darf nicht gleichzeitig als
    /// gültig geführt werden. Sonst stünde in einem Befund eine Begründung,
    /// die das Regelwerk selbst widerlegt.
    /// </summary>
    [Theory]
    [InlineData("ANG")]
    [InlineData("BGN")]
    [InlineData("HRK")]
    public void RückzugserläuterungenWidersprechenDemBestandNicht(string code)
    {
        Assert.True(CurrencyCodeList.TryGetWithdrawalReason(code, out string? grund));
        Assert.False(string.IsNullOrWhiteSpace(grund));
        Assert.False(CurrencyCodeList.IsValidPerEn16931(code));
    }

    /// <summary>Der Regelfall bleibt unberührt.</summary>
    [Fact]
    public void EuroBleibtUnverändertGültigUndAngeboten()
    {
        Assert.True(CurrencyCodeList.IsValidPerEn16931("EUR"));
        Assert.True(CurrencyCodeList.IsOffered("EUR"));
        Assert.True(CurrencyCodeList.TryGetName("eur", out string? name));
        Assert.Equal("Euro", name);

        Assert.DoesNotContain(
            Prüfe(MitWährung("EUR")).Findings,
            f => f.RuleId is "APP-DOC-008" or "APP-DOC-011");
    }

    // ------------------------------------- STD-01b Regelwerk: Währungsbefund

    /// <summary>
    /// Eine nicht normgültige Währung ist ein Fehler – und trägt den
    /// zutreffenden Normverweis.
    ///
    /// **Nicht BR-05.** Die Regel lautet im EN-16931-Schematron des gepinnten
    /// Prüfwerkzeugs „An Invoice shall have an Invoice currency code (BT-5)“
    /// und prüft damit nur das Vorhandensein. Für die Codeliste zuständig ist
    /// `BR-CL-04`: „Invoice currency code MUST be coded using ISO code list
    /// 4217 alpha-3“.
    /// </summary>
    [Theory]
    [InlineData("BGN")]
    [InlineData("XYZ")]
    public void EineUngültigeWährungIstEinFehlerMitBrCl04(string code)
    {
        ValidationFinding befund = Assert.Single(
            Prüfe(MitWährung(code)).Findings, f => f.RuleId == "APP-DOC-008");

        Assert.Equal(FindingSeverity.Error, befund.Severity);
        Assert.Equal("BR-CL-04", befund.NormRule);
    }

    /// <summary>
    /// **Der eigentliche Fix.** Eine normgültige, hier nur nicht angebotene
    /// Währung ist kein Normverstoß mehr, sondern ein Hinweis – und der
    /// Hinweis trägt keinen Normverweis, weil er über die Norm nichts sagt.
    /// </summary>
    [Theory]
    [InlineData("KZT")]
    [InlineData("PEN")]
    public void EineNormgültigeAberNichtAngeboteneWährungIstNurEinHinweis(string code)
    {
        ValidationReport bericht = Prüfe(MitWährung(code));

        Assert.DoesNotContain(bericht.Findings, f => f.RuleId == "APP-DOC-008");

        ValidationFinding befund = Assert.Single(
            bericht.Findings, f => f.RuleId == "APP-DOC-011");

        Assert.Equal(FindingSeverity.Warning, befund.Severity);
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

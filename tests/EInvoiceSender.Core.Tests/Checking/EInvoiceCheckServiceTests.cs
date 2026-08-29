using System.Security.Cryptography;
using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Checking;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Checking;

/// <summary>
/// ER-030-CHK-01A: die read-only Bestandsaufnahme einer fertigen
/// E-Rechnung.
///
/// **Woran sich diese Tests messen lassen.** Der Prüfmodus soll aufnehmen, was
/// in einer fremden Datei steht – nicht mehr. Zwei Fehler wären besonders
/// teuer und werden deshalb hier besonders gründlich abgesichert:
///
/// - Die Quelldatei anzufassen. Der Anwender übergibt eine fertige Rechnung,
///   oft die einzige Ausfertigung. Sie muss den Vorgang byteweise unverändert
///   überstehen.
/// - Eine Aussage zu treffen, die nicht geprüft wurde. „PDF/A-3B“ steht im
///   XMP, weil es jemand hineingeschrieben hat; ob es stimmt, entscheidet
///   veraPDF, und der läuft hier nicht.
/// </summary>
public sealed class EInvoiceCheckServiceTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "BorstWerk-Check-" + Guid.NewGuid().ToString("N"));

    public EInvoiceCheckServiceTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    // ------------------------------------------------------- 1, 9, 10 Kerndaten

    /// <summary>
    /// Eine gültige BorstWerk-Hybridrechnung wird gelesen, und zwar mit allen
    /// Kerndaten, die CHK-01A verlangt.
    /// </summary>
    [Fact]
    public async Task EineHybridrechnungWirdVollständigGelesen()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        string pfad = SchreibeHybridrechnung(szenario, "rechnung.pdf");

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.True(ergebnis.Completed);
        Assert.False(ergebnis.Canceled);

        CiiInvoiceSummary summary = Assert.IsType<CiiInvoiceSummary>(ergebnis.InvoiceSummary);

        // Profilkennung (Fall 9)
        Assert.Equal(CiiConstants.ProfileEn16931, summary.ProfileId);

        // Kerndaten (Fall 10) – gegen das Szenario, nicht gegen sich selbst.
        InvoiceTotals summen = InvoiceCalculator.Calculate(szenario.Invoice);

        Assert.Equal(szenario.Invoice.InvoiceNumber, summary.InvoiceNumber);
        Assert.Equal(szenario.Invoice.IssueDate, summary.IssueDate);
        Assert.Equal(((int)szenario.Invoice.TypeCode).ToString(System.Globalization.CultureInfo.InvariantCulture), summary.TypeCode);
        Assert.Equal(szenario.Invoice.Currency.Value, summary.Currency);
        Assert.Equal(summen.LineTotal, summary.LineTotal);
        Assert.Equal(summen.TaxBasisTotal, summary.TaxBasisTotal);
        Assert.Equal(summen.TaxTotal, summary.TaxTotal);
        Assert.Equal(summen.GrandTotal, summary.GrandTotal);
        Assert.Equal(summen.DuePayableAmount, summary.DuePayableAmount);
        Assert.Equal(szenario.Invoice.Lines.Count, summary.LineCount);

        // Bestandsaufnahme der PDF
        CheckedDocumentInfo info = Assert.IsType<CheckedDocumentInfo>(ergebnis.DocumentInfo);
        Assert.Equal(1, info.PageCount);
        Assert.False(string.IsNullOrWhiteSpace(info.PdfVersion));
        Assert.Equal("factur-x.xml", info.InvoiceAttachmentName);
        Assert.Equal(CheckedAttachmentKind.FacturX, info.InvoiceAttachmentKind);
        Assert.True(info.InvoiceAttachmentSizeInBytes > 0);

        Assert.Contains(ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.ProfileDetected);
    }

    // --------------------------------------------------- 2, 3 Read-only-Nachweis

    /// <summary>
    /// Die gemeldete Prüfsumme ist die der Quelldatei – und die Datei ist
    /// hinterher byteweise dieselbe.
    ///
    /// **Beides zusammen, nicht einzeln.** Ein richtiger Hash allein bewiese
    /// nur, dass irgendwann richtig gerechnet wurde; unveränderte Bytes allein
    /// bewiesen nicht, dass der Bericht sich auf diese Datei bezieht.
    /// </summary>
    [Fact]
    public async Task DieQuelldateiBleibtUnverändertUndDiePrüfsummeStimmt()
    {
        string pfad = SchreibeHybridrechnung(
            InvoiceScenarios.ByKey("01-dienstleistung-19"), "unberuehrt.pdf");

        byte[] vorher = await File.ReadAllBytesAsync(pfad);
        string erwarteterHash = Convert.ToHexStringLower(SHA256.HashData(vorher));
        DateTime geschrieben = File.GetLastWriteTimeUtc(pfad);

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        byte[] nachher = await File.ReadAllBytesAsync(pfad);

        Assert.Equal(erwarteterHash, ergebnis.SourceSha256);
        Assert.Equal(vorher.Length, nachher.Length);
        Assert.Equal(vorher.Length, ergebnis.SourceSizeInBytes);
        Assert.True(vorher.SequenceEqual(nachher), "Die Quelldatei wurde verändert.");

        // Zusatzwächter, ausdrücklich kein Ersatz für den Bytevergleich: Ein
        // Zeitstempel lässt sich zurücksetzen, Bytes nicht.
        Assert.Equal(geschrieben, File.GetLastWriteTimeUtc(pfad));

        // Es entsteht auch keine Ergebnisdatei neben der Quelle.
        Assert.Single(Directory.GetFiles(_workspace));
    }

    /// <summary>
    /// Auch der Fehlerfall darf die Quelle nicht anfassen. Gerade dort wäre
    /// eine Reparatur verlockend – und genau die ist ausgeschlossen.
    /// </summary>
    [Fact]
    public async Task AuchOhneRechnungsdatenBleibtDieQuelleUnverändert()
    {
        string pfad = Path.Combine(_workspace, "ohne-anhang.pdf");
        await File.WriteAllBytesAsync(pfad, AttachedPdfFactory.Create());

        byte[] vorher = await File.ReadAllBytesAsync(pfad);

        await Prüfe(pfad);

        byte[] nachher = await File.ReadAllBytesAsync(pfad);

        Assert.True(vorher.SequenceEqual(nachher), "Die Quelldatei wurde verändert.");
        Assert.Single(Directory.GetFiles(_workspace));
    }

    // ------------------------------------------------------- 4 Kein Rechnungsanhang

    [Fact]
    public async Task EinePdfOhneRechnungsanhangLiefertEinenEindeutigenBefund()
    {
        string pfad = Path.Combine(_workspace, "gewoehnlich.pdf");
        await File.WriteAllBytesAsync(pfad, AttachedPdfFactory.Create());

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        Assert.Null(ergebnis.InvoiceSummary);

        ValidationFinding befund = Assert.Single(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.NoInvoiceAttachment);

        Assert.Equal(FindingSeverity.Error, befund.Severity);
    }

    // ------------------------------------------------------- 5 Beschädigte XML

    [Fact]
    public async Task EineBeschädigteRechnungsXmlLiefertEinenEigenenBefund()
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "kaputt.pdf",
            new AttachedPdfFactory.Attachment(
                "factur-x.xml", Encoding.UTF8.GetBytes("<rsm:CrossIndustryInvoice><nicht")));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        Assert.Null(ergebnis.InvoiceSummary);

        Assert.Contains(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.InvoiceXmlNotWellFormed);

        // Ausdrücklich nicht der Befund für "keine Rechnungsdaten": Die Datei
        // trägt welche, sie sind nur kaputt.
        Assert.DoesNotContain(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.NoInvoiceAttachment);
    }

    // ---------------------------------------------- 6, 7 Erkannt, nicht unterstützt

    /// <summary>
    /// XRechnung und Order-X sind erkannte Formate. Sie dürfen **nicht** als
    /// „keine Rechnungsdaten gefunden“ ausgegeben werden – das wäre die
    /// falsche Auskunft: Der Anwender hielte seine Datei für leer, obwohl sie
    /// vollständige Rechnungsdaten trägt.
    /// </summary>
    [Theory]
    [InlineData("xrechnung.xml", CheckedAttachmentKind.XRechnung)]
    [InlineData("order-x.xml", CheckedAttachmentKind.OrderX)]
    public async Task EinErkanntesAberNichtUnterstütztesFormatIstKeinLeerbefund(
        string anhangsname, CheckedAttachmentKind erwartet)
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "fremdformat.pdf",
            new AttachedPdfFactory.Attachment(
                anhangsname, Encoding.UTF8.GetBytes("<Invoice><ID>RE-1</ID></Invoice>")));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);

        ValidationFinding befund = Assert.Single(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.UnsupportedInvoiceFormat);

        Assert.Contains(
            CheckedAttachmentNames.Describe(erwartet), befund.Message, StringComparison.Ordinal);

        Assert.DoesNotContain(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.NoInvoiceAttachment);
        Assert.DoesNotContain(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.InvoiceXmlNotCii);
    }

    // ------------------------------------------------------- 8 Mehrdeutigkeit

    /// <summary>
    /// Bei mehreren rechnungsartigen Anhängen wird nicht der erste genommen.
    ///
    /// Ein Bericht über die falsche der beiden Rechnungen sähe vollkommen
    /// glaubwürdig aus – das macht diesen Fall gefährlicher als einen
    /// offensichtlichen Fehler.
    /// </summary>
    [Fact]
    public async Task MehrereRechnungsanhängeFührenNichtZuEinerWillkürlichenAuswahl()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        byte[] gültig = ErzeugeXml(szenario);

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "mehrdeutig.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", gültig),
            new AttachedPdfFactory.Attachment("zugferd-invoice.xml", gültig));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);

        // Der entscheidende Teil: Es wird nichts ausgewertet.
        Assert.Null(ergebnis.InvoiceSummary);
        Assert.Null(ergebnis.DocumentInfo?.InvoiceAttachmentName);

        ValidationFinding befund = Assert.Single(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.AmbiguousInvoiceAttachments);

        Assert.Equal(FindingSeverity.Error, befund.Severity);
        Assert.Contains("factur-x.xml", befund.TechnicalDetail ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("zugferd-invoice.xml", befund.TechnicalDetail ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Auch eine Mischung aus unterstütztem und nicht unterstütztem Format ist
    /// mehrdeutig – der unterstützte darf nicht stillschweigend gewinnen.
    /// </summary>
    [Fact]
    public async Task AuchEineMischungAusFormatenGiltAlsMehrdeutig()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "gemischt.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", ErzeugeXml(szenario)),
            new AttachedPdfFactory.Attachment("xrechnung.xml", Encoding.UTF8.GetBytes("<Invoice/>")));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.Contains(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.AmbiguousInvoiceAttachments);
        Assert.Null(ergebnis.InvoiceSummary);
    }

    /// <summary>
    /// Ein gewöhnlicher Anhang neben der Rechnung macht die Datei nicht
    /// mehrdeutig. Sonst wäre jede Rechnung mit beigelegtem Lieferschein
    /// ungeprüft.
    /// </summary>
    [Fact]
    public async Task EinGewöhnlicherAnhangNebenDerRechnungStörtNicht()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "mit-beilage.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", ErzeugeXml(szenario)),
            new AttachedPdfFactory.Attachment("lieferschein.xml", Encoding.UTF8.GetBytes("<Lieferschein/>")));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.True(ergebnis.Completed);
        Assert.NotNull(ergebnis.InvoiceSummary);
        Assert.DoesNotContain(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.AmbiguousInvoiceAttachments);
    }

    // ------------------------------------------------- 11 DTD und externe Entität

    /// <summary>
    /// Der abgesicherte Leser bleibt in Kraft. Eine eingebettete XML mit DTD
    /// und externer Entität wird abgewiesen – und zwar ohne die Datei zu
    /// öffnen, auf die sie zeigt.
    /// </summary>
    [Fact]
    public async Task EineXmlMitDtdUndExternerEntitätWirdAbgewiesen()
    {
        const string angriff = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE angriff [
              <!ENTITY geheim SYSTEM "file:///etc/passwd">
            ]>
            <rsm:CrossIndustryInvoice xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100">
              <hinweis>&geheim;</hinweis>
            </rsm:CrossIndustryInvoice>
            """;

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "dtd.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", Encoding.UTF8.GetBytes(angriff)));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        Assert.Null(ergebnis.InvoiceSummary);
        Assert.Contains(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.InvoiceXmlNotWellFormed);
    }

    // ------------------------------------------------------- 12 Übergroße XML

    /// <summary>
    /// Eine XML jenseits der Größengrenze wird gar nicht erst geparst.
    ///
    /// Geprüft wird hier der Leser unmittelbar: Eine Datei von über acht
    /// Megabyte durch die PDF-Erzeugung zu schicken kostet Zeit und beweist
    /// nichts, was diese Zusicherung nicht schon zeigt.
    /// </summary>
    [Fact]
    public void EineÜbergroßeXmlWirdAbgewiesen()
    {
        byte[] zuGroß = new byte[SecureXml.MaxXmlSizeInBytes + 1];
        Array.Fill(zuGroß, (byte)'a');

        CiiInspection ergebnis = new CiiInvoiceReader().Inspect(zuGroß);

        Assert.Equal(CiiStructureStatus.TooLarge, ergebnis.Status);
        Assert.Null(ergebnis.Summary);
    }

    /// <summary>Ein leerer Anhang ist ein eigener Fall, kein Parserfehler.</summary>
    [Fact]
    public async Task EinLeererRechnungsanhangLiefertEinenEigenenBefund()
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "leer.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", []));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.Contains(ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.InvoiceXmlEmpty);
    }

    // --------------------------------------------------- 13 Beschädigte PDF

    [Fact]
    public async Task EineBeschädigtePdfBeendetDiePrüfungKontrolliert()
    {
        string pfad = Path.Combine(_workspace, "defekt.pdf");
        await File.WriteAllBytesAsync(pfad, Encoding.ASCII.GetBytes("%PDF-1.7\nkein gültiger Inhalt"));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        Assert.False(ergebnis.Canceled);
        Assert.Contains(ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.PdfDamaged);

        // Auch hier gilt: gerechnet wurde über die echte Datei.
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(pfad))),
            ergebnis.SourceSha256);
    }

    [Fact]
    public async Task EineDateiDieKeinePdfIstWirdAbgewiesen()
    {
        string pfad = Path.Combine(_workspace, "keine.pdf");
        await File.WriteAllTextAsync(pfad, "Das ist nur Text.");

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        Assert.Contains(ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.NotAPdf);
    }

    [Fact]
    public async Task EineFehlendeDateiWirdAbgewiesen()
    {
        CheckEInvoiceResult ergebnis = await Prüfe(Path.Combine(_workspace, "gibtesnicht.pdf"));

        Assert.False(ergebnis.Completed);
        Assert.Contains(ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.SourceMissing);
    }

    // ------------------------------------------- 14 Signatur ist kein Mangel

    /// <summary>
    /// **Der Unterschied zwischen Preflight und Prüfung.** Für die Erzeugung
    /// ist eine digitale Signatur ein Hindernis: Das Einbetten der XML bräche
    /// sie. Bei einer bereits fertigen Rechnung ist dieselbe Signatur kein
    /// Mangel – eher das Gegenteil.
    ///
    /// Der Test bildet den Fall über den Blocker ab, den die Analyse liefert,
    /// und stellt sicher, dass daraus ein Hinweis wird und kein Fehler.
    /// </summary>
    [Fact]
    public async Task EineDigitaleSignaturMachtDieRechnungNichtUngültig()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        string pfad = SchreibeHybridrechnung(szenario, "signiert.pdf");

        var analyse = new SignierendeAnalyse(NeueAnalyse());
        CheckEInvoiceResult ergebnis = await Prüfe(pfad, analyse);

        // Die Prüfung läuft vollständig durch.
        Assert.True(ergebnis.Completed);
        Assert.NotNull(ergebnis.InvoiceSummary);
        Assert.True(ergebnis.DocumentInfo!.IsDigitallySigned);

        ValidationFinding befund = Assert.Single(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.DigitallySigned);

        Assert.Equal(FindingSeverity.Information, befund.Severity);
        Assert.DoesNotContain(ergebnis.Report.Findings, f => f.Severity == FindingSeverity.Error);
    }

    // ------------------------------------------- PDF/A bleibt eine Deklaration

    /// <summary>
    /// Der Bericht darf aus einer XMP-Angabe keine Konformität machen.
    /// veraPDF ist nicht gelaufen, und das muss dastehen.
    /// </summary>
    [Fact]
    public async Task DiePdfADeklarationWirdNichtAlsKonformitätAusgegeben()
    {
        string pfad = SchreibeHybridrechnung(
            InvoiceScenarios.ByKey("01-dienstleistung-19"), "deklariert.pdf");

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        ValidationFinding befund = Assert.Single(
            ergebnis.Report.Findings,
            f => f.RuleId is CheckRuleIds.PdfADeclarationFound or CheckRuleIds.PdfADeclarationMissing);

        Assert.Equal(FindingSeverity.Information, befund.Severity);

        foreach (ValidationFinding jeder in ergebnis.Report.Findings)
        {
            Assert.DoesNotContain("normkonform", jeder.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gültige E-Rechnung", jeder.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PDF/A-konform", jeder.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------ Hilfsmittel

    private static byte[] ErzeugeXml(InvoiceScenario szenario)
        => new CiiInvoiceWriter().Write(szenario.Invoice, InvoiceCalculator.Calculate(szenario.Invoice));

    private string SchreibeHybridrechnung(InvoiceScenario szenario, string dateiname)
        => AttachedPdfFactory.WriteTo(
            _workspace, dateiname,
            new AttachedPdfFactory.Attachment("factur-x.xml", ErzeugeXml(szenario)));

    private static PdfAnalyzer NeueAnalyse()
        => new(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);

    private static Task<CheckEInvoiceResult> Prüfe(string pfad, IPdfAnalyzer? analyse = null)
    {
        PdfAnalyzer standard = NeueAnalyse();

        var dienst = new EInvoiceCheckService(
            analyse ?? standard,
            standard,
            new CiiInvoiceReader(),
            NullLogger<EInvoiceCheckService>.Instance);

        return dienst.CheckAsync(new CheckEInvoiceRequest(pfad));
    }

    /// <summary>
    /// Reicht die echte Analyse durch und ergänzt den Signatur-Blocker.
    ///
    /// Eine echt signierte PDF zu erzeugen bräuchte ein Zertifikat samt
    /// privatem Schlüssel im Repository – ausgeschlossen. Geprüft werden soll
    /// hier ohnehin nicht, ob Signaturen erkannt werden (das tut die Analyse
    /// und ist dort geprüft), sondern wie der Prüfmodus mit dem Befund umgeht.
    /// </summary>
    private sealed class SignierendeAnalyse(IPdfAnalyzer inner) : IPdfAnalyzer
    {
        public async Task<PdfAnalysisResult> AnalyzeAsync(
            string filePath, CancellationToken cancellationToken = default)
        {
            PdfAnalysisResult echt = await inner.AnalyzeAsync(filePath, cancellationToken);

            return echt with
            {
                UpgradeBlockers = [.. echt.UpgradeBlockers, PdfUpgradeBlocker.DigitallySigned],
            };
        }

        public Task<bool> LooksLikePdfAsync(string filePath, CancellationToken cancellationToken = default)
            => inner.LooksLikePdfAsync(filePath, cancellationToken);
    }
}

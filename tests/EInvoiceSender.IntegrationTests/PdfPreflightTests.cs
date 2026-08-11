using System.Runtime.Versioning;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Prüft die Eingangsprüfung (Preflight).
///
/// Zwei Eigenschaften stehen hier im Mittelpunkt:
/// 1. Das Urteil ist dreistufig und trifft in jedem Fall zu.
/// 2. Wird eine Datei abgelehnt, nennt die Meldung eine konkrete Einstellung,
///    die der Anwender in seinem Programm ändern kann. Eine Ablehnung ohne
///    Handlungsanweisung wäre für ihn wertlos.
/// </summary>
// Die Kette schließt den Rasterweg ein, und der braucht PDFium. Die
// Angabe hält den Prüfer davon ab, Zielsysteme anzunehmen, auf denen
// diese Anwendung nie läuft.
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PdfPreflightTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly PdfPreflightService _preflight;
    private readonly PdfAInvoiceComposer _composer;
    private readonly CiiInvoiceWriter _writer = new();

    public PdfPreflightTests()
    {
        var analyzer = new PdfAnalyzer(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);

        _preflight = PipelineParts.Preflight(analyzer);
        _composer = new PdfAInvoiceComposer(analyzer, NullLogger<PdfAInvoiceComposer>.Instance);
    }

    [Fact]
    public async Task EinfachePdfIstGeeignet()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf(pageCount: 3));

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.Suitable, report.Verdict);
        Assert.True(report.CanProceed);
        Assert.True(report.IsReadable);
        Assert.False(report.IsEncrypted);
        Assert.False(report.IsDamaged);
        Assert.False(report.HasDigitalSignature);
        Assert.True(report.AllFontsEmbedded);
        Assert.False(report.HasActiveContent);
        Assert.Equal(3, report.PageCount);
        Assert.False(report.HasExistingInvoice);
        Assert.Empty(report.EmbeddedFiles);
        Assert.NotEqual("unbekannt", report.PdfVersion);
        Assert.True(report.FileSizeInBytes > 0);
        Assert.False(report.Findings.HasErrors);
    }

    /// <summary>
    /// Eine nicht eingebettete Schrift versperrt den direkten Weg – aber nicht
    /// jeden. Sie führt deshalb nicht mehr zu einer Ablehnung, sondern zu einem
    /// Angebot.
    ///
    /// **Der Unterschied ist nicht kosmetisch.** Vorher stand dort ein roter
    /// Fehler mit dem Rat, die Datei im Ausgangsprogramm anders zu exportieren.
    /// Wer dieses Programm nicht mehr hat – ein Steuerberaterbeleg, eine
    /// Fremdrechnung –, war damit am Ende. Jetzt gibt es einen Weg, und der Satz
    /// beschreibt ihn, statt nur zu bedauern.
    /// </summary>
    [Fact]
    public async Task PdfMitNichtEingebetteterSchriftBekommtDenRasterwegAngeboten()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.RasterFallback, report.Route);
        Assert.Equal(PreflightVerdict.SuitableWithWarnings, report.Verdict);
        Assert.True(report.CanProceed);
        Assert.True(report.RequiresRasterFallback);
        Assert.False(report.AllFontsEmbedded);

        // Kein Fehler mehr – der Weg steht ja offen.
        Assert.False(report.Findings.HasErrors);

        ValidationFinding offer = Assert.Single(
            report.Findings.Findings,
            f => f.RuleId == "APP-PRE-011" && f.Severity == FindingSeverity.Warning);

        Assert.Contains("sichtbare PDF/A-Kopie", offer.Message, StringComparison.Ordinal);
        Assert.Contains("Original bleibt unverändert", offer.Message, StringComparison.Ordinal);

        // Was der Weg kostet, steht daneben – vor der Zustimmung, nicht danach.
        ValidationFinding cost = Assert.Single(
            report.Findings.Findings, f => f.RuleId == "APP-PRE-016");

        Assert.Contains("durchsuchbar", cost.Message, StringComparison.Ordinal);
        Assert.Contains("maschinenlesbar", cost.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kommt zur fehlenden Schrifteinbettung ein zweites Hindernis hinzu, gilt
    /// das strengere. Sonst wäre der Rasterweg eine Hintertür, durch die alles
    /// andere mit hindurchginge.
    /// </summary>
    [Fact]
    public async Task EinZweitesHindernisNimmtDemRasterwegDieGrundlage()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFontAndJavaScript());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.Rejected, report.Route);
        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        Assert.False(report.CanProceed);
        Assert.True(report.HasActiveContent);

        // Und die Schrift ist hier wieder ein Fehler, kein Angebot.
        FindError(report, "APP-PRE-011");
        FindError(report, "APP-PRE-012");
    }

    /// <summary>
    /// Eine eingescannte Seite wird nicht wegen einer Schrift abgelehnt, die
    /// kein Zeichen zeichnet.
    ///
    /// **Der Befund aus dem Windows-Testlauf.** Sehr viele Scanner und
    /// Ausgabetreiber schreiben eine Schriftressource in jede Seite, auch wenn
    /// darauf nur ein Bild liegt. Die Datei bekam dafür APP-PRE-011 – „nicht
    /// alle Schriftarten eingebettet“ – und der Anwender sollte etwas umstellen,
    /// das mit seiner Rechnung nichts zu tun hatte.
    ///
    /// Dass der direkte Weg für eine solche Datei wirklich zulässig ist, sagt
    /// nicht dieser Test, sondern veraPDF: siehe
    /// <c>EndToEndConformanceTests.EineEingescannteSeiteMitUnbenutzterSchriftBestehtDenDirektenWeg</c>.
    /// </summary>
    [Fact]
    public async Task EineEingescannteSeiteGehtDenDirektenWeg()
    {
        string path = Temp(TestPdfFactory.CreateScanOnlyPdf());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.Direct, report.Route);
        Assert.Equal(PreflightVerdict.Suitable, report.Verdict);
        Assert.True(report.AllFontsEmbedded);
        Assert.False(report.Findings.HasErrors);
    }

    /// <summary>
    /// Die Gegenprobe: Sobald eine nicht eingebettete Schrift wirklich Text
    /// zeichnet, bleibt es beim Hindernis – auch wenn daneben eine Bildseite
    /// steht.
    ///
    /// Ohne diesen Fall wäre die Lockerung oben eine Hintertür: „irgendwo ein
    /// Bild, also Schriftprüfung aus“.
    /// </summary>
    [Fact]
    public async Task GemischtesDokumentBleibtAmSchrifthindernisHängen()
    {
        string path = Temp(TestPdfFactory.CreateMixedScanAndTextPdf());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.False(report.AllFontsEmbedded);
        Assert.Equal(PdfProcessingRoute.RasterFallback, report.Route);
    }

    /// <summary>
    /// Text in einem Form-XObject zählt genauso. Der Seiteninhalt selbst
    /// enthält hier keinen einzigen Buchstaben – nur den Aufruf des Formulars.
    /// </summary>
    [Fact]
    public async Task TextInEinemFormularZähltAlsVerwendeteSchrift()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithTextInFormXObject());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.False(report.AllFontsEmbedded);
        Assert.Equal(PdfProcessingRoute.RasterFallback, report.Route);
    }

    /// <summary>
    /// Hängt ein fremder Anhang an der Datei, gibt es keine sichtbare Kopie.
    ///
    /// **Zwei Zusagen widersprachen sich.** Die Eingangsprüfung verspricht zu
    /// jedem Anhang „Er bleibt erhalten“; der Rasterweg baut ein neues Dokument
    /// und übernimmt ihn nicht. Wäre beides gleichzeitig in Kraft, verlöre der
    /// Anwender den Lieferschein, ohne dass ihm das jemand sagt – dem Ergebnis
    /// sieht man nicht an, was vorher daran hing.
    ///
    /// Für die erste Fassung fällt die Entscheidung zugunsten der Anhänge: kein
    /// Angebot statt stillem Verlust.
    /// </summary>
    [Fact]
    public async Task MitFremdemAnhangGibtEsKeineSichtbareKopie()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFontAndAttachment());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.Rejected, report.Route);
        Assert.False(report.CanProceed);

        ValidationFinding finding = FindError(report, "APP-PRE-017");
        Assert.Contains("lieferschein.txt", finding.Message, StringComparison.Ordinal);
        Assert.Contains("keine sichtbare Kopie", finding.Message, StringComparison.Ordinal);

        // Und die Zusage, der Anhang bleibe erhalten, steht hier nicht mehr:
        // Sie gilt nur, wo sie auch eingehalten wird.
        ValidationFinding anhang = Assert.Single(
            report.Findings.Findings, f => f.RuleId == "APP-PRE-021");

        Assert.DoesNotContain("bleibt erhalten", anhang.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Auf dem direkten Weg bleibt die Zusage bestehen – dort werden die Seiten
    /// des Originals samt allem, was daran hängt, übernommen.
    /// </summary>
    [Fact]
    public async Task AufDemDirektenWegBleibtDieZusageZumAnhangBestehen()
    {
        // Eine geeignete Datei mit Anhang: die fertige E-Rechnung von oben
        // trägt die Rechnungs-XML, die hier nicht zählt – deshalb eine Datei,
        // deren Anhang ein fremder ist und deren Schriften eingebettet sind.
        string path = Temp(TestPdfFactory.CreateSimplePdfWithAttachment());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.Direct, report.Route);

        ValidationFinding anhang = Assert.Single(
            report.Findings.Findings, f => f.RuleId == "APP-PRE-021");

        Assert.Contains("bleibt erhalten", anhang.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Zu tief verschachtelte Formulare gelten als nicht bestätigt, nicht als
    /// in Ordnung.
    ///
    /// Die Tiefengrenze schützt vor einer Datei, die es darauf anlegt. Öffnete
    /// sie nach außen – „zu tief, also Schriften in Ordnung“ –, wäre sie selbst
    /// der Weg daran vorbei.
    /// </summary>
    [Fact]
    public async Task ZuTiefVerschachtelteFormulareGeltenAlsNichtBestätigt()
    {
        string path = Temp(TestPdfFactory.CreateDeeplyNestedFormPdf(depth: 12));

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.False(report.AllFontsEmbedded);
    }

    /// <summary>
    /// Bis zur Grenze wird tatsächlich nachgesehen. Ohne diese Gegenprobe wäre
    /// der Test darüber wertlos: Er liefe auch dann grün, wenn der Abstieg gar
    /// nicht stattfände.
    /// </summary>
    [Fact]
    public async Task InnerhalbDerGrenzeWirdWirklichNachgesehen()
    {
        string path = Temp(TestPdfFactory.CreateDeeplyNestedFormPdf(depth: 3));

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        // Gefunden wird die Schrift am Ende der Kette – nicht die Tiefe.
        Assert.False(report.AllFontsEmbedded);
        Assert.Equal(PdfProcessingRoute.RasterFallback, report.Route);
    }

    /// <summary>
    /// Ein Besitzerkennwort ist kein Öffnungskennwort: Die Datei lässt sich
    /// lesen und darstellen. PDFsharp meldet sie deshalb als unverschlüsselt.
    /// Trotzdem wird sie nicht stillschweigend gerastert – der Rechteinhaber hat
    /// festgelegt, was mit ihr geschehen darf.
    /// </summary>
    [Fact]
    public async Task PdfMitBesitzerkennwortWirdNichtStillGerastert()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithOwnerPassword());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.Rejected, report.Route);
        Assert.False(report.CanProceed);
        Assert.True(report.IsEncrypted);

        ValidationFinding finding = FindError(report, "APP-PRE-015");

        Assert.Contains("Berechtigungseinschränkungen", finding.Message, StringComparison.Ordinal);
        Assert.Contains("angezeigt", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Besitzerkennwort", finding.TechnicalDetail!, StringComparison.Ordinal);

        // Die Datei ist nicht kaputt, und das darf ihr niemand nachsagen.
        Assert.DoesNotContain("beschädigt", finding.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(report.IsDamaged);
    }

    /// <summary>
    /// Auch die Erzeugung selbst nennt eine rechtebeschränkte Datei nicht
    /// „beschädigt“.
    ///
    /// **Der Befund aus dem Windows-Testlauf.** Die Datei kam bis in Schritt 4
    /// und scheiterte dort an PDFsharp: „owner password is required to modify
    /// the document“. Übersetzt bekam der Anwender „Sie ist vermutlich
    /// beschädigt“ – und machte sich auf die Suche nach einer heilen Datei, die
    /// er längst hatte. Die Eingangsprüfung fängt den Fall inzwischen vorher ab;
    /// dass auch die Erzeugung die Wahrheit sagt, steht hier.
    /// </summary>
    [Fact]
    public async Task DieErzeugungNenntEineRechtebeschränkteDateiNichtBeschädigt()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithOwnerPassword());

        InvoiceScenario scenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        var totals = InvoiceCalculator.Calculate(scenario.Invoice);

        CompositionResult composed = await _composer.ComposeAsync(
            new PdfACompositionRequest(
                path,
                _writer.Write(scenario.Invoice, totals),
                "Rechnung",
                scenario.Invoice.Seller.Name,
                "Rechnung",
                DateTimeOffset.UnixEpoch,
                _writer.Attachment),
            TestContext.Current.CancellationToken);

        Assert.False(composed.Succeeded);

        ValidationFinding finding = Assert.Single(
            composed.Report.Findings, f => f.Severity == FindingSeverity.Error);

        Assert.Equal("APP-PDF-006", finding.RuleId);
        Assert.Contains("Berechtigungseinschränkungen", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("beschädigt", finding.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verlangt die Datei zum Öffnen ein Kennwort, gibt es nichts darzustellen.
    /// Der Rasterweg kommt gar nicht erst in Betracht.
    /// </summary>
    [Fact]
    public async Task PdfMitÖffnungskennwortWirdSauberAbgelehnt()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithUserPassword());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PdfProcessingRoute.Rejected, report.Route);
        Assert.False(report.CanProceed);
        Assert.True(report.IsEncrypted);

        FindError(report, "APP-PRE-010");
    }

    [Fact]
    public async Task BeschädigtePdfIstNichtGeeignet()
    {
        string path = Temp(TestPdfFactory.CreateDamagedPdf());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        Assert.True(report.IsDamaged);

        ValidationFinding finding = FindError(report, "APP-PRE-013");
        Assert.Contains("neu", finding.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DateiOhnePdfInhaltWirdAbgelehntTrotzPdfEndung()
    {
        string path = Temp(TestPdfFactory.CreateNonPdf());

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);

        ValidationFinding finding = FindError(report, "APP-PRE-004");
        Assert.Contains("Dateiendung", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LeereDateiWirdAbgelehnt()
    {
        string path = Temp([]);

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        FindError(report, "APP-PRE-002");
    }

    [Fact]
    public async Task FehlendeDateiWirdAbgelehnt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"gibt-es-nicht-{Guid.NewGuid():N}.pdf");

        PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);
        FindError(report, "APP-PRE-001");
    }

    [Fact]
    public async Task ZuGroßeDateiWirdAbgelehntUndNenntEinenAusweg()
    {
        PdfPreflightService strict = PipelineParts.Preflight(
            PipelineParts.Analyzer(), maxFileSizeMegabytes: 1);

        // Eine Datei knapp über der Grenze.
        byte[] large = new byte[(1024 * 1024) + 1];
        "%PDF-1.4\n"u8.CopyTo(large);
        string path = Temp(large);

        PdfPreflightReport report = await strict.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);

        ValidationFinding finding = FindError(report, "APP-PRE-003");
        Assert.Contains("Bildauflösung", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BereitsHybridePdfIstGeeignetAberMitWarnung()
    {
        // Eine fertige E-Rechnung erzeugen und erneut prüfen.
        string sourcePath = Temp(TestPdfFactory.CreateSimplePdf());

        InvoiceScenario scenario = InvoiceScenarios.ByKey("01-dienstleistung-19");
        var totals = InvoiceCalculator.Calculate(scenario.Invoice);

        CompositionResult composed = await _composer.ComposeAsync(
            new PdfACompositionRequest(
                sourcePath,
                _writer.Write(scenario.Invoice, totals),
                "Rechnung",
                scenario.Invoice.Seller.Name,
                "Rechnung",
                DateTimeOffset.UnixEpoch,
                _writer.Attachment),
            TestContext.Current.CancellationToken);

        Assert.True(composed.Succeeded);

        string hybridPath = Temp(composed.PdfBytes!);
        PdfPreflightReport report = await _preflight.InspectAsync(hybridPath, TestContext.Current.CancellationToken);

        // Sie ist verarbeitbar – aber der Anwender wird ausdrücklich gewarnt.
        Assert.Equal(PreflightVerdict.SuitableWithWarnings, report.Verdict);
        Assert.True(report.CanProceed);
        Assert.True(report.HasExistingInvoice);
        Assert.Equal(CiiConstants.ProfileEn16931, report.ExistingInvoiceProfile);
        Assert.True(report.HasXmpMetadata);
        Assert.Equal("3B", report.DeclaredPdfALevel);

        ValidationFinding warning = Assert.Single(
            report.Findings.Findings,
            f => f.RuleId == "APP-PRE-020" && f.Severity == FindingSeverity.Warning);

        Assert.Contains("ersetzt", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreflightVerändertDasOriginalNicht()
    {
        byte[] original = TestPdfFactory.CreateSimplePdf();
        string path = Temp(original);

        await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);
        await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task JedeAblehnungNenntEineHandlungUndEinTechnischesDetail()
    {
        // Alle Ablehnungswege durchlaufen und die Meldungsqualität prüfen.
        string[] paths =
        [
            Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFontAndJavaScript()),
            Temp(TestPdfFactory.CreateDamagedPdf()),
            Temp(TestPdfFactory.CreateNonPdf()),
            Temp(TestPdfFactory.CreatePdfWithOwnerPassword()),
            Temp(TestPdfFactory.CreatePdfWithUserPassword()),
        ];

        foreach (string path in paths)
        {
            PdfPreflightReport report = await _preflight.InspectAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(PreflightVerdict.NotSuitable, report.Verdict);

            foreach (ValidationFinding finding in report.Findings.Findings
                         .Where(f => f.Severity == FindingSeverity.Error))
            {
                Assert.StartsWith("APP-PRE-", finding.RuleId, StringComparison.Ordinal);

                // Verständlicher deutscher Satz, kein bloßer Fehlercode.
                Assert.True(
                    finding.Message.Length > 40,
                    $"Meldung zu {finding.RuleId} ist zu knapp: {finding.Message}");
                Assert.EndsWith(".", finding.Message.TrimEnd(), StringComparison.Ordinal);
                Assert.NotNull(finding.TechnicalDetail);
            }
        }
    }

    private static ValidationFinding FindError(PdfPreflightReport report, string ruleId)
    {
        ValidationFinding? finding = report.Findings.Findings
            .FirstOrDefault(f => f.RuleId == ruleId && f.Severity == FindingSeverity.Error);

        Assert.True(
            finding is not null,
            $"Erwarteter Fehler {ruleId} fehlt. Gefunden: "
            + string.Join(", ", report.Findings.Findings.Select(f => $"{f.Severity} {f.RuleId}")));

        return finding!;
    }

    private string Temp(byte[] content)
    {
        string path = TestPdfFactory.WriteToTempFile(content);
        _temporaryFiles.Add(path);

        return path;
    }

    public void Dispose()
    {
        foreach (string path in _temporaryFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Aufräumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

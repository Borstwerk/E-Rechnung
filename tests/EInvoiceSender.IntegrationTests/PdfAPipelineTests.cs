using System.Text;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Formats.Cii;
using EInvoiceSender.Infrastructure.PdfA;
using Microsoft.Extensions.Logging.Abstractions;
using EInvoiceSender.TestSupport;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Prueft den vollstaendigen Weg von der Eingangs-PDF bis zur fertigen
/// ZUGFeRD-Datei und wieder zurueck.
///
/// Der entscheidende Nachweis steckt in
/// <see cref="ErzeugteDateiLaesstSichWiederOeffnenUndDieXmlExtrahieren"/>:
/// Die erzeugte Datei wird erneut geoeffnet, die XML herausgeholt und mit der
/// urspruenglich erzeugten verglichen. Erst damit ist belegt, dass die
/// Einbettung wirklich funktioniert hat.
/// </summary>
public sealed class PdfAPipelineTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly CiiInvoiceWriter _writer = new();
    private readonly CiiInvoiceReader _reader = new();
    private readonly PdfAnalyzer _analyzer;
    private readonly PdfAInvoiceComposer _composer;

    public PdfAPipelineTests()
    {
        _analyzer = new PdfAnalyzer(_reader, NullLogger<PdfAnalyzer>.Instance);
        _composer = new PdfAInvoiceComposer(_analyzer, NullLogger<PdfAInvoiceComposer>.Instance);
    }

    [Fact]
    public async Task EinfachePdfWirdAlsAufwertbarErkannt()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf(pageCount: 2));

        PdfAnalysisResult analysis = await _analyzer.AnalyzeAsync(path);

        Assert.True(analysis.CanBeUpgraded, "Eine reine Vektorgrafik-PDF muss aufwertbar sein.");
        Assert.Equal(2, analysis.PageCount);
        Assert.False(analysis.IsEncrypted);
        Assert.False(analysis.HasExistingInvoiceXml);
        Assert.Empty(analysis.UpgradeBlockers);
    }

    [Fact]
    public async Task PdfMitNichtEingebetteterSchriftWirdAbgelehnt()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        PdfAnalysisResult analysis = await _analyzer.AnalyzeAsync(path);

        Assert.Contains(PdfUpgradeBlocker.FontsNotEmbedded, analysis.UpgradeBlockers);
        Assert.False(analysis.CanBeUpgraded);
    }

    [Fact]
    public async Task NichtKonvertierbarePdfLiefertVerstaendlicheMeldungUndKeineDatei()
    {
        string path = Temp(TestPdfFactory.CreatePdfWithNonEmbeddedFont());

        CompositionResult result = await _composer.ComposeAsync(BuildRequest(path));

        Assert.False(result.Succeeded);
        Assert.Null(result.PdfBytes);
        Assert.True(result.Report.HasErrors);

        ValidationFinding finding = result.Report.Findings.First(f => f.Severity == FindingSeverity.Error);

        // Die Meldung muss dem Anwender sagen, was er tun kann – nicht nur,
        // dass etwas schiefging.
        Assert.Contains("Schriftarten", finding.Message, StringComparison.Ordinal);
        Assert.Contains("einbetten", finding.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(finding.TechnicalDetail);
    }

    [Fact]
    public async Task BeschaedigtePdfWirdAlsSolcheGemeldet()
    {
        string path = Temp(TestPdfFactory.CreateDamagedPdf());

        PdfAnalysisResult analysis = await _analyzer.AnalyzeAsync(path);

        Assert.False(analysis.CanBeUpgraded);
        Assert.NotEmpty(analysis.UpgradeBlockers);
    }

    [Fact]
    public async Task DateiOhnePdfSignaturWirdAbgewiesen()
    {
        string path = Temp(TestPdfFactory.CreateNonPdf(), ".pdf");

        // Die Endung allein reicht als Nachweis nicht aus.
        Assert.False(await _analyzer.LooksLikePdfAsync(path));
    }

    [Fact]
    public async Task ErzeugteDateiLaesstSichWiederOeffnenUndDieXmlExtrahieren()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf());
        byte[] originalBytes = await File.ReadAllBytesAsync(path);

        PdfACompositionRequest request = BuildRequest(path);
        CompositionResult result = await _composer.ComposeAsync(request);

        Assert.True(result.Succeeded, Describe(result.Report));
        Assert.NotNull(result.PdfBytes);

        // Das Original darf sich nicht veraendert haben.
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));

        string outputPath = Temp(result.PdfBytes);

        // Erneut oeffnen und die eingebettete XML herausholen.
        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(outputPath);

        Assert.True(reopened.HasExistingInvoiceXml, "Die eingebettete XML wurde nicht wiedergefunden.");
        Assert.Equal(CiiConstants.ProfileEn16931, reopened.ExistingInvoiceProfile);
        Assert.Equal("3", reopened.DeclaredPdfAPart);
        Assert.Equal("B", reopened.DeclaredPdfAConformance);

        EmbeddedFileInfo attachment = Assert.Single(
            reopened.EmbeddedFiles,
            f => f.FileName == CiiConstants.EmbeddedFileName);
        Assert.Equal("/Alternative", attachment.Relationship);
        Assert.Equal("text/xml", attachment.MimeType);

        // Die extrahierte XML muss byte-identisch mit der erzeugten sein.
        Assert.Equal(request.InvoiceXml, reopened.ExistingInvoiceXml);

        // Und sie muss sich erneut auswerten lassen.
        InvoiceEcho? echo = _reader.ReadEcho(reopened.ExistingInvoiceXml!);
        Assert.NotNull(echo);
        Assert.Equal("RE-2026-0001", echo.InvoiceNumber);
        Assert.Equal(1190.00m, echo.GrandTotal);
    }

    [Fact]
    public async Task BereitsHybridePdfWirdErkanntUndGemeldet()
    {
        // Erst eine Hybridrechnung erzeugen ...
        string sourcePath = Temp(TestPdfFactory.CreateSimplePdf());
        CompositionResult first = await _composer.ComposeAsync(BuildRequest(sourcePath));
        Assert.True(first.Succeeded, Describe(first.Report));

        // ... und diese dann erneut verarbeiten.
        string hybridPath = Temp(first.PdfBytes!);
        CompositionResult second = await _composer.ComposeAsync(BuildRequest(hybridPath));

        Assert.True(second.Succeeded);
        Assert.True(second.Report.HasWarnings);
        Assert.Contains(
            second.Report.Findings,
            f => f.RuleId == "APP-PDF-020" && f.Severity == FindingSeverity.Warning);
    }

    /// <summary>
    /// Legt die erzeugte Datei fuer die externe Gegenpruefung ab
    /// (build/validate-golden-masters.sh mit veraPDF und CEN-Schematron).
    /// Ohne diesen Schritt wuerde nur die eigene Pruefung die eigene Ausgabe
    /// bestaetigen – das waere kein Nachweis.
    /// </summary>
    [Fact]
    public async Task SchreibtErgebnisDateiFuerDieExterneGegenpruefung()
    {
        string path = Temp(TestPdfFactory.CreateSimplePdf());
        CompositionResult result = await _composer.ComposeAsync(BuildRequest(path));

        Assert.True(result.Succeeded, Describe(result.Report));

        string directory = Path.Combine(RepositoryRoot, "artifacts", "golden-masters", "valid");
        Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(
            Path.Combine(directory, "20-zugferd-ergebnis.pdf"), result.PdfBytes!);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                   && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                   ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
        }
    }

    [Fact]
    public void IccProfilHatGueltigenAufbau()
    {
        byte[] profile = SRgbIccProfile.GetBytes();

        Assert.True(profile.Length > 128, "Ein ICC-Profil hat mindestens einen Kopf von 128 Byte.");

        // Groesse im Kopf muss zur tatsaechlichen Groesse passen.
        int declaredSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(profile);
        Assert.Equal(profile.Length, declaredSize);

        // Pflichtkennung 'acsp' an Position 36.
        Assert.Equal("acsp", Encoding.ASCII.GetString(profile, 36, 4));
        Assert.Equal("mntr", Encoding.ASCII.GetString(profile, 12, 4));
        Assert.Equal("RGB ", Encoding.ASCII.GetString(profile, 16, 4));
        Assert.Equal("XYZ ", Encoding.ASCII.GetString(profile, 20, 4));

        // Reproduzierbarkeit: zwei Aufrufe liefern dieselben Bytes.
        Assert.Equal(profile, SRgbIccProfile.GetBytes());
    }

    [Fact]
    public void XmpPaketEnthaeltAllePflichtangaben()
    {
        byte[] xmp = XmpMetadataBuilder.Build(
            "Rechnung RE-1", "Musterbetrieb", "Rechnung", "EInvoiceSender",
            new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.FromHours(1)),
            CiiConstants.EmbeddedFileName);

        string text = Encoding.UTF8.GetString(xmp);

        Assert.Contains("<pdfaid:part>3</pdfaid:part>", text, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:conformance>B</pdfaid:conformance>", text, StringComparison.Ordinal);
        Assert.Contains(XmpMetadataBuilder.FacturXNamespace, text, StringComparison.Ordinal);
        Assert.Contains("<fx:DocumentType>INVOICE</fx:DocumentType>", text, StringComparison.Ordinal);
        Assert.Contains("<fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>", text, StringComparison.Ordinal);
        Assert.Contains("<fx:ConformanceLevel>EN 16931</fx:ConformanceLevel>", text, StringComparison.Ordinal);
        Assert.Contains("pdfaExtension:schemas", text, StringComparison.Ordinal);
        Assert.Contains("<?xpacket end=\"w\"?>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void XmpMaskiertSonderzeichenAusBenutzereingaben()
    {
        byte[] xmp = XmpMetadataBuilder.Build(
            "Rechnung <script>&amp;", "Meier & Sohn \"GmbH\"", "Test", "EInvoiceSender",
            DateTimeOffset.UnixEpoch, "factur-x.xml");

        string text = Encoding.UTF8.GetString(xmp);

        Assert.DoesNotContain("<script>", text, StringComparison.Ordinal);
        Assert.Contains("Meier &amp; Sohn", text, StringComparison.Ordinal);

        // Das Ergebnis muss trotz der Eingaben wohlgeformte XML bleiben.
        string packet = text[(text.IndexOf("<x:xmpmeta", StringComparison.Ordinal))..];
        packet = packet[..(packet.IndexOf("</x:xmpmeta>", StringComparison.Ordinal) + "</x:xmpmeta>".Length)];

        System.Xml.Linq.XDocument.Parse(packet);
    }

    /// <summary>Baut einen Erzeugungsauftrag mit der Standardtestrechnung.</summary>
    private PdfACompositionRequest BuildRequest(string sourcePath)
    {
        Invoice invoice = BuildInvoice();
        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);

        return new PdfACompositionRequest(
            SourcePdfPath: sourcePath,
            InvoiceXml: _writer.Write(invoice, totals),
            Title: $"Rechnung {invoice.InvoiceNumber}",
            Author: invoice.Seller.Name,
            Subject: $"Rechnung {invoice.InvoiceNumber} an {invoice.Buyer.Name}",
            CreationDate: new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1)),
            Attachment: _writer.Attachment);
    }

    private static Invoice BuildInvoice() => new()
    {
        InvoiceNumber = "RE-2026-0001",
        IssueDate = new DateOnly(2026, 3, 15),
        DueDate = new DateOnly(2026, 3, 29),
        DeliveryDate = new DateOnly(2026, 3, 14),
        Currency = CurrencyCode.Euro,
        Seller = new SellerParty(
            "Musterbetrieb Beispiel GmbH",
            new PostalAddress("Beispielweg 1", null, "10115", "Berlin", CountryCode.Germany),
            Email: "rechnung@example.invalid",
            VatId: "DE123456789"),
        Buyer = new BuyerParty(
            "Beispielkunde AG",
            new PostalAddress("Kundenstrasse 7", null, "20095", "Hamburg", CountryCode.Germany),
            Email: "einkauf@example.invalid"),
        Lines =
        [
            new InvoiceLine(1, "Beratungsleistung", 10m, UnitCode.Hour, 100m, VatCategory.StandardRate, 19m),
        ],
        Payment = new PaymentDetails(
            PaymentMeansCode.SepaCreditTransfer,
            new BankAccount("Musterbetrieb Beispiel GmbH", Iban.Parse("DE89370400440532013000")),
            "Zahlbar innerhalb von 14 Tagen."),
    };

    private static string Describe(ValidationReport report)
        => string.Join("; ", report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));

    private string Temp(byte[] content, string extension = ".pdf")
    {
        string path = TestPdfFactory.WriteToTempFile(content, extension);
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
                // Aufraeumen darf einen Testlauf nicht scheitern lassen.
            }
        }
    }
}

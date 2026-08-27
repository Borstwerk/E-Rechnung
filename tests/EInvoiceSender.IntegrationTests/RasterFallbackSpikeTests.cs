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
/// Der technische Versuch: Lässt sich eine PDF, die den direkten Weg zu Recht
/// nicht gehen darf, über ein Rastern der Seiten trotzdem in eine gültige
/// PDF/A-3b-ZUGFeRD-Datei überführen?
///
/// **Was hier bewiesen werden soll und was nicht.** Diese Tests belegen, dass
/// der Rasterweg technisch trägt: Seitenzahl, Seitenformat und Ausrichtung
/// bleiben erhalten, das Original bleibt unangetastet, die Rechnungs-XML kommt
/// unverändert wieder heraus. Sie belegen **nicht**, dass das Ergebnis
/// normgerecht ist. Diesen Nachweis kann nur ein fremdes Werkzeug führen, und
/// er steht in <see cref="DerExterneValidatorBestätigtDenRasterweg"/>: veraPDF
/// für PDF/A-3b, CEN-Schematron für EN 16931. Ohne ihn ist der Versuch
/// gescheitert, gleichgültig wie grün alles andere leuchtet.
///
/// Die erzeugten Dateien werden zusätzlich unter
/// <c>artifacts/spike-raster</c> abgelegt, damit sie außerhalb des Testlaufs
/// nachgeprüft und angesehen werden können.
/// </summary>
[Collection(ExternalValidatorTestGroup.Name)]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class RasterFallbackSpikeTests(ExternalValidatorFixture external) : IDisposable
{
    private readonly ExternalValidatorFixture _external = external;
    private readonly List<string> _temporaryFiles = [];
    private readonly CiiInvoiceWriter _writer = new();
    private readonly CiiInvoiceReader _reader = new();
    private readonly PdfAnalyzer _analyzer = new(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);
    private readonly RasterizedPdfBuilder _raster = new(NullLogger<RasterizedPdfBuilder>.Instance);

    /// <summary>
    /// Die Voraussetzung des ganzen Versuchs – und der Test, der ihn wertlos
    /// machen würde, wenn er nicht hielte.
    ///
    /// Die Testrechnung ist mit der Standardschrift Helvetica gesetzt, und
    /// Standardschriften werden nicht eingebettet. Der direkte Weg **muss**
    /// sie deshalb ablehnen. Täte er es nicht, bewiese der Rasterweg an dieser
    /// Datei überhaupt nichts: Er würde ein Problem lösen, das es nicht gibt.
    /// </summary>
    [Fact]
    public async Task DerDirekteWegLehntDieTestrechnungWegenDerSchriftAb()
    {
        string path = Temp(TestInvoice.CreatePdf());

        PdfAnalysisResult analysis = await _analyzer.AnalyzeAsync(path);

        Assert.Contains(PdfUpgradeBlocker.FontsNotEmbedded, analysis.UpgradeBlockers);
        Assert.False(analysis.CanBeUpgraded);

        var composer = new PdfAInvoiceComposer(_analyzer, NullLogger<PdfAInvoiceComposer>.Instance);
        CompositionResult direct = await composer.ComposeAsync(BuildRequest(path));

        Assert.False(direct.Succeeded);
        Assert.Null(direct.PdfBytes);
    }

    /// <summary>
    /// Der Kern des Versuchs: dieselbe Datei, über den Rasterweg.
    /// </summary>
    [Fact]
    public void DerRasterwegLiefertEineDateiFürGenauDieseRechnung()
    {
        string path = Temp(TestInvoice.CreatePdf());

        RasterizedPdfResult result = _raster.Build(BuildRequest(path));

        Assert.Equal(1, result.PageCount);
        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal(RasterizedPdfBuilder.DefaultDpi, result.Dpi);
    }

    [Fact]
    public void DieSeitenzahlBleibtGleich()
    {
        string path = Temp(TestPdfFactory.CreateMultiPagePdf(pageCount: 4));

        RasterizedPdfResult result = _raster.Build(BuildRequest(path), dpi: 150);

        Assert.Equal(4, result.PageCount);
        Assert.Equal(4, OpenPageCount(result.PdfBytes));
    }

    /// <summary>
    /// Seitenformat und Ausrichtung müssen erhalten bleiben. Eine Rechnung,
    /// die als A4 quer erstellt wurde, darf nicht als Hochformat mit weißen
    /// Rändern herauskommen.
    /// </summary>
    [Fact]
    public void SeitengrößeUndAusrichtungBleibenErhalten()
    {
        string path = Temp(TestPdfFactory.CreateMixedPageSizesPdf());

        RasterizedPdfResult result = _raster.Build(BuildRequest(path), dpi: 150);

        Assert.Equal(3, result.PageCount);

        Assert.False(result.Pages[0].IsLandscape);
        Assert.True(result.Pages[1].IsLandscape);

        Assert.Equal(595, result.Pages[0].WidthInPoints, 0.5);
        Assert.Equal(842, result.Pages[0].HeightInPoints, 0.5);
        Assert.Equal(842, result.Pages[1].WidthInPoints, 0.5);
        Assert.Equal(595, result.Pages[1].HeightInPoints, 0.5);

        // Und dasselbe noch einmal an der fertigen Datei, nicht nur am Bericht.
        double[] widths = [.. OpenPageSizes(result.PdfBytes).Select(p => p.Width)];
        Assert.Equal(595, widths[0], 1.0);
        Assert.Equal(842, widths[1], 1.0);
    }

    /// <summary>
    /// Die dritte Seite der Vorlage ist im Datenmodell hoch und trägt
    /// <c>/Rotate 90</c>; angezeigt wird sie quer. Genau so muss sie
    /// herauskommen.
    ///
    /// PDFium meldet die **sichtbare** Größe, rechnet die Drehung also bereits
    /// ein. Die neue Seite übernimmt diese Größe und braucht selbst kein
    /// <c>/Rotate</c> mehr. Wer stattdessen die MediaBox des Originals nähme,
    /// bekäme ein Hochformat mit quer hineingestauchtem Inhalt.
    /// </summary>
    [Fact]
    public void DieGedrehteSeiteBehältIhrSichtbaresFormat()
    {
        string path = Temp(TestPdfFactory.CreateMixedPageSizesPdf());

        RasterizedPdfResult result = _raster.Build(BuildRequest(path), dpi: 150);

        RasterizedPageInfo gedreht = result.Pages[2];

        Assert.True(
            gedreht.IsLandscape,
            "Eine um 90 Grad gedrehte Hochformatseite wird quer angezeigt und muss quer bleiben.");
        Assert.Equal(842, gedreht.WidthInPoints, 0.5);
        Assert.Equal(595, gedreht.HeightInPoints, 0.5);

        // Das Bild muss dieselbe Ausrichtung haben wie die Seite – sonst wäre
        // es verzerrt oder beschnitten.
        Assert.True(gedreht.PixelWidth > gedreht.PixelHeight);
    }

    /// <summary>
    /// Das Seitenverhältnis des Bildes muss zu dem der Seite passen. Weicht es
    /// ab, wird beim Einpassen gestaucht oder beschnitten – beides fällt an
    /// einer Rechnung sofort auf.
    /// </summary>
    [Fact]
    public void DasSeitenverhältnisDesBildesPasstZurSeite()
    {
        string path = Temp(TestPdfFactory.CreateMixedPageSizesPdf());

        RasterizedPdfResult result = _raster.Build(BuildRequest(path), dpi: 150);

        Assert.All(result.Pages, page => Assert.Equal(
            page.WidthInPoints / page.HeightInPoints,
            (double)page.PixelWidth / page.PixelHeight,
            0.01));
    }

    /// <summary>Das Original wird gelesen und sonst nichts.</summary>
    [Fact]
    public void DasOriginalBleibtByteIdentisch()
    {
        byte[] original = TestInvoice.CreatePdf();
        string path = Temp(original);

        _ = _raster.Build(BuildRequest(path), dpi: 150);

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    /// <summary>
    /// Der zweite Kernnachweis: Die eingebettete XML muss sich unverändert
    /// wieder herausholen lassen. Ohne das wäre die Datei ein Bild mit einem
    /// Anhang, aber keine E-Rechnung.
    /// </summary>
    [Fact]
    public async Task DieRechnungsXmlKommtByteIdentischWiederHeraus()
    {
        string path = Temp(TestInvoice.CreatePdf());
        PdfACompositionRequest request = BuildRequest(path);

        RasterizedPdfResult result = _raster.Build(request, dpi: 150);
        string outputPath = Temp(result.PdfBytes);

        PdfAnalysisResult reopened = await _analyzer.AnalyzeAsync(outputPath);

        Assert.True(reopened.HasReadableExistingInvoiceXml);
        Assert.Equal(request.InvoiceXml, reopened.ExistingInvoiceXml);
        Assert.Equal(CiiConstants.ProfileEn16931, reopened.ExistingInvoiceProfile);
        Assert.Equal("3", reopened.DeclaredPdfAPart);
        Assert.Equal("B", reopened.DeclaredPdfAConformance);

        EmbeddedFileInfo attachment = Assert.Single(
            reopened.EmbeddedFiles, f => f.FileName == CiiConstants.EmbeddedFileName);
        Assert.Equal("/Alternative", attachment.Relationship);
        Assert.Equal("text/xml", attachment.MimeType);

        InvoiceEcho? echo = _reader.ReadEcho(reopened.ExistingInvoiceXml!);
        Assert.NotNull(echo);
        Assert.Equal("RE-2026-0815", echo.InvoiceNumber);
        Assert.Equal(TestInvoice.Gross, echo.GrandTotal);
    }

    /// <summary>
    /// Die gerasterte Ausgabe enthält keinen Text mehr. Das ist keine
    /// Nebenwirkung, sondern der Kern des Verfahrens – und der Preis dafür.
    /// Der Test hält ihn fest, damit niemand später Textsuche verspricht.
    /// </summary>
    [Fact]
    public async Task ImSichtbarenDokumentStehtKeinTextMehr()
    {
        string path = Temp(TestInvoice.CreatePdf());
        var extractor = new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance);

        PdfTextResult ausOriginal = await extractor.ExtractAsync(path);

        RasterizedPdfResult result = _raster.Build(BuildRequest(path), dpi: 150);
        PdfTextResult ausRaster = await extractor.ExtractAsync(Temp(result.PdfBytes));

        Assert.True(ausOriginal.HasUsableText);
        Assert.Contains(TestInvoice.InvoiceNumber, ausOriginal.FullText, StringComparison.Ordinal);

        // Genau deshalb läuft die Erkennung auf dem Original und niemals auf
        // der Ausgabe: Aus dieser Datei ist nichts mehr zu holen.
        Assert.False(
            ausRaster.HasUsableText,
            "Die gerasterte Ausgabe enthält keinen Text mehr – das ist der Preis des Verfahrens.");
        Assert.DoesNotContain(TestInvoice.InvoiceNumber, ausRaster.FullText, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Das Freigabegate.** veraPDF prüft PDF/A-3b, das CEN-Schematron prüft
    /// EN 16931. Beide laufen über die Mustang-CLI, beide vollständig örtlich.
    /// Kein eigener Test darf diesen Nachweis ersetzen.
    /// </summary>
    [Theory]
    [InlineData("rasterweg-testrechnung", 300)]
    [InlineData("rasterweg-mehrseitig", 200)]
    [InlineData("rasterweg-seitenformate", 200)]
    public async Task DerExterneValidatorBestätigtDenRasterweg(string name, int dpi)
    {
        IExternalDocumentValidator validator = _external.RequireValidator();

        byte[] quelle = name switch
        {
            "rasterweg-mehrseitig" => TestPdfFactory.CreateMultiPagePdf(pageCount: 3),
            "rasterweg-seitenformate" => TestPdfFactory.CreateMixedPageSizesPdf(),
            _ => TestInvoice.CreatePdf(),
        };

        string path = Temp(quelle);
        RasterizedPdfResult result = _raster.Build(BuildRequest(path), dpi);

        string outputPath = WriteArtifact($"{name}.pdf", result.PdfBytes);

        ValidationReport report = await validator.ValidateAsync(outputPath);

        Assert.False(
            report.HasErrors,
            "Der externe Validator hat die gerasterte Datei beanstandet: "
            + string.Join("; ", report.Findings.Select(f => $"{f.RuleId}: {f.Message}")));
    }

    /// <summary>
    /// Legt für den Sichtvergleich und die Messreihe je eine Datei je
    /// Auflösung ab. Kein Prüfschritt – eine Zulieferung an den Bericht.
    /// </summary>
    [Theory]
    [InlineData(150)]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(400)]
    public void SchreibtDieMessreiheFürDenBericht(int dpi)
    {
        string einseitig = Temp(TestInvoice.CreatePdf());
        string mehrseitig = Temp(TestPdfFactory.CreateMultiPagePdf(pageCount: 5));
        string dichtBedruckt = Temp(TextPdfBuilder.Create(DichteRechnungszeilen(150)));

        WriteArtifact($"messreihe-einseitig-{dpi}dpi.pdf", _raster.Build(BuildRequest(einseitig), dpi).PdfBytes);
        WriteArtifact($"messreihe-mehrseitig-{dpi}dpi.pdf", _raster.Build(BuildRequest(mehrseitig), dpi).PdfBytes);
        WriteArtifact($"messreihe-dicht-{dpi}dpi.pdf", _raster.Build(BuildRequest(dichtBedruckt), dpi).PdfBytes);
    }

    /// <summary>
    /// Eine dicht bedruckte Rechnung über mehrere Seiten.
    ///
    /// Die schlichte mehrseitige Vorlage trägt je Seite eine Zeile und einen
    /// Rahmen; ihre Dateigröße sagt über eine echte Rechnung wenig aus, weil
    /// eine fast leere Seite sich nahezu beliebig gut zusammendrücken lässt.
    /// Für eine belastbare Zahl je Seite braucht die Messreihe eine Vorlage,
    /// die wie eine wirklich lange Rechnung aussieht.
    /// </summary>
    private static string[] DichteRechnungszeilen(int count) =>
    [
        "Muster IT GmbH – Sammelrechnung",
        "Pos Bezeichnung                       Menge Einheit Einzelpreis Betrag",
        .. Enumerable.Range(1, count).Select(i => string.Create(
            System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            $"{i,3} Leistungsposition mit längerer Bezeichnung {i,6:N2} Std {85.50m,10:N2} {i * 85.50m,10:N2}")),
    ];

    /// <summary>
    /// Legt die Eingangsdateien daneben ab – ohne sie ist kein Sichtvergleich
    /// möglich. Sie liegen bewusst in einem Unterverzeichnis: Sie sind nicht
    /// normgerecht, und die Gegenprüfung darf sie nicht als Ergebnis ansehen.
    /// </summary>
    [Fact]
    public void SchreibtDieVorlagenFürDenSichtvergleich()
    {
        WriteArtifact(Path.Combine("quellen", "testrechnung.pdf"), TestInvoice.CreatePdf());
        WriteArtifact(Path.Combine("quellen", "seitenformate.pdf"), TestPdfFactory.CreateMixedPageSizesPdf());
    }

    private static int OpenPageCount(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        using PdfSharp.Pdf.PdfDocument document =
            PdfSharp.Pdf.IO.PdfReader.Open(stream, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);

        return document.PageCount;
    }

    private static IEnumerable<(double Width, double Height)> OpenPageSizes(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        using PdfSharp.Pdf.PdfDocument document =
            PdfSharp.Pdf.IO.PdfReader.Open(stream, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);

        return [.. document.Pages.Cast<PdfSharp.Pdf.PdfPage>()
            .Select(p => (p.Width.Point, p.Height.Point))];
    }

    private string WriteArtifact(string fileName, byte[] content)
    {
        string path = Path.Combine(_external.RepositoryRoot, "artifacts", "spike-raster", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);

        return path;
    }

    /// <summary>
    /// Baut den Erzeugungsauftrag mit den Werten der Testrechnung, damit die
    /// eingebettete XML zur sichtbaren Seite passt.
    /// </summary>
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
            CreationDate: new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.FromHours(2)),
            Attachment: _writer.Attachment);
    }

    private static Invoice BuildInvoice() => new()
    {
        InvoiceNumber = TestInvoice.InvoiceNumber,
        IssueDate = TestInvoice.IssueDate,
        DueDate = TestInvoice.DueDate,
        DeliveryDate = TestInvoice.DeliveryDate,
        Currency = CurrencyCode.Euro,
        Seller = new SellerParty(
            "Muster IT GmbH",
            new PostalAddress("Musterstraße 10", null, "18055", "Rostock", CountryCode.Germany),
            Email: "rechnung@example.invalid",
            VatId: "DE123456789"),
        Buyer = new BuyerParty(
            TestInvoice.BuyerName,
            new PostalAddress(
                "Hafenstraße 22", null, TestInvoice.BuyerPostalCode,
                TestInvoice.BuyerCity, CountryCode.Germany),
            Email: "einkauf@example.invalid"),
        Lines =
        [
            new InvoiceLine(1, "Systemadministration", 4m, UnitCode.Hour, 85m, VatCategory.StandardRate, 19m),
            new InvoiceLine(2, "Netzwerkbetreuung", 2m, UnitCode.Hour, 95m, VatCategory.StandardRate, 19m),
            new InvoiceLine(3, "Sicherungskonzept", 1m, UnitCode.Piece, 70m, VatCategory.StandardRate, 19m),
        ],
        Payment = new PaymentDetails(
            PaymentMeansCode.SepaCreditTransfer,
            new BankAccount("Muster IT GmbH", Iban.Parse(TestInvoice.Iban)),
            "Zahlbar innerhalb von 14 Tagen ohne Abzug."),
    };

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

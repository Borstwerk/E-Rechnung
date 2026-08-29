using System.Text;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Checking;
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
/// Wann der Prüfmodus einen Anhang tatsächlich entpackt – und wann nicht.
/// </summary>
/// <remarks>
/// <para>
/// <b>Das Problem, um das es geht.</b> Einen Anhang zu entpacken kostet
/// Speicher in der Größe des <i>entpackten</i> Inhalts, und die bestimmt die
/// fremde Datei. Nachgemessen an PDFsharp 6.2.4: Eine PDF-Datei von 67 KB
/// entfaltet einen Anhang auf 64 MiB, Verhältnis 1:1029. Die Größenangabe des
/// Datenstroms hilft nicht – sie nennt die komprimierte Größe. Und
/// <c>OutOfMemoryException</c> fängt der PDF-Leser bewusst nicht ab.
/// </para>
/// <para>
/// Zwei Zusagen folgen daraus, und beide werden hier gemessen: Es wird nur
/// entpackt, was tatsächlich ausgewertet wird, und dieses eine nur bis zur
/// Grenze.
/// </para>
/// </remarks>
public sealed class AttachmentMaterialisationTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "BorstWerk-Material-" + Guid.NewGuid().ToString("N"));

    public AttachmentMaterialisationTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    /// <summary>
    /// Fall 1: Neben der Rechnung liegt ein großer gewöhnlicher Anhang. Er
    /// wird für die Prüfung nicht gebraucht und darf deshalb nicht entpackt
    /// werden.
    /// </summary>
    [Fact]
    public async Task EinGroßerFremdanhangWirdNichtEntpackt()
    {
        byte[] gross = new byte[4 * 1024 * 1024];
        Array.Fill(gross, (byte)'X');

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "mit-beilage.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", GültigeXml()),
            new AttachedPdfFactory.Attachment("beilage.bin", gross, Compress: true));

        var spion = new MitschreibenderLeser(NeueAnalyse());
        CheckEInvoiceResult ergebnis = await Prüfe(pfad, spion);

        Assert.True(ergebnis.Completed);
        Assert.NotNull(ergebnis.InvoiceSummary);

        // Der eigentliche Nachweis: genau ein Anhang wurde angefordert.
        Assert.Equal(["factur-x.xml"], spion.Angefordert);
    }

    /// <summary>
    /// Fall 2: Bei mehreren Rechnungsanhängen bricht die Prüfung ab – und
    /// zwar, bevor irgendein Inhalt entpackt wird. Die Mehrdeutigkeit steht
    /// schon an den Namen fest.
    /// </summary>
    [Fact]
    public async Task BeiMehrdeutigkeitWirdKeinInhaltEntpackt()
    {
        byte[] xml = GültigeXml();

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "mehrdeutig.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", xml),
            new AttachedPdfFactory.Attachment("zugferd-invoice.xml", xml));

        var spion = new MitschreibenderLeser(NeueAnalyse());
        CheckEInvoiceResult ergebnis = await Prüfe(pfad, spion);

        Assert.Contains(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.AmbiguousInvoiceAttachments);
        Assert.Empty(spion.Angefordert);
    }

    /// <summary>
    /// Fall 3: Ein Fremdformat wird an seinem Namen erkannt. Sein Inhalt wird
    /// weder entpackt noch geparst – wir werten ihn ohnehin nicht aus.
    /// </summary>
    [Fact]
    public async Task EinNichtUnterstütztesFormatWirdNichtEntpackt()
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "fremdformat.pdf",
            new AttachedPdfFactory.Attachment(
                "xrechnung.xml", Encoding.UTF8.GetBytes("<Invoice><ID>RE-1</ID></Invoice>")));

        var spion = new MitschreibenderLeser(NeueAnalyse());
        CheckEInvoiceResult ergebnis = await Prüfe(pfad, spion);

        Assert.Contains(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.UnsupportedInvoiceFormat);
        Assert.Empty(spion.Angefordert);
    }

    /// <summary>
    /// **Fall 4, der wichtigste.** Ein Rechnungsanhang, der sich weit über die
    /// Grenze entfaltet, muss einen ordentlichen Befund ergeben – nicht einen
    /// Prozessabbruch.
    ///
    /// Die Datei auf der Platte bleibt dabei winzig; genau das macht den Fall
    /// gefährlich. Eine Prüfung, die erst entpackt und dann die Länge ansieht,
    /// hätte hier bereits 64 MiB belegt.
    /// </summary>
    [Fact]
    public async Task EinÜbergroßerRechnungsanhangWirdBegrenztAbgewiesen()
    {
        byte[] bombe = new byte[64 * 1024 * 1024];
        Array.Fill(bombe, (byte)'A');

        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "bombe.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", bombe, Compress: true));

        long aufDerPlatte = new FileInfo(pfad).Length;

        // Die Ausgangslage selbst ist Teil der Aussage: kleine Datei, großer
        // Inhalt. Ohne diese Zusicherung liefe der Test irgendwann gegen eine
        // harmlose Datei und niemandem fiele es auf.
        Assert.True(
            aufDerPlatte < 1024 * 1024,
            $"Die Testdatei ist mit {aufDerPlatte} Bytes zu groß – der Fall bildet dann keine "
            + "Entfaltung mehr ab.");

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        Assert.Null(ergebnis.InvoiceSummary);

        ValidationFinding befund = Assert.Single(
            ergebnis.Report.Findings, f => f.RuleId == CheckRuleIds.InvoiceXmlTooLarge);

        // **Der Befund allein genügt nicht.** Auch ein Weg, der erst 64 MiB
        // entpackt und danach misst, käme zu derselben Kennung – und genau
        // den soll dieser Test ausschließen. Belegt wird deshalb, dass die
        // Grenze schon beim Entpacken gegriffen hat.
        Assert.Contains(
            "an der Grenze abgebrochen",
            befund.TechnicalDetail ?? string.Empty,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Gegenprobe zu Fall 4: Ein komprimierter Anhang <b>unter</b> der
    /// Grenze wird ganz normal gelesen. Ohne sie könnte die Grenze auch bei
    /// null liegen und der Test wäre trotzdem grün.
    /// </summary>
    [Fact]
    public async Task EinKomprimierterRechnungsanhangUnterDerGrenzeWirdGelesen()
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "gepackt.pdf",
            new AttachedPdfFactory.Attachment("factur-x.xml", GültigeXml(), Compress: true));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.True(ergebnis.Completed);
        Assert.NotNull(ergebnis.InvoiceSummary);
        Assert.Equal(CiiConstants.ProfileEn16931, ergebnis.InvoiceSummary!.ProfileId);
    }

    /// <summary>
    /// Der begrenzte Leser gibt die Grenze nicht auf, nur weil ein Anhang ein
    /// Verfahren verwendet, das er nicht sicher entpacken kann. Er lehnt ab –
    /// und sagt, woran es lag.
    /// </summary>
    [Fact]
    public async Task EinNichtBegrenzbarGepackterAnhangWirdAbgelehnt()
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, "fremdfilter.pdf",
            new AttachedPdfFactory.Attachment(
                "factur-x.xml", GültigeXml(), Compress: true, WithDecodeParms: true));

        CheckEInvoiceResult ergebnis = await Prüfe(pfad);

        Assert.False(ergebnis.Completed);
        ValidationFindingIst(ergebnis, CheckRuleIds.InvoiceXmlUnsupportedFilter);
    }

    // ------------------------------------------------------------ Hilfsmittel

    private static void ValidationFindingIst(CheckEInvoiceResult ergebnis, string kennung)
        => Assert.Contains(ergebnis.Report.Findings, f => f.RuleId == kennung);

    private static byte[] GültigeXml()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");

        return new CiiInvoiceWriter().Write(
            szenario.Invoice, InvoiceCalculator.Calculate(szenario.Invoice));
    }

    private static PdfAnalyzer NeueAnalyse()
        => new(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);

    private static Task<CheckEInvoiceResult> Prüfe(string pfad, IPdfAttachmentReader? leser = null)
    {
        PdfAnalyzer analyse = NeueAnalyse();

        var dienst = new EInvoiceCheckService(
            analyse,
            leser ?? analyse,
            new CiiInvoiceReader(),
            NullLogger<EInvoiceCheckService>.Instance);

        return dienst.CheckAsync(new CheckEInvoiceRequest(pfad));
    }

    /// <summary>
    /// Reicht an den echten Leser durch und schreibt mit, welche Anhänge
    /// überhaupt angefordert wurden.
    ///
    /// Das ist der belastbarste Nachweis, der sich von außen führen lässt:
    /// Was nie angefordert wird, kann auch nicht entpackt worden sein.
    /// </summary>
    private sealed class MitschreibenderLeser(IPdfAttachmentReader inner) : IPdfAttachmentReader
    {
        private readonly List<string> _angefordert = [];

        public IReadOnlyList<string> Angefordert => _angefordert;

        public Task<EmbeddedFileReadResult> ReadEmbeddedFileAsync(
            string filePath, string fileName, int maxBytes, CancellationToken cancellationToken = default)
        {
            _angefordert.Add(fileName);

            Assert.Equal(SecureXml.MaxXmlSizeInBytes, maxBytes);

            return inner.ReadEmbeddedFileAsync(filePath, fileName, maxBytes, cancellationToken);
        }
    }
}

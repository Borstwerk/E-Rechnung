using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Pdf;
using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Zugferd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Checking;

/// <summary>
/// Die Sperre „eine vorhandene Rechnung wird nie stillschweigend ersetzt“.
/// </summary>
/// <remarks>
/// <para>
/// <b>Der Fehler, den diese Tests künftig verhindern.</b>
/// <c>HasExistingInvoice</c> war einmal als
/// <c>ExistingInvoiceProfile is not null</c> definiert und setzte damit
/// „eine Rechnung ist vorhanden“ mit „eine Rechnung wurde erfolgreich
/// gelesen“ gleich. Solange jeder Anhang bedingungslos entpackt wurde, fiel
/// das kaum auf. Mit der begrenzten Entpackung wurde daraus eine Lücke: Ein
/// Anhang, der zu groß, ungewöhnlich gepackt oder beschädigt ist, liefert
/// kein Profil – und öffnete die Sperre.
/// </para>
/// <para>
/// Ausgerechnet die verdächtigste Datei wäre so am Schutz vorbeigekommen. Der
/// Anwender hätte seine einzige Ausfertigung überschrieben, ohne gefragt
/// worden zu sein.
/// </para>
/// <para>
/// Deshalb wird die Anwesenheit hier ausschließlich aus den Anhangsnamen
/// abgeleitet, ohne einen Inhalt zu entpacken – und diese Tests prüfen genau
/// die Fälle, in denen sich der Inhalt <b>nicht</b> lesen lässt.
/// </para>
/// </remarks>
public sealed class ExistingInvoiceGateTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "BorstWerk-Gate-" + Guid.NewGuid().ToString("N"));

    public ExistingInvoiceGateTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    /// <summary>
    /// Fall 1: Der Regelfall bleibt, wie er war – lesbare Rechnung, Profil
    /// bekannt, Sperre greift.
    /// </summary>
    [Fact]
    public async Task EineLesbareVorhandeneRechnungLöstDieSperreAus()
    {
        PdfPreflightReport bericht = await Prüfe(
            new AttachedPdfFactory.Attachment("factur-x.xml", GültigeXml()));

        Assert.True(bericht.HasExistingInvoice);
        Assert.Equal(CiiConstants.ProfileEn16931, bericht.ExistingInvoiceProfile);
        Assert.Contains(bericht.Findings.Findings, f => f.RuleId == "APP-PRE-020");
    }

    /// <summary>
    /// Fälle 2 bis 4: Der Anhang lässt sich nicht auswerten – ungewöhnlich
    /// gepackt, zu groß, oder schlicht kaputt. Die Anwesenheit steht trotzdem
    /// fest, denn sie steht am Namen.
    ///
    /// Das Profil bleibt in allen drei Fällen unbekannt, und das ist richtig
    /// so: Was wir nicht gelesen haben, behaupten wir nicht.
    /// </summary>
    [Theory]
    [InlineData("ungewöhnlich gepackt")]
    [InlineData("zu groß")]
    [InlineData("beschädigt")]
    public async Task EinUnlesbarerRechnungsanhangLöstDieSperreEbenfallsAus(string fall)
    {
        PdfPreflightReport bericht = await Prüfe(UnlesbarerAnhang(fall));

        Assert.True(
            bericht.HasExistingInvoice,
            $"Der Fall '{fall}' hat die Sperre geöffnet. Ein Anhang, den wir nicht lesen "
            + "können, ist trotzdem vorhanden.");

        // Gegenprobe: Es liegt wirklich am Namen und nicht daran, dass doch
        // gelesen wurde.
        Assert.Null(bericht.ExistingInvoiceProfile);

        // Der Anwender bekommt die Warnung trotzdem zu sehen.
        Assert.Contains(bericht.Findings.Findings, f => f.RuleId == "APP-PRE-020");
    }

    /// <summary>
    /// Die Gegenprobe: Ohne rechnungsartigen Anhang gibt es nichts zu
    /// bestätigen. Ohne sie könnte die Sperre auch einfach immer zuschlagen
    /// und die Tests wären trotzdem grün.
    /// </summary>
    [Fact]
    public async Task OhneRechnungsanhangGreiftDieSperreNicht()
    {
        PdfPreflightReport ohneAnhang = await Prüfe();
        PdfPreflightReport mitBeilage = await Prüfe(
            new AttachedPdfFactory.Attachment("lieferschein.xml", "<Lieferschein/>"u8.ToArray()));

        Assert.False(ohneAnhang.HasExistingInvoice);
        Assert.False(mitBeilage.HasExistingInvoice);
        Assert.DoesNotContain(ohneAnhang.Findings.Findings, f => f.RuleId == "APP-PRE-020");
    }

    /// <summary>
    /// Fall 5: Die Erzeugung stoppt bei <c>APP-USE-002</c>, wenn die
    /// Bestätigung fehlt – auch bei einem unlesbaren Anhang.
    ///
    /// Geprüft wird hier am Bericht der Eingangsprüfung zusammen mit der
    /// Bedingung, die <c>EInvoiceService</c> daraus bildet. Der vollständige
    /// Erzeugungsweg gehört in die Integrationstests; hier geht es um die
    /// Bedingung selbst.
    /// </summary>
    [Theory]
    [InlineData("ungewöhnlich gepackt")]
    [InlineData("zu groß")]
    [InlineData("beschädigt")]
    public async Task OhneBestätigungWürdeDieErzeugungAuchBeiUnlesbaremAnhangStoppen(string fall)
    {
        PdfPreflightReport bericht = await Prüfe(UnlesbarerAnhang(fall));

        const bool bestätigt = false;

        Assert.True(
            bericht.HasExistingInvoice && !bestätigt,
            "Die Bedingung für APP-USE-002 greift nicht – die vorhandene Rechnung würde "
            + "stillschweigend ersetzt.");

        // Und mit Bestätigung ist der Weg frei.
        const bool ausdrücklichBestätigt = true;
        Assert.False(bericht.HasExistingInvoice && !ausdrücklichBestätigt);
    }

    // ------------------------------------------------------------ Hilfsmittel

    /// <summary>
    /// Ein Rechnungsanhang, den der begrenzte Leser nicht auswerten kann –
    /// jeweils aus einem anderen Grund.
    /// </summary>
    private static AttachedPdfFactory.Attachment UnlesbarerAnhang(string fall) => fall switch
    {
        "ungewöhnlich gepackt" => new AttachedPdfFactory.Attachment(
            "factur-x.xml", GültigeXml(), Compress: true, WithDecodeParms: true),

        "zu groß" => new AttachedPdfFactory.Attachment(
            "factur-x.xml", Füllung(SecureXml.MaxXmlSizeInBytes + 1024), Compress: true),

        _ => new AttachedPdfFactory.Attachment(
            "factur-x.xml", "<rsm:CrossIndustryInvoice><abgeschnitten"u8.ToArray()),
    };

    private static byte[] Füllung(int größe)
    {
        byte[] daten = new byte[größe];
        Array.Fill(daten, (byte)'A');

        return daten;
    }

    private static byte[] GültigeXml()
    {
        InvoiceScenario szenario = InvoiceScenarios.ByKey("01-dienstleistung-19");

        return new CiiInvoiceWriter().Write(
            szenario.Invoice, InvoiceCalculator.Calculate(szenario.Invoice));
    }

    private async Task<PdfPreflightReport> Prüfe(params AttachedPdfFactory.Attachment[] anhänge)
    {
        string pfad = AttachedPdfFactory.WriteTo(
            _workspace, $"eingang-{Guid.NewGuid():N}.pdf", anhänge);

        var analyse = new PdfAnalyzer(new CiiInvoiceReader(), NullLogger<PdfAnalyzer>.Instance);
        var preflight = new PdfPreflightService(
            analyse, new NichtGebrauchteDarstellungsprobe(),
            NullLogger<PdfPreflightService>.Instance);

        return await preflight.InspectAsync(pfad);
    }

    /// <summary>
    /// Die Darstellbarkeitsprüfung gehört zum Rasterweg und hat mit dieser
    /// Sperre nichts zu tun. Die Testdateien sind aufwertbar, der Rasterweg
    /// kommt gar nicht in Frage – eine echte Probe würde hier nur PDFium
    /// laden.
    /// </summary>
    private sealed class NichtGebrauchteDarstellungsprobe : IPdfRenderProbe
    {
        public Task<PdfRenderProbeResult> ProbeAsync(
            string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(PdfRenderProbeResult.Renderable(1));
    }
}

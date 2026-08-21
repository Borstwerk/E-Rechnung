using System.Text;
using System.Xml.Linq;
using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using EInvoiceSender.Core.Validation;
using EInvoiceSender.Core.Zugferd;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Regression für das aus Rechnungsdatum und Standardzahlungsziel abgeleitete
/// Fälligkeitsdatum.
///
/// Die Ableitung darf nur so lange mitlaufen, wie das Fälligkeitsdatum ein
/// Programm- oder Vorlagenstandard ist. Eine PDF-Erkennung oder bewusste
/// Benutzereingabe beendet die automatische Nachführung.
/// </summary>
public sealed class DueDateDerivationTests
{
    private const string PaymentTerms = "Zahlbar innerhalb von 14 Tagen ohne Abzug.";

    [Fact]
    public void PdfRechnungsdatumBerechnetDieVorlagenfälligkeitNeu()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        Assert.Equal(new DateOnly(2026, 9, 3), draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));

        DraftPrefiller.Apply(draft, Detection(issueDate: new DateOnly(2026, 6, 9)));

        Assert.Equal(new DateOnly(2026, 6, 9), draft.IssueDate);
        Assert.Equal(FieldOrigin.DetectedUncertain, draft.OriginOf(nameof(draft.IssueDate)));
        Assert.Equal(new DateOnly(2026, 6, 23), draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void ManuellesRechnungsdatumVerschiebtEineAutomatischeFälligkeit()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        draft.IssueDate = new DateOnly(2026, 6, 10);

        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.IssueDate)));
        Assert.Equal(new DateOnly(2026, 6, 24), draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void ErkanntesFälligkeitsdatumGewinntUndBleibtGeschützt()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        DraftPrefiller.Apply(
            draft,
            Detection(
                issueDate: new DateOnly(2026, 6, 9),
                dueDate: new DateOnly(2026, 7, 1)));

        Assert.Equal(new DateOnly(2026, 7, 1), draft.DueDate);
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.DueDate)));

        DraftPrefiller.Apply(draft, Detection(issueDate: new DateOnly(2026, 6, 10)));
        CompanyTemplateApplier.Apply(draft, Template with { DefaultPaymentTermDays = 0 });
        draft.IssueDate = null;

        Assert.Equal(new DateOnly(2026, 7, 1), draft.DueDate);
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void ManuellesFälligkeitsdatumBleibtBeiDatumsUndVorlagenänderungenStehen()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        draft.DueDate = new DateOnly(2026, 7, 15);
        draft.IssueDate = new DateOnly(2026, 6, 10);
        CompanyTemplateApplier.Apply(draft, Template with { DefaultPaymentTermDays = 0 });
        draft.IssueDate = null;

        Assert.Equal(new DateOnly(2026, 7, 15), draft.DueDate);
        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void ManuellGeleerteFälligkeitWirdNichtWiederErzeugt()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        draft.DueDate = null;
        draft.IssueDate = new DateOnly(2026, 6, 10);
        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Null(draft.DueDate);
        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void RechnungsdatumLöschenUndWiederSetzenFührtDieAutomatischeFälligkeitFort()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        draft.IssueDate = null;

        Assert.Null(draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));

        draft.IssueDate = new DateOnly(2026, 6, 9);

        Assert.Equal(new DateOnly(2026, 6, 23), draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NichtPositivesZahlungszielEntferntNurDieAutomatischeFälligkeit(int days)
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        IReadOnlyList<string> changed = CompanyTemplateApplier.Apply(
            draft,
            Template with { DefaultPaymentTermDays = days });

        Assert.Null(draft.DueDate);
        Assert.Contains(nameof(draft.DueDate), changed);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));

        draft.IssueDate = new DateOnly(2026, 6, 9);

        Assert.Null(draft.DueDate);
    }

    [Fact]
    public void SpätestesRechnungsdatumMitZahlungszielBleibtOhneAutomatischeFälligkeit()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        Exception? exception = Record.Exception(() => draft.IssueDate = DateOnly.MaxValue);

        Assert.Null(exception);
        Assert.Null(draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void SehrGroßesZahlungszielBleibtOhneAutomatischeFälligkeit()
    {
        var draft = new InvoiceDraft();
        draft.Prefill(d => d.IssueDate = new DateOnly(2026, 6, 9));

        Exception? exception = Record.Exception(
            () => CompanyTemplateApplier.Apply(
                draft,
                Template with { DefaultPaymentTermDays = int.MaxValue }));

        Assert.Null(exception);
        Assert.Null(draft.DueDate);
        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void ResetVerwirftDieAlteAbleitungsinformation()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        draft.Reset();
        draft.IssueDate = new DateOnly(2026, 6, 9);

        Assert.Null(draft.DueDate);
        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.DueDate)));

        CompanyTemplateApplier.Apply(draft, Template);

        Assert.Equal(new DateOnly(2026, 6, 23), draft.DueDate);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void ZahlungsbedingungstextBleibtBeiDerNeuberechnungUnverändert()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));

        draft.IssueDate = new DateOnly(2026, 6, 9);

        Assert.Equal(PaymentTerms, draft.PaymentTerms);
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.PaymentTerms)));
    }

    [Fact]
    public void AbgeleiteteFälligkeitGelangtAlsBt9InDieCii()
    {
        InvoiceDraft draft = DraftWithAutomaticDueDate(new DateOnly(2026, 8, 20));
        DraftPrefiller.Apply(draft, Detection(issueDate: new DateOnly(2026, 6, 9)));
        FillRequiredInvoiceData(draft);

        ValidationReport report = draft.TryBuildInvoice(out Invoice? invoice);

        Assert.False(report.HasErrors, Describe(report));
        Assert.NotNull(invoice);
        Assert.Equal(new DateOnly(2026, 6, 23), invoice.DueDate);

        byte[] cii = new CiiInvoiceWriter().Write(
            invoice,
            InvoiceCalculator.Calculate(invoice));
        XDocument document = XDocument.Parse(Encoding.UTF8.GetString(cii));
        XNamespace ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
        XNamespace udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

        XElement dueDate = Assert.Single(
            document.Descendants(ram + "DueDateDateTime")
                .Elements(udt + "DateTimeString"));

        Assert.Equal("102", dueDate.Attribute("format")?.Value);
        Assert.Equal("20260623", dueDate.Value);
    }

    private static CompanyTemplate Template { get; } = new()
    {
        DefaultPaymentTerms = PaymentTerms,
        DefaultPaymentTermDays = 14,
    };

    private static InvoiceDraft DraftWithAutomaticDueDate(DateOnly issueDate)
    {
        var draft = new InvoiceDraft();

        draft.Prefill(d => d.IssueDate = issueDate);
        CompanyTemplateApplier.Apply(draft, Template);

        return draft;
    }

    private static InvoiceDetectionResult Detection(DateOnly issueDate, DateOnly? dueDate = null)
        => new()
        {
            HasUsableText = true,
            IssueDate = new DetectedValue<DateOnly>(issueDate, DetectionConfidence.Medium),
            DueDate = dueDate is { } value
                ? new DetectedValue<DateOnly>(value, DetectionConfidence.High)
                : null,
        };

    private static void FillRequiredInvoiceData(InvoiceDraft draft)
    {
        draft.InvoiceNumber = "RE-2026-0001";
        draft.SellerName = "Musterbetrieb Beispiel GmbH";
        draft.SellerStreet = "Beispielweg 1";
        draft.SellerPostalCode = "10115";
        draft.SellerCity = "Berlin";
        draft.SellerVatId = "DE123456789";
        draft.BuyerName = "Beispielkunde AG";
        draft.BuyerStreet = "Kundenstraße 7";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerCountry = "DE";

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Beratungsleistung";
        line.Quantity = "10";
        line.Unit = "HUR";
        line.NetUnitPrice = "100,00";
        line.VatRate = "19";
    }

    private static string Describe(ValidationReport report)
        => string.Join(" | ", report.Findings.Select(f => $"{f.RuleId}: {f.Message}"));
}

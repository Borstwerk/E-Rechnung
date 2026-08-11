using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Regression für die Trennung zwischen echten Firmenstammdaten und
/// gespeicherten Komfortvorgaben.
/// </summary>
public sealed class TemplateDefaultPriorityTests
{
    private static CompanyTemplate Defaults { get; } = new()
    {
        DefaultCurrency = "EUR",
        DefaultPaymentTerms = "Zahlbar innerhalb von 15 Tagen ohne Abzug.",
        DefaultPaymentTermDays = 15,
    };

    [Fact]
    public void PdfErkennungErsetztGespeicherteStandardwerte()
    {
        var draft = new InvoiceDraft { IssueDate = new DateOnly(2026, 8, 10) };

        CompanyTemplateApplier.Apply(draft, Defaults);

        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.Currency)));
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.PaymentTerms)));
        Assert.Equal(FieldOrigin.TemplateDefault, draft.OriginOf(nameof(draft.DueDate)));
        Assert.Equal(new DateOnly(2026, 8, 25), draft.DueDate);

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Currency = new DetectedValue<string>("USD", DetectionConfidence.High),
            DueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 24), DetectionConfidence.High),
        });

        Assert.Equal("USD", draft.Currency);
        Assert.Equal(new DateOnly(2026, 8, 24), draft.DueDate);
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.Currency)));
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void GespeicherteStandardwerteÜberschreibenKeinePdfErkennung()
    {
        var draft = new InvoiceDraft { IssueDate = new DateOnly(2026, 8, 10) };

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Currency = new DetectedValue<string>("USD", DetectionConfidence.High),
            DueDate = new DetectedValue<DateOnly>(new DateOnly(2026, 8, 24), DetectionConfidence.High),
        });

        CompanyTemplateApplier.Apply(draft, Defaults);

        Assert.Equal("USD", draft.Currency);
        Assert.Equal(new DateOnly(2026, 8, 24), draft.DueDate);
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.Currency)));
        Assert.Equal(FieldOrigin.DetectedReliably, draft.OriginOf(nameof(draft.DueDate)));
    }

    [Fact]
    public void EigeneStammdatenBleibenStärkerAlsPdfErkennung()
    {
        var draft = new InvoiceDraft();
        var template = new CompanyTemplate { SellerName = "Muster IT GmbH" };

        CompanyTemplateApplier.Apply(draft, template);

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            Seller = new DetectedParty
            {
                Name = new DetectedValue<string>("Fremde AG", DetectionConfidence.High),
            },
        });

        Assert.Equal("Muster IT GmbH", draft.SellerName);
        Assert.Equal(FieldOrigin.Template, draft.OriginOf(nameof(draft.SellerName)));
    }
}

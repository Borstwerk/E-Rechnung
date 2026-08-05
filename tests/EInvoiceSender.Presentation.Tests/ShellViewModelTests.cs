using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Application.UseCases;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Presentation.Editing;
using EInvoiceSender.Presentation.ViewModels;
using EInvoiceSender.Validation.Rules;
using Xunit;

namespace EInvoiceSender.Presentation.Tests;

/// <summary>
/// Prueft den Ablauf der Oberflaeche.
///
/// Diese Tests laufen bewusst ohne WPF – die gesamte Ablauflogik liegt im
/// plattformneutralen Projekt <c>EInvoiceSender.Presentation</c>. Damit ist der
/// Weg durch die fuenf Schritte auch auf einem Linux-Agenten pruefbar, und nicht
/// erst auf einem Windows-Rechner mit Bildschirm.
/// </summary>
public sealed class ShellViewModelTests
{
    [Fact]
    public void OhneAusgewaehltePdfIstDerWegNachVornGesperrt()
    {
        using ShellViewModel viewModel = BuildViewModel();

        Assert.Equal(WizardStep.SelectPdf, viewModel.CurrentStep);
        Assert.False(viewModel.HasPdf);
        Assert.False(viewModel.GoForwardCommand.CanExecute(null));
    }

    [Fact]
    public void OhneBestaetigungIstDieErzeugungGesperrt()
    {
        using ShellViewModel viewModel = BuildViewModel();

        Assert.False(viewModel.ContentMatchConfirmed);
        Assert.False(viewModel.GenerateCommand.CanExecute(null));

        viewModel.ContentMatchConfirmed = true;

        Assert.True(viewModel.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void AusDerKontrollansichtGehtEsNurMitBestaetigungWeiter()
    {
        using ShellViewModel viewModel = BuildViewModel();
        viewModel.CurrentStep = WizardStep.Review;

        Assert.False(viewModel.GoForwardCommand.CanExecute(null));

        viewModel.GoForward();
        Assert.Equal(WizardStep.Review, viewModel.CurrentStep);

        viewModel.ContentMatchConfirmed = true;
        viewModel.GoForward();

        Assert.Equal(WizardStep.Generate, viewModel.CurrentStep);
    }

    [Fact]
    public void VorlageFuelltDieVerkaeuferfelderUndDasZahlungsziel()
    {
        using ShellViewModel viewModel = BuildViewModel();
        viewModel.Draft.IssueDate = new DateOnly(2026, 3, 15);

        viewModel.ApplyTemplate(new CompanyTemplate
        {
            SellerName = "Musterbetrieb Beispiel GmbH",
            SellerStreet = "Beispielweg 1",
            SellerPostalCode = "10115",
            SellerCity = "Berlin",
            SellerEmail = "rechnung@example.invalid",
            SellerVatId = "DE123456789",
            BankAccountHolder = "Musterbetrieb Beispiel GmbH",
            BankIban = "DE89370400440532013000",
            DefaultPaymentTermDays = 14,
            DefaultEmailSubject = "Ihre Rechnung",
            LastOutputDirectory = "/tmp/ausgabe",
        });

        Assert.Equal("Musterbetrieb Beispiel GmbH", viewModel.Draft.SellerName);
        Assert.Equal("DE123456789", viewModel.Draft.SellerVatId);
        Assert.Equal(new DateOnly(2026, 3, 29), viewModel.Draft.DueDate);
        Assert.Equal("Ihre Rechnung", viewModel.EmailSubject);
        Assert.Equal("/tmp/ausgabe", viewModel.OutputDirectory);
    }

    [Fact]
    public void SummenWerdenAusDenPositionenBerechnet()
    {
        using ShellViewModel viewModel = BuildViewModel();
        FillValidDraft(viewModel.Draft);

        viewModel.RecalculateTotals();

        Assert.NotNull(viewModel.Totals);
        Assert.Equal(1000.00m, viewModel.Totals.LineTotal);
        Assert.Equal(190.00m, viewModel.Totals.TaxTotal);
        Assert.Equal(1190.00m, viewModel.Totals.GrandTotal);
    }

    [Fact]
    public void UnlesbareEingabeErzeugtEinenVerstaendlichenBefundStattEinerAusnahme()
    {
        using ShellViewModel viewModel = BuildViewModel();
        FillValidDraft(viewModel.Draft);

        viewModel.Draft.Lines[0].NetUnitPrice = "hundert Euro";

        Assert.False(viewModel.ValidateData());

        FindingViewModel finding = Assert.Single(
            viewModel.Findings, f => f.Finding.RuleId == "APP-EDT-022");

        Assert.Contains("keine gueltige Zahl", finding.Message, StringComparison.Ordinal);
        Assert.Contains("APP-EDT-022", finding.TechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void UngueltigeIbanWirdMaskiertGemeldet()
    {
        using ShellViewModel viewModel = BuildViewModel();
        FillValidDraft(viewModel.Draft);

        viewModel.Draft.BankIban = "DE89370400440532013001";

        Assert.False(viewModel.ValidateData());

        FindingViewModel finding = Assert.Single(
            viewModel.Findings, f => f.Finding.RuleId == "APP-EDT-030");

        // Die vollstaendige IBAN darf nicht im Klartext im Detail stehen.
        Assert.DoesNotContain(
            "DE89370400440532013001", finding.TechnicalDetail, StringComparison.Ordinal);
        Assert.Contains("*", finding.TechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void GueltigeDatenWerdenNichtBeanstandet()
    {
        using ShellViewModel viewModel = BuildViewModel();
        FillValidDraft(viewModel.Draft);

        Assert.True(viewModel.ValidateData(), Describe(viewModel));
        Assert.DoesNotContain(viewModel.Findings, f => f.Severity == FindingSeverity.Error);
    }

    [Fact]
    public void BefundeWerdenNachSchweregradSortiertUndNichtNurFarblichGekennzeichnet()
    {
        using ShellViewModel viewModel = BuildViewModel();
        FillValidDraft(viewModel.Draft);

        viewModel.Draft.InvoiceNumber = string.Empty;
        viewModel.Draft.SellerCity = string.Empty;

        viewModel.ValidateData();

        Assert.NotEmpty(viewModel.Findings);
        Assert.Equal(FindingSeverity.Error, viewModel.Findings[0].Severity);

        foreach (FindingViewModel finding in viewModel.Findings)
        {
            // Jeder Befund traegt Wort und Zeichen, nicht nur eine Farbe.
            Assert.NotEmpty(finding.SeverityLabel);
            Assert.NotEmpty(finding.SeverityGlyph);
        }
    }

    [Fact]
    public void PositionenWerdenFortlaufendNummeriert()
    {
        using ShellViewModel viewModel = BuildViewModel();

        InvoiceLineDraft first = viewModel.Draft.AddLine();
        InvoiceLineDraft second = viewModel.Draft.AddLine();
        InvoiceLineDraft third = viewModel.Draft.AddLine();

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.Equal(3, third.Number);

        viewModel.Draft.Lines.Remove(second);
        viewModel.Draft.RenumberLines();

        Assert.Equal(1, viewModel.Draft.Lines[0].Number);
        Assert.Equal(2, viewModel.Draft.Lines[1].Number);
        Assert.Equal(third, viewModel.Draft.Lines[1]);
    }

    [Theory]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("-5,00", -5.0)]
    [InlineData("0", 0.0)]
    public void BetraegeWerdenMitKommaUndPunktGelesen(string input, double expected)
    {
        Assert.True(InvoiceDraft.TryParseDecimal(input, out decimal result));
        Assert.Equal((decimal)expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    public void UnlesbareBetraegeWerdenAbgelehnt(string input)
        => Assert.False(InvoiceDraft.TryParseDecimal(input, out _));

    [Fact]
    public void SchritttitelNennenImmerDieSchrittnummer()
    {
        using ShellViewModel viewModel = BuildViewModel();

        foreach (WizardStep step in Enum.GetValues<WizardStep>())
        {
            viewModel.CurrentStep = step;

            Assert.Contains("Schritt", viewModel.StepTitle, StringComparison.Ordinal);
            Assert.Contains("von 5", viewModel.StepTitle, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------ Aufbau

    private static ShellViewModel BuildViewModel()
        => new(
            new StubPreflight(),
            new En16931RuleValidator(),
            new StubUseCase(),
            new StubEmailDraftService(),
            new StubSettingsStore());

    private static void FillValidDraft(InvoiceDraft draft)
    {
        draft.InvoiceNumber = "RE-2026-0001";
        draft.IssueDate = new DateOnly(2026, 3, 15);
        draft.DueDate = new DateOnly(2026, 3, 29);
        draft.SellerName = "Musterbetrieb Beispiel GmbH";
        draft.SellerStreet = "Beispielweg 1";
        draft.SellerPostalCode = "10115";
        draft.SellerCity = "Berlin";
        draft.SellerVatId = "DE123456789";
        draft.SellerEmail = "rechnung@example.invalid";
        draft.BuyerName = "Beispielkunde AG";
        draft.BuyerStreet = "Kundenstrasse 7";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerEmail = "einkauf@example.invalid";
        draft.BankAccountHolder = "Musterbetrieb Beispiel GmbH";
        draft.BankIban = "DE89370400440532013000";
        draft.PaymentTerms = "Zahlbar innerhalb von 14 Tagen.";

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Beratungsleistung";
        line.Quantity = "10";
        line.Unit = "HUR";
        line.NetUnitPrice = "100,00";
        line.VatRate = "19";
    }

    private static string Describe(ShellViewModel viewModel)
        => string.Join(
            " | ",
            viewModel.Findings
                .Where(f => f.Severity == FindingSeverity.Error)
                .Select(f => $"{f.Finding.RuleId}: {f.Message}"));
}

/// <summary>Eingangspruefung ohne Dateizugriff.</summary>
internal sealed class StubPreflight : IPdfPreflightService
{
    public Task<PdfPreflightReport> InspectAsync(
        string filePath, CancellationToken cancellationToken = default)
        => Task.FromResult(new PdfPreflightReport(
            PreflightVerdict.Suitable, filePath, Path.GetFileName(filePath), 1024,
            true, false, false, false, true, false, "1.7", 1, [], null, false, null,
            ValidationReport.Empty));
}

/// <summary>Mailentwurf ohne Dateizugriff.</summary>
internal sealed class StubEmailDraftService : IEmailDraftService
{
    public string Name => "Test";

    public Task<EmailDraftResult> CreateDraftAsync(
        EmailDraft draft, CancellationToken cancellationToken = default)
        => Task.FromResult(new EmailDraftResult(true, "/tmp/test.eml", null, "ok"));

    public Uri BuildMailtoUri(EmailDraft draft) => new("mailto:test@example.invalid");
}

/// <summary>Einstellungen ohne Dateizugriff.</summary>
internal sealed class StubSettingsStore : ISettingsStore
{
    public bool SupportsProtectedStorage => false;

    public Task<CompanyTemplate> LoadTemplateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new CompanyTemplate());

    public Task SaveTemplateAsync(
        CompanyTemplate companyTemplate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<ApplicationSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ApplicationSettings());

    public Task SaveSettingsAsync(
        ApplicationSettings settings, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Anwendungsfall ohne Dateizugriff.</summary>
internal sealed class StubUseCase : ICreateEInvoiceUseCase
{
    public Task<CreateEInvoiceResult> ExecuteAsync(
        CreateEInvoiceRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new CreateEInvoiceResult(
            Succeeded: false,
            OutputFile: null,
            ReportJsonFile: null,
            ReportTextFile: null,
            Report: ValidationReport.Empty,
            CompletedSteps: [],
            CreatedAt: DateTimeOffset.UnixEpoch,
            StandardDescription: "Test",
            ProfileId: "urn:cen.eu:en16931:2017",
            Validators: []));
}

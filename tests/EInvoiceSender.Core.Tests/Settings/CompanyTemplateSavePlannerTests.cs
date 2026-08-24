using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using Xunit;

namespace EInvoiceSender.Core.Tests.Settings;

/// <summary>
/// Sichert die enge Datenbewegung vom Rechnungsentwurf in die Firmenvorlage.
/// Der wichtigste Nachweis sind die Negativfälle: PDF- und Käuferdaten dürfen
/// selbst in gemischten Entwürfen nicht in den Kandidaten gelangen.
/// </summary>
public sealed class CompanyTemplateSavePlannerTests
{
    private static readonly string[] ExpectedAllowlist =
    [
        nameof(InvoiceDraft.SellerName),
        nameof(InvoiceDraft.SellerStreet),
        nameof(InvoiceDraft.SellerPostalCode),
        nameof(InvoiceDraft.SellerCity),
        nameof(InvoiceDraft.SellerCountry),
        nameof(InvoiceDraft.SellerEmail),
        nameof(InvoiceDraft.SellerVatId),
        nameof(InvoiceDraft.SellerTaxNumber),
        nameof(InvoiceDraft.SellerLegalRegistrationId),
        nameof(InvoiceDraft.BankAccountHolder),
        nameof(InvoiceDraft.BankIban),
        nameof(InvoiceDraft.BankBic),
    ];

    [Fact]
    public void AllowlistIstVollständigUndEnthältKeineRechnungsOderKäuferfelder()
    {
        Assert.Equal(ExpectedAllowlist, CompanyTemplateSavePlanner.AllowedFields);
        Assert.DoesNotContain(
            CompanyTemplateSavePlanner.AllowedFields,
            field => field.StartsWith("Buyer", StringComparison.Ordinal));
        Assert.DoesNotContain(nameof(InvoiceDraft.InvoiceNumber), CompanyTemplateSavePlanner.AllowedFields);
        Assert.DoesNotContain(nameof(InvoiceDraft.PaymentTerms), CompanyTemplateSavePlanner.AllowedFields);
    }

    [Fact]
    public void ManuelleUnternehmensdatenWerdenFeldweiseÜbernommen()
    {
        var draft = new InvoiceDraft
        {
            SellerName = "  BorstWerk GmbH  ",
            SellerStreet = "Werkstraße 7",
            SellerPostalCode = "18055",
            SellerCity = "Rostock",
            SellerEmail = "rechnung@borstwerk.example",
            SellerVatId = "DE123456789",
            BankAccountHolder = "BorstWerk GmbH",
            BankIban = "DE89 3704 0044 0532 0130 00",
            BankBic = "COBADEFFXXX",
        };

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.True(plan.CanSave);
        Assert.False(plan.RequiresConfirmation);
        Assert.Equal("BorstWerk GmbH", plan.Candidate.SellerName);
        Assert.Equal("DE", plan.Candidate.SellerCountry);
        Assert.Equal("DE89 3704 0044 0532 0130 00", plan.Candidate.BankIban);
    }

    [Fact]
    public void ErkannteVerkäuferUndBankdatenWerdenNiemalsNeuGespeichert()
    {
        var draft = new InvoiceDraft();
        Detect(draft, nameof(draft.SellerName), () => draft.SellerName = "PDF-FREMDFIRMA-MARKER");
        Detect(draft, nameof(draft.SellerStreet), () => draft.SellerStreet = "PDF-STRAßE-MARKER");
        Detect(draft, nameof(draft.SellerVatId), () => draft.SellerVatId = "PDF-STEUER-MARKER");
        Detect(draft, nameof(draft.BankIban), () => draft.BankIban = "PDF-IBAN-MARKER");
        Detect(draft, nameof(draft.BankBic), () => draft.BankBic = "PDF-BIC-MARKER");

        draft.SellerTaxNumber = "MANUELL-123";

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());
        string candidate = string.Join('|', CompanyValues(plan.Candidate));

        Assert.DoesNotContain("PDF-", candidate, StringComparison.Ordinal);
        Assert.Null(plan.Candidate.SellerName);
        Assert.Null(plan.Candidate.BankIban);
        Assert.Equal("MANUELL-123", plan.Candidate.SellerTaxNumber);
    }

    [Fact]
    public void ZuverlässigUndUnsicherErkannteWerteSindGleichStrengAusgeschlossen()
    {
        var draft = new InvoiceDraft { SellerName = "Manuelle Firma", SellerTaxNumber = "123" };
        draft.Prefill(d => d.BankIban = "ERKANNT-ZUVERLAESSIG");
        draft.MarkOrigin(nameof(draft.BankIban), FieldOrigin.DetectedReliably);
        draft.Prefill(d => d.BankBic = "ERKANNT-UNSICHER");
        draft.MarkOrigin(nameof(draft.BankBic), FieldOrigin.DetectedUncertain);

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.Null(plan.Candidate.BankIban);
        Assert.Null(plan.Candidate.BankBic);
    }

    [Fact]
    public void KonkretesSellerProposalErlaubtNurSeineUnverändertenFelder()
    {
        var draft = new InvoiceDraft();
        Detect(draft, nameof(draft.SellerName), () => draft.SellerName = "ERKANNTE EIGENE FIRMA",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerStreet), () => draft.SellerStreet = "Werkstraße 7",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerPostalCode), () => draft.SellerPostalCode = "18055",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerCity), () => draft.SellerCity = "Rostock",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerVatId), () => draft.SellerVatId = "DE123456789",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerEmail), () => draft.SellerEmail = "NICHT-IM-PROPOSAL@example.test",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.BankIban), () => draft.BankIban = "DE89370400440532013000");
        Detect(draft, nameof(draft.BankBic), () => draft.BankBic = "NICHT-IM-PROPOSAL");
        draft.BuyerName = "KÄUFER-MARKER";

        var proposal = new DetectedOwnCompanyProposal
        {
            Fields =
            [
                Proposed(DetectedOwnCompanyFieldKind.SellerName, draft.SellerName),
                Proposed(DetectedOwnCompanyFieldKind.SellerStreet, draft.SellerStreet),
                Proposed(DetectedOwnCompanyFieldKind.SellerPostalCode, draft.SellerPostalCode),
                Proposed(DetectedOwnCompanyFieldKind.SellerCity, draft.SellerCity),
                Proposed(DetectedOwnCompanyFieldKind.SellerVatId, draft.SellerVatId),
                Proposed(
                    DetectedOwnCompanyFieldKind.BankIban,
                    draft.BankIban,
                    DetectionConfidence.High),
            ],
        };
        string before = DraftSnapshot(draft);

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(
            draft, new CompanyTemplate(), proposal);

        Assert.True(plan.HasProposalInput);
        Assert.True(plan.CanSave);
        Assert.Equal("ERKANNTE EIGENE FIRMA", plan.Candidate.SellerName);
        Assert.Equal("DE89370400440532013000", plan.Candidate.BankIban);
        Assert.Null(plan.Candidate.SellerEmail);
        Assert.Null(plan.Candidate.BankBic);
        Assert.DoesNotContain("KÄUFER", string.Join('|', CompanyValues(plan.Candidate)),
            StringComparison.Ordinal);
        Assert.Equal(before, DraftSnapshot(draft));
    }

    [Fact]
    public void GegenüberProposalVeränderterErkannterWertWirdNichtÜbernommen()
    {
        var draft = new InvoiceDraft();
        Detect(draft, nameof(draft.SellerName), () => draft.SellerName = "Erkannte Firma",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerVatId), () => draft.SellerVatId = "DE123456789",
            FieldOrigin.DetectedUncertain);
        var proposal = new DetectedOwnCompanyProposal
        {
            Fields =
            [
                Proposed(DetectedOwnCompanyFieldKind.SellerName, "Erkannte Firma"),
                Proposed(DetectedOwnCompanyFieldKind.SellerVatId, "DE123456789"),
            ],
        };

        // Simuliert einen inzwischen programmgesteuert ausgetauschten Wert,
        // ohne ihn fälschlich als manuelle Eingabe zu behandeln.
        draft.Prefill(current => current.SellerName = "Anderer erkannter Wert");

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(
            draft, new CompanyTemplate(), proposal);

        Assert.Null(plan.Candidate.SellerName);
        Assert.Equal("DE123456789", plan.Candidate.SellerVatId);
        Assert.False(plan.CanSave);
    }

    [Fact]
    public void ManuelleÄnderungBleibtAuchNebenProposalEineNormaleBenutzereingabe()
    {
        var draft = new InvoiceDraft();
        Detect(draft, nameof(draft.SellerName), () => draft.SellerName = "Erkannte Firma",
            FieldOrigin.DetectedUncertain);
        Detect(draft, nameof(draft.SellerVatId), () => draft.SellerVatId = "DE123456789",
            FieldOrigin.DetectedUncertain);
        var proposal = new DetectedOwnCompanyProposal
        {
            Fields =
            [
                Proposed(DetectedOwnCompanyFieldKind.SellerName, "Erkannte Firma"),
                Proposed(DetectedOwnCompanyFieldKind.SellerVatId, "DE123456789"),
            ],
        };

        draft.SellerName = "Von Hand bestätigte Schreibweise";

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(
            draft, new CompanyTemplate(), proposal);

        Assert.Equal(FieldOrigin.Manual, draft.OriginOf(nameof(draft.SellerName)));
        Assert.Equal("Von Hand bestätigte Schreibweise", plan.Candidate.SellerName);
        Assert.Equal("DE123456789", plan.Candidate.SellerVatId);
        Assert.True(plan.CanSave);
    }

    [Fact]
    public void NeueRechnungNutztNachProposalSpeicherungDieVorlage()
    {
        var detectedDraft = new InvoiceDraft();
        Detect(detectedDraft, nameof(detectedDraft.SellerName),
            () => detectedDraft.SellerName = "Erkannte Firma", FieldOrigin.DetectedUncertain);
        Detect(detectedDraft, nameof(detectedDraft.SellerVatId),
            () => detectedDraft.SellerVatId = "DE123456789", FieldOrigin.DetectedUncertain);
        var proposal = new DetectedOwnCompanyProposal
        {
            Fields =
            [
                Proposed(DetectedOwnCompanyFieldKind.SellerName, "Erkannte Firma"),
                Proposed(DetectedOwnCompanyFieldKind.SellerVatId, "DE123456789"),
            ],
        };
        CompanyTemplateSavePlan save = CompanyTemplateSavePlanner.Plan(
            detectedDraft, new CompanyTemplate(), proposal);
        var nextDraft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(nextDraft, save.Candidate);

        Assert.Equal("Erkannte Firma", nextDraft.SellerName);
        Assert.Equal("DE123456789", nextDraft.SellerVatId);
        Assert.Equal(FieldOrigin.Template, nextDraft.OriginOf(nameof(nextDraft.SellerName)));
    }

    [Fact]
    public void MarkanteKäuferUndRechnungsdatenGelangenNichtInDenKandidaten()
    {
        var draft = new InvoiceDraft
        {
            SellerName = "Eigene Firma",
            SellerTaxNumber = "EIGEN-123",
            BuyerName = "KÄUFER-NAME-MARKER",
            BuyerStreet = "KÄUFER-STRAßE-MARKER",
            BuyerEmail = "kunde-marker@example.invalid",
            InvoiceNumber = "RECHNUNGSNUMMER-MARKER",
            PaymentTerms = "ZAHLUNGSWERT-MARKER",
        };
        InvoiceLineDraft line = draft.AddLine();
        line.Name = "POSITIONS-MARKER";

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());
        string candidate = string.Join('|', CompanyValues(plan.Candidate));

        Assert.DoesNotContain("KÄUFER", candidate, StringComparison.Ordinal);
        Assert.DoesNotContain("RECHNUNGSNUMMER", candidate, StringComparison.Ordinal);
        Assert.DoesNotContain("ZAHLUNGSWERT", candidate, StringComparison.Ordinal);
        Assert.DoesNotContain("POSITIONS", candidate, StringComparison.Ordinal);
    }

    [Fact]
    public void GemischterZustandÄndertNurDasEineManuelleFeld()
    {
        CompanyTemplate existing = CompleteTemplate() with
        {
            DefaultCurrency = "CHF",
            DefaultPaymentTermDays = 31,
            DefaultPaymentTerms = "BESTEHENDE-ZAHLUNGSBEDINGUNG",
            DefaultEmailSubject = "BESTEHENDER-BETREFF",
            DefaultEmailBody = "BESTEHENDER-TEXT",
            LastOutputDirectory = "C:\\EXAKT\\ERHALTEN",
        };
        var draft = new InvoiceDraft();
        CompanyTemplateApplier.Apply(draft, existing);
        draft.SellerCity = "Neuer Ort";

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, existing);

        Assert.True(plan.RequiresConfirmation);
        Assert.Equal([nameof(InvoiceDraft.SellerCity)], plan.ChangedFields);
        Assert.Equal("Neuer Ort", plan.Candidate.SellerCity);
        Assert.Equal(existing.SellerName, plan.Candidate.SellerName);
        Assert.Equal(existing.BankIban, plan.Candidate.BankIban);
        Assert.Equal(existing.DefaultCurrency, plan.Candidate.DefaultCurrency);
        Assert.Equal(existing.DefaultPaymentTermDays, plan.Candidate.DefaultPaymentTermDays);
        Assert.Equal(existing.DefaultPaymentTerms, plan.Candidate.DefaultPaymentTerms);
        Assert.Equal(existing.DefaultEmailSubject, plan.Candidate.DefaultEmailSubject);
        Assert.Equal(existing.DefaultEmailBody, plan.Candidate.DefaultEmailBody);
        Assert.Equal(existing.LastOutputDirectory, plan.Candidate.LastOutputDirectory);
    }

    [Theory]
    [InlineData(FieldOrigin.Template)]
    [InlineData(FieldOrigin.TemplateDefault)]
    public void VorlagenherkünfteSindKeineNeueBenutzereingabe(FieldOrigin origin)
    {
        CompanyTemplate existing = CompleteTemplate();
        var draft = new InvoiceDraft();
        draft.Prefill(d => d.SellerName = "Nicht manuell");
        draft.MarkOrigin(nameof(draft.SellerName), origin);

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, existing);

        Assert.False(plan.HasManualInput);
        Assert.Equal(existing.SellerName, plan.Candidate.SellerName);
        Assert.False(plan.IsChanged);
    }

    [Fact]
    public void UnberührtesStandardlandDeutschlandIstDieEinzigeAusnahme()
    {
        var draft = new InvoiceDraft { SellerName = "Eigene Firma", SellerTaxNumber = "123" };

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.SellerCountry)));
        Assert.Equal("DE", plan.Candidate.SellerCountry);
    }

    [Fact]
    public void StandardlandÜberschreibtKeinVorhandenesNichtManuellesLand()
    {
        var draft = new InvoiceDraft { SellerName = "Eigene Firma" };
        CompanyTemplate existing = CompleteTemplate() with { SellerCountry = "AT" };

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, existing);

        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.SellerCountry)));
        Assert.Equal("AT", plan.Candidate.SellerCountry);
    }

    [Theory]
    [InlineData(FieldOrigin.DetectedReliably)]
    [InlineData(FieldOrigin.DetectedUncertain)]
    public void ErkanntesDeutschlandNutztDieStandardlandAusnahmeNicht(FieldOrigin origin)
    {
        var draft = new InvoiceDraft { SellerName = "Eigene Firma", SellerTaxNumber = "123" };
        draft.Prefill(d => d.SellerCountry = "DE");
        draft.MarkOrigin(nameof(draft.SellerCountry), origin);

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.Null(plan.Candidate.SellerCountry);
        Assert.Contains(plan.Errors, error => error.Contains("Land", StringComparison.Ordinal));
    }

    [Fact]
    public void KomfortvorgabenAlleinSindKeineVorhandeneUnternehmensvorlage()
    {
        var defaultsOnly = new CompanyTemplate
        {
            SellerCountry = "DE",
            DefaultCurrency = "EUR",
            DefaultPaymentTermDays = 21,
            DefaultPaymentTerms = "14 Tage",
            DefaultEmailSubject = "Betreff",
            DefaultEmailBody = "Text",
            LastOutputDirectory = "C:\\Ausgabe",
        };

        Assert.False(CompanyTemplateSavePlanner.HasCompanyData(defaultsOnly));
        var firstDraft = new InvoiceDraft
        {
            SellerName = "Erste echte Firma",
            SellerTaxNumber = "123",
        };
        CompanyTemplateSavePlan firstSave = CompanyTemplateSavePlanner.Plan(firstDraft, defaultsOnly);

        Assert.False(firstSave.HasExistingCompanyData);
        Assert.False(firstSave.RequiresConfirmation);
        Assert.True(CompanyTemplateSavePlanner.HasCompanyData(
            defaultsOnly with { BankBic = "COBADEFFXXX" }));
        Assert.True(CompanyTemplateSavePlanner.HasCompanyData(
            defaultsOnly with { SellerCountry = "AT" }));
    }

    [Fact]
    public void IdentischerKandidatWirdNichtAlsSchreibbarGeplant()
    {
        CompanyTemplate existing = CompleteTemplate();
        var draft = new InvoiceDraft();
        CompanyTemplateApplier.Apply(draft, existing);
        draft.SellerName = "Vorübergehend anders";
        draft.SellerName = existing.SellerName!;

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, existing);

        Assert.True(plan.HasManualInput);
        Assert.False(plan.IsChanged);
        Assert.False(plan.CanSave);
    }

    [Fact]
    public void PlanungVerändertWederEntwurfNochFeldherkünfte()
    {
        var draft = new InvoiceDraft
        {
            SellerName = "Eigene Firma",
            SellerTaxNumber = "123",
            BuyerName = "Käufer bleibt",
            InvoiceNumber = "RE-42",
        };
        string before = DraftSnapshot(draft);

        _ = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.Equal(before, DraftSnapshot(draft));
    }

    [Fact]
    public void LeereOderUngültigePflichtwerteWerdenKontrolliertBeanstandet()
    {
        var draft = new InvoiceDraft { SellerEmail = "keine-mail", BankIban = "keine-iban" };
        draft.SellerCountry = "XX";

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.False(plan.CanSave);
        Assert.Contains(plan.Errors, error => error.Contains("Firmenname", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("Land", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("USt-IdNr.", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("E-Mail", StringComparison.Ordinal));
        Assert.Contains(plan.Errors, error => error.Contains("IBAN", StringComparison.Ordinal));
    }

    private static void Detect(
        InvoiceDraft draft, string property, Action assign,
        FieldOrigin origin = FieldOrigin.DetectedReliably)
    {
        draft.Prefill(_ => assign());
        draft.MarkOrigin(property, origin);
    }

    private static DetectedOwnCompanyField Proposed(
        DetectedOwnCompanyFieldKind kind,
        string value,
        DetectionConfidence confidence = DetectionConfidence.Medium)
        => new(kind, value, confidence, ["synthetische Testevidenz"]);

    private static CompanyTemplate CompleteTemplate() => new()
    {
        SellerName = "Bestehende Firma",
        SellerStreet = "Alte Straße 1",
        SellerPostalCode = "18055",
        SellerCity = "Rostock",
        SellerCountry = "DE",
        SellerEmail = "alt@example.test",
        SellerVatId = "DE123456789",
        SellerTaxNumber = "123/456/789",
        BankAccountHolder = "Bestehende Firma",
        BankIban = "DE89370400440532013000",
        BankBic = "COBADEFFXXX",
    };

    private static IEnumerable<string?> CompanyValues(CompanyTemplate template)
    {
        yield return template.SellerName;
        yield return template.SellerStreet;
        yield return template.SellerPostalCode;
        yield return template.SellerCity;
        yield return template.SellerCountry;
        yield return template.SellerEmail;
        yield return template.SellerVatId;
        yield return template.SellerTaxNumber;
        yield return template.BankAccountHolder;
        yield return template.BankIban;
        yield return template.BankBic;
    }

    private static string DraftSnapshot(InvoiceDraft draft)
        => string.Join('|',
            draft.SellerName,
            draft.SellerStreet,
            draft.SellerPostalCode,
            draft.SellerCity,
            draft.SellerCountry,
            draft.SellerEmail,
            draft.SellerVatId,
            draft.SellerTaxNumber,
            draft.BankAccountHolder,
            draft.BankIban,
            draft.BankBic,
            draft.BuyerName,
            draft.InvoiceNumber,
            string.Join(',', draft.Origins.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{pair.Value}")));
}

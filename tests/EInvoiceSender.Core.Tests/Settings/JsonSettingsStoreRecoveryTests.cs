using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Settings;

/// <summary>Prüft fehlende und beschädigte lokale Vorlagendateien.</summary>
public sealed class JsonSettingsStoreRecoveryTests
{
    [Fact]
    public async Task FehlendeDateiLiefertLeereVorlageUndKannAusdrücklichGespeichertWerden()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);
            CompanyTemplate empty = await store.LoadTemplateAsync(TestContext.Current.CancellationToken);

            Assert.False(CompanyTemplateSavePlanner.HasCompanyData(empty));

            CompanyTemplate expected = Template();
            await store.SaveTemplateAsync(expected, TestContext.Current.CancellationToken);
            CompanyTemplate actual = await store.LoadTemplateAsync(TestContext.Current.CancellationToken);

            Assert.Equal(expected.SellerName, actual.SellerName);
            Assert.Equal(expected.DefaultEmailBody, actual.DefaultEmailBody);
            Assert.Equal(expected.LastOutputDirectory, actual.LastOutputDirectory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BeschädigteDateiWirdKontrolliertAlsLeerBehandeltUndNeuGeschrieben()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string path = Path.Combine(directory, "firmenvorlage.json");
            await File.WriteAllTextAsync(
                path, "{ BESCHÄDIGT", TestContext.Current.CancellationToken);
            var store = CreateStore(directory);

            CompanyTemplate empty = await store.LoadTemplateAsync(TestContext.Current.CancellationToken);
            Assert.False(CompanyTemplateSavePlanner.HasCompanyData(empty));

            await store.SaveTemplateAsync(Template(), TestContext.Current.CancellationToken);
            CompanyTemplate recovered = await store.LoadTemplateAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Eigene Firma", recovered.SellerName);
            Assert.Equal("AUSGABE-MARKER", recovered.LastOutputDirectory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MarkanteKäuferdatenAusDemEntwurfStehenNichtInDerGespeichertenJsonDatei()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var draft = new EInvoiceSender.Core.Models.InvoiceDraft
            {
                SellerName = "Eigene Firma",
                SellerTaxNumber = "123",
                BuyerName = "KÄUFER-DARF-NICHT-IN-JSON",
                BuyerEmail = "kunde-marker@example.invalid",
                InvoiceNumber = "RECHNUNG-DARF-NICHT-IN-JSON",
            };
            CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(
                draft, new CompanyTemplate { DefaultEmailBody = "EXAKT-ERHALTEN" });
            var store = CreateStore(directory);

            await store.SaveTemplateAsync(plan.Candidate, TestContext.Current.CancellationToken);
            string json = await File.ReadAllTextAsync(
                Path.Combine(directory, "firmenvorlage.json"),
                TestContext.Current.CancellationToken);

            Assert.DoesNotContain("KÄUFER", json, StringComparison.Ordinal);
            Assert.DoesNotContain("kunde-marker", json, StringComparison.Ordinal);
            Assert.DoesNotContain("RECHNUNG", json, StringComparison.Ordinal);
            Assert.Contains("EXAKT-ERHALTEN", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task NeueRechnungErhältDieAusdrücklichGespeicherteVorlage()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = CreateStore(directory);
            var firstDraft = new EInvoiceSender.Core.Models.InvoiceDraft
            {
                SellerName = "Vorlage für neue Rechnung",
                SellerStreet = "Werkstraße 7",
                SellerCity = "Rostock",
                SellerTaxNumber = "123",
            };
            CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(
                firstDraft, await store.LoadTemplateAsync(TestContext.Current.CancellationToken));
            await store.SaveTemplateAsync(plan.Candidate, TestContext.Current.CancellationToken);

            CompanyTemplate loaded = await store.LoadTemplateAsync(TestContext.Current.CancellationToken);
            var newDraft = new EInvoiceSender.Core.Models.InvoiceDraft();
            CompanyTemplateApplier.Apply(newDraft, loaded);

            Assert.Equal("Vorlage für neue Rechnung", newDraft.SellerName);
            Assert.Equal("Werkstraße 7", newDraft.SellerStreet);
            Assert.Equal("Rostock", newDraft.SellerCity);
            Assert.Equal(
                EInvoiceSender.Core.Models.FieldOrigin.Template,
                newDraft.OriginOf(nameof(newDraft.SellerName)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonSettingsStore CreateStore(string directory)
        => new(NullLogger<JsonSettingsStore>.Instance, directory);

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"EInvoiceSender-SET-01-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static CompanyTemplate Template() => new()
    {
        SellerName = "Eigene Firma",
        SellerCountry = "DE",
        SellerTaxNumber = "123",
        DefaultEmailBody = "KOMFORT-MARKER",
        LastOutputDirectory = "AUSGABE-MARKER",
    };
}

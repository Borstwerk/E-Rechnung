using System.Text.Json;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.Core.Tests.Settings;

/// <summary>
/// Die beiden neuen Verkäuferkennungen gehören nicht an denselben Ort.
///
/// **Die Registerkennung (BT-30) ist Firmenstamm.** Sie ändert sich nicht von
/// Rechnung zu Rechnung, also gehört sie in die Firmenvorlage – wie die
/// USt-IdNr. auch.
///
/// **Die Verkäuferkennung (BT-29) ist es nicht.** Sie ist die Nummer, unter
/// der ein bestimmter Kunde diesen Lieferanten führt. Global gespeichert
/// stünde sie auf der nächsten Rechnung an einen anderen Kunden – falsch, und
/// zwar unbemerkt. Deshalb prüfen diese Tests ausdrücklich beides: dass die
/// eine gespeichert wird und die andere nicht.
/// </summary>
public sealed class SellerIdentificationTemplateTests
{
    // -------------------------------------------------------------- Vorbefüllen

    [Fact]
    public void DieVorlageFülltDieRegisterkennung()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template() with { SellerLegalRegistrationId = "HRB 12345" });

        Assert.Equal("HRB 12345", draft.SellerLegalRegistrationId);
        Assert.Equal(
            FieldOrigin.Template,
            draft.OriginOf(nameof(draft.SellerLegalRegistrationId)));
    }

    /// <summary>
    /// Eine von Hand eingetragene Registerkennung überlebt jede spätere
    /// Vorbefüllung – dieselbe Regel wie bei allen anderen Stammdaten.
    /// </summary>
    [Fact]
    public void EineEigeneRegisterkennungBleibtStehen()
    {
        var draft = new InvoiceDraft { SellerLegalRegistrationId = "HRB 99999" };

        CompanyTemplateApplier.Apply(draft, Template() with { SellerLegalRegistrationId = "HRB 12345" });

        Assert.Equal("HRB 99999", draft.SellerLegalRegistrationId);
    }

    /// <summary>
    /// Die Verkäuferkennung kennt die Vorlage gar nicht. Nach dem Anwenden
    /// einer Vorlage steht das Feld deshalb leer da – auch dann, wenn die
    /// vorige Rechnung eine trug.
    /// </summary>
    [Fact]
    public void DieVorlageFülltDieVerkäuferkennungNicht()
    {
        var draft = new InvoiceDraft();

        CompanyTemplateApplier.Apply(draft, Template() with { SellerLegalRegistrationId = "HRB 12345" });

        Assert.Equal(string.Empty, draft.SellerIdentifier);
        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.SellerIdentifier)));
    }

    // ------------------------------------------------------------- Speicherplan

    [Fact]
    public void DieAllowlistEnthältDieRegisterkennungUndNichtDieVerkäuferkennung()
    {
        Assert.Contains(
            nameof(InvoiceDraft.SellerLegalRegistrationId),
            CompanyTemplateSavePlanner.AllowedFields);

        Assert.DoesNotContain(
            nameof(InvoiceDraft.SellerIdentifier),
            CompanyTemplateSavePlanner.AllowedFields);
    }

    [Fact]
    public void EineManuelleRegisterkennungWirdGespeichert()
    {
        var draft = new InvoiceDraft
        {
            SellerName = "BorstWerk GmbH",
            SellerTaxNumber = "079/123/45678",
            SellerLegalRegistrationId = "HRB 12345",
        };

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.Equal("HRB 12345", plan.Candidate.SellerLegalRegistrationId);
        Assert.Contains(nameof(InvoiceDraft.SellerLegalRegistrationId), plan.ChangedFields);
        Assert.True(plan.CanSave);
    }

    /// <summary>
    /// **Der wichtigste Negativfall dieser Datei.** Die Verkäuferkennung darf
    /// auf keinem Weg in die Vorlage gelangen – weder von Hand eingetragen noch
    /// über einen Erkennungsvorschlag. Geprüft wird nicht eine einzelne
    /// Eigenschaft, sondern der gesamte Kandidat: Ein künftiges Feld, das den
    /// Wert versehentlich mitnimmt, fällt damit ebenfalls auf.
    /// </summary>
    [Fact]
    public void DieVerkäuferkennungWirdNiemalsInDieVorlageGespeichert()
    {
        var draft = new InvoiceDraft
        {
            SellerName = "BorstWerk GmbH",
            SellerTaxNumber = "079/123/45678",
            SellerIdentifier = "KUNDENNUMMER-DARF-NICHT-IN-DIE-VORLAGE",
        };

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.DoesNotContain(
            "KUNDENNUMMER-DARF-NICHT-IN-DIE-VORLAGE",
            JsonSerializer.Serialize(plan.Candidate),
            StringComparison.Ordinal);

        Assert.DoesNotContain(nameof(InvoiceDraft.SellerIdentifier), plan.ChangedFields);
    }

    /// <summary>
    /// Auch eine gespeicherte Registerkennung allein macht die Vorlage zu einer
    /// inhaltlichen Unternehmensvorlage – sonst würde sie beim nächsten
    /// Speichern stillschweigend überschrieben.
    /// </summary>
    [Fact]
    public void EineRegisterkennungAlleinIstBereitsEineUnternehmensvorlage()
    {
        Assert.True(CompanyTemplateSavePlanner.HasCompanyData(
            new CompanyTemplate { SellerLegalRegistrationId = "HRB 12345" }));
    }

    /// <summary>
    /// **Die Speicherregel der Vorlage bleibt unverändert.** Sie verlangt eine
    /// USt-IdNr. oder eine Steuernummer und ist nicht dasselbe wie BR-CO-26:
    /// Die eine sagt, was eine brauchbare Firmenvorlage ausmacht, die andere,
    /// woran der Empfänger den Rechnungssteller erkennt. Eine Registerkennung
    /// ersetzt deshalb hier keine steuerliche Angabe.
    /// </summary>
    [Fact]
    public void EineRegisterkennungErsetztInDerVorlageKeineSteuerlicheAngabe()
    {
        var draft = new InvoiceDraft
        {
            SellerName = "BorstWerk GmbH",
            SellerLegalRegistrationId = "HRB 12345",
        };

        CompanyTemplateSavePlan plan = CompanyTemplateSavePlanner.Plan(draft, new CompanyTemplate());

        Assert.Contains(
            plan.Errors,
            e => e.Contains("USt-IdNr.", StringComparison.Ordinal));
        Assert.False(plan.CanSave);
    }

    // -------------------------------------------------------------- Persistenz

    [Fact]
    public async Task DieRegisterkennungÜberstehtSpeichernUndLaden()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = new JsonSettingsStore(NullLogger<JsonSettingsStore>.Instance, directory);

            await store.SaveTemplateAsync(
                Template() with { SellerLegalRegistrationId = "HRB 12345" },
                TestContext.Current.CancellationToken);

            CompanyTemplate loaded = await store.LoadTemplateAsync(
                TestContext.Current.CancellationToken);

            Assert.Equal("HRB 12345", loaded.SellerLegalRegistrationId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// **Eine vorhandene Installation darf nicht stolpern.** Wer die Anwendung
    /// schon benutzt hat, hat eine Vorlagendatei ohne dieses Feld auf der
    /// Platte. Sie muss sich weiterhin lesen lassen; das neue Feld ist dann
    /// schlicht leer.
    /// </summary>
    [Fact]
    public async Task EineÄltereVorlagendateiOhneDasFeldWirdWeiterhinGelesen()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "firmenvorlage.json"),
                """
                {
                  "SellerName": "Vorhandene Firma",
                  "SellerCountry": "DE",
                  "SellerTaxNumber": "079/123/45678",
                  "DefaultPaymentTermDays": 14
                }
                """,
                TestContext.Current.CancellationToken);

            CompanyTemplate loaded = await new JsonSettingsStore(
                    NullLogger<JsonSettingsStore>.Instance, directory)
                .LoadTemplateAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Vorhandene Firma", loaded.SellerName);
            Assert.Equal("079/123/45678", loaded.SellerTaxNumber);
            Assert.Null(loaded.SellerLegalRegistrationId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ------------------------------------------------------------- Hilfsmittel

    private static CompanyTemplate Template() => new()
    {
        SellerName = "BorstWerk GmbH",
        SellerStreet = "Werkstraße 7",
        SellerPostalCode = "18055",
        SellerCity = "Rostock",
        SellerCountry = "DE",
        SellerTaxNumber = "079/123/45678",
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"EInvoiceSender-SELL-ID-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }
}

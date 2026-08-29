using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Rules;

/// <summary>
/// Vollständige Positiv-/Negativprüfung jeder einzelnen Regelgruppe des
/// <see cref="En16931RuleValidator"/>.
///
/// Jeder Testfall geht vom Golden Master "01-dienstleistung-19" aus und
/// verändert gezielt genau ein Feld per <c>with</c>, um genau einen
/// Regelverstoß auszulösen. Damit ist nachvollziehbar, welche Änderung
/// welche Kennung auslöst – anders als bei der Grundsicherung in
/// <see cref="RuleValidatorBaselineTests"/>, die bewusst mehrere Mängel
/// gleichzeitig mischt.
///
/// Die Basisdaten selbst gelten als "sauber": Wo sinnvoll, wird vor dem
/// eigentlichen Negativtest geprüft, dass die unveränderte Rechnung die
/// betroffene Kennung nicht auswirft. Das ist der Positivtest.
/// </summary>
public sealed class En16931RuleValidatorTests
{
    /// <summary>Feste Zeitquelle, damit Datumsprüfungen reproduzierbar sind.</summary>
    private static readonly TimeProvider FixedClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(2)));

    private static readonly En16931RuleValidator Validator = new(FixedClock);

    /// <summary>Unveränderte Ausgangsrechnung für alle Einzelfeldänderungen.</summary>
    private static Invoice BaseInvoice => InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice;

    // ---------------------------------------------------------------- Helfer

    private static ValidationReport Prüfe(Invoice invoice)
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(invoice);
        return Validator.Validate(invoice, totals);
    }

    private static void ErwarteFehler(ValidationReport report, string kennung)
        => Assert.True(
            report.Findings.Any(f => f.RuleId == kennung && f.Severity == FindingSeverity.Error),
            $"Erwarteter Fehler '{kennung}' fehlt im Bericht. Vorhandene Befunde: {Beschreibe(report)}");

    private static void ErwarteWarnung(ValidationReport report, string kennung)
        => Assert.True(
            report.Findings.Any(f => f.RuleId == kennung && f.Severity == FindingSeverity.Warning),
            $"Erwartete Warnung '{kennung}' fehlt im Bericht. Vorhandene Befunde: {Beschreibe(report)}");

    private static void ErwarteKeinenFehler(ValidationReport report, string kennung)
        => Assert.False(
            report.Findings.Any(f => f.RuleId == kennung && f.Severity == FindingSeverity.Error),
            $"Unerwarteter Fehler '{kennung}' im Bericht: {Beschreibe(report)}");

    private static void ErwarteKeineWarnung(ValidationReport report, string kennung)
        => Assert.False(
            report.Findings.Any(f => f.RuleId == kennung && f.Severity == FindingSeverity.Warning),
            $"Unerwartete Warnung '{kennung}' im Bericht: {Beschreibe(report)}");

    private static string Beschreibe(ValidationReport report)
        => string.Join(" | ", report.Findings.Select(f => $"{f.RuleId} ({f.Severity}): {f.Message}"));

    // ---------------------------------------------------------------- Dokument

    [Fact]
    public void Dokument_RechnungsnummerLeer_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-DOC-001");

        Invoice kaputt = BaseInvoice with { InvoiceNumber = "   " };

        ErwarteFehler(Prüfe(kaputt), "APP-DOC-001");
    }

    [Fact]
    public void Dokument_RechnungsnummerÜber60Zeichen_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-DOC-002");

        Invoice kaputt = BaseInvoice with { InvoiceNumber = new string('9', 61) };

        ErwarteWarnung(Prüfe(kaputt), "APP-DOC-002");
    }

    [Fact]
    public void Dokument_RechnungsdatumVorJahr2000_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-DOC-003");

        Invoice kaputt = BaseInvoice with { IssueDate = new DateOnly(1999, 12, 31) };

        ErwarteFehler(Prüfe(kaputt), "APP-DOC-003");
    }

    [Fact]
    public void Dokument_RechnungsdatumInDerZukunft_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-DOC-004");

        // DueDate wird mitverschoben, damit nicht zusätzlich APP-DOC-005 anspringt.
        Invoice kaputt = BaseInvoice with
        {
            IssueDate = new DateOnly(2026, 4, 15),
            DueDate = new DateOnly(2026, 4, 29),
        };

        ErwarteWarnung(Prüfe(kaputt), "APP-DOC-004");
    }

    [Fact]
    public void Dokument_FälligkeitVorRechnungsdatum_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-DOC-005");

        Invoice kaputt = BaseInvoice with { DueDate = BaseInvoice.IssueDate.AddDays(-1) };

        ErwarteFehler(Prüfe(kaputt), "APP-DOC-005");
    }

    [Fact]
    public void Dokument_WederFälligkeitNochZahlungsbedingung_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-DOC-006");

        Invoice kaputt = BaseInvoice with
        {
            DueDate = null,
            Payment = BaseInvoice.Payment! with { Terms = null },
        };

        ErwarteFehler(Prüfe(kaputt), "APP-DOC-006");
    }

    /// <summary>
    /// Eine Währung außerhalb des EN-16931-Codebestands ist ein Normbefund.
    ///
    /// **Was dieser Test früher behauptete.** Er verlangte einen Fehler für
    /// jeden Code außerhalb der kuratierten BorstWerk-Auswahl – und schrieb
    /// damit die Verwechslung fest, um die es in ER-030-STD-01b geht: „von
    /// BorstWerk nicht angeboten“ ist keine Aussage über die Norm. Geprüft
    /// wird jetzt gegen den vollständigen Codebestand v17b, deshalb sind BGN
    /// (zurückgezogen) und XYZ (nie vergeben) beide Fehler, KZT (gültig, nur
    /// nicht angeboten) dagegen nicht. Die genaue Abgrenzung prüft
    /// <c>StandardRefreshTests</c>.
    /// </summary>
    [Fact]
    public void Dokument_NichtNormgültigeWährung_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-DOC-008");

        foreach (string code in new[] { "BGN", "XYZ" })
        {
            Invoice kaputt = BaseInvoice with { Currency = CurrencyCode.Parse(code) };

            ErwarteFehler(Prüfe(kaputt), "APP-DOC-008");
        }

        Invoice gültigNurNichtAngeboten =
            BaseInvoice with { Currency = CurrencyCode.Parse("KZT") };

        ErwarteKeinenFehler(Prüfe(gültigNurNichtAngeboten), "APP-DOC-008");
    }

    [Fact]
    public void Dokument_AbrechnungszeitraumEndeVorBeginn_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-DOC-009");

        Invoice kaputt = BaseInvoice with
        {
            BillingPeriodStart = new DateOnly(2026, 3, 1),
            BillingPeriodEnd = new DateOnly(2026, 2, 1),
        };

        ErwarteFehler(Prüfe(kaputt), "APP-DOC-009");
    }

    [Fact]
    public void Dokument_LeistungsdatumÜberEinJahrEntfernt_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-DOC-010");

        Invoice kaputt = BaseInvoice with { DeliveryDate = BaseInvoice.IssueDate.AddDays(400) };

        ErwarteWarnung(Prüfe(kaputt), "APP-DOC-010");
    }

    // -------------------------------------------------------------- Verkäufer

    [Fact]
    public void Verkäufer_NameLeer_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-SEL-001");

        Invoice kaputt = BaseInvoice with { Seller = BaseInvoice.Seller with { Name = "  " } };

        ErwarteFehler(Prüfe(kaputt), "APP-SEL-001");
    }

    [Fact]
    public void Verkäufer_OrtFehlt_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-SEL-002");

        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with { Address = BaseInvoice.Seller.Address with { City = "" } },
        };

        ErwarteWarnung(Prüfe(kaputt), "APP-SEL-002");
    }

    [Fact]
    public void Verkäufer_StraßeFehlt_LöstWarnungAus()
    {
        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with { Address = BaseInvoice.Seller.Address with { Street = null } },
        };

        ErwarteWarnung(Prüfe(kaputt), "APP-SEL-002");
    }

    [Fact]
    public void Verkäufer_UnbekanntesLand_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-SEL-003");

        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                Address = BaseInvoice.Seller.Address with { Country = CountryCode.Parse("QQ") },
            },
        };

        ErwarteFehler(Prüfe(kaputt), "APP-SEL-003");
    }

    /// <summary>
    /// BR-CO-26 verlangt eine Kennung, mit der ein Empfänger den Verkäufer
    /// **maschinell** identifizieren kann: USt-IdNr. (BT-31) oder
    /// Handelsregisternummer (BT-30) – im Modell dieser Anwendung.
    ///
    /// **Die Steuernummer (BT-32) genügt ausdrücklich nicht.** Sie steht im
    /// CII als <c>schemeID="FC"</c>; das CEN-Schematron prüft für BR-CO-26
    /// aber nur <c>schemeID="VA"</c>, <c>ram:ID</c>, <c>ram:GlobalID</c> und
    /// <c>SpecifiedLegalOrganization/ram:ID</c>.
    ///
    /// **Warum diese Tests entstanden sind:** Die bisherige Regel ließ die
    /// Steuernummer als Ersatz durchgehen. Eine Rechnung mit ausschließlich
    /// Steuernummer kam damit intern durch, und der Anwender erfuhr erst vom
    /// externen Validator, dass sie ungültig ist – wenn er ihn überhaupt
    /// einsetzte. Genau so ist es in einer Windows-Abnahme passiert.
    /// </summary>
    [Fact]
    public void Verkäufer_MitVatIdOhneSteuernummer_IstIdentifizierbar()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = "DE123456789",
                TaxNumber = null,
                LegalRegistrationId = null,
            },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-SEL-004");
    }

    /// <summary>
    /// Die Handelsregisternummer allein trägt ebenfalls. BR-CO-26 verlangt
    /// keine USt-IdNr., sondern **eine** zulässige Kennung.
    /// </summary>
    [Fact]
    public void Verkäufer_NurMitHandelsregisternummer_IstIdentifizierbar()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = null,
                TaxNumber = null,
                LegalRegistrationId = "HRB 12345",
            },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-SEL-004");
    }

    /// <summary>
    /// Die dritte zulässige Kennung: eine Lieferanten- oder Kreditorennummer,
    /// die der Käufer vergeben hat (BT-29).
    ///
    /// Sie ist die einzige Möglichkeit für Rechnungssteller, die weder eine
    /// USt-IdNr. noch einen Registereintrag haben – etwa Kleinunternehmer, die
    /// für einen Geschäftskunden arbeiten. Ohne sie hieße BR-CO-26 für diese
    /// Gruppe faktisch „keine E-Rechnung möglich“.
    /// </summary>
    [Fact]
    public void Verkäufer_NurMitLieferantennummer_IstIdentifizierbar()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = null,
                TaxNumber = null,
                LegalRegistrationId = null,
                SellerIdentifier = "LIEF-4711",
            },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-SEL-004");
    }

    /// <summary>
    /// Steuernummer plus eine der zulässigen Kennungen ist der Regelfall für
    /// Rechnungssteller ohne USt-IdNr.
    /// </summary>
    [Theory]
    [InlineData("HRB 12345", null)]
    [InlineData(null, "LIEF-4711")]
    public void Verkäufer_MitSteuernummerUndZulässigerKennung_IstIdentifizierbar(
        string? registration, string? identifier)
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = null,
                TaxNumber = "079/123/45678",
                LegalRegistrationId = registration,
                SellerIdentifier = identifier,
            },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-SEL-004");
    }

    /// <summary>
    /// Der Befundtext nennt jetzt alle drei zulässigen Wege. Nur auf die
    /// USt-IdNr. zu verweisen wäre ein Rat, der zwei gültige Möglichkeiten
    /// verschweigt.
    /// </summary>
    [Fact]
    public void DerBefundNenntAlleDreiZulässigenKennungen()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = null,
                TaxNumber = "079/123/45678",
                LegalRegistrationId = null,
                SellerIdentifier = null,
            },
        };

        ValidationFinding finding = Assert.Single(
            Prüfe(invoice).Findings, f => f.RuleId == "APP-SEL-004");

        Assert.Contains("USt-ID", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Registerkennung", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Kreditorennummer", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Steuernummer allein", finding.Message, StringComparison.Ordinal);
        Assert.Equal("BR-CO-26", finding.NormRule);
    }

    /// <summary>
    /// **Der Fall aus der Abnahme.** Nur eine Steuernummer – das reicht der
    /// Norm nicht, und deshalb darf es auch dieser Anwendung nicht reichen.
    /// </summary>
    [Fact]
    public void Verkäufer_NurMitSteuernummer_IstNichtIdentifizierbar()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = null,
                TaxNumber = "079/123/45678",
                LegalRegistrationId = null,
            },
        };

        ErwarteFehler(Prüfe(invoice), "APP-SEL-004");
    }

    [Fact]
    public void Verkäufer_OhneJedeKennung_IstNichtIdentifizierbar()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = null,
                TaxNumber = null,
                LegalRegistrationId = null,
            },
        };

        ErwarteFehler(Prüfe(invoice), "APP-SEL-004");
    }

    /// <summary>
    /// Die Steuernummer bleibt eine zulässige zusätzliche Angabe (BT-32). Sie
    /// verliert nur ihre falsche Rolle als Ersatz für die Identifikation.
    /// </summary>
    [Fact]
    public void Verkäufer_MitVatIdUndSteuernummer_BleibtZulässig()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                VatId = "DE123456789",
                TaxNumber = "079/123/45678",
            },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-SEL-004");
    }

    [Fact]
    public void Verkäufer_VatIdUngewöhnlichesFormat_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-SEL-005");

        Invoice kaputt = BaseInvoice with { Seller = BaseInvoice.Seller with { VatId = "12345" } };

        ErwarteWarnung(Prüfe(kaputt), "APP-SEL-005");
    }

    [Fact]
    public void Verkäufer_EmailUngültig_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-SEL-006");

        Invoice kaputt = BaseInvoice with { Seller = BaseInvoice.Seller with { Email = "kaputt" } };

        ErwarteFehler(Prüfe(kaputt), "APP-SEL-006");
    }

    // ------------------------------------------------------------------ Käufer

    [Fact]
    public void Käufer_NameLeer_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-BUY-001");

        Invoice kaputt = BaseInvoice with { Buyer = BaseInvoice.Buyer with { Name = "  " } };

        ErwarteFehler(Prüfe(kaputt), "APP-BUY-001");
    }

    [Fact]
    public void Käufer_UnbekanntesLand_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-BUY-003");

        Invoice kaputt = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with
            {
                Address = BaseInvoice.Buyer.Address with { Country = CountryCode.Parse("QQ") },
            },
        };

        ErwarteFehler(Prüfe(kaputt), "APP-BUY-003");
    }

    [Fact]
    public void Käufer_EmailUngültig_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-BUY-004");

        Invoice kaputt = BaseInvoice with { Buyer = BaseInvoice.Buyer with { Email = "kaputt" } };

        ErwarteFehler(Prüfe(kaputt), "APP-BUY-004");
    }

    [Fact]
    public void Käufer_WederEmailNochElektronischeAdresse_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-BUY-005");

        Invoice kaputt = BaseInvoice with { Buyer = BaseInvoice.Buyer with { Email = null } };

        ErwarteWarnung(Prüfe(kaputt), "APP-BUY-005");
    }

    [Fact]
    public void Käufer_ElektronischeAdresseOhneSchema_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-BUY-006");

        Invoice kaputt = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with
            {
                ElectronicAddress = "9930000000001",
                ElectronicAddressScheme = null,
            },
        };

        ErwarteWarnung(Prüfe(kaputt), "APP-BUY-006");
    }

    [Theory]
    [InlineData("DE1234")]
    [InlineData("ATU12345678")]
    [InlineData("EL123456789")]
    [InlineData("GR123456789")]
    public void Käufer_GültigeUstIdAllgemeinUndElSonderpräfixWerdenAkzeptiert(string vatId)
    {
        Invoice invoice = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with { VatId = vatId },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-BUY-007");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("QQ12345")]
    [InlineData("DE-12345")]
    public void Käufer_UngültigesUstIdFormatLöstFehlerAus(string vatId)
    {
        Invoice invoice = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with { VatId = vatId },
        };

        ErwarteFehler(Prüfe(invoice), "APP-BUY-007");
    }

    [Fact]
    public void Käufer_UstIdPräfixWirdNichtMitBuyerlandGleichgesetzt()
    {
        Invoice invoice = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with
            {
                Address = BaseInvoice.Buyer.Address with { Country = CountryCode.Parse("AT") },
                VatId = "DE987654321",
            },
        };

        ErwarteKeinenFehler(Prüfe(invoice), "APP-BUY-007");
    }

    [Fact]
    public void BuyerPräfixprüfungVerschärftBestehendeSellerregelNicht()
    {
        Invoice invoice = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with { VatId = "QQ12345" },
        };

        ErwarteKeineWarnung(Prüfe(invoice), "APP-SEL-005");
    }

    [Theory]
    [InlineData(VatCategory.ReverseCharge, "APP-VAT-014")]
    [InlineData(VatCategory.IntraCommunitySupply, "APP-VAT-015")]
    public void AeUndKOhneBuyerUstIdLösenFehlerAus(VatCategory category, string ruleId)
    {
        Invoice invoice = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with { VatId = null },
            Lines = [BaseInvoice.Lines[0] with { VatCategory = category, VatRate = 0m }],
            ExemptionReasons = [new VatExemptionReason(category, "Steuerfreie Testkonstellation")],
        };

        ErwarteFehler(Prüfe(invoice), ruleId);
    }

    [Theory]
    [InlineData(VatCategory.ReverseCharge, "APP-VAT-014")]
    [InlineData(VatCategory.IntraCommunitySupply, "APP-VAT-015")]
    public void AeUndKMitBuyerUstIdErfüllenDieZusatzregel(VatCategory category, string ruleId)
    {
        Invoice invoice = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with { VatId = "ATU12345678" },
            Lines = [BaseInvoice.Lines[0] with { VatCategory = category, VatRate = 0m }],
            ExemptionReasons = [new VatExemptionReason(category, "Steuerfreie Testkonstellation")],
        };

        ErwarteKeinenFehler(Prüfe(invoice), ruleId);
    }

    // --------------------------------------------------------------- Positionen

    [Fact]
    public void Positionen_KeinePositionen_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-001");

        Invoice kaputt = BaseInvoice with { Lines = [] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-001");
    }

    [Fact]
    public void Positionen_DoppelteNummer_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-002");

        InvoiceLine erste = BaseInvoice.Lines[0];
        Invoice kaputt = BaseInvoice with
        {
            Lines =
            [
                erste,
                erste with { Name = "Zweite Position mit gleicher Nummer" },
            ],
        };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-002");
    }

    [Fact]
    public void Positionen_NameLeer_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-003");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { Name = "   " };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-003");
    }

    [Fact]
    public void Positionen_MengeNull_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-004");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { Quantity = 0m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-004");
    }

    [Fact]
    public void Positionen_UnbekannteMengeneinheit_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-005");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { Unit = UnitCode.Parse("ZZZ") };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-005");
    }

    [Fact]
    public void Positionen_EinzelpreisNegativ_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-006");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { NetUnitPrice = -1m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-006");
    }

    [Fact]
    public void Positionen_PreisbasismengeNull_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-007");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { PriceBaseQuantity = 0m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-007");
    }

    [Fact]
    public void Positionen_RabattNegativ_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-008");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { AllowanceAmount = -10m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-008");
    }

    [Fact]
    public void Positionen_RabattGrößerAlsPositionsbetrag_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-009");

        // Quantity 10 * NetUnitPrice 95 = 950 brutto; ein Rabatt von 2000 macht
        // den Positionsbetrag negativ, ohne selbst negativ zu sein
        // (das wäre APP-LIN-008).
        InvoiceLine zeile = BaseInvoice.Lines[0] with { AllowanceAmount = 2000m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-009");
    }

    [Fact]
    public void Positionen_LeistungszeitraumEndeVorBeginn_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-LIN-010");

        InvoiceLine zeile = BaseInvoice.Lines[0] with
        {
            ServicePeriodStart = new DateOnly(2026, 3, 1),
            ServicePeriodEnd = new DateOnly(2026, 2, 1),
        };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-LIN-010");
    }

    // ------------------------------------------------------- Steuersatz je Position

    [Fact]
    public void Steuer_SatzNegativ_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-VAT-001");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatRate = -5m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-VAT-001");
    }

    [Fact]
    public void Steuer_SatzÜber100Prozent_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-VAT-002");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatRate = 150m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-VAT-002");
    }

    [Fact]
    public void Steuer_RegelbesteuertMitSatzNull_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-VAT-003");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatRate = 0m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-VAT-003");
    }

    [Fact]
    public void Steuer_NullsatzbesteuertMitSatzGrößerNull_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-VAT-004");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatCategory = VatCategory.ZeroRated, VatRate = 19m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Prüfe(kaputt), "APP-VAT-004");
    }

    [Fact]
    public void Steuer_SteuerbefreitMitSatzGrößerNull_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-VAT-005");

        // Begründung wird mitgeliefert, damit nicht zusätzlich APP-VAT-012
        // (fehlende Begründung) anspringt – hier geht es allein um den Satz.
        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatCategory = VatCategory.Exempt, VatRate = 19m };
        Invoice kaputt = BaseInvoice with
        {
            Lines = [zeile],
            ExemptionReasons = [new VatExemptionReason(VatCategory.Exempt, "Testbegründung")],
        };

        ErwarteFehler(Prüfe(kaputt), "APP-VAT-005");
    }

    // ------------------------------------------------------- Steueraufschlüsselung

    [Fact]
    public void Steuer_SteuerbefreiteKategorieOhneBegründung_LöstFehlerAus()
    {
        InvoiceScenario steuerfrei = InvoiceScenarios.ByKey("04-steuerfrei");
        ErwarteKeinenFehler(Prüfe(steuerfrei.Invoice), "APP-VAT-012");

        Invoice kaputt = steuerfrei.Invoice with { ExemptionReasons = [] };

        ErwarteFehler(Prüfe(kaputt), "APP-VAT-012");
    }

    [Fact]
    public void Steuer_UnbekannterBegründungscode_LöstWarnungAus()
    {
        // Hinweis: Der Golden Master "04-steuerfrei" verwendet bereits den
        // Subcode "VATEX-EU-132-1A", der in der kuratierten Codeliste nicht
        // enthalten ist (nur "VATEX-EU-132") – er löst die Warnung also
        // bereits selbst aus. Für den Positivtest wird deshalb zuerst auf
        // einen bekannten Code umgestellt, bevor gezielt ein unbekannter
        // Code für den Negativtest eingesetzt wird.
        InvoiceScenario steuerfrei = InvoiceScenarios.ByKey("04-steuerfrei");
        VatExemptionReason original = steuerfrei.Invoice.ExemptionReasons[0];

        Invoice bekannterCode = steuerfrei.Invoice with
        {
            ExemptionReasons = [original with { ReasonCode = "VATEX-EU-132" }],
        };

        ErwarteKeineWarnung(Prüfe(bekannterCode), "APP-VAT-013");

        Invoice kaputt = steuerfrei.Invoice with
        {
            ExemptionReasons = [original with { ReasonCode = "VATEX-UNBEKANNT-1" }],
        };

        ErwarteWarnung(Prüfe(kaputt), "APP-VAT-013");
    }

    // ------------------------------------------------------------------ Summen

    [Fact]
    public void Summen_LineTotalWeichtAb_LöstFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-001");

        InvoiceTotals manipuliert = totals with { LineTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-001");
    }

    [Fact]
    public void Summen_NettosummeWeichtAb_LöstFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-002");

        InvoiceTotals manipuliert = totals with { TaxBasisTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-002");
    }

    [Fact]
    public void Summen_SteuersummeWeichtAb_LöstFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-003");

        InvoiceTotals manipuliert = totals with { TaxTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-003");
    }

    [Fact]
    public void Summen_BruttosummeWeichtAb_LöstFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-004");

        InvoiceTotals manipuliert = totals with { GrandTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-004");
    }

    [Fact]
    public void Summen_ZahlbetragWeichtAb_LöstFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-005");

        InvoiceTotals manipuliert = totals with { DuePayableAmount = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-005");
    }

    [Fact]
    public void Summen_BetragMitDreiNachkommastellen_LöstFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-006");

        // RoundingAmount wird nur auf die Nachkommastellen geprüft, nicht mit
        // einer Neuberechnung verglichen – so bleibt der Verstoß isoliert.
        InvoiceTotals manipuliert = totals with { RoundingAmount = 0.001m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-006");
    }

    [Fact]
    public void Summen_BereitsGezahlterBetragNegativ_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-SUM-007");

        Invoice kaputt = BaseInvoice with { PaidAmount = -10m };

        ErwarteFehler(Prüfe(kaputt), "APP-SUM-007");
    }

    [Fact]
    public void Summen_BereitsGezahlterBetragÜberBruttosumme_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-SUM-008");

        Invoice kaputt = BaseInvoice with { PaidAmount = 5000m };

        ErwarteWarnung(Prüfe(kaputt), "APP-SUM-008");
    }

    [Fact]
    public void Summen_RundungsbetragUngewöhnlichHoch_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-SUM-009");

        Invoice kaputt = BaseInvoice with { RoundingAmount = 5.00m };

        ErwarteWarnung(Prüfe(kaputt), "APP-SUM-009");
    }

    [Fact]
    public void Summen_NegativerZahlbetragBeiNormalerRechnung_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-SUM-010");

        // TypeCode bleibt die normale Handelsrechnung (380); ein stark negativer
        // Rundungsbetrag reißt den Zahlbetrag ins Minus, ohne PaidAmount zu
        // verändern (das ist Voraussetzung der Regel).
        Invoice kaputt = BaseInvoice with { RoundingAmount = -2000m };

        ErwarteFehler(Prüfe(kaputt), "APP-SUM-010");
    }

    // ---------------------------------------------------------------- Zahlung

    [Fact]
    public void Zahlung_KeineZahlungsangabenBeiOffenemBetrag_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-PAY-001");

        // DueDate bleibt erhalten, damit nicht zusätzlich APP-DOC-006 anspringt.
        Invoice kaputt = BaseInvoice with { Payment = null };

        ErwarteWarnung(Prüfe(kaputt), "APP-PAY-001");
    }

    [Fact]
    public void Zahlung_ÜberweisungOhneBankverbindung_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-PAY-003");

        Invoice kaputt = BaseInvoice with { Payment = BaseInvoice.Payment! with { BankAccount = null } };

        ErwarteFehler(Prüfe(kaputt), "APP-PAY-003");
    }

    [Fact]
    public void Zahlung_KontoinhaberLeer_LöstWarnungAus()
    {
        ErwarteKeineWarnung(Prüfe(BaseInvoice), "APP-PAY-004");

        Invoice kaputt = BaseInvoice with
        {
            Payment = BaseInvoice.Payment! with
            {
                BankAccount = BaseInvoice.Payment!.BankAccount! with { AccountHolder = "" },
            },
        };

        ErwarteWarnung(Prüfe(kaputt), "APP-PAY-004");
    }

    [Fact]
    public void Zahlung_BicUngültig_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-PAY-005");

        Invoice kaputt = BaseInvoice with
        {
            Payment = BaseInvoice.Payment! with
            {
                BankAccount = BaseInvoice.Payment!.BankAccount! with { Bic = "ABC" },
            },
        };

        ErwarteFehler(Prüfe(kaputt), "APP-PAY-005");
    }

    [Fact]
    public void Zahlung_IbanLänderpräfixUnbekannt_LöstFehlerAus()
    {
        ErwarteKeinenFehler(Prüfe(BaseInvoice), "APP-PAY-006");

        // Formal gültige IBAN (Prüfziffer nach ISO 7064 Mod 97-10 stimmt),
        // aber mit einem Länderpräfix ('QQ'), das in keiner ISO-3166-Liste
        // vergeben ist.
        Invoice kaputt = BaseInvoice with
        {
            Payment = BaseInvoice.Payment! with
            {
                BankAccount = BaseInvoice.Payment!.BankAccount! with
                {
                    Iban = Iban.Parse("QQ33370400440532013000"),
                },
            },
        };

        ErwarteFehler(Prüfe(kaputt), "APP-PAY-006");
    }

    // --------------------------------------------------------- Gruppenübergreifend

    [Fact]
    public void FehlerInEinerGruppeVerhindertNichtDiePrüfungAndererGruppen()
    {
        // Je ein Verstoß aus Dokument, Verkäufer, Käufer, Positionen und
        // Zahlung gleichzeitig. Alle Kennungen müssen im selben Bericht
        // auftauchen – keine Gruppe darf eine andere verdecken oder die
        // Prüfung vorzeitig abbrechen.
        Invoice kaputt = BaseInvoice with
        {
            InvoiceNumber = "   ",
            Seller = BaseInvoice.Seller with { Name = "" },
            Buyer = BaseInvoice.Buyer with { Name = "" },
            Lines = [BaseInvoice.Lines[0] with { Name = "" }],
            Payment = BaseInvoice.Payment! with
            {
                BankAccount = BaseInvoice.Payment!.BankAccount! with { Bic = "ABC" },
            },
        };

        ValidationReport report = Prüfe(kaputt);

        ErwarteFehler(report, "APP-DOC-001");
        ErwarteFehler(report, "APP-SEL-001");
        ErwarteFehler(report, "APP-BUY-001");
        ErwarteFehler(report, "APP-LIN-003");
        ErwarteFehler(report, "APP-PAY-005");

        Assert.True(
            report.Findings.Select(f => f.RuleId).Distinct(StringComparer.Ordinal).Count() >= 5,
            $"Es werden mindestens fünf unterschiedliche Kennungen erwartet: {Beschreibe(report)}");
    }
}

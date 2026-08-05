using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Validation;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.TestSupport;
using EInvoiceSender.Validation.Rules;
using Xunit;

namespace EInvoiceSender.Validation.Tests.Rules;

/// <summary>
/// Vollstaendige Positiv-/Negativpruefung jeder einzelnen Regelgruppe des
/// <see cref="En16931RuleValidator"/>.
///
/// Jeder Testfall geht vom Golden Master "01-dienstleistung-19" aus und
/// veraendert gezielt genau ein Feld per <c>with</c>, um genau einen
/// Regelverstoss auszuloesen. Damit ist nachvollziehbar, welche Aenderung
/// welche Kennung ausloest – anders als bei der Grundsicherung in
/// <see cref="RuleValidatorBaselineTests"/>, die bewusst mehrere Maengel
/// gleichzeitig mischt.
///
/// Die Basisdaten selbst gelten als "sauber": Wo sinnvoll, wird vor dem
/// eigentlichen Negativtest geprueft, dass die unveraenderte Rechnung die
/// betroffene Kennung nicht auswirft. Das ist der Positivtest.
/// </summary>
public sealed class En16931RuleValidatorTests
{
    /// <summary>Feste Zeitquelle, damit Datumspruefungen reproduzierbar sind.</summary>
    private static readonly TimeProvider FixedClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(2)));

    private static readonly En16931RuleValidator Validator = new(FixedClock);

    /// <summary>Unveraenderte Ausgangsrechnung fuer alle Einzelfeldaenderungen.</summary>
    private static Invoice BaseInvoice => InvoiceScenarios.ByKey("01-dienstleistung-19").Invoice;

    // ---------------------------------------------------------------- Helfer

    private static ValidationReport Pruefe(Invoice invoice)
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
    public void Dokument_RechnungsnummerLeer_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-DOC-001");

        Invoice kaputt = BaseInvoice with { InvoiceNumber = "   " };

        ErwarteFehler(Pruefe(kaputt), "APP-DOC-001");
    }

    [Fact]
    public void Dokument_RechnungsnummerUeber60Zeichen_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-DOC-002");

        Invoice kaputt = BaseInvoice with { InvoiceNumber = new string('9', 61) };

        ErwarteWarnung(Pruefe(kaputt), "APP-DOC-002");
    }

    [Fact]
    public void Dokument_RechnungsdatumVorJahr2000_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-DOC-003");

        Invoice kaputt = BaseInvoice with { IssueDate = new DateOnly(1999, 12, 31) };

        ErwarteFehler(Pruefe(kaputt), "APP-DOC-003");
    }

    [Fact]
    public void Dokument_RechnungsdatumInDerZukunft_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-DOC-004");

        // DueDate wird mitverschoben, damit nicht zusaetzlich APP-DOC-005 anspringt.
        Invoice kaputt = BaseInvoice with
        {
            IssueDate = new DateOnly(2026, 4, 15),
            DueDate = new DateOnly(2026, 4, 29),
        };

        ErwarteWarnung(Pruefe(kaputt), "APP-DOC-004");
    }

    [Fact]
    public void Dokument_FaelligkeitVorRechnungsdatum_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-DOC-005");

        Invoice kaputt = BaseInvoice with { DueDate = BaseInvoice.IssueDate.AddDays(-1) };

        ErwarteFehler(Pruefe(kaputt), "APP-DOC-005");
    }

    [Fact]
    public void Dokument_WederFaelligkeitNochZahlungsbedingung_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-DOC-006");

        Invoice kaputt = BaseInvoice with
        {
            DueDate = null,
            Payment = BaseInvoice.Payment! with { Terms = null },
        };

        ErwarteFehler(Pruefe(kaputt), "APP-DOC-006");
    }

    [Fact]
    public void Dokument_UnbekannteWaehrung_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-DOC-008");

        Invoice kaputt = BaseInvoice with { Currency = CurrencyCode.Parse("XYZ") };

        ErwarteFehler(Pruefe(kaputt), "APP-DOC-008");
    }

    [Fact]
    public void Dokument_AbrechnungszeitraumEndeVorBeginn_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-DOC-009");

        Invoice kaputt = BaseInvoice with
        {
            BillingPeriodStart = new DateOnly(2026, 3, 1),
            BillingPeriodEnd = new DateOnly(2026, 2, 1),
        };

        ErwarteFehler(Pruefe(kaputt), "APP-DOC-009");
    }

    [Fact]
    public void Dokument_LeistungsdatumUeberEinJahrEntfernt_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-DOC-010");

        Invoice kaputt = BaseInvoice with { DeliveryDate = BaseInvoice.IssueDate.AddDays(400) };

        ErwarteWarnung(Pruefe(kaputt), "APP-DOC-010");
    }

    // -------------------------------------------------------------- Verkaeufer

    [Fact]
    public void Verkaeufer_NameLeer_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-SEL-001");

        Invoice kaputt = BaseInvoice with { Seller = BaseInvoice.Seller with { Name = "  " } };

        ErwarteFehler(Pruefe(kaputt), "APP-SEL-001");
    }

    [Fact]
    public void Verkaeufer_OrtFehlt_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-SEL-002");

        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with { Address = BaseInvoice.Seller.Address with { City = "" } },
        };

        ErwarteWarnung(Pruefe(kaputt), "APP-SEL-002");
    }

    [Fact]
    public void Verkaeufer_StrasseFehlt_LoestWarnungAus()
    {
        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with { Address = BaseInvoice.Seller.Address with { Street = null } },
        };

        ErwarteWarnung(Pruefe(kaputt), "APP-SEL-002");
    }

    [Fact]
    public void Verkaeufer_UnbekanntesLand_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-SEL-003");

        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with
            {
                Address = BaseInvoice.Seller.Address with { Country = CountryCode.Parse("QQ") },
            },
        };

        ErwarteFehler(Pruefe(kaputt), "APP-SEL-003");
    }

    [Fact]
    public void Verkaeufer_WederVatIdNochSteuernummer_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-SEL-004");

        Invoice kaputt = BaseInvoice with
        {
            Seller = BaseInvoice.Seller with { VatId = null, TaxNumber = null },
        };

        ErwarteFehler(Pruefe(kaputt), "APP-SEL-004");
    }

    [Fact]
    public void Verkaeufer_VatIdUngewoehnlichesFormat_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-SEL-005");

        Invoice kaputt = BaseInvoice with { Seller = BaseInvoice.Seller with { VatId = "12345" } };

        ErwarteWarnung(Pruefe(kaputt), "APP-SEL-005");
    }

    [Fact]
    public void Verkaeufer_EmailUngueltig_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-SEL-006");

        Invoice kaputt = BaseInvoice with { Seller = BaseInvoice.Seller with { Email = "kaputt" } };

        ErwarteFehler(Pruefe(kaputt), "APP-SEL-006");
    }

    // ------------------------------------------------------------------ Kaeufer

    [Fact]
    public void Kaeufer_NameLeer_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-BUY-001");

        Invoice kaputt = BaseInvoice with { Buyer = BaseInvoice.Buyer with { Name = "  " } };

        ErwarteFehler(Pruefe(kaputt), "APP-BUY-001");
    }

    [Fact]
    public void Kaeufer_UnbekanntesLand_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-BUY-003");

        Invoice kaputt = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with
            {
                Address = BaseInvoice.Buyer.Address with { Country = CountryCode.Parse("QQ") },
            },
        };

        ErwarteFehler(Pruefe(kaputt), "APP-BUY-003");
    }

    [Fact]
    public void Kaeufer_EmailUngueltig_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-BUY-004");

        Invoice kaputt = BaseInvoice with { Buyer = BaseInvoice.Buyer with { Email = "kaputt" } };

        ErwarteFehler(Pruefe(kaputt), "APP-BUY-004");
    }

    [Fact]
    public void Kaeufer_WederEmailNochElektronischeAdresse_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-BUY-005");

        Invoice kaputt = BaseInvoice with { Buyer = BaseInvoice.Buyer with { Email = null } };

        ErwarteWarnung(Pruefe(kaputt), "APP-BUY-005");
    }

    [Fact]
    public void Kaeufer_ElektronischeAdresseOhneSchema_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-BUY-006");

        Invoice kaputt = BaseInvoice with
        {
            Buyer = BaseInvoice.Buyer with
            {
                ElectronicAddress = "9930000000001",
                ElectronicAddressScheme = null,
            },
        };

        ErwarteWarnung(Pruefe(kaputt), "APP-BUY-006");
    }

    // --------------------------------------------------------------- Positionen

    [Fact]
    public void Positionen_KeinePositionen_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-001");

        Invoice kaputt = BaseInvoice with { Lines = [] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-001");
    }

    [Fact]
    public void Positionen_DoppelteNummer_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-002");

        InvoiceLine erste = BaseInvoice.Lines[0];
        Invoice kaputt = BaseInvoice with
        {
            Lines =
            [
                erste,
                erste with { Name = "Zweite Position mit gleicher Nummer" },
            ],
        };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-002");
    }

    [Fact]
    public void Positionen_NameLeer_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-003");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { Name = "   " };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-003");
    }

    [Fact]
    public void Positionen_MengeNull_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-004");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { Quantity = 0m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-004");
    }

    [Fact]
    public void Positionen_UnbekannteMengeneinheit_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-005");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { Unit = UnitCode.Parse("ZZZ") };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-005");
    }

    [Fact]
    public void Positionen_EinzelpreisNegativ_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-006");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { NetUnitPrice = -1m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-006");
    }

    [Fact]
    public void Positionen_PreisbasismengeNull_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-007");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { PriceBaseQuantity = 0m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-007");
    }

    [Fact]
    public void Positionen_RabattNegativ_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-008");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { AllowanceAmount = -10m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-008");
    }

    [Fact]
    public void Positionen_RabattGroesserAlsPositionsbetrag_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-009");

        // Quantity 10 * NetUnitPrice 95 = 950 brutto; ein Rabatt von 2000 macht
        // den Positionsbetrag negativ, ohne selbst negativ zu sein
        // (das waere APP-LIN-008).
        InvoiceLine zeile = BaseInvoice.Lines[0] with { AllowanceAmount = 2000m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-009");
    }

    [Fact]
    public void Positionen_LeistungszeitraumEndeVorBeginn_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-LIN-010");

        InvoiceLine zeile = BaseInvoice.Lines[0] with
        {
            ServicePeriodStart = new DateOnly(2026, 3, 1),
            ServicePeriodEnd = new DateOnly(2026, 2, 1),
        };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-LIN-010");
    }

    // ------------------------------------------------------- Steuersatz je Position

    [Fact]
    public void Steuer_SatzNegativ_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-VAT-001");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatRate = -5m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-VAT-001");
    }

    [Fact]
    public void Steuer_SatzUeber100Prozent_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-VAT-002");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatRate = 150m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-VAT-002");
    }

    [Fact]
    public void Steuer_RegelbesteuertMitSatzNull_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-VAT-003");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatRate = 0m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-VAT-003");
    }

    [Fact]
    public void Steuer_NullsatzbesteuertMitSatzGroesserNull_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-VAT-004");

        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatCategory = VatCategory.ZeroRated, VatRate = 19m };
        Invoice kaputt = BaseInvoice with { Lines = [zeile] };

        ErwarteFehler(Pruefe(kaputt), "APP-VAT-004");
    }

    [Fact]
    public void Steuer_SteuerbefreitMitSatzGroesserNull_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-VAT-005");

        // Begruendung wird mitgeliefert, damit nicht zusaetzlich APP-VAT-012
        // (fehlende Begruendung) anspringt – hier geht es allein um den Satz.
        InvoiceLine zeile = BaseInvoice.Lines[0] with { VatCategory = VatCategory.Exempt, VatRate = 19m };
        Invoice kaputt = BaseInvoice with
        {
            Lines = [zeile],
            ExemptionReasons = [new VatExemptionReason(VatCategory.Exempt, "Testbegruendung")],
        };

        ErwarteFehler(Pruefe(kaputt), "APP-VAT-005");
    }

    // ------------------------------------------------------- Steueraufschluesselung

    [Fact]
    public void Steuer_SteuerbefreiteKategorieOhneBegruendung_LoestFehlerAus()
    {
        InvoiceScenario steuerfrei = InvoiceScenarios.ByKey("04-steuerfrei");
        ErwarteKeinenFehler(Pruefe(steuerfrei.Invoice), "APP-VAT-012");

        Invoice kaputt = steuerfrei.Invoice with { ExemptionReasons = [] };

        ErwarteFehler(Pruefe(kaputt), "APP-VAT-012");
    }

    [Fact]
    public void Steuer_UnbekannterBegruendungscode_LoestWarnungAus()
    {
        // Hinweis: Der Golden Master "04-steuerfrei" verwendet bereits den
        // Subcode "VATEX-EU-132-1A", der in der kuratierten Codeliste nicht
        // enthalten ist (nur "VATEX-EU-132") – er loest die Warnung also
        // bereits selbst aus. Fuer den Positivtest wird deshalb zuerst auf
        // einen bekannten Code umgestellt, bevor gezielt ein unbekannter
        // Code fuer den Negativtest eingesetzt wird.
        InvoiceScenario steuerfrei = InvoiceScenarios.ByKey("04-steuerfrei");
        VatExemptionReason original = steuerfrei.Invoice.ExemptionReasons[0];

        Invoice bekannterCode = steuerfrei.Invoice with
        {
            ExemptionReasons = [original with { ReasonCode = "VATEX-EU-132" }],
        };

        ErwarteKeineWarnung(Pruefe(bekannterCode), "APP-VAT-013");

        Invoice kaputt = steuerfrei.Invoice with
        {
            ExemptionReasons = [original with { ReasonCode = "VATEX-UNBEKANNT-1" }],
        };

        ErwarteWarnung(Pruefe(kaputt), "APP-VAT-013");
    }

    // ------------------------------------------------------------------ Summen

    [Fact]
    public void Summen_LineTotalWeichtAb_LoestFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-001");

        InvoiceTotals manipuliert = totals with { LineTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-001");
    }

    [Fact]
    public void Summen_NettosummeWeichtAb_LoestFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-002");

        InvoiceTotals manipuliert = totals with { TaxBasisTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-002");
    }

    [Fact]
    public void Summen_SteuersummeWeichtAb_LoestFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-003");

        InvoiceTotals manipuliert = totals with { TaxTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-003");
    }

    [Fact]
    public void Summen_BruttosummeWeichtAb_LoestFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-004");

        InvoiceTotals manipuliert = totals with { GrandTotal = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-004");
    }

    [Fact]
    public void Summen_ZahlbetragWeichtAb_LoestFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-005");

        InvoiceTotals manipuliert = totals with { DuePayableAmount = 1m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-005");
    }

    [Fact]
    public void Summen_BetragMitDreiNachkommastellen_LoestFehlerAus()
    {
        InvoiceTotals totals = InvoiceCalculator.Calculate(BaseInvoice);
        ErwarteKeinenFehler(Validator.Validate(BaseInvoice, totals), "APP-SUM-006");

        // RoundingAmount wird nur auf die Nachkommastellen geprueft, nicht mit
        // einer Neuberechnung verglichen – so bleibt der Verstoss isoliert.
        InvoiceTotals manipuliert = totals with { RoundingAmount = 0.001m };

        ErwarteFehler(Validator.Validate(BaseInvoice, manipuliert), "APP-SUM-006");
    }

    [Fact]
    public void Summen_BereitsGezahlterBetragNegativ_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-SUM-007");

        Invoice kaputt = BaseInvoice with { PaidAmount = -10m };

        ErwarteFehler(Pruefe(kaputt), "APP-SUM-007");
    }

    [Fact]
    public void Summen_BereitsGezahlterBetragUeberBruttosumme_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-SUM-008");

        Invoice kaputt = BaseInvoice with { PaidAmount = 5000m };

        ErwarteWarnung(Pruefe(kaputt), "APP-SUM-008");
    }

    [Fact]
    public void Summen_RundungsbetragUngewoehnlichHoch_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-SUM-009");

        Invoice kaputt = BaseInvoice with { RoundingAmount = 5.00m };

        ErwarteWarnung(Pruefe(kaputt), "APP-SUM-009");
    }

    [Fact]
    public void Summen_NegativerZahlbetragBeiNormalerRechnung_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-SUM-010");

        // TypeCode bleibt die normale Handelsrechnung (380); ein stark negativer
        // Rundungsbetrag reisst den Zahlbetrag ins Minus, ohne PaidAmount zu
        // veraendern (das ist Voraussetzung der Regel).
        Invoice kaputt = BaseInvoice with { RoundingAmount = -2000m };

        ErwarteFehler(Pruefe(kaputt), "APP-SUM-010");
    }

    // ---------------------------------------------------------------- Zahlung

    [Fact]
    public void Zahlung_KeineZahlungsangabenBeiOffenemBetrag_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-PAY-001");

        // DueDate bleibt erhalten, damit nicht zusaetzlich APP-DOC-006 anspringt.
        Invoice kaputt = BaseInvoice with { Payment = null };

        ErwarteWarnung(Pruefe(kaputt), "APP-PAY-001");
    }

    [Fact]
    public void Zahlung_UeberweisungOhneBankverbindung_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-PAY-003");

        Invoice kaputt = BaseInvoice with { Payment = BaseInvoice.Payment! with { BankAccount = null } };

        ErwarteFehler(Pruefe(kaputt), "APP-PAY-003");
    }

    [Fact]
    public void Zahlung_KontoinhaberLeer_LoestWarnungAus()
    {
        ErwarteKeineWarnung(Pruefe(BaseInvoice), "APP-PAY-004");

        Invoice kaputt = BaseInvoice with
        {
            Payment = BaseInvoice.Payment! with
            {
                BankAccount = BaseInvoice.Payment!.BankAccount! with { AccountHolder = "" },
            },
        };

        ErwarteWarnung(Pruefe(kaputt), "APP-PAY-004");
    }

    [Fact]
    public void Zahlung_BicUngueltig_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-PAY-005");

        Invoice kaputt = BaseInvoice with
        {
            Payment = BaseInvoice.Payment! with
            {
                BankAccount = BaseInvoice.Payment!.BankAccount! with { Bic = "ABC" },
            },
        };

        ErwarteFehler(Pruefe(kaputt), "APP-PAY-005");
    }

    [Fact]
    public void Zahlung_IbanLaenderpraefixUnbekannt_LoestFehlerAus()
    {
        ErwarteKeinenFehler(Pruefe(BaseInvoice), "APP-PAY-006");

        // Formal gueltige IBAN (Pruefziffer nach ISO 7064 Mod 97-10 stimmt),
        // aber mit einem Laenderpraefix ('QQ'), das in keiner ISO-3166-Liste
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

        ErwarteFehler(Pruefe(kaputt), "APP-PAY-006");
    }

    // --------------------------------------------------------- Gruppenuebergreifend

    [Fact]
    public void FehlerInEinerGruppeVerhindertNichtDiePruefungAndererGruppen()
    {
        // Je ein Verstoss aus Dokument, Verkaeufer, Kaeufer, Positionen und
        // Zahlung gleichzeitig. Alle Kennungen muessen im selben Bericht
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

        ValidationReport report = Pruefe(kaputt);

        ErwarteFehler(report, "APP-DOC-001");
        ErwarteFehler(report, "APP-SEL-001");
        ErwarteFehler(report, "APP-BUY-001");
        ErwarteFehler(report, "APP-LIN-003");
        ErwarteFehler(report, "APP-PAY-005");

        Assert.True(
            report.Findings.Select(f => f.RuleId).Distinct(StringComparer.Ordinal).Count() >= 5,
            $"Es werden mindestens fuenf unterschiedliche Kennungen erwartet: {Beschreibe(report)}");
    }
}

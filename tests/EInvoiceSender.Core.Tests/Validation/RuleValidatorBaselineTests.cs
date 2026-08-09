using EInvoiceSender.Core.Calculation;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Tests.Support;
using EInvoiceSender.Core.Validation;
using Xunit;

namespace EInvoiceSender.Core.Tests.Rules;

/// <summary>
/// Grundsicherung des Regelvalidators.
///
/// Der wichtigste Test hier ist der erste: **Keiner der Golden Master darf
/// beanstandet werden.** Alle acht sind vom offiziellen CEN-Schematron als
/// gültig bestätigt. Meldet die eigene Vorabprüfung dort einen Fehler, ist
/// die eigene Regel falsch – nicht die Rechnung. Ein zu strenger Validator
/// wäre schlimmer als gar keiner, weil er den Anwender an einer korrekten
/// Rechnung hindert.
/// </summary>
public sealed class RuleValidatorBaselineTests
{
    /// <summary>Feste Zeitquelle, damit Datumsprüfungen reproduzierbar sind.</summary>
    private static readonly TimeProvider FixedClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(2)));

    private static readonly En16931RuleValidator Validator = new(FixedClock);

    [Theory]
    [MemberData(nameof(ValidScenarioKeys))]
    public void GoldenMasterWirdNichtBeanstandet(string key)
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey(key);
        InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);

        ValidationReport report = Validator.Validate(scenario.Invoice, totals);

        Assert.False(
            report.HasErrors,
            $"Der Golden Master '{key}' ist vom CEN-Schematron als gültig bestätigt, "
            + $"wird von der eigenen Prüfung aber beanstandet: {Describe(report)}");
    }

    [Fact]
    public void JederBefundTrägtEineStabileKennungUndEinenDeutschenSatz()
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey("01-dienstleistung-19");

        // Eine Rechnung mit mehreren Mängeln erzeugen, damit genügend Befunde
        // zusammenkommen.
        var broken = scenario.Invoice with
        {
            InvoiceNumber = "   ",
            Currency = CurrencyCode.Parse("XYZ"),
            DueDate = scenario.Invoice.IssueDate.AddDays(-5),
            Seller = scenario.Invoice.Seller with { VatId = null, TaxNumber = null, Email = "kaputt" },
        };

        ValidationReport report = Validator.Validate(broken, InvoiceCalculator.Calculate(broken));

        Assert.True(report.HasErrors);

        foreach (ValidationFinding finding in report.Findings)
        {
            Assert.StartsWith("APP-", finding.RuleId, StringComparison.Ordinal);
            Assert.NotEmpty(finding.Message);

            // Die Meldung muss ein Satz sein, kein Regelkürzel.
            Assert.EndsWith(".", finding.Message.TrimEnd(), StringComparison.Ordinal);
            Assert.DoesNotContain("BR-", finding.Message, StringComparison.Ordinal);

            // Die technische Zusammenfassung trägt die Kennung und, falls
            // vorhanden, die Normregel.
            string technical = finding.BuildTechnicalSummary();
            Assert.Contains(finding.RuleId, technical, StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(finding.NormRule))
            {
                Assert.Contains(finding.NormRule, technical, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void KennungenSindInnerhalbEinesBerichtsEindeutigZugeordnet()
    {
        // Dieselbe Kennung darf nicht für zwei fachlich verschiedene Aussagen
        // stehen. Geprüft über alle Golden Master hinweg.
        var meldungenJeKennung = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (InvoiceScenario scenario in InvoiceScenarios.All)
        {
            InvoiceTotals totals = InvoiceCalculator.Calculate(scenario.Invoice);

            foreach (ValidationFinding finding in Validator.Validate(scenario.Invoice, totals).Findings)
            {
                if (meldungenJeKennung.TryGetValue(finding.RuleId, out string? bekannt))
                {
                    Assert.Equal(bekannt, ExtractTemplate(finding.Message));
                }
                else
                {
                    meldungenJeKennung[finding.RuleId] = ExtractTemplate(finding.Message);
                }
            }
        }
    }

    [Fact]
    public void ValidatorVerändertDieRechnungNicht()
    {
        InvoiceScenario scenario = InvoiceScenarios.ByKey("05-rabatt");
        InvoiceTotals before = InvoiceCalculator.Calculate(scenario.Invoice);

        Validator.Validate(scenario.Invoice, before);

        InvoiceTotals after = InvoiceCalculator.Calculate(scenario.Invoice);

        Assert.Equal(before.GrandTotal, after.GrandTotal);
        Assert.Equal(before.DuePayableAmount, after.DuePayableAmount);
        Assert.Equal(before.LineNetAmounts, after.LineNetAmounts);
    }

    public static TheoryData<string> ValidScenarioKeys()
    {
        var data = new TheoryData<string>();

        foreach (InvoiceScenario scenario in InvoiceScenarios.All.Where(s => s.ExpectedToBeValid))
        {
            data.Add(scenario.Key);
        }

        return data;
    }

    private static string Describe(ValidationReport report)
        => string.Join(
            " | ",
            report.Findings
                .Where(f => f.Severity == FindingSeverity.Error)
                .Select(f => $"{f.RuleId} ({f.NormRule}): {f.Message} [{f.TechnicalDetail}]"));

    /// <summary>Entfernt eingesetzte Werte, damit Meldungen vergleichbar bleiben.</summary>
    private static string ExtractTemplate(string message)
    {
        int quote = message.IndexOf('\'', StringComparison.Ordinal);

        return quote < 0 ? message : message[..quote];
    }
}

/// <summary>Zeitquelle mit festem Wert für reproduzierbare Datumsprüfungen.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

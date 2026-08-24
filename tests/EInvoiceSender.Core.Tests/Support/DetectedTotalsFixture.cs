using EInvoiceSender.Core.Pdf.Detection;

namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Erzeugt die Dokumentsummen, gegen die der Positionsdetektor seine Tabelle
/// prüft.
///
/// Die Werte sind hier ausdrücklich gesetzt und nicht selbst erkannt. Das
/// trennt die beiden Fragen sauber: Ob der <c>TotalsDetector</c> eine
/// Summenzeile liest, prüfen seine eigenen Tests; hier geht es allein darum,
/// ob die Positionstabelle gegen eine gegebene Summe hält.
/// </summary>
public static class DetectedTotalsFixture
{
    /// <summary>Netto, Steuer und Brutto, alle zweifelsfrei gelesen.</summary>
    public static DetectedTotals High(decimal net, decimal tax, decimal gross) => new()
    {
        Net = Value(net),
        Tax = Value(tax),
        Gross = Value(gross),
        VatRates = [],
    };

    /// <summary>
    /// Dieselben Summen zuzüglich genau eines sicheren Steuersatzes – für
    /// Tabellen ohne eigene Steuerspalte, die ihren Satz aus dem Dokument
    /// beziehen müssen.
    /// </summary>
    public static DetectedTotals WithVatRate(
        decimal net, decimal tax, decimal gross, decimal rate)
        => High(net, tax, gross) with { VatRates = [Value(rate)] };

    /// <summary>
    /// Summen mit frei wählbaren Vertrauensstufen – für den arithmetisch
    /// bestätigten Steuersatz-Rückfall, der ausdrücklich davon abhängt,
    /// welcher Wert wie sicher gelesen wurde.
    /// </summary>
    public static DetectedTotals Custom(
        decimal net,
        decimal tax,
        decimal gross,
        decimal? rate = null,
        DetectionConfidence netConfidence = DetectionConfidence.High,
        DetectionConfidence taxConfidence = DetectionConfidence.High,
        DetectionConfidence grossConfidence = DetectionConfidence.High,
        DetectionConfidence rateConfidence = DetectionConfidence.Medium)
        => new()
        {
            Net = Value(net, netConfidence),
            Tax = Value(tax, taxConfidence),
            Gross = Value(gross, grossConfidence),
            VatRates = rate is { } single ? [Value(single, rateConfidence)] : [],
        };

    /// <summary>Mehrere Dokumentsteuersätze – bleibt mehrdeutig.</summary>
    public static DetectedTotals WithVatRates(
        decimal net, decimal tax, decimal gross, params decimal[] rates)
        => High(net, tax, gross) with
        {
            VatRates = [.. rates.Select(rate => Value(rate, DetectionConfidence.Medium))],
        };

    private static DetectedValue<decimal> Value(
        decimal value, DetectionConfidence confidence = DetectionConfidence.High)
        => new(value, confidence, "Synthetische Summenzeile", "Eindeutiger Testwert");
}

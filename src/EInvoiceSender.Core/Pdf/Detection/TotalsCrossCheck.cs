using System.Globalization;
using EInvoiceSender.Core.Calculation;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Das Ergebnis des Abgleichs zwischen PDF-Summen und berechneten Summen.</summary>
/// <param name="WasPerformed">Konnte ueberhaupt verglichen werden?</param>
/// <param name="Matches">Stimmen die Betraege ueberein?</param>
/// <param name="Message">Ein Satz fuer den Anwender.</param>
public sealed record TotalsComparison(bool WasPerformed, bool Matches, string Message)
{
    /// <summary>Es lag kein erkannter Betrag vor, mit dem sich vergleichen liesse.</summary>
    public static TotalsComparison NotPossible { get; } = new(
        false, false,
        "In der PDF wurde kein Gesamtbetrag gefunden, mit dem sich die erfassten Daten "
        + "vergleichen lassen.");
}

/// <summary>
/// Vergleicht den aus der PDF gelesenen Gesamtbetrag mit dem aus den erfassten
/// Positionen berechneten.
///
/// **Der PDF-Betrag ist dabei keine rechtliche Wahrheit.** Er ist ein zweites,
/// unabhaengiges Signal. Stimmen beide ueberein, ist das ein starker Hinweis,
/// dass die Positionen vollstaendig erfasst wurden. Weichen sie ab, sagt das
/// noch nicht, welcher Wert falsch ist – nur, dass jemand hinsehen sollte.
///
/// Deshalb blockiert eine Abweichung nichts. Sie erzeugt einen Hinweis.
/// </summary>
public static class TotalsCrossCheck
{
    /// <summary>Zwei Cent Spielraum fuer Rundungsunterschiede der Vorlage.</summary>
    private const decimal Tolerance = 0.02m;

    /// <summary>Vergleicht die erkannten mit den berechneten Summen.</summary>
    public static TotalsComparison Compare(DetectedTotals detected, InvoiceTotals calculated)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(calculated);

        decimal? fromPdf = detected.Payable?.Value ?? detected.Gross?.Value;

        if (fromPdf is not { } pdfAmount)
        {
            return TotalsComparison.NotPossible;
        }

        decimal ourAmount = detected.Payable is not null
            ? calculated.DuePayableAmount
            : calculated.GrandTotal;

        decimal difference = Math.Abs(pdfAmount - ourAmount);

        if (difference <= Tolerance)
        {
            return new TotalsComparison(
                true, true,
                $"Der Gesamtbetrag stimmt mit der PDF ueberein ({Money(ourAmount)}).");
        }

        return new TotalsComparison(
            true, false,
            "Der aus der PDF gelesene Gesamtbetrag unterscheidet sich vom berechneten. "
            + $"PDF: {Money(pdfAmount)}, erfasste Daten: {Money(ourAmount)}. "
            + "Bitte pruefen Sie die Rechnungspositionen.");
    }

    private static string Money(decimal value)
        => value.ToString("N2", CultureInfo.GetCultureInfo("de-DE"));
}

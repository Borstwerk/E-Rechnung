using System.Globalization;
using EInvoiceSender.Core.Calculation;

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Das Ergebnis des Abgleichs zwischen PDF-Summen und berechneten Summen.</summary>
/// <param name="WasPerformed">Konnte überhaupt verglichen werden?</param>
/// <param name="Matches">Stimmen die Beträge überein?</param>
/// <param name="Message">Ein Satz für den Anwender.</param>
public sealed record TotalsComparison(bool WasPerformed, bool Matches, string Message)
{
    /// <summary>Es lag kein erkannter Betrag vor, mit dem sich vergleichen ließe.</summary>
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
/// unabhängiges Signal. Stimmen beide überein, ist das ein starker Hinweis,
/// dass die Positionen vollständig erfasst wurden. Weichen sie ab, sagt das
/// noch nicht, welcher Wert falsch ist – nur, dass jemand hinsehen sollte.
///
/// Deshalb blockiert eine Abweichung nichts. Sie erzeugt einen Hinweis.
/// </summary>
public static class TotalsCrossCheck
{
    /// <summary>Zwei Cent Spielraum für Rundungsunterschiede der Vorlage.</summary>
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
                $"Der Gesamtbetrag stimmt mit der PDF überein ({Money(ourAmount)}).");
        }

        return new TotalsComparison(
            true, false,
            "Der aus der PDF gelesene Gesamtbetrag unterscheidet sich vom berechneten. "
            + $"PDF: {Money(pdfAmount)}, erfasste Daten: {Money(ourAmount)}. "
            + "Bitte prüfen Sie die Rechnungspositionen.");
    }

    private static string Money(decimal value)
        => value.ToString("N2", CultureInfo.GetCultureInfo("de-DE"));
}

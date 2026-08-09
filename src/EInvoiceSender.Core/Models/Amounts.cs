using System.Globalization;

namespace EInvoiceSender.Core.Models;

/// <summary>
/// Rundung und Formatierung von Geldbeträgen.
/// EN 16931 erlaubt für Betragsfelder höchstens zwei Nachkommastellen
/// (BR-DEC-09 bis BR-DEC-17). Das CEN-Schematron rundet als
/// <c>round(x * 10 * 10) div 100</c>, also kaufmännisch von der Null weg.
/// Genau dieses Verhalten bildet <see cref="Round"/> ab.
/// </summary>
public static class Amounts
{
    /// <summary>Anzahl der Nachkommastellen für Beträge nach EN 16931.</summary>
    public const int AmountDecimals = 2;

    /// <summary>
    /// Kaufmännische Rundung auf zwei Nachkommastellen.
    /// <c>MidpointRounding.AwayFromZero</c> entspricht der XPath-Funktion
    /// <c>round()</c>, die das CEN-Schematron verwendet.
    /// </summary>
    public static decimal Round(decimal value)
        => decimal.Round(value, AmountDecimals, MidpointRounding.AwayFromZero);

    /// <summary>Rundung auf eine frei gewählte Stellenzahl, gleiche Regel.</summary>
    public static decimal Round(decimal value, int decimals)
        => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Prüft, ob ein Wert höchstens die erlaubte Zahl an Nachkommastellen hat.
    /// Nachgestellte Nullen zählen nicht (1,50 hat zwei, 1,5000 ebenfalls).
    /// </summary>
    public static bool HasAtMostDecimals(decimal value, int decimals)
        => value == decimal.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Vergleicht zwei Beträge mit einer Toleranz von einem Cent.
    /// Wird für Konsistenzprüfungen benutzt, bei denen unterschiedliche
    /// Rundungsreihenfolgen zu einer Abweichung von 0,01 führen können.
    /// </summary>
    public static bool NearlyEqual(decimal a, decimal b, decimal tolerance = 0.01m)
        => Math.Abs(a - b) <= tolerance;

    /// <summary>
    /// Schreibt einen Betrag so, wie er in der XML stehen muss:
    /// Punkt als Dezimaltrennzeichen, immer zwei Nachkommastellen,
    /// kein Tausendertrennzeichen.
    /// </summary>
    public static string ToXmlString(decimal value)
        => Round(value).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Schreibt einen Prozentsatz für die XML. Steuersätze dürfen
    /// Nachkommastellen haben (z. B. 8,25), sollen aber nicht unnötig
    /// aufgefüllt werden.
    /// </summary>
    public static string RateToXmlString(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero)
            .ToString("0.##", CultureInfo.InvariantCulture);
}

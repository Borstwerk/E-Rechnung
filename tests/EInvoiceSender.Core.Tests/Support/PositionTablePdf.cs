using System.Globalization;

namespace EInvoiceSender.Core.Tests.Support;

/// <summary>Eine Zeile der synthetischen Positionstabelle.</summary>
/// <param name="Name">Bezeichnung in der Beschreibungsspalte.</param>
/// <param name="Quantity">Menge in deutscher Schreibweise.</param>
/// <param name="Unit">Einheit, wie sie in der PDF steht (etwa <c>Std</c>).</param>
/// <param name="UnitPrice">Einzelpreis in deutscher Schreibweise.</param>
/// <param name="LineTotal">Gesamtpreis der Zeile.</param>
/// <param name="VatRate">Steuersatz, etwa <c>19 %</c>.</param>
/// <param name="Continuation">
/// Zusätzliche Beschreibungszeile unmittelbar darunter, falls vorhanden.
/// </param>
public sealed record PositionTableRow(
    string Name,
    string Quantity,
    string Unit,
    string UnitPrice,
    string LineTotal,
    string VatRate,
    string? Continuation = null);

/// <summary>
/// Baut PDF-Dateien mit einer Positionstabelle, die die Phase-A-Erkennung
/// vollständig annimmt.
///
/// **Warum das hier steht und nicht in <c>PositionDetectorTests</c>:** Dort
/// wird die Erkennung isoliert geprüft, mit von Hand gesetzten Summen. Phase B
/// braucht denselben Aufbau für den *produktiven* Weg – also mit Summen, die
/// der <c>TotalsDetector</c> selbst aus der Seite liest. Beide Seiten benutzen
/// dieselbe Spaltengeometrie; sie steht deshalb an einer Stelle.
///
/// Die Maße entsprechen einer gewöhnlichen Rechnungstabelle: Beschreibung
/// links, Menge und Einheit in der Mitte, Preise rechtsbündig, Steuersatz ganz
/// rechts.
/// </summary>
public static class PositionTablePdf
{
    private const double PositionLeft = 30;
    private const double DescriptionLeft = 100;
    private const double QuantityLeft = 310;
    private const double UnitLeft = 350;
    private const double UnitPriceRight = 465;
    private const double LineTotalRight = 548;
    private const double VatLeft = 560;
    private const double HeaderTop = 100;
    private const double FirstRowTop = 126;
    private const double RowStep = 18;

    /// <summary>
    /// Zwei Positionen mit verschiedenen Einheiten und 19 %:
    /// 2 Std à 100,00 und 3 Stk à 20,00 – netto 260,00, Steuer 49,40,
    /// brutto 309,40.
    /// </summary>
    public static byte[] TwoLines() => Create(
        net: "260,00",
        tax: "49,40",
        gross: "309,40",
        rows:
        [
            new PositionTableRow("Beratung", "2", "Std", "100,00", "200,00", "19 %"),
            new PositionTableRow("Schulungsunterlagen", "3", "Stk", "20,00", "60,00", "19 %"),
        ]);

    /// <summary>
    /// Setzt eine vollständige Seite mit Kopf, Zeilen und Summen zusammen.
    ///
    /// <paramref name="taxLabel"/> beschriftet die Steuerzeile. Der Standard
    /// passt zu einer Seite mit einem einzigen Steuersatz; bei gemischten
    /// Sätzen gehört dort keine Prozentzahl hin, sonst behauptete die
    /// Testseite etwas, das ihre eigenen Zeilen widerlegen.
    /// </summary>
    public static byte[] Create(
        string net,
        string tax,
        string gross,
        string taxLabel = "Umsatzsteuer 19 %",
        params PositionTableRow[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var fragments = new List<PositionedPdfText>
        {
            new("Muster IT GmbH · Musterstraße 10 · 18055 Rostock", 56, 32),
            new("Rechnungsnummer: RE-2026-0815 · Rechnungsdatum: 09.08.2026", 56, 50),
            new("Leistungsübersicht der digital erstellten Rechnung", 56, 68),

            new("Pos.", PositionLeft, HeaderTop),
            new("Beschreibung", DescriptionLeft, HeaderTop),
            new("Menge", QuantityLeft, HeaderTop),
            new("Einheit", UnitLeft, HeaderTop),
            new("Einzelpreis", 400, HeaderTop),
            new("Gesamtpreis", 490, HeaderTop),
            new("MwSt", VatLeft, HeaderTop),
        };

        double top = FirstRowTop;

        for (int index = 0; index < rows.Length; index++)
        {
            PositionTableRow row = rows[index];
            string number = (index + 1).ToString(CultureInfo.InvariantCulture);

            fragments.Add(new PositionedPdfText(number, PositionLeft, top));
            fragments.Add(new PositionedPdfText(row.Name, DescriptionLeft, top));
            fragments.Add(new PositionedPdfText(row.Quantity, QuantityLeft, top));
            fragments.Add(new PositionedPdfText(row.Unit, UnitLeft, top));
            fragments.Add(Right(row.UnitPrice, UnitPriceRight, top));
            fragments.Add(Right(row.LineTotal, LineTotalRight, top));
            fragments.Add(new PositionedPdfText(row.VatRate, VatLeft, top));
            top += RowStep;

            if (row.Continuation is not null)
            {
                fragments.Add(new PositionedPdfText(row.Continuation, DescriptionLeft, top));
                top += RowStep;
            }
        }

        fragments.Add(new PositionedPdfText("Gesamt Netto", DescriptionLeft, top));
        fragments.Add(Right(net, LineTotalRight, top));
        top += RowStep;

        fragments.Add(new PositionedPdfText(taxLabel, DescriptionLeft, top));
        fragments.Add(Right(tax, LineTotalRight, top));
        top += RowStep;

        fragments.Add(new PositionedPdfText("Gesamtbetrag", DescriptionLeft, top));
        fragments.Add(Right(gross, LineTotalRight, top));

        return TextPdfBuilder.CreatePositioned(fragments);
    }

    private static PositionedPdfText Right(string text, double right, double top)
        => new(text, right - Width(text), top);

    /// <summary>
    /// Näherung der Helvetica-Breite. Sie muss nicht genau sein – nur gut
    /// genug, damit rechtsbündige Beträge in ihrer Spalte bleiben.
    /// </summary>
    private static double Width(string text)
        => text.Sum(character => character is '.' or ',' ? 2.78 : 5.56);
}

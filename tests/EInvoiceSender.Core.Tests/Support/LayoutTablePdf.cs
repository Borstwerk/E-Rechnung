namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Eine Spalte einer synthetischen Positionstabelle.
/// </summary>
/// <param name="Header">Die Beschriftung, wie sie im Tabellenkopf steht.</param>
/// <param name="HeaderLeft">Linke Kante der Beschriftung.</param>
/// <param name="CellAnchor">
/// Linke Kante der Zellen – oder bei <paramref name="RightAligned"/> deren
/// rechte Kante.
/// </param>
/// <param name="RightAligned">
/// Beträge stehen in Rechnungen rechtsbündig. Die Spaltengrenzen leitet der
/// Detektor aus dem Kopf ab; eine Zelle, die über ihre Spalte hinausragt, ist
/// nicht mehr eindeutig zuzuordnen. Genau deshalb steht die Geometrie hier
/// ausdrücklich und nicht geraten.
/// </param>
public sealed record TableColumn(
    string Header,
    double HeaderLeft,
    double CellAnchor,
    bool RightAligned = false)
{
    /// <summary>Eine linksbündige Textspalte – Kopf und Zellen an derselben Kante.</summary>
    public static TableColumn Text(string header, double left) => new(header, left, left);

    /// <summary>Eine rechtsbündige Betragsspalte.</summary>
    public static TableColumn Money(string header, double headerLeft, double cellRight)
        => new(header, headerLeft, cellRight, RightAligned: true);
}

/// <summary>
/// Baut synthetische Rechnungsseiten mit frei wählbarem Tabellenaufbau.
///
/// **Warum neben <see cref="PositionTablePdf"/> noch dieser Baustein:** Jener
/// bildet genau eine feste Spaltenfolge ab und ist damit für den
/// Phase-B-Durchstich richtig. Phase C prüft dagegen mehrere reale
/// Tabellenfamilien – mit und ohne Einheitsspalte, mit Lieferdatum, mit
/// finanziellen Kontrollspalten. Dafür muss der Aufbau selbst zum Parameter
/// werden.
///
/// Alle Seiten entstehen synthetisch. Es wandert keine fremde Rechnung, kein
/// Screenshot und keine echte Kundendatei in das Repository.
/// </summary>
public static class LayoutTablePdf
{
    private const double HeaderTop = 100;
    private const double FirstRowTop = 126;
    private const double RowStep = 18;

    /// <summary>
    /// Setzt Kopfzeilen, Tabellenkopf, Positionszeilen und Summenblock
    /// zusammen. <paramref name="rows"/> enthält je Zeile genau so viele
    /// Zellen wie <paramref name="columns"/> Spalten hat; eine leere
    /// Zeichenkette lässt die Zelle weg.
    /// </summary>
    public static byte[] Create(
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string net,
        string tax,
        string gross,
        string taxLabel = "Umsatzsteuer 19 %")
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var fragments = new List<PositionedPdfText>
        {
            new("Muster IT GmbH · Musterstraße 10 · 18055 Rostock", 56, 32),
            new("Rechnungsnummer: RE-2026-0815 · Rechnungsdatum: 09.08.2026", 56, 50),
            new("Leistungsübersicht der digital erstellten Rechnung", 56, 68),
        };

        fragments.AddRange(columns.Select(
            column => new PositionedPdfText(column.Header, column.HeaderLeft, HeaderTop)));

        double top = FirstRowTop;

        foreach (IReadOnlyList<string> row in rows)
        {
            if (row.Count != columns.Count)
            {
                throw new ArgumentException(
                    $"Die Zeile hat {row.Count} Zellen, der Kopf aber {columns.Count} Spalten.",
                    nameof(rows));
            }

            for (int index = 0; index < row.Count; index++)
            {
                if (row[index].Length > 0)
                {
                    fragments.Add(Cell(columns[index], row[index], top));
                }
            }

            top += RowStep;
        }

        double labelLeft = columns[0].HeaderLeft;
        double amountRight = columns[^1].RightAligned ? columns[^1].CellAnchor : 520;

        fragments.Add(new PositionedPdfText("Gesamt Netto", labelLeft, top));
        fragments.Add(Right(net, amountRight, top));
        top += RowStep;

        fragments.Add(new PositionedPdfText(taxLabel, labelLeft, top));
        fragments.Add(Right(tax, amountRight, top));
        top += RowStep;

        fragments.Add(new PositionedPdfText("Gesamtbetrag", labelLeft, top));
        fragments.Add(Right(gross, amountRight, top));

        return TextPdfBuilder.CreatePositioned(fragments);
    }

    private static PositionedPdfText Cell(TableColumn column, string text, double top)
        => column.RightAligned
            ? Right(text, column.CellAnchor, top)
            : new PositionedPdfText(text, column.CellAnchor, top);

    private static PositionedPdfText Right(string text, double right, double top)
        => new(text, right - Width(text), top);

    /// <summary>
    /// Näherung der Helvetica-Breite. Sie muss nicht genau sein – nur gut
    /// genug, damit rechtsbündige Beträge in ihrer Spalte bleiben.
    /// </summary>
    private static double Width(string text)
        => text.Sum(character => character is '.' or ',' ? 2.78 : 5.56);
}

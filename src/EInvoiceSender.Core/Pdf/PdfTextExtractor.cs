using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace EInvoiceSender.Core.Pdf;

/// <summary>Eine Textzeile aus der PDF, mit ihrer Herkunft.</summary>
/// <param name="Text">Der Zeileninhalt, Wörter durch je ein Leerzeichen getrennt.</param>
/// <param name="PageNumber">Die Seite, beginnend bei 1.</param>
/// <param name="Top">
/// Abstand vom oberen Seitenrand in PDF-Punkten. Wird gebraucht, um Briefkopf
/// und Fußbereich von der Dokumentmitte zu unterscheiden.
/// </param>
public sealed record PdfTextLine(
    string Text,
    int PageNumber,
    double Top,
    IReadOnlyList<PdfTextSegment> Segments)
{
    /// <summary>Der linke Rand des ersten Abschnitts.</summary>
    public double Left => Segments.Count > 0 ? Segments[0].Left : 0;
}

/// <summary>
/// Ein waagerecht zusammenhängender Textblock innerhalb einer Zeile.
///
/// Zweispaltige Rechnungen setzen Empfängerblock und Rechnungsdaten auf
/// dieselbe Grundlinie. Als eine Textzeile gelesen ergibt das Unsinn –
/// „Nordlicht Handel GmbH Rechnungsnummer: RE-2026-0815“. Die Abschnitte
/// halten deshalb fest, was räumlich zusammengehört, ohne dass die Zeile
/// selbst zerrissen wird: Summenzeilen brauchen Beschriftung und Betrag
/// weiterhin gemeinsam.
/// </summary>
/// <param name="Text">Der Inhalt des Abschnitts.</param>
/// <param name="Left">Sein linker Rand in PDF-Punkten.</param>
public sealed record PdfTextSegment(string Text, double Left);

/// <summary>Das Ergebnis der Textextraktion.</summary>
/// <param name="Lines">Alle Zeilen in Lesereihenfolge.</param>
/// <param name="PageCount">Anzahl der Seiten.</param>
/// <param name="HasUsableText">
/// Enthält die Datei genug Text, um eine Auswertung zu rechtfertigen? Bei
/// eingescannten Rechnungen ist das nicht der Fall.
/// </param>
public sealed record PdfTextResult(
    IReadOnlyList<PdfTextLine> Lines,
    int PageCount,
    bool HasUsableText)
{
    /// <summary>Ein Ergebnis ohne verwertbaren Text.</summary>
    public static PdfTextResult Empty { get; } = new([], 0, false);

    /// <summary>Der gesamte Text, Zeilen durch Zeilenumbruch getrennt.</summary>
    public string FullText => string.Join("\n", Lines.Select(l => l.Text));
}

/// <summary>Liest den bereits maschinenlesbaren Text einer PDF-Datei.</summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Liest den eingebetteten Text. Es findet **keine** Texterkennung an
    /// Bildern statt und es verlässt nichts das Gerät.
    /// </summary>
    Task<PdfTextResult> ExtractAsync(string pdfPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Liest den eingebetteten Text einer PDF über PdfPig.
///
/// **Ausschließlich örtlich.** Die Datei wird gelesen, der Text im
/// Arbeitsspeicher gehalten und nach der Auswertung verworfen. Er wird nicht
/// gespeichert, nicht protokolliert und nicht übertragen.
///
/// **Kein OCR.** Ausgewertet wird nur Text, der bereits in der Datei steht.
/// Eine eingescannte Rechnung liefert hier nichts – das ist kein Fehler,
/// sondern die richtige Antwort, und der Anwender wird darauf hingewiesen.
///
/// Warum eine eigene Bibliothek: PdfSharp, das die Anwendung ohnehin
/// verwendet, kann PDF-Dateien schreiben und verändern, hat aber **keine**
/// Textextraktion. Aus den rohen Zeichenanweisungen lesbaren Text zu machen
/// hieße, Zeichensatzkodierungen und ToUnicode-Tabellen selbst umzusetzen.
/// Ein Fehler darin erzeugt still verfälschten Text – und der landete dann in
/// Rechnungsfeldern. Deshalb PdfPig (Apache-2.0), siehe
/// docs/THIRD-PARTY-NOTICES.md.
/// </summary>
public sealed partial class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : IPdfTextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger = logger;

    /// <summary>
    /// Wörter, deren Grundlinien weniger als dieser Abstand trennt, gelten als
    /// dieselbe Zeile. Zwei Punkte fangen die üblichen kleinen Schwankungen
    /// innerhalb einer Zeile ab, ohne benachbarte Zeilen zu verschmelzen.
    /// </summary>
    private const double LineToleranceInPoints = 2.5;

    /// <summary>
    /// Unterhalb dieser Zeichenzahl gilt eine Datei als nicht auswertbar. Eine
    /// eingescannte Rechnung liefert oft ein paar Zeichen aus Metadaten oder
    /// einem Wasserzeichen; das ist keine Grundlage für eine Erkennung.
    /// </summary>
    private const int MinimumUsableCharacters = 120;

    /// <summary>
    /// Ab diesem waagerechten Abstand zwischen zwei Wörtern beginnt ein neuer
    /// Abschnitt. Der Wert liegt bewusst hoch: Innerhalb eines Textblocks
    /// stehen Wörter selten so weit auseinander, zwischen zwei Spalten fast
    /// immer.
    /// </summary>
    private const double ColumnGapInPoints = 60;

    /// <inheritdoc />
    public async Task<PdfTextResult> ExtractAsync(
        string pdfPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        try
        {
            return await Task.Run(() => Extract(pdfPath, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Die Erkennung ist eine Komfortfunktion. Schlägt sie fehl, wird
            // die Rechnung eben von Hand erfasst - der Ablauf läuft weiter.
            // Protokolliert wird nur die Fehlerart, niemals Dateiinhalt.
            string reason = exception.GetType().Name;
            LogExtractionFailed(reason);

            return PdfTextResult.Empty;
        }
    }

    private static PdfTextResult Extract(string pdfPath, CancellationToken cancellationToken)
    {
        using PdfDocument document = PdfDocument.Open(pdfPath);

        var lines = new List<PdfTextLine>();
        int characters = 0;

        foreach (Page page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (PdfTextLine line in BuildLines(page))
            {
                lines.Add(line);
                characters += line.Text.Length;
            }
        }

        return new PdfTextResult(lines, document.NumberOfPages, characters >= MinimumUsableCharacters);
    }

    /// <summary>
    /// Fasst die Wörter einer Seite zu Zeilen zusammen.
    ///
    /// PdfPig liefert einzelne Wörter mit ihrer Lage. Für die Erkennung ist
    /// die Zeile die entscheidende Einheit: "Rechnungsnummer: RE-2026-0815"
    /// ergibt nur zusammenhängend einen Sinn.
    /// </summary>
    private static IEnumerable<PdfTextLine> BuildLines(Page page)
    {
        IReadOnlyList<Word> words = [.. page.GetWords()];

        if (words.Count == 0)
        {
            yield break;
        }

        double pageTop = page.Height;

        IEnumerable<IGrouping<double, Word>> rows = words
            .GroupBy(w => Math.Round(w.BoundingBox.Bottom / LineToleranceInPoints) * LineToleranceInPoints)
            .OrderByDescending(g => g.Key);

        foreach (IGrouping<double, Word> row in rows)
        {
            List<PdfTextSegment> segments = SplitIntoSegments([.. row.OrderBy(w => w.BoundingBox.Left)]);

            if (segments.Count == 0)
            {
                continue;
            }

            string text = string.Join(" ", segments.Select(s => s.Text));

            yield return new PdfTextLine(text, page.Number, pageTop - row.Key, segments);
        }
    }

    /// <summary>
    /// Zerlegt die Wörter einer Grundlinie dort in Abschnitte, wo eine große
    /// Lücke auf einen Spaltenwechsel hindeutet.
    /// </summary>
    private static List<PdfTextSegment> SplitIntoSegments(IReadOnlyList<Word> words)
    {
        var segments = new List<PdfTextSegment>();
        var builder = new StringBuilder();
        double left = 0;
        double previousRight = 0;

        foreach (Word word in words)
        {
            bool startsNewSegment = builder.Length > 0
                                    && word.BoundingBox.Left - previousRight > ColumnGapInPoints;

            if (startsNewSegment)
            {
                Flush(segments, builder, left);
            }

            if (builder.Length == 0)
            {
                left = word.BoundingBox.Left;
            }
            else
            {
                builder.Append(' ');
            }

            builder.Append(word.Text);
            previousRight = word.BoundingBox.Right;
        }

        Flush(segments, builder, left);

        return segments;
    }

    private static void Flush(List<PdfTextSegment> segments, StringBuilder builder, double left)
    {
        string text = builder.ToString().Trim();

        if (text.Length > 0)
        {
            segments.Add(new PdfTextSegment(text, left));
        }

        builder.Clear();
    }

    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Der PDF-Text konnte nicht ausgewertet werden ({Reason}). Die Rechnungsdaten "
                  + "werden von Hand erfasst.")]
    private partial void LogExtractionFailed(string reason);
}

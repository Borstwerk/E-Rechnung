using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace EInvoiceSender.Core.Pdf;

/// <summary>Eine Textzeile aus der PDF, mit ihrer Herkunft.</summary>
/// <param name="Text">Der Zeileninhalt, Woerter durch je ein Leerzeichen getrennt.</param>
/// <param name="PageNumber">Die Seite, beginnend bei 1.</param>
/// <param name="Top">
/// Abstand vom oberen Seitenrand in PDF-Punkten. Wird gebraucht, um Briefkopf
/// und Fussbereich von der Dokumentmitte zu unterscheiden.
/// </param>
public sealed record PdfTextLine(string Text, int PageNumber, double Top);

/// <summary>Das Ergebnis der Textextraktion.</summary>
/// <param name="Lines">Alle Zeilen in Lesereihenfolge.</param>
/// <param name="PageCount">Anzahl der Seiten.</param>
/// <param name="HasUsableText">
/// Enthaelt die Datei genug Text, um eine Auswertung zu rechtfertigen? Bei
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
    /// Bildern statt und es verlaesst nichts das Geraet.
    /// </summary>
    Task<PdfTextResult> ExtractAsync(string pdfPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Liest den eingebetteten Text einer PDF ueber PdfPig.
///
/// **Ausschliesslich oertlich.** Die Datei wird gelesen, der Text im
/// Arbeitsspeicher gehalten und nach der Auswertung verworfen. Er wird nicht
/// gespeichert, nicht protokolliert und nicht uebertragen.
///
/// **Kein OCR.** Ausgewertet wird nur Text, der bereits in der Datei steht.
/// Eine eingescannte Rechnung liefert hier nichts – das ist kein Fehler,
/// sondern die richtige Antwort, und der Anwender wird darauf hingewiesen.
///
/// Warum eine eigene Bibliothek: PdfSharp, das die Anwendung ohnehin
/// verwendet, kann PDF-Dateien schreiben und veraendern, hat aber **keine**
/// Textextraktion. Aus den rohen Zeichenanweisungen lesbaren Text zu machen
/// hiesse, Zeichensatzkodierungen und ToUnicode-Tabellen selbst umzusetzen.
/// Ein Fehler darin erzeugt still verfaelschten Text – und der landete dann in
/// Rechnungsfeldern. Deshalb PdfPig (Apache-2.0), siehe
/// docs/THIRD-PARTY-NOTICES.md.
/// </summary>
public sealed partial class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : IPdfTextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger = logger;

    /// <summary>
    /// Woerter, deren Grundlinien weniger als dieser Abstand trennt, gelten als
    /// dieselbe Zeile. Zwei Punkte fangen die ueblichen kleinen Schwankungen
    /// innerhalb einer Zeile ab, ohne benachbarte Zeilen zu verschmelzen.
    /// </summary>
    private const double LineToleranceInPoints = 2.5;

    /// <summary>
    /// Unterhalb dieser Zeichenzahl gilt eine Datei als nicht auswertbar. Eine
    /// eingescannte Rechnung liefert oft ein paar Zeichen aus Metadaten oder
    /// einem Wasserzeichen; das ist keine Grundlage fuer eine Erkennung.
    /// </summary>
    private const int MinimumUsableCharacters = 120;

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
            // Die Erkennung ist eine Komfortfunktion. Schlaegt sie fehl, wird
            // die Rechnung eben von Hand erfasst - der Ablauf laeuft weiter.
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
    /// Fasst die Woerter einer Seite zu Zeilen zusammen.
    ///
    /// PdfPig liefert einzelne Woerter mit ihrer Lage. Fuer die Erkennung ist
    /// die Zeile die entscheidende Einheit: "Rechnungsnummer: RE-2026-0815"
    /// ergibt nur zusammenhaengend einen Sinn.
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
            var builder = new StringBuilder();

            foreach (Word word in row.OrderBy(w => w.BoundingBox.Left))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(word.Text);
            }

            string text = builder.ToString().Trim();

            if (text.Length > 0)
            {
                yield return new PdfTextLine(text, page.Number, pageTop - row.Key);
            }
        }
    }

    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Der PDF-Text konnte nicht ausgewertet werden ({Reason}). Die Rechnungsdaten "
                  + "werden von Hand erfasst.")]
    private partial void LogExtractionFailed(string reason);
}

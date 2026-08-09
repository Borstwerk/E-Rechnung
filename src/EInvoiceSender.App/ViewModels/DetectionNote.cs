using EInvoiceSender.Core.Pdf.Detection;

namespace EInvoiceSender.App.ViewModels;

/// <summary>Die Art einer Meldung in der Erkennungsübersicht.</summary>
public enum DetectionNoteKind
{
    /// <summary>Wert eindeutig gefunden.</summary>
    Found,

    /// <summary>Wert gefunden, aber nicht eindeutig – bitte prüfen.</summary>
    Uncertain,

    /// <summary>Nichts gefunden; die Angabe muss von Hand erfasst werden.</summary>
    Missing,
}

/// <summary>
/// Eine Zeile der Übersicht „PDF analysiert“, wie das Fenster sie anzeigt.
///
/// Der Text kommt aus <see cref="DetectionOverview"/>; hier kommt nur die
/// Darstellung dazu. Das Zeichen steht neben dem Text, nicht statt seiner: Die
/// Übersicht muss auch ohne Farbwahrnehmung vollständig lesbar sein.
/// </summary>
public sealed record DetectionNote(DetectionNoteKind Kind, string Text)
{
    /// <summary>Übernimmt eine Zeile aus der Zusammenfassung des Kerns.</summary>
    public static DetectionNote From(DetectionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new DetectionNote(
            entry.Kind switch
            {
                DetectionEntryKind.Found => DetectionNoteKind.Found,
                DetectionEntryKind.Uncertain => DetectionNoteKind.Uncertain,
                _ => DetectionNoteKind.Missing,
            },
            entry.Text);
    }

    /// <summary>Zeichen zur farbunabhängigen Kennzeichnung.</summary>
    public string Glyph => Kind switch
    {
        DetectionNoteKind.Found => "✓",
        DetectionNoteKind.Uncertain => "?",
        _ => "!",
    };

    /// <summary>Die Art als Wort, für Sprachausgabe und Lesbarkeit.</summary>
    public string KindLabel => Kind switch
    {
        DetectionNoteKind.Found => "Erkannt",
        DetectionNoteKind.Uncertain => "Bitte prüfen",
        _ => "Nicht gefunden",
    };
}

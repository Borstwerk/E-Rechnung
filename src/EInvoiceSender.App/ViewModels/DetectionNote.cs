namespace EInvoiceSender.App.ViewModels;

/// <summary>Die Art einer Meldung in der Erkennungsuebersicht.</summary>
public enum DetectionNoteKind
{
    /// <summary>Wert eindeutig gefunden.</summary>
    Found,

    /// <summary>Wert gefunden, aber nicht eindeutig – bitte pruefen.</summary>
    Uncertain,

    /// <summary>Nichts gefunden; die Angabe muss von Hand erfasst werden.</summary>
    Missing,
}

/// <summary>
/// Eine Zeile der Uebersicht "PDF analysiert".
///
/// Das Zeichen steht neben dem Text, nicht statt seiner: Die Uebersicht muss
/// auch ohne Farbwahrnehmung vollstaendig lesbar sein.
/// </summary>
public sealed record DetectionNote(DetectionNoteKind Kind, string Text)
{
    /// <summary>Zeichen zur farbunabhaengigen Kennzeichnung.</summary>
    public string Glyph => Kind switch
    {
        DetectionNoteKind.Found => "\u2713",
        DetectionNoteKind.Uncertain => "?",
        _ => "!",
    };

    /// <summary>Die Art als Wort, fuer Sprachausgabe und Lesbarkeit.</summary>
    public string KindLabel => Kind switch
    {
        DetectionNoteKind.Found => "Erkannt",
        DetectionNoteKind.Uncertain => "Bitte pruefen",
        _ => "Nicht gefunden",
    };
}

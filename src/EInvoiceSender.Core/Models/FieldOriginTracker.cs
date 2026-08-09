namespace EInvoiceSender.Core.Models;

/// <summary>
/// Merkt sich je Formularfeld, woher sein Inhalt stammt.
///
/// Bewusst eine eigene Klasse und per Komposition eingebunden: Das Formular
/// haelt die Werte, dieser Verfolger haelt ihre Herkunft. Zusammen waren das
/// zwei Aufgaben in einer ohnehin grossen Klasse.
/// </summary>
public sealed class FieldOriginTracker
{
    private readonly Dictionary<string, FieldOrigin> _origins = [];

    /// <summary>Laeuft gerade eine Vorbefuellung?</summary>
    public bool IsPrefilling { get; private set; }

    /// <summary>
    /// Alle vermerkten Herkuenfte. Die Oberflaeche bindet daran, um die
    /// Kennzeichnung neben den Feldern anzuzeigen.
    /// </summary>
    public IReadOnlyDictionary<string, FieldOrigin> Origins => _origins;

    /// <summary>
    /// Woher der Inhalt eines Feldes stammt.
    ///
    /// Ein nie vermerktes Feld traegt einen Programmstandard. Genau das war
    /// frueher falsch: Es galt als Benutzereingabe und war damit
    /// unueberschreibbar.
    /// </summary>
    public FieldOrigin OriginOf(string propertyName)
        => _origins.TryGetValue(propertyName, out FieldOrigin origin) ? origin : FieldOrigin.Default;

    /// <summary>Vermerkt die Herkunft eines Feldes.</summary>
    public void Mark(string propertyName, FieldOrigin origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        _origins[propertyName] = origin;
    }

    /// <summary>
    /// Vermerkt eine Aenderung durch den Anwender. Liefert <c>true</c>, wenn
    /// sich dadurch etwas geaendert hat – dann muss die Oberflaeche die
    /// Kennzeichnung neu zeichnen.
    /// </summary>
    public bool MarkAsManual(string propertyName)
    {
        if (OriginOf(propertyName) == FieldOrigin.Manual)
        {
            return false;
        }

        _origins[propertyName] = FieldOrigin.Manual;

        return true;
    }

    /// <summary>
    /// Fuehrt eine Vorbefuellung aus. Aenderungen innerhalb von
    /// <paramref name="fill"/> gelten nicht als Benutzereingabe.
    /// </summary>
    public void DuringPrefill(Action fill)
    {
        ArgumentNullException.ThrowIfNull(fill);

        IsPrefilling = true;

        try
        {
            fill();
        }
        finally
        {
            IsPrefilling = false;
        }
    }
}

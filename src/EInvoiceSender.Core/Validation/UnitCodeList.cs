using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Mengeneinheiten nach UN/ECE Recommendation 20 (Codes for Units of
/// Measure) und Recommendation 21 (Packaging Types), soweit sie für
/// Rechnungspositionen (BT-130) relevant sind. Reine Nachschlagetabelle
/// ohne Geschäftslogik.
/// </summary>
/// <remarks>
/// Diese Liste ist eine kuratierte Teilmenge, keine vollständige Abbildung
/// von Rec. 20/21 (mehrere hundert Codes). Aufgenommen wurden ausschließlich
/// Codes, deren Bedeutung sicher belegt ist; bei Unklarheit (z. B. der
/// Packstückcode für Paletten) wurde bewusst auf eine Aufnahme verzichtet,
/// statt einen möglicherweise falschen Code zu vermuten.
/// </remarks>
public static class UnitCodeList
{
    private static readonly FrozenDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["C62"] = "Stück",
        ["H87"] = "Stück (Alternativcode)",
        ["HUR"] = "Stunde",
        ["MIN"] = "Minute",
        ["DAY"] = "Tag",
        ["WEE"] = "Woche",
        ["MON"] = "Monat",
        ["ANN"] = "Jahr",
        ["KGM"] = "Kilogramm",
        ["GRM"] = "Gramm",
        ["TNE"] = "Tonne",
        ["MTR"] = "Meter",
        ["CMT"] = "Zentimeter",
        ["MMT"] = "Millimeter",
        ["KMT"] = "Kilometer",
        ["MTK"] = "Quadratmeter",
        ["MTQ"] = "Kubikmeter",
        ["LTR"] = "Liter",
        ["MLT"] = "Milliliter",
        ["KWH"] = "Kilowattstunde",
        ["SET"] = "Satz",
        ["PR"] = "Paar",
        ["NAR"] = "Anzahl Artikel",
        ["P1"] = "Prozent",
        ["E48"] = "Dienstleistungseinheit",
        ["LS"] = "Pauschale",
        ["XPK"] = "Packung",
        ["XBX"] = "Karton",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Die für Rechnungen wichtigsten Einheiten, in einer für eine
    /// Auswahlliste in der Oberfläche sinnvollen Reihenfolge (Stück
    /// zuerst). Jeder enthaltene Code besteht auch <see cref="IsValid"/>.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name)> CommonUnits { get; } =
    [
        ("C62", "Stück"),
        ("H87", "Stück (Alternativcode)"),
        ("HUR", "Stunde"),
        ("DAY", "Tag"),
        ("WEE", "Woche"),
        ("MON", "Monat"),
        ("ANN", "Jahr"),
        ("KGM", "Kilogramm"),
        ("GRM", "Gramm"),
        ("TNE", "Tonne"),
        ("MTR", "Meter"),
        ("CMT", "Zentimeter"),
        ("MMT", "Millimeter"),
        ("MTK", "Quadratmeter"),
        ("MTQ", "Kubikmeter"),
        ("LTR", "Liter"),
        ("KWH", "Kilowattstunde"),
        ("SET", "Satz"),
        ("PR", "Paar"),
        ("P1", "Prozent"),
        ("LS", "Pauschale"),
        ("E48", "Dienstleistungseinheit"),
    ];

    /// <summary>
    /// Prüft, ob <paramref name="code"/> in der kuratierten Teilmenge
    /// enthalten ist. Gross-/Kleinschreibung und umgebende Leerzeichen
    /// spielen keine Rolle. Liefert <see langword="false"/> bei
    /// <see langword="null"/>, leerem oder reinem Leerraum-Text – wirft nie.
    /// </summary>
    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return Names.ContainsKey(code.Trim());
    }

    /// <summary>
    /// Liefert den deutschen Namen der Einheit, sofern sie in der Liste
    /// geführt wird.
    /// </summary>
    public static bool TryGetName(string? code, [MaybeNullWhen(false)] out string germanName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            germanName = null;
            return false;
        }

        return Names.TryGetValue(code.Trim(), out germanName);
    }
}

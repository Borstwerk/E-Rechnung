namespace EInvoiceSender.Core.Models;

/// <summary>
/// Währungskennung nach ISO 4217 (BT-5). Prüft hier nur die Form;
/// die Prüfung gegen die tatsächliche Codeliste erfolgt in
/// <c>EInvoiceSender.Validation</c>, damit die Domain frei von Datentabellen bleibt.
/// </summary>
public readonly record struct CurrencyCode
{
    private CurrencyCode(string value) => Value = value;

    /// <summary>Dreistelliger Großbuchstabencode, z. B. EUR.</summary>
    public string Value { get; }

    /// <summary>Euro – Vorgabewert der Anwendung.</summary>
    public static CurrencyCode Euro { get; } = new("EUR");

    public override string ToString() => Value;

    /// <summary>Liest eine Währungskennung, wenn sie formal korrekt ist.</summary>
    public static bool TryParse(string? input, out CurrencyCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim().ToUpperInvariant();
        if (trimmed.Length != 3)
        {
            return false;
        }

        foreach (char c in trimmed)
        {
            if (!char.IsAsciiLetterUpper(c))
            {
                return false;
            }
        }

        code = new CurrencyCode(trimmed);
        return true;
    }

    /// <summary>Liest eine Währungskennung oder wirft.</summary>
    public static CurrencyCode Parse(string input)
        => TryParse(input, out var code)
            ? code
            : throw new FormatException($"'{input}' ist keine gültige Währungskennung.");
}

/// <summary>
/// Länderkennung nach ISO 3166-1 alpha-2 (BT-40 / BT-55). Formprüfung hier,
/// Prüfung gegen die Codeliste in <c>EInvoiceSender.Validation</c>.
/// </summary>
public readonly record struct CountryCode
{
    private CountryCode(string value) => Value = value;

    /// <summary>Zweistelliger Großbuchstabencode, z. B. DE.</summary>
    public string Value { get; }

    /// <summary>Deutschland – Vorgabewert der Anwendung.</summary>
    public static CountryCode Germany { get; } = new("DE");

    public override string ToString() => Value;

    /// <summary>Liest eine Länderkennung, wenn sie formal korrekt ist.</summary>
    public static bool TryParse(string? input, out CountryCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim().ToUpperInvariant();
        if (trimmed.Length != 2 || !char.IsAsciiLetterUpper(trimmed[0]) || !char.IsAsciiLetterUpper(trimmed[1]))
        {
            return false;
        }

        code = new CountryCode(trimmed);
        return true;
    }

    /// <summary>Liest eine Länderkennung oder wirft.</summary>
    public static CountryCode Parse(string input)
        => TryParse(input, out var code)
            ? code
            : throw new FormatException($"'{input}' ist keine gültige Länderkennung.");
}

/// <summary>
/// Mengeneinheit nach UN/ECE Recommendation 20/21 (BT-130).
/// Die zulässigen Werte prüft <c>EInvoiceSender.Validation</c>.
/// </summary>
public readonly record struct UnitCode
{
    private UnitCode(string value) => Value = value;

    /// <summary>Codewert, z. B. C62 (Stück) oder HUR (Stunde).</summary>
    public string Value { get; }

    /// <summary>C62 – Stück. Vorgabe, wenn nichts anderes gewählt wurde.</summary>
    public static UnitCode Piece { get; } = new("C62");

    /// <summary>HUR – Stunde. Häufigster Fall bei Dienstleistungen.</summary>
    public static UnitCode Hour { get; } = new("HUR");

    /// <summary>DAY – Tag.</summary>
    public static UnitCode Day { get; } = new("DAY");

    public override string ToString() => Value;

    /// <summary>Liest einen Einheitencode, wenn er formal korrekt ist.</summary>
    public static bool TryParse(string? input, out UnitCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim().ToUpperInvariant();
        if (trimmed.Length is < 1 or > 3)
        {
            return false;
        }

        foreach (char c in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        code = new UnitCode(trimmed);
        return true;
    }

    /// <summary>Liest einen Einheitencode oder wirft.</summary>
    public static UnitCode Parse(string input)
        => TryParse(input, out var code)
            ? code
            : throw new FormatException($"'{input}' ist kein gültiger Einheitencode.");
}

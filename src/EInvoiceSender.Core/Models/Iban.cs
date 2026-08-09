using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace EInvoiceSender.Core.Models;

/// <summary>
/// Internationale Bankkontonummer mit geprueften Pruefziffern nach
/// ISO 13616 in Verbindung mit ISO 7064 (Mod 97-10).
/// Der Typ existiert nur in gueltigem Zustand.
/// </summary>
public readonly record struct Iban
{
    /// <summary>Kuerzeste bekannte IBAN (Norwegen).</summary>
    private const int MinLength = 15;

    /// <summary>Laengste zulaessige IBAN nach ISO 13616.</summary>
    private const int MaxLength = 34;

    private Iban(string normalized) => Value = normalized;

    /// <summary>Normalisierte Darstellung: Grossbuchstaben, ohne Leerzeichen.</summary>
    public string Value { get; }

    /// <summary>Laenderkennung der IBAN (die ersten beiden Zeichen).</summary>
    public string CountryPrefix => Value[..2];

    /// <summary>
    /// Darstellung in Vierergruppen, wie sie auf Rechnungen ueblich ist.
    /// </summary>
    public string ToDisplayString()
    {
        var groups = new List<string>((Value.Length / 4) + 1);
        for (int i = 0; i < Value.Length; i += 4)
        {
            groups.Add(Value.Substring(i, Math.Min(4, Value.Length - i)));
        }

        return string.Join(' ', groups);
    }

    public override string ToString() => Value;

    /// <summary>
    /// Versucht, eine IBAN zu lesen. Leerzeichen und Bindestriche werden
    /// entfernt, Kleinbuchstaben werden hochgestellt.
    /// </summary>
    public static bool TryParse(string? input, out Iban iban)
    {
        iban = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string normalized = Normalize(input);

        if (normalized.Length is < MinLength or > MaxLength)
        {
            return false;
        }

        // Aufbau: zwei Buchstaben Land, zwei Ziffern Pruefsumme, dann alphanumerisch.
        if (!char.IsAsciiLetterUpper(normalized[0]) || !char.IsAsciiLetterUpper(normalized[1])
            || !char.IsAsciiDigit(normalized[2]) || !char.IsAsciiDigit(normalized[3]))
        {
            return false;
        }

        foreach (char c in normalized)
        {
            if (!char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        if (ComputeMod97(normalized) != 1)
        {
            return false;
        }

        iban = new Iban(normalized);
        return true;
    }

    /// <summary>
    /// Liest eine IBAN oder wirft. Nur verwenden, wenn die Eingabe bereits
    /// geprueft ist – im Erfassungsweg immer <see cref="TryParse"/> benutzen.
    /// </summary>
    public static Iban Parse(string input)
        => TryParse(input, out var iban)
            ? iban
            : throw new FormatException($"'{Mask(input)}' ist keine gueltige IBAN.");

    /// <summary>Entfernt Leerzeichen und Bindestriche, stellt auf Grossbuchstaben.</summary>
    public static string Normalize(string input)
    {
        Span<char> buffer = input.Length <= 64 ? stackalloc char[input.Length] : new char[input.Length];
        int length = 0;
        foreach (char c in input)
        {
            if (c is ' ' or '-' or '\t')
            {
                continue;
            }

            buffer[length++] = char.ToUpperInvariant(c);
        }

        return new string(buffer[..length]);
    }

    /// <summary>
    /// ISO 7064 Mod 97-10. Die ersten vier Zeichen wandern ans Ende, Buchstaben
    /// werden durch 10..35 ersetzt, danach wird stueckweise modulo 97 gerechnet,
    /// damit keine Zahl ueberlaeuft. Gueltig ist genau der Rest 1.
    /// </summary>
    private static int ComputeMod97(string normalized)
    {
        int remainder = 0;

        for (int i = 0; i < normalized.Length; i++)
        {
            // Rotation: Zeichen 4..n zuerst, danach die ersten vier.
            char c = normalized[(i + 4) % normalized.Length];

            if (char.IsAsciiDigit(c))
            {
                remainder = ((remainder * 10) + (c - '0')) % 97;
            }
            else
            {
                int numeric = c - 'A' + 10;
                remainder = ((remainder * 100) + numeric) % 97;
            }
        }

        return remainder;
    }

    /// <summary>
    /// Maskiert eine IBAN fuer Protokolle und Fehlermeldungen: nur die ersten
    /// vier und die letzten zwei Zeichen bleiben sichtbar.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string normalized = Normalize(value);
        if (normalized.Length <= 6)
        {
            return new string('*', normalized.Length);
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{normalized[..4]}{new string('*', normalized.Length - 6)}{normalized[^2..]}");
    }

    /// <summary>Maskierte Darstellung dieser IBAN fuer Protokolle.</summary>
    public string ToMaskedString() => Mask(Value);
}

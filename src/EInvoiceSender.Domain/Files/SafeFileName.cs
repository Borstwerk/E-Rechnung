using System.Globalization;
using System.Text;

namespace EInvoiceSender.Domain.Files;

/// <summary>
/// Erzeugt sichere Dateinamen aus fachlichen Angaben wie Rechnungsnummer und
/// Empfaengername.
///
/// Die Bereinigung ist bewusst streng: Sie entfernt nicht nur die unter Windows
/// verbotenen Zeichen, sondern auch Pfadtrenner, Steuerzeichen und fuehrende
/// Punkte. Damit kann eine manipulierte Rechnungsnummer keinen Pfadwechsel
/// ausloesen (Path Traversal) – siehe docs/SECURITY.md.
/// </summary>
public static class SafeFileName
{
    /// <summary>
    /// Unter Windows reservierte Geraetenamen. Eine Datei mit einem dieser Namen
    /// laesst sich nicht anlegen, deshalb wird ein Unterstrich angehaengt.
    /// </summary>
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Ersatzname, wenn nach der Bereinigung nichts uebrig bleibt.</summary>
    public const string Fallback = "Rechnung";

    /// <summary>
    /// Hoechstlaenge eines einzelnen Namensbestandteils. Bewusst konservativ,
    /// damit der zusammengesetzte Name samt Verzeichnis unter der
    /// Windows-Pfadgrenze bleibt.
    /// </summary>
    public const int MaxSegmentLength = 60;

    /// <summary>
    /// Bereinigt einen einzelnen Namensbestandteil.
    /// Umlaute bleiben erhalten – NTFS kommt damit zurecht und der Anwender
    /// erkennt seine Datei wieder.
    /// </summary>
    public static string Sanitize(string? value, int maxLength = MaxSegmentLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fallback;
        }

        var builder = new StringBuilder(value.Length);
        bool lastWasSeparator = false;

        foreach (char c in value.Normalize(NormalizationForm.FormC))
        {
            bool isForbidden =
                char.IsControl(c)
                || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                || c == Path.DirectorySeparatorChar
                || c == Path.AltDirectorySeparatorChar;

            if (isForbidden || char.IsWhiteSpace(c))
            {
                // Mehrere ungueltige Zeichen hintereinander werden zu einem
                // einzigen Unterstrich zusammengezogen.
                if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }

                continue;
            }

            builder.Append(c);
            lastWasSeparator = false;
        }

        // Fuehrende und abschliessende Punkte sowie Unterstriche entfernen.
        // Ein abschliessender Punkt ist unter Windows nicht zulaessig.
        string cleaned = builder.ToString().Trim('.', '_', ' ');

        if (cleaned.Length == 0)
        {
            return Fallback;
        }

        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned[..maxLength].TrimEnd('.', '_', ' ');
            if (cleaned.Length == 0)
            {
                return Fallback;
            }
        }

        if (IsReserved(cleaned))
        {
            cleaned += "_";
        }

        return cleaned;
    }

    /// <summary>
    /// Baut den vorgeschlagenen Dateinamen der Ausgabedatei:
    /// <c>&lt;Rechnungsnummer&gt;_&lt;Empfaenger&gt;_ZUGFeRD.pdf</c>.
    /// </summary>
    public static string BuildOutputFileName(
        string? invoiceNumber,
        string? recipientName,
        string suffix = "ZUGFeRD",
        string extension = ".pdf")
    {
        string number = Sanitize(invoiceNumber, 40);
        string recipient = Sanitize(recipientName, 40);
        string cleanSuffix = Sanitize(suffix, 20);

        string name = string.Create(
            CultureInfo.InvariantCulture,
            $"{number}_{recipient}_{cleanSuffix}");

        return name + extension;
    }

    /// <summary>
    /// Fuegt an einen Dateinamen einen Zaehler an, um ein vorhandenes Ziel nicht
    /// zu ueberschreiben: <c>Rechnung.pdf</c> wird zu <c>Rechnung (2).pdf</c>.
    /// </summary>
    public static string AppendCounter(string fileName, int counter)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentOutOfRangeException.ThrowIfLessThan(counter, 2);

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        return string.Create(CultureInfo.InvariantCulture, $"{stem} ({counter}){extension}");
    }

    /// <summary>Prueft, ob der Name einem reservierten Windows-Geraetenamen entspricht.</summary>
    private static bool IsReserved(string name)
    {
        int dotIndex = name.IndexOf('.', StringComparison.Ordinal);
        string stem = dotIndex >= 0 ? name[..dotIndex] : name;

        foreach (string reserved in ReservedNames)
        {
            if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

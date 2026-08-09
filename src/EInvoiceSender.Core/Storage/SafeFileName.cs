using System.Globalization;
using System.Text;

namespace EInvoiceSender.Core.Storage;

/// <summary>
/// Erzeugt sichere Dateinamen aus fachlichen Angaben wie Rechnungsnummer und
/// Empfängername.
///
/// Die Bereinigung ist bewusst streng: Sie entfernt nicht nur die unter Windows
/// verbotenen Zeichen, sondern auch Pfadtrenner, Steuerzeichen und führende
/// Punkte. Damit kann eine manipulierte Rechnungsnummer keinen Pfadwechsel
/// auslösen (Path Traversal) – siehe docs/SECURITY.md.
/// </summary>
public static class SafeFileName
{
    /// <summary>
    /// Unter Windows reservierte Gerätenamen. Eine Datei mit einem dieser Namen
    /// lässt sich nicht anlegen, deshalb wird ein Unterstrich angehängt.
    /// </summary>
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    /// <summary>Ersatzname, wenn nach der Bereinigung nichts übrig bleibt.</summary>
    public const string Fallback = "Rechnung";

    /// <summary>
    /// Höchstlänge eines einzelnen Namensbestandteils. Bewusst konservativ,
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
                // Mehrere ungültige Zeichen hintereinander werden zu einem
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

        // Führende und abschließende Punkte sowie Unterstriche entfernen.
        // Ein abschließender Punkt ist unter Windows nicht zulässig.
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
    /// <c>&lt;Rechnungsnummer&gt;_&lt;Empfänger&gt;_ZUGFeRD.pdf</c>.
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
    /// Fügt an einen Dateinamen einen Zähler an, um ein vorhandenes Ziel nicht
    /// zu überschreiben: <c>Rechnung.pdf</c> wird zu <c>Rechnung (2).pdf</c>.
    /// </summary>
    public static string AppendCounter(string fileName, int counter)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentOutOfRangeException.ThrowIfLessThan(counter, 2);

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        return string.Create(CultureInfo.InvariantCulture, $"{stem} ({counter}){extension}");
    }

    /// <summary>Prüft, ob der Name einem reservierten Windows-Gerätenamen entspricht.</summary>
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

using System.Globalization;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Was mehrere Regelgruppen gleich brauchen: die geduldete
/// Rundungsabweichung, die Ausgabe von Betraegen und Datumsangaben in
/// Meldungstexten und zwei grobe Formpruefungen.
///
/// Die Formatierung steht hier, damit alle Meldungen dieselbe Schreibweise
/// verwenden - eine Meldung, in der Betraege mal mit Punkt und mal mit Komma
/// erscheinen, wirkt fehlerhaft, auch wenn sie es nicht ist.
/// </summary>
internal static class SharedRules
{
    /// <summary>Groesste geduldete Abweichung bei Summenvergleichen.</summary>
    public const decimal ToleranceInCurrency = 0.01m;

    /// <summary>
    /// Sehr einfache Syntaxpruefung fuer E-Mail-Adressen: genau ein
    /// At-Zeichen, davor und danach etwas, im hinteren Teil ein Punkt.
    ///
    /// Bewusst nicht strenger - eine formal korrekte Adresse kann trotzdem
    /// unzustellbar sein, und eine zu strenge Pruefung lehnt gueltige Adressen
    /// ab. Das waere der schlimmere Fehler.
    /// </summary>
    public static bool LooksLikeEmail(string value)
    {
        string trimmed = value.Trim();
        int at = trimmed.IndexOf('@', StringComparison.Ordinal);

        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            return false;
        }

        string domain = trimmed[(at + 1)..];

        return domain.Contains('.', StringComparison.Ordinal)
               && !domain.StartsWith('.')
               && !domain.EndsWith('.')
               && !trimmed.Contains(' ', StringComparison.Ordinal);
    }

    public static string Format(decimal value)
        => value.ToString("0.00", CultureInfo.GetCultureInfo("de-DE"));

    public static string Format(DateOnly value)
        => value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>
    /// Prueft die Form einer Umsatzsteuer-Identifikationsnummer: zwei
    /// Buchstaben als Laenderkennzeichen, danach mindestens zwei alphanumerische
    /// Zeichen. Die laenderspezifischen Pruefziffern werden bewusst nicht
    /// geprueft – dafuer waere ein Abgleich mit dem MIAS-Dienst noetig, und der
    /// wuerde Daten nach draussen geben.
    /// </summary>
    public static bool LooksLikeVatId(string value)
    {
        string trimmed = value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

        return trimmed.Length >= 4
               && char.IsAsciiLetter(trimmed[0])
               && char.IsAsciiLetter(trimmed[1])
               && trimmed.Skip(2).All(char.IsAsciiLetterOrDigit);
    }
}

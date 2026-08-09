using System.Globalization;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.Core.Validation.Rules;

/// <summary>
/// Was mehrere Regelgruppen gleich brauchen: die geduldete
/// Rundungsabweichung, die Ausgabe von Beträgen und Datumsangaben in
/// Meldungstexten und zwei grobe Formprüfungen.
///
/// Die Formatierung steht hier, damit alle Meldungen dieselbe Schreibweise
/// verwenden - eine Meldung, in der Beträge mal mit Punkt und mal mit Komma
/// erscheinen, wirkt fehlerhaft, auch wenn sie es nicht ist.
/// </summary>
internal static class SharedRules
{
    /// <summary>Größte geduldete Abweichung bei Summenvergleichen.</summary>
    public const decimal ToleranceInCurrency = 0.01m;

    /// <summary>
    /// Sehr einfache Syntaxprüfung für E-Mail-Adressen: genau ein
    /// At-Zeichen, davor und danach etwas, im hinteren Teil ein Punkt.
    ///
    /// Bewusst nicht strenger - eine formal korrekte Adresse kann trotzdem
    /// unzustellbar sein, und eine zu strenge Prüfung lehnt gültige Adressen
    /// ab. Das wäre der schlimmere Fehler.
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
    /// Prüft die Form einer Umsatzsteuer-Identifikationsnummer: zwei
    /// Buchstaben als Länderkennzeichen, danach mindestens zwei alphanumerische
    /// Zeichen. Die länderspezifischen Prüfziffern werden bewusst nicht
    /// geprüft – dafür wäre ein Abgleich mit dem MIAS-Dienst nötig, und der
    /// würde Daten nach draußen geben.
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

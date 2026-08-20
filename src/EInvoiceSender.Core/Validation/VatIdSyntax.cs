namespace EInvoiceSender.Core.Validation;

/// <summary>
/// Kleine Zusatzprüfung für das Länderpräfix einer USt-IdNr.
///
/// Die allgemeine Formprüfung bleibt unverändert in <c>SharedRules</c>, damit
/// BUY-02 das bestehende Seller-Verhalten nicht nebenbei verschärft. Diese
/// Hilfe prüft ausschließlich, ob die ersten beiden Buchstaben ein zulässiges
/// Ausgabeland bezeichnen. <c>EL</c> ist die von EN 16931 ausdrücklich
/// zugelassene griechische Sonderform zum ISO-Code <c>GR</c>.
/// </summary>
public static class VatIdSyntax
{
    public static bool HasKnownCountryPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

        if (compact.Length < 2
            || !char.IsAsciiLetter(compact[0])
            || !char.IsAsciiLetter(compact[1]))
        {
            return false;
        }

        string prefix = compact[..2].ToUpperInvariant();

        return prefix == "EL" || CountryCodeList.IsValid(prefix);
    }
}

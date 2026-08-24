namespace EInvoiceSender.Core.Services;

/// <summary>
/// Lokal gespeicherte Vorlage für wiederkehrende Angaben.
/// Kennwörter werden hier bewusst nicht abgebildet – sie werden nicht gespeichert.
/// </summary>
public sealed record CompanyTemplate
{
    /// <summary>Firmenname des Verkäufers.</summary>
    public string? SellerName { get; init; }

    /// <summary>Straße und Hausnummer.</summary>
    public string? SellerStreet { get; init; }

    /// <summary>Postleitzahl.</summary>
    public string? SellerPostalCode { get; init; }

    /// <summary>Ort.</summary>
    public string? SellerCity { get; init; }

    /// <summary>Länderkennung, Vorgabe DE.</summary>
    public string? SellerCountry { get; init; }

    /// <summary>E-Mail-Adresse des Verkäufers.</summary>
    public string? SellerEmail { get; init; }

    /// <summary>Umsatzsteuer-Identifikationsnummer.</summary>
    public string? SellerVatId { get; init; }

    /// <summary>Steuernummer.</summary>
    public string? SellerTaxNumber { get; init; }

    /// <summary>
    /// Registerkennung (BT-30), etwa eine Handelsregisternummer.
    ///
    /// Die Verkäuferkennung (BT-29) steht bewusst **nicht** hier: Sie ist die
    /// Nummer, unter der ein bestimmter Kunde diesen Lieferanten führt, und
    /// gehört damit zur einzelnen Rechnung, nicht zum Firmenstamm.
    /// </summary>
    public string? SellerLegalRegistrationId { get; init; }

    /// <summary>Kontoinhaber der Standardbankverbindung.</summary>
    public string? BankAccountHolder { get; init; }

    /// <summary>IBAN der Standardbankverbindung. Wird geschützt abgelegt.</summary>
    public string? BankIban { get; init; }

    /// <summary>BIC, sofern benötigt.</summary>
    public string? BankBic { get; init; }

    /// <summary>Standardwährung neuer Rechnungen.</summary>
    public string? DefaultCurrency { get; init; }

    /// <summary>Standardzahlungsziel in Tagen.</summary>
    public int DefaultPaymentTermDays { get; init; } = 14;

    /// <summary>Standardtext der Zahlungsbedingungen.</summary>
    public string? DefaultPaymentTerms { get; init; }

    /// <summary>Standardbetreff des E-Mail-Entwurfs. Platzhalter siehe Benutzeranleitung.</summary>
    public string? DefaultEmailSubject { get; init; }

    /// <summary>Standardtext des E-Mail-Entwurfs.</summary>
    public string? DefaultEmailBody { get; init; }

    /// <summary>Zuletzt verwendetes Ausgabeverzeichnis.</summary>
    public string? LastOutputDirectory { get; init; }
}

/// <summary>
/// Anwendungseinstellungen, die nicht zur Firmenvorlage gehören.
/// </summary>
public sealed record ApplicationSettings
{
    /// <summary>Größte zulässige Eingabedatei in Megabyte.</summary>
    public int MaxInputFileSizeMegabytes { get; init; } = 20;

    /// <summary>
    /// Pfad zu einem externen Validator (Mustang-CLI). Leer bedeutet:
    /// nur die eigenen Prüfungen laufen, was der Bericht ausweist.
    /// </summary>
    public string? ExternalValidatorPath { get; init; }

    /// <summary>Pfad zur Java-Laufzeit für den externen Validator.</summary>
    public string? JavaExecutablePath { get; init; }

    /// <summary>Zeitlimit für externe Validatoren in Sekunden.</summary>
    public int ExternalValidatorTimeoutSeconds { get; init; } = 120;
}

/// <summary>
/// Speichert Vorlagen und Einstellungen lokal.
/// Sensible Werte werden unter Windows per DPAPI geschützt; auf anderen
/// Plattformen meldet die Umsetzung offen, dass kein Schutz möglich ist.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Liest die Firmenvorlage. Liefert eine leere Vorlage, wenn keine existiert.</summary>
    Task<CompanyTemplate> LoadTemplateAsync(CancellationToken cancellationToken = default);

    /// <summary>Speichert die Firmenvorlage.</summary>
    Task SaveTemplateAsync(CompanyTemplate companyTemplate, CancellationToken cancellationToken = default);

    /// <summary>Liest die Anwendungseinstellungen.</summary>
    Task<ApplicationSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Speichert die Anwendungseinstellungen.</summary>
    Task SaveSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gibt an, ob sensible Werte auf diesem System tatsächlich geschützt
    /// abgelegt werden können. Ist das nicht der Fall, warnt die Oberfläche.
    /// </summary>
    bool SupportsProtectedStorage { get; }
}

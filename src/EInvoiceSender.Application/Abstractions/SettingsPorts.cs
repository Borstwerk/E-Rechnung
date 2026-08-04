namespace EInvoiceSender.Application.Abstractions;

/// <summary>
/// Lokal gespeicherte Vorlage fuer wiederkehrende Angaben.
/// Kennwoerter werden hier bewusst nicht abgebildet – sie werden nicht gespeichert.
/// </summary>
public sealed record CompanyTemplate
{
    /// <summary>Firmenname des Verkaeufers.</summary>
    public string? SellerName { get; init; }

    /// <summary>Strasse und Hausnummer.</summary>
    public string? SellerStreet { get; init; }

    /// <summary>Postleitzahl.</summary>
    public string? SellerPostalCode { get; init; }

    /// <summary>Ort.</summary>
    public string? SellerCity { get; init; }

    /// <summary>Laenderkennung, Vorgabe DE.</summary>
    public string? SellerCountry { get; init; }

    /// <summary>E-Mail-Adresse des Verkaeufers.</summary>
    public string? SellerEmail { get; init; }

    /// <summary>Umsatzsteuer-Identifikationsnummer.</summary>
    public string? SellerVatId { get; init; }

    /// <summary>Steuernummer.</summary>
    public string? SellerTaxNumber { get; init; }

    /// <summary>Kontoinhaber der Standardbankverbindung.</summary>
    public string? BankAccountHolder { get; init; }

    /// <summary>IBAN der Standardbankverbindung. Wird geschuetzt abgelegt.</summary>
    public string? BankIban { get; init; }

    /// <summary>BIC, sofern benoetigt.</summary>
    public string? BankBic { get; init; }

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
/// Anwendungseinstellungen, die nicht zur Firmenvorlage gehoeren.
/// </summary>
public sealed record ApplicationSettings
{
    /// <summary>Groesste zulaessige Eingabedatei in Megabyte.</summary>
    public int MaxInputFileSizeMegabytes { get; init; } = 20;

    /// <summary>
    /// Pfad zu einem externen Validator (Mustang-CLI). Leer bedeutet:
    /// nur die eigenen Pruefungen laufen, was der Bericht ausweist.
    /// </summary>
    public string? ExternalValidatorPath { get; init; }

    /// <summary>Pfad zur Java-Laufzeit fuer den externen Validator.</summary>
    public string? JavaExecutablePath { get; init; }

    /// <summary>Zeitlimit fuer externe Validatoren in Sekunden.</summary>
    public int ExternalValidatorTimeoutSeconds { get; init; } = 120;
}

/// <summary>
/// Speichert Vorlagen und Einstellungen lokal.
/// Sensible Werte werden unter Windows per DPAPI geschuetzt; auf anderen
/// Plattformen meldet die Umsetzung offen, dass kein Schutz moeglich ist.
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
    /// Gibt an, ob sensible Werte auf diesem System tatsaechlich geschuetzt
    /// abgelegt werden koennen. Ist das nicht der Fall, warnt die Oberflaeche.
    /// </summary>
    bool SupportsProtectedStorage { get; }
}

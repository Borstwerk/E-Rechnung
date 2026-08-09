using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;

namespace EInvoiceSender.Core.Settings;

/// <summary>
/// Speichert Vorlage und Einstellungen als JSON im lokalen Anwendungsdatenordner
/// des Benutzers.
///
/// **Schutz sensibler Werte:** Die IBAN ist die einzige wirklich schutzwuerdige
/// Angabe in der Vorlage. Sie wird unter Windows mit DPAPI im Geltungsbereich
/// des angemeldeten Benutzers verschluesselt abgelegt, sodass ein anderes
/// Benutzerkonto auf demselben Rechner sie nicht lesen kann.
///
/// Auf Plattformen ohne DPAPI (Linux, macOS – im Produkt nicht vorgesehen, aber
/// in der Entwicklung und in Tests relevant) wird die IBAN **nicht** gespeichert.
/// Sie still im Klartext abzulegen waere die schlechtere Wahl:
/// <see cref="SupportsProtectedStorage"/> meldet den Zustand, und die
/// Oberflaeche weist darauf hin.
///
/// **Kennwoerter werden grundsaetzlich nicht gespeichert** – weder geschuetzt
/// noch ungeschuetzt. Die Anwendung braucht keine.
/// </summary>
public sealed partial class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Zusatz, an dem ein geschuetzter Wert erkennbar ist.</summary>
    private const string ProtectedPrefix = "dpapi:";

    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly string _directory;

    public JsonSettingsStore(ILogger<JsonSettingsStore> logger, string? directory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EInvoiceSender");
    }

    /// <inheritdoc />
    public bool SupportsProtectedStorage => OperatingSystem.IsWindows();

    private string TemplatePath => Path.Combine(_directory, "firmenvorlage.json");

    private string SettingsPath => Path.Combine(_directory, "einstellungen.json");

    /// <inheritdoc />
    public async Task<CompanyTemplate> LoadTemplateAsync(CancellationToken cancellationToken = default)
    {
        CompanyTemplate? template = await ReadAsync<CompanyTemplate>(TemplatePath, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return new CompanyTemplate();
        }

        return template with { BankIban = Unprotect(template.BankIban) };
    }

    /// <inheritdoc />
    public Task SaveTemplateAsync(
        CompanyTemplate companyTemplate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(companyTemplate);

        CompanyTemplate toStore = companyTemplate with { BankIban = Protect(companyTemplate.BankIban) };

        return WriteAsync(TemplatePath, toStore, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApplicationSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        => await ReadAsync<ApplicationSettings>(SettingsPath, cancellationToken).ConfigureAwait(false)
           ?? new ApplicationSettings();

    /// <inheritdoc />
    public Task SaveSettingsAsync(
        ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return WriteAsync(SettingsPath, settings, cancellationToken);
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Eine beschaedigte Einstellungsdatei darf den Programmstart nicht
            // verhindern. Der Benutzer beginnt dann mit leeren Vorgaben.
            LogReadFailed(_logger, Path.GetFileName(path), ex.GetType().Name);

            return null;
        }
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);

        // Atomar schreiben, damit ein Absturz waehrend des Speicherns die
        // vorhandene Vorlage nicht zerstoert.
        string temporaryPath = path + ".tmp";

        await using (var stream = new FileStream(
            temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    /// <summary>
    /// Verschluesselt einen Wert mit DPAPI. Ist das nicht moeglich, wird der
    /// Wert verworfen statt im Klartext gespeichert.
    /// </summary>
    private string? Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            LogProtectionUnavailable(_logger);

            return null;
        }

        try
        {
            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value), optionalEntropy: null, DataProtectionScope.CurrentUser);

            return ProtectedPrefix + Convert.ToBase64String(encrypted);
        }
        catch (CryptographicException)
        {
            LogProtectionUnavailable(_logger);

            return null;
        }
    }

    /// <summary>Entschluesselt einen zuvor geschuetzten Wert.</summary>
    private string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!value.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            // Ein unverschluesselter Wert kann aus einer aelteren Fassung oder
            // einer von Hand bearbeiteten Datei stammen. Er wird uebernommen,
            // beim naechsten Speichern aber geschuetzt abgelegt.
            return value;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            byte[] decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(value[ProtectedPrefix.Length..]),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Etwa nach einem Benutzerwechsel oder einer Neuinstallation.
            LogUnprotectFailed(_logger);

            return null;
        }
    }

    [LoggerMessage(
        EventId = 8001, Level = LogLevel.Warning,
        Message = "Einstellungsdatei {FileName} konnte nicht gelesen werden ({Reason}). Es werden Vorgaben verwendet.")]
    private static partial void LogReadFailed(ILogger logger, string fileName, string reason);

    [LoggerMessage(
        EventId = 8002, Level = LogLevel.Warning,
        Message = "Geschuetzte Ablage ist auf diesem System nicht verfuegbar. Die Bankverbindung wird nicht gespeichert.")]
    private static partial void LogProtectionUnavailable(ILogger logger);

    [LoggerMessage(
        EventId = 8003, Level = LogLevel.Warning,
        Message = "Ein geschuetzter Wert konnte nicht entschluesselt werden und wurde verworfen.")]
    private static partial void LogUnprotectFailed(ILogger logger);
}

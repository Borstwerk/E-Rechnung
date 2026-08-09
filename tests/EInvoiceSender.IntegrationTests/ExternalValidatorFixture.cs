using EInvoiceSender.Core.Security;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Stellt den externen Validator fuer die Ende-zu-Ende-Tests bereit.
///
/// Verhalten, wenn das Werkzeug fehlt:
/// * Standardfall (Entwicklungsrechner): Die betroffenen Tests werden
///   uebersprungen, damit ein frisch geklontes Repository nicht sofort rot ist.
/// * Ist die Umgebungsvariable <c>REQUIRE_EXTERNAL_VALIDATORS=1</c> gesetzt
///   (so laeuft die CI), **scheitern** die Tests stattdessen. Nur so bleibt die
///   Aussage belastbar: In der Pipeline gibt es kein stilles Ueberspringen des
///   Freigabegates.
/// </summary>
public sealed class ExternalValidatorFixture
{
    public ExternalValidatorFixture()
    {
        RepositoryRoot = FindRepositoryRoot();
        JarPath = FindMustangJar(RepositoryRoot);

        if (JarPath is not null)
        {
            var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);

            Validator = new MustangValidator(
                runner,
                MustangOptions.ForJar(JarPath) with { Timeout = TimeSpan.FromMinutes(3) },
                NullLogger<MustangValidator>.Instance);
        }
    }

    /// <summary>Wurzelverzeichnis des Repositorys.</summary>
    public string RepositoryRoot { get; }

    /// <summary>Pfad zur Mustang-JAR, sofern vorhanden.</summary>
    public string? JarPath { get; }

    /// <summary>Der Validator, sofern das Werkzeug vorhanden ist.</summary>
    public IExternalDocumentValidator? Validator { get; }

    /// <summary>Ist das Werkzeug einsatzbereit?</summary>
    public bool IsAvailable => Validator is not null;

    /// <summary>
    /// Verlangt einen einsatzbereiten Validator. Fehlt er, wird der Test je nach
    /// Umgebung uebersprungen oder als Fehler gemeldet.
    /// </summary>
    public IExternalDocumentValidator RequireValidator()
    {
        if (Validator is not null)
        {
            return Validator;
        }

        const string hinweis =
            "Der externe Validator fehlt. Mit './build/fetch-validators.sh' beschaffen.";

        if (Environment.GetEnvironmentVariable("REQUIRE_EXTERNAL_VALIDATORS") == "1")
        {
            throw new InvalidOperationException(
                "REQUIRE_EXTERNAL_VALIDATORS=1 ist gesetzt, aber " + hinweis);
        }

        Assert.Skip(hinweis);

        throw new InvalidOperationException("nicht erreichbar");
    }

    private static string? FindMustangJar(string repositoryRoot)
    {
        string directory = Path.Combine(repositoryRoot, "tools", "mustang");

        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.GetFiles(directory, "Mustang-CLI-*.jar")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}

/// <summary>Sammlung, damit der Validator nur einmal je Testlauf aufgebaut wird.</summary>
[CollectionDefinition(Name)]
public sealed class ExternalValidatorTestGroup : ICollectionFixture<ExternalValidatorFixture>
{
    /// <summary>Name der Sammlung.</summary>
    public const string Name = "ExterneValidatoren";
}

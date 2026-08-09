using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Services;
using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Reports;

/// <summary>
/// Erzeugt den Validierungsbericht in zwei Fassungen.
///
/// * **Maschinenlesbar (JSON)** – fuer Archivierung und spaetere Auswertung.
/// * **Menschenlesbar (Text)** – damit der Anwender ohne Werkzeug nachvollziehen
///   kann, was geprueft wurde.
///
/// Beide enthalten Pruefsumme, Zeitpunkt, Standard, Profil und die Versionen
/// aller beteiligten Pruefwerkzeuge. Wurde ein Werkzeug nicht ausgefuehrt, steht
/// das ausdruecklich im Bericht – eine fehlende Pruefung darf nicht wie eine
/// bestandene aussehen.
/// </summary>
public static class ValidationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Erzeugt den maschinenlesbaren Bericht.</summary>
    public static byte[] ToJson(
        CreateEInvoiceRequest request,
        StoredFile stored,
        string checksum,
        ValidationReport report,
        DateTimeOffset createdAt,
        string standardDescription,
        string profileId,
        IReadOnlyList<ValidatorInfo> validators)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(validators);

        var document = new
        {
            berichtsversion = "1.0",
            erzeugtAm = createdAt.ToString("O", CultureInfo.InvariantCulture),
            standard = standardDescription,
            profil = profileId,
            rechnung = new
            {
                nummer = request.Invoice.InvoiceNumber,
                datum = request.Invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                waehrung = request.Invoice.Currency.Value,
                verkaeufer = request.Invoice.Seller.Name,
                kaeufer = request.Invoice.Buyer.Name,
            },
            quelle = new
            {
                datei = Path.GetFileName(request.SourcePdfPath),
                hinweis = "Die Originaldatei wurde ausschliesslich gelesen und nicht veraendert.",
            },
            ergebnis = new
            {
                datei = Path.GetFileName(stored.FullPath),
                groesseInBytes = stored.SizeInBytes,
                sha256 = checksum,
            },
            pruefwerkzeuge = validators.Select(v => new
            {
                name = v.Name,
                version = v.Version,
                ausgefuehrt = v.WasExecuted,
                hinweis = v.Note,
            }),
            zusammenfassung = new
            {
                fehler = report.ErrorCount,
                warnungen = report.WarningCount,
                hinweise = report.Findings.Count(f => f.Severity == FindingSeverity.Information),
            },
            befunde = report.Findings.Select(f => new
            {
                schweregrad = f.Severity.ToString(),
                kennung = f.RuleId,
                normregel = f.NormRule,
                meldung = f.Message,
                feld = string.IsNullOrEmpty(f.FieldPath) ? null : f.FieldPath,
                technischesDetail = f.TechnicalDetail,
            }),
        };

        return JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
    }

    /// <summary>Erzeugt die menschenlesbare Zusammenfassung.</summary>
    public static byte[] ToText(
        CreateEInvoiceRequest request,
        StoredFile stored,
        string checksum,
        ValidationReport report,
        DateTimeOffset createdAt,
        string standardDescription,
        string profileId,
        IReadOnlyList<ValidatorInfo> validators)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(validators);

        var german = CultureInfo.GetCultureInfo("de-DE");
        var text = new StringBuilder(2048);

        text.AppendLine("Pruefbericht zur elektronischen Rechnung");
        text.AppendLine("=======================================");
        text.AppendLine();
        text.Append("Erzeugt am:        ")
            .AppendLine(createdAt.ToString("dd.MM.yyyy HH:mm:ss zzz", german));
        text.Append("Standard:          ").AppendLine(standardDescription);
        text.Append("Profil:            ").AppendLine(profileId);
        text.AppendLine();

        text.AppendLine("Rechnung");
        text.AppendLine("--------");
        text.Append("Nummer:            ").AppendLine(request.Invoice.InvoiceNumber);
        text.Append("Datum:             ").AppendLine(request.Invoice.IssueDate.ToString("dd.MM.yyyy", german));
        text.Append("Verkaeufer:        ").AppendLine(request.Invoice.Seller.Name);
        text.Append("Kaeufer:           ").AppendLine(request.Invoice.Buyer.Name);
        text.AppendLine();

        text.AppendLine("Dateien");
        text.AppendLine("-------");
        text.Append("Ausgangsdatei:     ").AppendLine(Path.GetFileName(request.SourcePdfPath));
        text.AppendLine("                   (unveraendert, nur gelesen)");
        text.Append("Ergebnisdatei:     ").AppendLine(Path.GetFileName(stored.FullPath));
        text.Append("Groesse:           ")
            .AppendLine(stored.SizeInBytes.ToString("N0", german) + " Bytes");
        text.Append("SHA-256:           ").AppendLine(checksum);
        text.AppendLine();

        text.AppendLine("Verwendete Pruefwerkzeuge");
        text.AppendLine("-------------------------");

        if (validators.Count == 0)
        {
            text.AppendLine("Es war kein externer Validator eingerichtet.");
            text.AppendLine("Die Datei wurde nur mit den eingebauten Pruefungen kontrolliert.");
        }
        else
        {
            foreach (ValidatorInfo validator in validators)
            {
                text.Append("- ").Append(validator.Name).Append(": ");

                if (validator.WasExecuted)
                {
                    text.AppendLine(validator.Version ?? "Version unbekannt");
                }
                else
                {
                    text.Append("NICHT AUSGEFUEHRT");
                    text.AppendLine(
                        string.IsNullOrWhiteSpace(validator.Note) ? string.Empty : " – " + validator.Note);
                }
            }
        }

        text.AppendLine();
        text.AppendLine("Ergebnis");
        text.AppendLine("--------");
        text.Append("Fehler:            ").AppendLine(report.ErrorCount.ToString(german));
        text.Append("Warnungen:         ").AppendLine(report.WarningCount.ToString(german));
        text.AppendLine();

        AppendFindings(text, report, FindingSeverity.Error, "Fehler");
        AppendFindings(text, report, FindingSeverity.Warning, "Warnungen");
        AppendFindings(text, report, FindingSeverity.Information, "Hinweise");

        text.AppendLine();
        text.AppendLine(
            "Hinweis: Dieser Bericht dokumentiert die technische Pruefung des Formats. "
            + "Fuer die inhaltliche und steuerliche Richtigkeit der Rechnung ist der "
            + "Rechnungssteller verantwortlich.");

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }

    private static void AppendFindings(
        StringBuilder text, ValidationReport report, FindingSeverity severity, string heading)
    {
        var findings = report.Findings.Where(f => f.Severity == severity).ToList();

        if (findings.Count == 0)
        {
            return;
        }

        text.AppendLine(heading);
        text.AppendLine(new string('-', heading.Length));

        foreach (ValidationFinding finding in findings)
        {
            text.Append("* ").AppendLine(finding.Message);
            text.Append("  ").AppendLine(finding.BuildTechnicalSummary());

            if (!string.IsNullOrEmpty(finding.FieldPath))
            {
                text.Append("  Feld: ").AppendLine(finding.FieldPath);
            }

            text.AppendLine();
        }
    }
}

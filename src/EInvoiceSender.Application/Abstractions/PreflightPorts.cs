using EInvoiceSender.Domain.Validation;

namespace EInvoiceSender.Application.Abstractions;

/// <summary>
/// Gesamturteil der Eingangspruefung.
/// </summary>
public enum PreflightVerdict
{
    /// <summary>Die Datei kann ohne Einschraenkung verarbeitet werden.</summary>
    Suitable,

    /// <summary>
    /// Die Datei kann verarbeitet werden, es gibt aber Hinweise, die der
    /// Benutzer zur Kenntnis nehmen sollte – etwa eine bereits eingebettete
    /// Rechnungs-XML, die ersetzt wuerde.
    /// </summary>
    SuitableWithWarnings,

    /// <summary>
    /// Die Datei kann nicht verarbeitet werden. Der Bericht nennt den Grund und
    /// sagt, was der Benutzer in seinem PDF-Erzeugungsprogramm umstellen muss.
    /// </summary>
    NotSuitable,
}

/// <summary>
/// Das Ergebnis der Eingangspruefung einer PDF-Datei.
///
/// Der Bericht ist bewusst vollstaendig strukturiert und nicht nur ein
/// Ja/Nein: Die Oberflaeche zeigt ihn dem Benutzer als Liste einzelner
/// Pruefpunkte, damit er sieht, was geprueft wurde – und nicht nur, dass etwas
/// nicht geht.
/// </summary>
/// <param name="Verdict">Gesamturteil.</param>
/// <param name="FilePath">Geprueftes Original. Wird nie veraendert.</param>
/// <param name="FileName">Dateiname zur Anzeige.</param>
/// <param name="FileSizeInBytes">Dateigroesse.</param>
/// <param name="IsReadable">Konnte die Datei als PDF geoeffnet werden?</param>
/// <param name="IsEncrypted">Ist sie verschluesselt oder kennwortgeschuetzt?</param>
/// <param name="IsDamaged">Ist die Struktur beschaedigt?</param>
/// <param name="HasDigitalSignature">Enthaelt sie eine digitale Signatur?</param>
/// <param name="AllFontsEmbedded">Sind alle Schriften eingebettet?</param>
/// <param name="HasActiveContent">Enthaelt sie JavaScript oder startende Aktionen?</param>
/// <param name="PdfVersion">Im Header angegebene PDF-Version.</param>
/// <param name="PageCount">Seitenzahl.</param>
/// <param name="EmbeddedFiles">Bereits vorhandene Anhaenge.</param>
/// <param name="ExistingInvoiceProfile">
/// Profil einer bereits eingebetteten Rechnungs-XML, falls vorhanden.
/// </param>
/// <param name="HasXmpMetadata">Sind XMP-Metadaten vorhanden?</param>
/// <param name="DeclaredPdfALevel">
/// Aus dem XMP gelesene PDF/A-Kennzeichnung, z. B. "3B", sonst null.
/// </param>
/// <param name="Findings">Einzelbefunde mit verstaendlichen Erklaerungen.</param>
public sealed record PdfPreflightReport(
    PreflightVerdict Verdict,
    string FilePath,
    string FileName,
    long FileSizeInBytes,
    bool IsReadable,
    bool IsEncrypted,
    bool IsDamaged,
    bool HasDigitalSignature,
    bool AllFontsEmbedded,
    bool HasActiveContent,
    string PdfVersion,
    int PageCount,
    IReadOnlyList<EmbeddedFileInfo> EmbeddedFiles,
    string? ExistingInvoiceProfile,
    bool HasXmpMetadata,
    string? DeclaredPdfALevel,
    ValidationReport Findings)
{
    /// <summary>Kann mit dieser Datei weitergearbeitet werden?</summary>
    public bool CanProceed => Verdict != PreflightVerdict.NotSuitable;

    /// <summary>Enthaelt die Datei bereits eine Rechnungs-XML?</summary>
    public bool HasExistingInvoice => ExistingInvoiceProfile is not null;

    /// <summary>Dateigroesse in Megabyte, gerundet fuer die Anzeige.</summary>
    public double FileSizeInMegabytes => Math.Round(FileSizeInBytes / 1024.0 / 1024.0, 2);
}

/// <summary>
/// Prueft eine Eingangs-PDF darauf, ob sie zu einer normgerechten E-Rechnung
/// aufgewertet werden kann.
///
/// Die Datei wird ausschliesslich gelesen. Es findet keinerlei Veraenderung am
/// Original statt – weder hier noch spaeter im Ablauf.
/// </summary>
public interface IPdfPreflightService
{
    /// <summary>Fuehrt die Eingangspruefung durch.</summary>
    Task<PdfPreflightReport> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}

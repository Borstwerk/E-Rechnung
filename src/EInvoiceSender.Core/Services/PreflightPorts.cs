using EInvoiceSender.Core.Validation;

namespace EInvoiceSender.Core.Services;

/// <summary>
/// Gesamturteil der Eingangsprüfung.
///
/// Das Urteil sagt, **wie gut** es um die Datei steht. Auf welchem Weg sie
/// verarbeitet werden kann, steht getrennt davon in
/// <see cref="PdfProcessingRoute"/>, und ob der Benutzer den nötigen Weg
/// gutgeheißen hat, steht noch einmal getrennt am Erzeugungsauftrag. Die drei
/// Fragen absichtlich auseinanderzuhalten ist der Kern dieser Erweiterung:
/// Wären sie ein Wert, ließe sich „technisch möglich“ nicht mehr von
/// „vom Benutzer gewollt“ unterscheiden.
/// </summary>
public enum PreflightVerdict
{
    /// <summary>Die Datei kann ohne Einschränkung verarbeitet werden.</summary>
    Suitable,

    /// <summary>
    /// Die Datei kann verarbeitet werden, es gibt aber Hinweise, die der
    /// Benutzer zur Kenntnis nehmen sollte – etwa eine bereits eingebettete
    /// Rechnungs-XML, die ersetzt würde.
    /// </summary>
    SuitableWithWarnings,

    /// <summary>
    /// Die Datei kann nicht verarbeitet werden. Der Bericht nennt den Grund und
    /// sagt, was der Benutzer in seinem PDF-Erzeugungsprogramm umstellen muss.
    /// </summary>
    NotSuitable,
}

/// <summary>
/// Das Ergebnis der Eingangsprüfung einer PDF-Datei.
///
/// Der Bericht ist bewusst vollständig strukturiert und nicht nur ein
/// Ja/Nein: Die Oberfläche zeigt ihn dem Benutzer als Liste einzelner
/// Prüfpunkte, damit er sieht, was geprüft wurde – und nicht nur, dass etwas
/// nicht geht.
/// </summary>
/// <param name="Verdict">Gesamturteil.</param>
/// <param name="Route">
/// Auf welchem Weg diese Datei verarbeitet werden kann. Beim Rasterweg ist die
/// Verarbeitung damit noch nicht beschlossen – dazu gehört die ausdrückliche
/// Zustimmung des Benutzers.
/// </param>
/// <param name="FilePath">Geprüftes Original. Wird nie verändert.</param>
/// <param name="FileName">Dateiname zur Anzeige.</param>
/// <param name="FileSizeInBytes">Dateigröße.</param>
/// <param name="IsReadable">Konnte die Datei als PDF geöffnet werden?</param>
/// <param name="IsEncrypted">Ist sie verschlüsselt oder kennwortgeschützt?</param>
/// <param name="IsDamaged">Ist die Struktur beschädigt?</param>
/// <param name="HasDigitalSignature">Enthält sie eine digitale Signatur?</param>
/// <param name="AllFontsEmbedded">Sind alle Schriften eingebettet?</param>
/// <param name="HasActiveContent">Enthält sie JavaScript oder startende Aktionen?</param>
/// <param name="PdfVersion">Im Header angegebene PDF-Version.</param>
/// <param name="PageCount">Seitenzahl.</param>
/// <param name="EmbeddedFiles">Bereits vorhandene Anhänge.</param>
/// <param name="ExistingInvoiceProfile">
/// Profil einer bereits eingebetteten Rechnungs-XML, falls vorhanden.
/// </param>
/// <param name="HasXmpMetadata">Sind XMP-Metadaten vorhanden?</param>
/// <param name="DeclaredPdfALevel">
/// Aus dem XMP gelesene PDF/A-Kennzeichnung, z. B. "3B", sonst null.
/// </param>
/// <param name="Findings">Einzelbefunde mit verständlichen Erklärungen.</param>
public sealed record PdfPreflightReport(
    PreflightVerdict Verdict,
    PdfProcessingRoute Route,
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
    /// <summary>
    /// Gibt es überhaupt einen Weg? Beim Rasterweg heißt das noch nicht, dass
    /// er beschritten werden darf – siehe <see cref="RequiresRasterFallback"/>.
    /// </summary>
    public bool CanProceed => Route != PdfProcessingRoute.Rejected;

    /// <summary>
    /// Führt der einzige verbleibende Weg über eine sichtbare Kopie? Dann muss
    /// der Benutzer dem Qualitätsverlust ausdrücklich zustimmen, bevor es
    /// weitergeht.
    /// </summary>
    public bool RequiresRasterFallback => Route == PdfProcessingRoute.RasterFallback;

    /// <summary>Enthält die Datei bereits eine Rechnungs-XML?</summary>
    public bool HasExistingInvoice => ExistingInvoiceProfile is not null;

    /// <summary>Dateigröße in Megabyte, gerundet für die Anzeige.</summary>
    public double FileSizeInMegabytes => Math.Round(FileSizeInBytes / 1024.0 / 1024.0, 2);
}

/// <summary>
/// Prüft eine Eingangs-PDF darauf, ob sie zu einer normgerechten E-Rechnung
/// aufgewertet werden kann.
///
/// Die Datei wird ausschließlich gelesen. Es findet keinerlei Veränderung am
/// Original statt – weder hier noch später im Ablauf.
/// </summary>
public interface IPdfPreflightService
{
    /// <summary>Führt die Eingangsprüfung durch.</summary>
    Task<PdfPreflightReport> InspectAsync(string filePath, CancellationToken cancellationToken = default);
}

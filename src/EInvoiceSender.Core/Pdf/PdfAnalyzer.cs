using System.Text;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Untersucht PDF-Dateien, ohne sie zu veraendern.
///
/// Zwei Aufgaben:
/// 1. Feststellen, ob die Datei ueberhaupt ein PDF ist und was sie enthaelt
///    (Seiten, Anhaenge, bereits eingebettete Rechnungsdaten).
/// 2. Feststellen, ob sie zu PDF/A-3 aufgewertet werden kann.
///
/// Der Analysator wirft bei beschaedigten Dateien nicht, sondern meldet den
/// Zustand als Hindernis. Ein Stapelabzug hilft dem Anwender nicht weiter.
/// </summary>
public sealed partial class PdfAnalyzer : IPdfAnalyzer
{
    private readonly IInvoiceXmlReader _xmlReader;
    private readonly ILogger<PdfAnalyzer> _logger;

    /// <summary>Die Bytefolge, mit der jede PDF-Datei beginnen muss.</summary>
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public PdfAnalyzer(IInvoiceXmlReader xmlReader, ILogger<PdfAnalyzer> logger)
    {
        _xmlReader = xmlReader ?? throw new ArgumentNullException(nameof(xmlReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> LooksLikePdfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            await using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            byte[] header = new byte[PdfSignature.Length];
            int read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);

            return read == header.Length && header.AsSpan().SequenceEqual(PdfSignature);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<PdfAnalysisResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Analyze(filePath), cancellationToken);
    }

    private PdfAnalysisResult Analyze(string filePath)
    {
        var blockers = new List<PdfUpgradeBlocker>();

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using PdfDocument document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

            bool isEncrypted = document.SecuritySettings.IsEncrypted;

            if (isEncrypted)
            {
                blockers.Add(PdfUpgradeBlocker.Encrypted);
            }

            PdfDictionary catalog = document.Internals.Catalog;

            if (HasActiveContent(catalog))
            {
                blockers.Add(PdfUpgradeBlocker.ActiveContent);
            }

            if (HasDigitalSignature(catalog))
            {
                blockers.Add(PdfUpgradeBlocker.DigitallySigned);
            }

            if (!AllFontsEmbedded(document))
            {
                blockers.Add(PdfUpgradeBlocker.FontsNotEmbedded);
            }

            IReadOnlyList<EmbeddedFileInfo> embeddedFiles = ReadEmbeddedFiles(catalog);
            (byte[]? invoiceXml, string? profile) = ExtractInvoiceXml(catalog);
            (string? part, string? conformance) = ReadDeclaredPdfALevel(catalog);

            return new PdfAnalysisResult(
                PageCount: document.PageCount,
                PdfVersion: FormatVersion(document.Version),
                IsEncrypted: isEncrypted,
                DeclaredPdfAPart: part,
                DeclaredPdfAConformance: conformance,
                EmbeddedFiles: embeddedFiles,
                ExistingInvoiceXml: invoiceXml,
                ExistingInvoiceProfile: profile,
                UpgradeBlockers: blockers);
        }
        // Bewusst breit gefasst: PDFsharp meldet Strukturfehler fremder Dateien
        // teils als nackte Exception ("The StartXRef table could not be found").
        // Eingehende PDFs sind nicht vertrauenswuerdig – jeder Lesefehler ist
        // ein Betriebsfall und darf die Anwendung nicht beenden.
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            // Eine beschaedigte Datei ist ein erwarteter Betriebsfall, kein Programmfehler.
            // Ein Kennwortschutz meldet sich als Lesefehler. Er ist fachlich
            // etwas anderes als eine kaputte Datei und bekommt eine eigene,
            // fuer den Anwender brauchbare Erklaerung.
            bool looksPasswordProtected =
                ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase);

            LogAnalysisFailed(_logger, ex.GetType().Name);

            blockers.Add(looksPasswordProtected
                ? PdfUpgradeBlocker.Encrypted
                : PdfUpgradeBlocker.Damaged);

            return new PdfAnalysisResult(
                PageCount: 0,
                PdfVersion: "unbekannt",
                IsEncrypted: false,
                DeclaredPdfAPart: null,
                DeclaredPdfAConformance: null,
                EmbeddedFiles: [],
                ExistingInvoiceXml: null,
                ExistingInvoiceProfile: null,
                UpgradeBlockers: blockers);
        }
    }

    /// <summary>
    /// Prueft alle Seiten auf Schriften ohne eingebettete Schriftdatei.
    /// Nicht eingebettete Schriften sind der haeufigste Grund, warum ein PDF
    /// nicht PDF/A-faehig ist.
    /// </summary>
    private static bool AllFontsEmbedded(PdfDocument document)
    {
        foreach (PdfPage page in document.Pages)
        {
            PdfDictionary? resources = page.Elements.GetDictionary("/Resources");
            PdfDictionary? fonts = resources?.Elements.GetDictionary("/Font");

            if (fonts is null)
            {
                continue;
            }

            foreach (string key in fonts.Elements.Keys)
            {
                PdfDictionary? font = fonts.Elements.GetDictionary(key);
                if (font is not null && !IsFontEmbedded(font))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsFontEmbedded(PdfDictionary font)
    {
        // Zusammengesetzte Schriften (Type0) tragen die Beschreibung in den
        // Nachfahren; dort muss die Schriftdatei liegen.
        PdfArray? descendants = font.Elements.GetArray("/DescendantFonts");
        if (descendants is not null)
        {
            for (int i = 0; i < descendants.Elements.Count; i++)
            {
                if (descendants.Elements.GetDictionary(i) is { } descendant
                    && !HasFontFile(descendant.Elements.GetDictionary("/FontDescriptor")))
                {
                    return false;
                }
            }

            return true;
        }

        // Die 14 Standardschriften (Helvetica, Times, Courier ...) haben keinen
        // FontDescriptor. Sie gelten damit als nicht eingebettet.
        return HasFontFile(font.Elements.GetDictionary("/FontDescriptor"));
    }

    private static bool HasFontFile(PdfDictionary? fontDescriptor)
        => fontDescriptor is not null
           && (fontDescriptor.Elements.ContainsKey("/FontFile")
               || fontDescriptor.Elements.ContainsKey("/FontFile2")
               || fontDescriptor.Elements.ContainsKey("/FontFile3"));

    private static bool HasActiveContent(PdfDictionary catalog)
    {
        if (catalog.Elements.ContainsKey("/OpenAction"))
        {
            return true;
        }

        PdfDictionary? names = catalog.Elements.GetDictionary("/Names");

        return names?.Elements.ContainsKey("/JavaScript") == true;
    }

    private static bool HasDigitalSignature(PdfDictionary catalog)
    {
        PdfDictionary? acroForm = catalog.Elements.GetDictionary("/AcroForm");

        if (acroForm is null)
        {
            return false;
        }

        // SigFlags ungleich null bedeutet: Das Dokument enthaelt Signaturfelder.
        return acroForm.Elements.ContainsKey("/SigFlags")
               && acroForm.Elements.GetInteger("/SigFlags") != 0;
    }

    /// <summary>Liest die Liste der eingebetteten Dateien aus dem Namensbaum.</summary>
    private static List<EmbeddedFileInfo> ReadEmbeddedFiles(PdfDictionary catalog)
    {
        var result = new List<EmbeddedFileInfo>();

        foreach ((string fileName, PdfDictionary specification) in EnumerateFileSpecifications(catalog))
        {
            PdfDictionary? embeddedFiles = specification.Elements.GetDictionary("/EF");
            PdfDictionary? stream = embeddedFiles?.Elements.GetDictionary("/F")
                                    ?? embeddedFiles?.Elements.GetDictionary("/UF");

            result.Add(new EmbeddedFileInfo(
                FileName: fileName,
                Relationship: specification.Elements.GetName("/AFRelationship"),
                MimeType: DecodePdfName(stream?.Elements.GetName("/Subtype")),
                SizeInBytes: stream?.Stream?.Length ?? 0));
        }

        return result;
    }

    /// <summary>
    /// Sucht eine bereits eingebettete Rechnungs-XML und liest deren Profil.
    /// Damit kann die Anwendung warnen, bevor eine Hybridrechnung erneut
    /// verarbeitet wird.
    /// </summary>
    private (byte[]? Xml, string? Profile) ExtractInvoiceXml(PdfDictionary catalog)
    {
        foreach ((string fileName, PdfDictionary specification) in EnumerateFileSpecifications(catalog))
        {
            if (!InvoiceAttachmentDescriptor.LooksLikeInvoiceFile(fileName))
            {
                continue;
            }

            PdfDictionary? embeddedFiles = specification.Elements.GetDictionary("/EF");
            PdfDictionary? stream = embeddedFiles?.Elements.GetDictionary("/F")
                                    ?? embeddedFiles?.Elements.GetDictionary("/UF");

            if (stream?.Stream is null)
            {
                continue;
            }

            byte[] xml = stream.Stream.UnfilteredValue;

            if (xml.Length == 0 || xml.Length > SecureXmlLimit)
            {
                continue;
            }

            // Die XML stammt aus einer fremden Datei und wird ausschliesslich
            // ueber den abgesicherten Leser ausgewertet.
            string? profile = _xmlReader.ReadProfileId(xml);

            return (xml, profile);
        }

        return (null, null);
    }

    /// <summary>Obergrenze fuer eine eingebettete XML, siehe SecureXml.</summary>
    private const int SecureXmlLimit = 8 * 1024 * 1024;

    /// <summary>
    /// Liefert alle Dateibeschreibungen des Dokuments, jede genau einmal.
    ///
    /// Eine korrekt aufgebaute Hybridrechnung verweist auf dieselbe
    /// Dateibeschreibung aus zwei Richtungen: aus dem Namensbaum
    /// /Names /EmbeddedFiles und aus dem Feld /AF. Ohne Entdopplung wuerde
    /// derselbe Anhang doppelt gemeldet.
    /// </summary>
    private static IEnumerable<(string FileName, PdfDictionary Specification)> EnumerateFileSpecifications(
        PdfDictionary catalog)
    {
        var alreadySeen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);

        foreach ((string fileName, PdfDictionary specification) in EnumerateFileSpecificationsRaw(catalog))
        {
            if (alreadySeen.Add(specification))
            {
                yield return (fileName, specification);
            }
        }
    }

    private static IEnumerable<(string FileName, PdfDictionary Specification)> EnumerateFileSpecificationsRaw(
        PdfDictionary catalog)
    {
        // Weg 1: der Namensbaum /Names /EmbeddedFiles
        PdfDictionary? names = catalog.Elements.GetDictionary("/Names");
        PdfDictionary? embeddedFiles = names?.Elements.GetDictionary("/EmbeddedFiles");
        PdfArray? nameArray = embeddedFiles?.Elements.GetArray("/Names");

        if (nameArray is not null)
        {
            // Der Baum speichert abwechselnd Name und Verweis.
            for (int i = 0; i + 1 < nameArray.Elements.Count; i += 2)
            {
                string fileName = nameArray.Elements.GetString(i);
                if (nameArray.Elements.GetDictionary(i + 1) is { } specification)
                {
                    yield return (fileName, specification);
                }
            }
        }

        // Weg 2: das Feld /AF am Katalog. Manche Erzeuger fuellen nur dieses.
        PdfArray? associatedFiles = catalog.Elements.GetArray("/AF");

        if (associatedFiles is not null)
        {
            for (int i = 0; i < associatedFiles.Elements.Count; i++)
            {
                if (associatedFiles.Elements.GetDictionary(i) is not { } specification)
                {
                    continue;
                }

                string fileName = specification.Elements.GetString("/UF");
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = specification.Elements.GetString("/F");
                }

                if (!string.IsNullOrEmpty(fileName))
                {
                    yield return (fileName, specification);
                }
            }
        }
    }

    /// <summary>
    /// Liest die PDF/A-Kennzeichnung aus dem XMP-Paket.
    /// Bewusst mit einer einfachen Textsuche statt mit einem XML-Parser: Das
    /// XMP fremder Dateien ist nicht vertrauenswuerdig, und fuer die reine
    /// Anzeige genuegt der Fund der beiden Felder.
    /// </summary>
    private static (string? Part, string? Conformance) ReadDeclaredPdfALevel(PdfDictionary catalog)
    {
        PdfDictionary? metadata = catalog.Elements.GetDictionary("/Metadata");

        if (metadata?.Stream is null)
        {
            return (null, null);
        }

        byte[] raw = metadata.Stream.UnfilteredValue;
        if (raw.Length is 0 or > SecureXmlLimit)
        {
            return (null, null);
        }

        string xmp = Encoding.UTF8.GetString(raw);

        return (
            ExtractBetween(xmp, "<pdfaid:part>", "</pdfaid:part>"),
            ExtractBetween(xmp, "<pdfaid:conformance>", "</pdfaid:conformance>"));
    }

    private static string? ExtractBetween(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return null;
        }

        startIndex += start.Length;
        int endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);

        return endIndex < 0 ? null : text[startIndex..endIndex].Trim();
    }

    /// <summary>
    /// Loest die <c>#hh</c>-Maskierung eines PDF-Namens auf, damit im Bericht
    /// "text/xml" steht und nicht "text#2Fxml".
    /// </summary>
    private static string? DecodePdfName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        string value = name.StartsWith('/') ? name[1..] : name;

        if (!value.Contains('#', StringComparison.Ordinal))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '#' && i + 2 < value.Length
                && byte.TryParse(
                    value.AsSpan(i + 1, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out byte decoded))
            {
                builder.Append((char)decoded);
                i += 2;
            }
            else
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }

    [LoggerMessage(
        EventId = 2010, Level = LogLevel.Warning,
        Message = "PDF konnte nicht analysiert werden ({Reason}). Datei wird als beschaedigt gemeldet.")]
    private static partial void LogAnalysisFailed(ILogger logger, string reason);

    /// <summary>Wandelt die interne Versionszahl (z. B. 17) in "1.7".</summary>
    private static string FormatVersion(int version)
        => version <= 0
            ? "unbekannt"
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{version / 10}.{version % 10}");
}

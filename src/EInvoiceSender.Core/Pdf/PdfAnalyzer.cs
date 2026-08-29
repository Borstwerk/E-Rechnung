using System.Text;
using EInvoiceSender.Core.Services;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.Exceptions;

// PdfPig führt denselben Typnamen wie PDFsharp. Hier wird PDFsharp gelesen,
// PdfPig nur für die Frage nach dem Verschlüsselungswörterbuch befragt.
using PigDocument = UglyToad.PdfPig.PdfDocument;

namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Untersucht PDF-Dateien, ohne sie zu verändern.
///
/// Zwei Aufgaben:
/// 1. Feststellen, ob die Datei überhaupt ein PDF ist und was sie enthält
///    (Seiten, Anhänge, bereits eingebettete Rechnungsdaten).
/// 2. Feststellen, ob sie zu PDF/A-3 aufgewertet werden kann.
///
/// Der Analysator wirft bei beschädigten Dateien nicht, sondern meldet den
/// Zustand als Hindernis. Ein Stapelabzug hilft dem Anwender nicht weiter.
/// </summary>
public sealed partial class PdfAnalyzer : IPdfAnalyzer, IPdfAttachmentReader
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

    /// <inheritdoc />
    public Task<EmbeddedFileReadResult> ReadEmbeddedFileAsync(
        string filePath,
        string fileName,
        int maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ReadEmbeddedFile(filePath, fileName, maxBytes), cancellationToken);
    }

    /// <summary>
    /// Sucht den Anhang mit dem angegebenen Namen und entpackt allein diesen,
    /// begrenzt auf <paramref name="maxBytes"/>.
    ///
    /// Verwendet dieselbe Traversierung wie die Analyse: Eine Hybridrechnung
    /// verweist auf denselben Anhang aus zwei Richtungen, und wer das nicht
    /// entdoppelt, meldet eine einwandfreie Datei als mehrdeutig.
    /// </summary>
    private EmbeddedFileReadResult ReadEmbeddedFile(string filePath, string fileName, int maxBytes)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using PdfDocument document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

            foreach ((string name, PdfDictionary specification) in
                     EnumerateFileSpecifications(document.Internals.Catalog))
            {
                if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Erst hier wird überhaupt etwas entpackt – und zwar nur
                // dieser eine Anhang, und nur bis zur Grenze.
                return BoundedEmbeddedFileReader.Read(specification, maxBytes);
            }

            return EmbeddedFileReadResult.Failed(EmbeddedFileReadStatus.NotFound);
        }
        // Über den Zustand einer beschädigten oder geschützten Datei urteilt
        // die Analyse. Hier gibt es dann schlicht nichts zu lesen.
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            string reason = ex.GetType().Name;
            LogAttachmentsUnreadable(_logger, reason);

            return EmbeddedFileReadResult.Failed(EmbeddedFileReadStatus.NotFound);
        }
    }

    private PdfAnalysisResult Analyze(string filePath)
    {
        var blockers = new List<PdfUpgradeBlocker>();
        PdfProtection protection = DetectProtection(filePath);

        if (protection == PdfProtection.PasswordRequired)
        {
            // Ohne Kennwort ist hier Schluss; PDFsharp käme über das Öffnen
            // ohnehin nicht hinaus.
            blockers.Add(PdfUpgradeBlocker.Encrypted);

            return Unreadable(blockers, isEncrypted: true);
        }

        if (protection == PdfProtection.RightsRestricted)
        {
            blockers.Add(PdfUpgradeBlocker.RightsRestricted);
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using PdfDocument document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

            bool isEncrypted = protection != PdfProtection.None
                               || document.SecuritySettings.IsEncrypted;

            if (document.SecuritySettings.IsEncrypted
                && !blockers.Contains(PdfUpgradeBlocker.Encrypted))
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

            if (!AllUsedFontsEmbedded(document))
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
        // Eingehende PDFs sind nicht vertrauenswürdig – jeder Lesefehler ist
        // ein Betriebsfall und darf die Anwendung nicht beenden.
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            // Eine beschädigte Datei ist ein erwarteter Betriebsfall, kein Programmfehler.
            // Ein Kennwortschutz meldet sich als Lesefehler. Er ist fachlich
            // etwas anderes als eine kaputte Datei und bekommt eine eigene,
            // für den Anwender brauchbare Erklärung – und zwar getrennt nach
            // Öffnungs- und Besitzerkennwort.
            PdfProtection fromMessage = ClassifyProtectionMessage(ex.Message);

            LogAnalysisFailed(_logger, ex.GetType().Name);

            PdfUpgradeBlocker blocker = fromMessage switch
            {
                PdfProtection.PasswordRequired => PdfUpgradeBlocker.Encrypted,
                PdfProtection.RightsRestricted => PdfUpgradeBlocker.RightsRestricted,
                _ => PdfUpgradeBlocker.Damaged,
            };

            if (!blockers.Contains(blocker))
            {
                blockers.Add(blocker);
            }

            return Unreadable(
                blockers,
                isEncrypted: fromMessage != PdfProtection.None || protection != PdfProtection.None);
        }
    }

    /// <summary>Ein Ergebnis für eine Datei, aus der nichts zu lesen war.</summary>
    private static PdfAnalysisResult Unreadable(
        IReadOnlyList<PdfUpgradeBlocker> blockers, bool isEncrypted)
        => new(
            PageCount: 0,
            PdfVersion: "unbekannt",
            IsEncrypted: isEncrypted,
            DeclaredPdfAPart: null,
            DeclaredPdfAConformance: null,
            EmbeddedFiles: [],
            ExistingInvoiceXml: null,
            ExistingInvoiceProfile: null,
            UpgradeBlockers: blockers);

    /// <summary>Wie eine Datei geschützt ist.</summary>
    private enum PdfProtection
    {
        /// <summary>Kein Verschlüsselungswörterbuch.</summary>
        None,

        /// <summary>
        /// Die Datei öffnet sich ohne Kennwort, trägt aber ein
        /// Verschlüsselungswörterbuch – ein Besitzerkennwort mit
        /// Rechteeinschränkungen.
        /// </summary>
        RightsRestricted,

        /// <summary>Zum Öffnen wird ein Kennwort verlangt.</summary>
        PasswordRequired,
    }

    /// <summary>
    /// Stellt fest, ob die Datei ein Verschlüsselungswörterbuch trägt.
    ///
    /// **Warum dafür eine zweite Bibliothek befragt wird.** PDFsharp beantwortet
    /// die Frage nicht verlässlich: Eine Datei mit Besitzerkennwort öffnet es
    /// anstandslos, und <c>SecuritySettings.IsEncrypted</c> meldet dabei
    /// <c>false</c>. Das ist nachgemessen und kein Verdacht. Der Eintrag
    /// <c>/Encrypt</c> steht im Trailer, und den legt PDFsharp nicht offen.
    ///
    /// PdfPig – im Kern ohnehin für die Textauswertung vorhanden – liest den
    /// Trailer und meldet den Fund über <c>IsEncrypted</c>. Wird zum Öffnen ein
    /// Kennwort verlangt, wirft es eine eigene Ausnahme; das ist der andere,
    /// fachlich deutlich verschiedene Fall.
    ///
    /// Das Öffnen kostet wenig: gemessen unter 20 ms selbst bei einer Datei von
    /// zwei Megabyte, weil nur die Struktur gelesen wird und nicht der Inhalt.
    /// </summary>
    private PdfProtection DetectProtection(string filePath)
    {
        try
        {
            using PigDocument document = PigDocument.Open(filePath);

            return document.IsEncrypted ? PdfProtection.RightsRestricted : PdfProtection.None;
        }
        catch (PdfDocumentEncryptedException)
        {
            return PdfProtection.PasswordRequired;
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            string reason = ex.GetType().Name;
            LogProtectionUnknown(_logger, reason);

            // Hier ist die Frage offen, nicht beantwortet. „Offen“ als
            // „ungeschützt“ zu lesen wäre genau der Fehler, der eine
            // rechtebeschränkte Datei bis in die Erzeugung durchwinkt, wo sie
            // dann mit einer falschen Begründung scheitert.
            return DetectProtectionByModifiability(filePath);
        }
    }

    /// <summary>
    /// Zweiter Weg zur selben Frage, wenn der erste keine Antwort gab: Lässt
    /// sich die Datei zum Ändern öffnen?
    ///
    /// Das ist genau die Fähigkeit, die der weitere Ablauf braucht – die
    /// Rechnungsdaten werden schließlich eingebettet. PDFsharp beantwortet sie
    /// eindeutig, wenn man sie so stellt: Beim Öffnen im Änderungsmodus verlangt
    /// eine rechtebeschränkte Datei das Besitzerkennwort.
    ///
    /// Der Modus wird nur hier verwendet und nicht für die reguläre Analyse: Er
    /// löst die Objektströme vollständig auf und kostet dadurch mehr, als eine
    /// bloße Untersuchung braucht.
    /// </summary>
    private static PdfProtection DetectProtectionByModifiability(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using PdfDocument document = PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

            return document.SecuritySettings.IsEncrypted
                ? PdfProtection.RightsRestricted
                : PdfProtection.None;
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            return ClassifyProtectionMessage(ex.Message);
        }
    }

    /// <summary>
    /// Ordnet die Meldung eines fehlgeschlagenen Öffnens ein.
    ///
    /// Die beiden Fälle sind fachlich weit auseinander und lesen sich fast
    /// gleich: „a password is required to open“ heißt, es gibt nichts zu sehen;
    /// „the owner password is required to modify“ heißt, man darf sehen, aber
    /// nicht ändern. Wer sie zusammenwirft, erzählt dem Anwender in einem der
    /// beiden Fälle etwas Falsches.
    ///
    /// Alles andere bleibt unbeantwortet – über eine beschädigte Datei
    /// entscheidet der Hauptweg.
    /// </summary>
    private static PdfProtection ClassifyProtectionMessage(string message)
    {
        bool ownerPassword = message.Contains("owner password", StringComparison.OrdinalIgnoreCase);

        if (ownerPassword)
        {
            return PdfProtection.RightsRestricted;
        }

        return message.Contains("password", StringComparison.OrdinalIgnoreCase)
               || message.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
            ? PdfProtection.PasswordRequired
            : PdfProtection.None;
    }

    /// <summary>
    /// Prüft, ob jede Schrift, mit der tatsächlich Text dargestellt wird, in der
    /// Datei steckt. Nicht eingebettete Schriften sind der häufigste Grund,
    /// warum ein PDF nicht PDF/A-fähig ist.
    ///
    /// **Warum die Frage nach der Verwendung nötig ist.** Früher stand hier
    /// „gibt es unter <c>/Resources /Font</c> irgendeinen Eintrag ohne
    /// Schriftdatei?“. Das klingt gleichbedeutend, ist es aber nicht: Sehr viele
    /// Erzeuger schreiben eine Schriftressource in jede Seite, auch wenn auf ihr
    /// nur ein Bild liegt. Eine eingescannte Rechnung wurde deshalb wegen einer
    /// Schrift abgelehnt, die kein einziges Zeichen zeichnet.
    ///
    /// Die Norm meint die Verwendung. ISO 19005 verlangt die Einbettung für
    /// Schriften, die zur Darstellung benutzt werden – eine unbenutzte Ressource
    /// stellt nichts dar.
    ///
    /// **Nicht** geschieht hier das Naheliegende und Falsche: Bild-PDF pauschal
    /// von der Prüfung auszunehmen. Eine Datei kann Bildseiten *und* echten Text
    /// mit fehlender Einbettung enthalten; dann bleibt es beim Hindernis.
    /// </summary>
    private bool AllUsedFontsEmbedded(PdfDocument document)
    {
        foreach (PdfPage page in document.Pages)
        {
            var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);

            if (!ScopeUsesOnlyEmbeddedFonts(
                    page.Elements.GetDictionary("/Resources"),
                    inheritedFonts: null,
                    () => ContentReader.ReadContent(page),
                    visited,
                    depth: 0))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Prüft einen Inhaltsbereich – eine Seite oder ein Form-XObject – samt der
    /// darin aufgerufenen Formulare.
    ///
    /// Gelingt das Lesen des Inhalts nicht, gilt wieder die alte, strengere
    /// Regel: Dann zählt jede Schriftressource als verwendet. Eine unlesbare
    /// Seite darf nicht zum Freibrief werden.
    ///
    /// **Zu <paramref name="inheritedFonts"/>:** Ein Formular ohne eigene
    /// Schriftliste greift auf die der umgebenden Ebene zurück. Die Regel ist
    /// alt und in neueren Fassungen der Norm abgekündigt, aber verbreitet – und
    /// wer sie nicht kennt, sieht in einem verschachtelten Formular ein
    /// <c>Tf /F1</c>, findet kein <c>/F1</c> und hält die Sache für erledigt.
    /// Genau dort verstecken sich nicht eingebettete Schriften.
    /// </summary>
    private bool ScopeUsesOnlyEmbeddedFonts(
        PdfDictionary? resources,
        PdfDictionary? inheritedFonts,
        Func<CSequence> readContent,
        HashSet<PdfDictionary> visited,
        int depth)
    {
        PdfDictionary? fonts = resources?.Elements.GetDictionary("/Font") ?? inheritedFonts;
        PdfDictionary? xObjects = resources?.Elements.GetDictionary("/XObject");

        // Ohne Schriften und ohne Formulare gibt es hier nichts zu entscheiden.
        if (fonts is null && xObjects is null)
        {
            return true;
        }

        CSequence content;

        try
        {
            content = readContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                      and not OutOfMemoryException
                                      and not StackOverflowException)
        {
            string reason = ex.GetType().Name;
            LogContentUnreadable(_logger, reason);

            return AllDeclaredFontsEmbedded(fonts);
        }

        (IReadOnlySet<string> usedFonts, bool textWithoutFont, IReadOnlySet<string> invokedForms) =
            ReadUsage(content);

        // Text ohne vorher gesetzte Schrift ist fehlerhafter Inhalt. Was dann
        // gezeichnet wird, ist nicht zu bestimmen – also gilt wieder alles.
        if (textWithoutFont && !AllDeclaredFontsEmbedded(fonts))
        {
            return false;
        }

        foreach (string name in usedFonts)
        {
            if (fonts?.Elements.GetDictionary(name) is { } font && !IsFontEmbedded(font))
            {
                return false;
            }
        }

        List<PdfDictionary> pending = PendingForms(xObjects, invokedForms, visited);

        if (pending.Count == 0)
        {
            return true;
        }

        // **Die Grenze darf nicht nach außen öffnen.** Vorher hieß „zu tief“
        // schlicht „in Ordnung“ – und damit wäre eine Datei, die es genau darauf
        // anlegt, an der Prüfung vorbei. Wer tiefer schachtelt, als hier
        // nachgesehen wird, bekommt kein Urteil über den Inhalt, sondern das
        // vorsichtige: nicht bestätigt. Der Weg über die sichtbare Kopie bleibt
        // ihm; er führt zu einer gültigen Datei.
        if (depth >= MaxFormDepth)
        {
            LogFormDepthExceeded(_logger, MaxFormDepth);

            return false;
        }

        return FormsUseOnlyEmbeddedFonts(pending, fonts, visited, depth);
    }

    /// <summary>
    /// Die Formulare, die noch zu untersuchen sind.
    ///
    /// Aussortiert wird alles, was kein Formular ist: <c>Do</c> zeichnet auch
    /// Bilder, und ein Bild hat keinen Inhaltsstrom zum Nachsehen. Ohne diese
    /// Trennung schlüge die Tiefengrenze bei jeder eingescannten Seite an.
    /// </summary>
    private static List<PdfDictionary> PendingForms(
        PdfDictionary? xObjects, IReadOnlySet<string> invokedForms, HashSet<PdfDictionary> visited)
    {
        if (xObjects is null)
        {
            return [];
        }

        return
        [
            .. from name in invokedForms
               let form = xObjects.Elements.GetDictionary(name)
               where form is not null
                     && form.Elements.GetName("/Subtype") == "/Form"
                     && form.Stream is not null
                     && !visited.Contains(form)
               select form,
        ];
    }

    /// <summary>
    /// Steigt in die aufgerufenen Form-XObjects ab.
    ///
    /// Viele Erzeuger legen den gesamten sichtbaren Seiteninhalt in ein solches
    /// Formular. Bliebe der Abstieg aus, sähe die Prüfung bei ihnen eine leere
    /// Seite und ließe jede fehlende Einbettung durchgehen.
    ///
    /// Die Schriftliste der umgebenden Ebene wird mitgegeben: Ein Formular ohne
    /// eigene greift auf sie zurück.
    /// </summary>
    private bool FormsUseOnlyEmbeddedFonts(
        List<PdfDictionary> forms,
        PdfDictionary? parentFonts,
        HashSet<PdfDictionary> visited,
        int depth)
    {
        foreach (PdfDictionary form in forms)
        {
            if (!visited.Add(form))
            {
                continue;
            }

            byte[] stream = form.Stream!.UnfilteredValue;

            if (!ScopeUsesOnlyEmbeddedFonts(
                    form.Elements.GetDictionary("/Resources"),
                    parentFonts,
                    () => ContentReader.ReadContent(stream),
                    visited,
                    depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Grenze für ineinander verschachtelte Formulare. Kein Rechnungsdokument
    /// braucht mehr; die Zahl schützt vor einer Datei, die es darauf anlegt.
    /// </summary>
    private const int MaxFormDepth = 8;

    /// <summary>
    /// Liest aus einem Inhaltsstrom, welche Schriften Text zeichnen und welche
    /// Formulare aufgerufen werden.
    ///
    /// Die Schrift gehört zum Grafikzustand: <c>Tf</c> setzt sie, <c>q</c> und
    /// <c>Q</c> legen sie ab und holen sie zurück. Ohne diesen Stapel würde eine
    /// nach <c>Q</c> gezeichnete Zeile der falschen Schrift zugerechnet.
    /// <c>BT</c> und <c>ET</c> setzen sie dagegen nicht zurück.
    /// </summary>
    private static (IReadOnlySet<string> UsedFonts, bool TextWithoutFont, IReadOnlySet<string> InvokedForms)
        ReadUsage(CSequence content)
    {
        var usedFonts = new HashSet<string>(StringComparer.Ordinal);
        var invokedForms = new HashSet<string>(StringComparer.Ordinal);
        var savedFonts = new Stack<string?>();

        string? currentFont = null;
        bool textWithoutFont = false;

        foreach (COperator op in Operators(content))
        {
            switch (op.OpCode.Name)
            {
                case "q":
                    savedFonts.Push(currentFont);
                    break;

                case "Q":
                    currentFont = savedFonts.Count > 0 ? savedFonts.Pop() : null;
                    break;

                case "Tf":
                    currentFont = FirstName(op);
                    break;

                // Die vier Anweisungen, die Zeichen auf das Blatt bringen.
                case "Tj":
                case "TJ":
                case "'":
                case "\"":
                    if (currentFont is null)
                    {
                        textWithoutFont = true;
                    }
                    else
                    {
                        usedFonts.Add(currentFont);
                    }

                    break;

                case "Do":
                    if (FirstName(op) is { } form)
                    {
                        invokedForms.Add(form);
                    }

                    break;

                default:
                    break;
            }
        }

        return (usedFonts, textWithoutFont, invokedForms);
    }

    /// <summary>
    /// Läuft die Inhaltsfolge ab, auch über verschachtelte Folgen hinweg.
    /// </summary>
    private static IEnumerable<COperator> Operators(CSequence content)
    {
        foreach (CObject item in content)
        {
            switch (item)
            {
                case COperator op:
                    yield return op;
                    break;

                case CSequence nested:
                    foreach (COperator inner in Operators(nested))
                    {
                        yield return inner;
                    }

                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>Der erste Name unter den Operanden, z. B. <c>/F1</c>.</summary>
    private static string? FirstName(COperator op)
        => op.Operands.OfType<CName>().FirstOrDefault()?.Name;

    /// <summary>Die alte, strengere Regel: jede erklärte Schrift zählt.</summary>
    private static bool AllDeclaredFontsEmbedded(PdfDictionary? fonts)
    {
        if (fonts is null)
        {
            return true;
        }

        foreach (string key in fonts.Elements.Keys)
        {
            if (fonts.Elements.GetDictionary(key) is { } font && !IsFontEmbedded(font))
            {
                return false;
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

        // SigFlags ungleich null bedeutet: Das Dokument enthält Signaturfelder.
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

            // **Begrenzt entpacken, nicht erst entpacken und dann messen.**
            // Früher stand hier UnfilteredValue mit einer Längenprüfung
            // danach. Das ist genau die falsche Reihenfolge: Nachgemessen
            // entfaltet eine PDF-Datei von 67 KB einen Anhang auf 64 MiB, und
            // der Speicher ist belegt, bevor die Prüfung überhaupt zum Zuge
            // kommt. Eine OutOfMemoryException fängt diese Klasse bewusst
            // nicht ab – eine fremde Datei könnte die Anwendung so beenden.
            EmbeddedFileReadResult content =
                BoundedEmbeddedFileReader.Read(specification, SecureXmlLimit);

            if (content.Status != EmbeddedFileReadStatus.Read || content.Content.Length == 0)
            {
                continue;
            }

            byte[] xml = content.Content;

            // Die XML stammt aus einer fremden Datei und wird ausschließlich
            // über den abgesicherten Leser ausgewertet.
            string? profile = _xmlReader.ReadProfileId(xml);

            return (xml, profile);
        }

        return (null, null);
    }

    /// <summary>Obergrenze für eine eingebettete XML, siehe SecureXml.</summary>
    private const int SecureXmlLimit = 8 * 1024 * 1024;

    /// <summary>
    /// Liefert alle Dateibeschreibungen des Dokuments, jede genau einmal.
    ///
    /// Eine korrekt aufgebaute Hybridrechnung verweist auf dieselbe
    /// Dateibeschreibung aus zwei Richtungen: aus dem Namensbaum
    /// /Names /EmbeddedFiles und aus dem Feld /AF. Ohne Entdopplung würde
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

        // Weg 2: das Feld /AF am Katalog. Manche Erzeuger füllen nur dieses.
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
    /// XMP fremder Dateien ist nicht vertrauenswürdig, und für die reine
    /// Anzeige genügt der Fund der beiden Felder.
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
    /// Löst die <c>#hh</c>-Maskierung eines PDF-Namens auf, damit im Bericht
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
        Message = "PDF konnte nicht analysiert werden ({Reason}). Datei wird als beschädigt gemeldet.")]
    private static partial void LogAnalysisFailed(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2012, Level = LogLevel.Information,
        Message = "Inhaltsstrom nicht lesbar ({Reason}). Jede erklärte Schrift gilt als verwendet.")]
    private static partial void LogContentUnreadable(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2013, Level = LogLevel.Information,
        Message = "Formulare tiefer als {MaxDepth} verschachtelt. Die Schrifteinbettung "
                  + "gilt als nicht bestätigt.")]
    private static partial void LogFormDepthExceeded(ILogger logger, int maxDepth);

    [LoggerMessage(
        EventId = 2011, Level = LogLevel.Information,
        Message = "Schutzzustand nicht feststellbar ({Reason}). Die Hauptanalyse entscheidet.")]
    private static partial void LogProtectionUnknown(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2014, Level = LogLevel.Information,
        Message = "Anhänge nicht lesbar ({Reason}). Die Analyse urteilt über den Zustand der Datei.")]
    private static partial void LogAttachmentsUnreadable(ILogger logger, string reason);

    /// <summary>Wandelt die interne Versionszahl (z. B. 17) in "1.7".</summary>
    private static string FormatVersion(int version)
        => version <= 0
            ? "unbekannt"
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{version / 10}.{version % 10}");
}

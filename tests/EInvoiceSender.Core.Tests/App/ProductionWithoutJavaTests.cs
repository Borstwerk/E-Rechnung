using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält fest, dass die ausgelieferte Anwendung ohne Java-Laufzeit auskommt.
///
/// **Warum das eine Regel ist:** Mustangproject, das CEN-Schematron und
/// veraPDF belegen, dass diese Anwendung normgerechte Dateien erzeugt. Sie
/// sind damit Werkzeuge der Entwicklung und der Freigabe – und sie brauchen
/// Java. Auf dem Rechner eines Kleinunternehmers hat das nichts zu suchen: Das
/// Installationspaket bringt keine Java-Laufzeit mit, und niemand soll eine
/// nachinstallieren müssen, um eine Rechnung zu schreiben.
///
/// Trägt die Zusammenstellung der Anwendung einen externen Validator ein,
/// sucht sie beim Erzeugen jeder Rechnung nach Java. Vorher war genau das der
/// Fall.
/// </summary>
public sealed class ProductionWithoutJavaTests
{
    /// <summary>Was in der ausgelieferten Anwendung nicht vorkommen darf.</summary>
    private static readonly string[] DevelopmentOnlyTypes =
    [
        "IExternalDocumentValidator", "MustangValidator", "MustangOptions",
    ];

    [Fact]
    public void DieAusgelieferteAnwendungRichtetKeinenExternenValidatorEin()
    {
        string composition = Source("App.xaml.cs");

        string[] gefunden =
        [
            .. from typ in DevelopmentOnlyTypes
               where CodeOf(composition).Contains(typ, StringComparison.Ordinal)
               select typ,
        ];

        Assert.True(
            gefunden.Length == 0,
            $"App.xaml.cs richtet {string.Join(", ", gefunden)} ein. Damit sucht die "
            + "ausgelieferte Anwendung beim Erzeugen jeder Rechnung nach einer "
            + "Java-Laufzeit. Die Referenzprüfung gehört in Entwicklung und Pipeline, "
            + "nicht auf den Rechner des Anwenders.");
    }

    /// <summary>
    /// Ohne diese Prüfung wäre der Test oben wertlos: Findet er die Datei
    /// nicht oder liest er nur Kommentare, meldet er fröhlich Erfolg.
    /// </summary>
    [Fact]
    public void DieZusammenstellungWirdTatsächlichGelesen()
    {
        string code = CodeOf(Source("App.xaml.cs"));

        Assert.Contains("IEInvoiceService", code, StringComparison.Ordinal);
        Assert.Contains("IProcessRunner", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Das Installationspaket darf keine Java-Werkzeuge mitliefern. Es nimmt
    /// den Inhalt des Veröffentlichungsverzeichnisses – dort darf nichts
    /// derartiges landen.
    /// </summary>
    [Fact]
    public void DasInstallationspaketLiefertKeineJavaWerkzeugeMit()
    {
        string[] treffer =
        [
            .. from path in ProjectFiles.With(".wxs", ".csproj")
               let text = File.ReadAllText(path)
               where text.Contains("mustang", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("verapdf", StringComparison.OrdinalIgnoreCase)
                     || text.Contains(".jar", StringComparison.OrdinalIgnoreCase)
               select ProjectFiles.Relative(path),
        ];

        Assert.True(
            treffer.Length == 0,
            $"Diese Dateien nehmen Java-Werkzeuge in den Bauvorgang auf: {string.Join(", ", treffer)}.");
    }

    /// <summary>
    /// Entfernt Kommentare. Der Kommentar in App.xaml.cs erklärt gerade,
    /// warum kein Validator eingetragen ist, und nennt dabei die Namen.
    /// </summary>
    private static string CodeOf(string source)
        => string.Join(
            "\n",
            source
                .Split('\n')
                .Select(line => line.TrimStart())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                               && !line.StartsWith("///", StringComparison.Ordinal)));

    private static string Source(string file)
        => File.ReadAllText(ProjectFiles.With(".cs").Single(p => Path.GetFileName(p) == file));
}

using System.Text.RegularExpressions;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft die ViewModels der WPF-Anwendung als Quelltext.
///
/// **Warum als Quelltext und nicht zur Laufzeit?** Die ViewModels liegen in
/// <c>EInvoiceSender.App</c>, einem WPF-Projekt. Ein Testprojekt, das darauf
/// verweist, läuft nur auf einem Windows-Rechner. Diese Prüfungen lesen die
/// Dateien deshalb als Text und laufen auf jedem Build-Agenten – und damit
/// tatsächlich bei jedem Lauf, nicht nur auf dem Entwicklerrechner.
///
/// Beide Regeln stammen aus Fehlern, die im laufenden Programm aufgetreten
/// sind und die kein bestehender Test finden konnte.
/// </summary>
public sealed class ViewModelSourceRuleTests
{
    /// <summary>
    /// In einem ViewModel muss die Fortsetzung nach dem <c>await</c> auf den
    /// Oberflächen-Thread zurückkehren.
    ///
    /// Mit <c>ConfigureAwait(false)</c> läuft sie auf einem Threadpool-Thread
    /// und meldet von dort an gebundene Bedienelemente. WPF bricht dann ab mit
    /// "Der aufrufende Thread kann nicht auf dieses Objekt zugreifen". Genau so
    /// ist die Anwendung beim ersten Start gescheitert.
    ///
    /// Ein Laufzeittest kann das hier nicht abfangen: Ein gewöhnlicher
    /// Testlauf hat keinen Synchronisierungskontext, und ohne Kontext
    /// verhalten sich <c>ConfigureAwait(true)</c> und <c>ConfigureAwait(false)</c>
    /// gleich.
    /// </summary>
    [Fact]
    public void KeinConfigureAwaitFalseInDerOberfläche()
    {
        string[] treffer =
        [
            .. AppSourceFiles()
                .Where(f => File.ReadAllText(f.FullName).Contains("ConfigureAwait(false)", StringComparison.Ordinal))
                .Select(f => f.Name),
        ];

        Assert.True(
            treffer.Length == 0,
            $"ConfigureAwait(false) steht in: {string.Join(", ", treffer)}. In einem ViewModel "
            + "kehrt die Fortsetzung damit nicht auf den Oberflächen-Thread zurück; jede "
            + "anschließende Meldung an ein gebundenes Bedienelement lässt WPF mit "
            + "InvalidOperationException abbrechen. In der Oberfläche gehört immer "
            + "ConfigureAwait(true) ans await.");
    }

    /// <summary>
    /// Jede Eigenschaft, die eine Freigabeprüfung liest, muss ihren Befehl
    /// auch benachrichtigen.
    ///
    /// Eine WPF-Schaltfläche fragt einen <c>RelayCommand</c> einmal beim
    /// Binden und danach nur noch, wenn <c>CanExecuteChanged</c> gemeldet wird.
    /// Fehlt die Benachrichtigung, bleibt sie im zuletzt bewerteten Zustand
    /// hängen – meist dauerhaft gesperrt. Genau so blieb "Weiter" gesperrt,
    /// obwohl die PDF verarbeitbar war.
    ///
    /// Ein Test, der schlicht <c>CanExecute</c> aufruft, findet das **nie**:
    /// Der Aufruf wertet die Bedingung jedes Mal frisch aus und liefert deshalb
    /// immer die richtige Antwort, auch wenn die Schaltfläche falsch aussieht.
    /// Geprüft werden muss die Verdrahtung, nicht der Wert.
    /// </summary>
    [Theory]
    [MemberData(nameof(ViewModelFiles))]
    public void JedeGeleseneEigenschaftBenachrichtigtIhrenBefehl(string dateiname)
    {
        string quelle = File.ReadAllText(
            AppSourceFiles().Single(f => f.Name == dateiname).FullName);

        string[] observable = [.. ObservablePropertyPattern
            .Matches(quelle)
            .Select(m => Capitalize(m.Groups["feld"].Value))];

        var fehler = new List<string>();

        foreach (Match command in CommandPattern.Matches(quelle))
        {
            string prüfung = command.Groups["prüfung"].Value;
            string befehl = CommandName(command.Groups["methode"].Value);

            Match body = Regex.Match(
                quelle,
                @"private\s+bool\s+" + Regex.Escape(prüfung) + @"\(\)\s*=>(?<rumpf>.*?);\s*\n",
                RegexOptions.Singleline);

            if (!body.Success)
            {
                continue;
            }

            foreach (string eigenschaft in observable)
            {
                bool wirdGelesen = Regex.IsMatch(
                    body.Groups["rumpf"].Value, @"(?<![\w.])" + Regex.Escape(eigenschaft) + @"(?![\w])");

                if (!wirdGelesen)
                {
                    continue;
                }

                bool benachrichtigt = Regex.IsMatch(
                    quelle,
                    @"NotifyCanExecuteChangedFor\(nameof\(" + Regex.Escape(befehl) + @"\)\)[^;]*?private\s+\w+\??\s+_"
                    + Regex.Escape(Uncapitalize(eigenschaft)) + @"\b",
                    RegexOptions.Singleline)
                    || AttributesOf(quelle, eigenschaft).Contains($"NotifyCanExecuteChangedFor(nameof({befehl}))", StringComparison.Ordinal);

                if (!benachrichtigt)
                {
                    fehler.Add($"{eigenschaft} wird von {prüfung} gelesen, benachrichtigt aber {befehl} nicht");
                }
            }
        }

        Assert.True(
            fehler.Count == 0,
            $"In {dateiname}: {string.Join("; ", fehler)}. Die zugehörige Schaltfläche bleibt "
            + "in WPF im alten Zustand hängen. Abhilfe: "
            + "[NotifyCanExecuteChangedFor(nameof(<Befehl>Command))] an der gelesenen Eigenschaft.");
    }

    /// <summary>Die ViewModel-Dateien der Anwendung.</summary>
    public static TheoryData<string> ViewModelFiles()
    {
        var data = new TheoryData<string>();

        foreach (FileInfo file in AppSourceFiles().Where(f => f.Name.EndsWith("ViewModel.cs", StringComparison.Ordinal)))
        {
            data.Add(file.Name);
        }

        return data;
    }

    private static readonly Regex ObservablePropertyPattern = new(
        @"\[ObservableProperty\][^;]*?private\s+[\w<>?\.]+\s+_(?<feld>\w+)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CommandPattern = new(
        @"\[RelayCommand\(CanExecute\s*=\s*nameof\((?<prüfung>\w+)\)\)\]\s*"
        + @"public\s+(?:async\s+)?[\w<>]+\s+(?<methode>\w+)\s*\(",
        RegexOptions.Compiled);

    /// <summary>Liefert den Attributblock unmittelbar vor einem Feld.</summary>
    private static string AttributesOf(string quelle, string eigenschaft)
    {
        Match m = Regex.Match(
            quelle,
            @"(?<attribute>(?:\s*\[[^\]]+\]\s*)+)private\s+[\w<>?\.]+\s+_"
            + Regex.Escape(Uncapitalize(eigenschaft)) + @"\b",
            RegexOptions.Singleline);

        return m.Success ? m.Groups["attribute"].Value : string.Empty;
    }

    /// <summary>
    /// Aus dem Methodennamen wird der Name der erzeugten Befehlseigenschaft:
    /// <c>GoForwardAsync</c> wird zu <c>GoForwardCommand</c>.
    /// </summary>
    private static string CommandName(string methode)
        => (methode.EndsWith("Async", StringComparison.Ordinal)
            ? methode[..^"Async".Length]
            : methode) + "Command";

    private static string Capitalize(string value)
        => char.ToUpperInvariant(value[0]) + value[1..];

    private static string Uncapitalize(string value)
        => char.ToLowerInvariant(value[0]) + value[1..];

    private static IEnumerable<FileInfo> AppSourceFiles()
        => new DirectoryInfo(Path.Combine(TestPaths.RepositoryRoot, "src", "EInvoiceSender.App"))
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}

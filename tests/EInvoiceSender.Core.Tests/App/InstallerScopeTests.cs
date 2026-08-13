using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Prüft, dass das Installationspaket als dual-purpose-Paket verfasst bleibt.
///
/// **Der Fehler:** Das Paket war <c>Scope="perUser"</c> und legte
/// INSTALLFOLDER fest unter <c>LocalAppDataFolder</c> ab. Damit landeten
/// sämtliche Programmdateien im Benutzerprofil – und genau das beanstandet
/// ICE38: Eine Komponente, die in das Benutzerprofil installiert, braucht
/// einen Registrierungswert als Schlüsselpfad, keine Datei. Bei rund 580
/// eingelesenen Laufzeitdateien ergab das 580 Fehler, und der Bau brach nach
/// der MSI-Prüfung ab.
///
/// Der Harvester kann keine Registrierungsschlüssel erfinden, eine von Hand
/// gepflegte Dateiliste über hunderte Einträge wäre eine Zumutung, und ICE38
/// zu unterdrücken hieße nur, die Prüfung zum Schweigen zu bringen. Die
/// Lösung liegt beim Ziel, nicht bei den Dateien.
///
/// **Zwei weitere Befunde aus dem Windows-Bau** stecken ebenfalls hier:
/// ICE57 am Schlüsselpfad der Verknüpfungen und ICE61 an der
/// Aktualisierungsregel. Beide sind an der Ursache behoben, keiner ist
/// unterdrückt.
///
/// **Warum als Quelltextprüfung:** Ein MSI entsteht nur unter Windows;
/// <c>wix.exe</c> bricht auf jeder anderen Plattform sofort ab. Dieser Test
/// hält deshalb die Merkmale fest, an denen die Entscheidung hängt.
/// </summary>
public sealed class InstallerScopeTests
{
    private static readonly XNamespace Wxs = "http://wixtoolset.org/schemas/v4/wxs";
    private const string DesktopFeatureId = "Desktopverkn" + "uepfung";
    private const string DesktopComponentId = "DesktopVerkn" + "uepfung";
    private const string StartmenuComponentId = "StartmenuVerkn" + "uepfung";

    [Fact]
    public void DasPaketIstNichtMehrReinBenutzerbezogen()
    {
        string scope = Scope();

        Assert.False(
            string.Equals(scope, "perUser", StringComparison.Ordinal),
            "Ein reines perUser-Paket installiert alle Programmdateien in das Benutzerprofil. "
            + "ICE38 verlangt dort einen Registrierungswert als Schlüsselpfad – für rund 580 "
            + "eingelesene Dateien nicht zu leisten.");
    }

    [Fact]
    public void DasPaketTaugtFürBeideInstallationsarten()
        => Assert.Equal("perUserOrMachine", Scope());

    /// <summary>
    /// Die Vorgabe „nur für mich“ kommt aus dem Scope, nicht aus einer eigenen
    /// Eigenschaft: WiX setzt für <c>perUserOrMachine</c> selbst
    /// <c>ALLUSERS=2</c> und <c>MSIINSTALLPERUSER=1</c>. Beide hier noch
    /// einmal festzulegen wäre überflüssig und je nach Verarbeitung eine
    /// doppelte Definition.
    /// </summary>
    [Theory]
    [InlineData("ALLUSERS")]
    [InlineData("MSIINSTALLPERUSER")]
    public void DieVorgabeKommtAusDemScopeUndNichtAusEinerEigenenEigenschaft(string property)
    {
        Assert.DoesNotContain(
            Package().Elements(Wxs + "Property"),
            p => string.Equals(p.Attribute("Id")?.Value, property, StringComparison.Ordinal));
    }

    [Fact]
    public void DieAnwendungLiegtNichtMehrFestImBenutzerprofil()
    {
        string[] verzeichnisse = [.. StandardDirectories()];

        Assert.DoesNotContain("LocalAppDataFolder", verzeichnisse);
    }

    /// <summary>
    /// Windows Installer soll den Zielordner je nach Installationsart selbst
    /// auflösen – benutzerbezogen unter %LOCALAPPDATA%\Programs, sonst unter
    /// C:\Program Files.
    /// </summary>
    [Fact]
    public void DerZielordnerWirdVonWindowsInstallerAufgelöst()
    {
        XElement programFiles = Assert.Single(
            Package().Elements(Wxs + "StandardDirectory"),
            d => d.Attribute("Id")?.Value == "ProgramFiles64Folder");

        Assert.Contains(
            programFiles.Descendants(Wxs + "Directory"),
            d => d.Attribute("Id")?.Value == "INSTALLFOLDER");
    }

    /// <summary>
    /// Der Schlüsselpfad der Verknüpfungskomponenten ist eindeutig
    /// benutzerbezogen.
    ///
    /// **Der Fehler:** Vorher stand dort HKMU – ein Pfad, der erst zur
    /// Installationszeit zu HKCU oder HKLM wird. ICE57 beanstandet genau
    /// das: „has both per-user data and a keypath that can be either
    /// per-user or per-machine.“ Eine Verknüpfung im Startmenü ist immer
    /// benutzerbezogene Angabe; ihr Schlüsselpfad darf deshalb nicht offen
    /// lassen, für wen er gilt.
    ///
    /// HKCU erfüllt zugleich ICE38, das für alles, was in ein Benutzerprofil
    /// installiert, einen Schlüsselpfad unter HKCU verlangt.
    /// </summary>
    [Fact]
    public void DerSchlüsselpfadDerVerknüpfungenIstEindeutigBenutzerbezogen()
    {
        string[] wurzeln =
        [
            .. Package()
                .Descendants(Wxs + "RegistryValue")
                .Select(r => r.Attribute("Root")?.Value ?? string.Empty),
        ];

        Assert.NotEmpty(wurzeln);
        Assert.All(wurzeln, root => Assert.Equal("HKCU", root));
    }

    /// <summary>
    /// Das Paket ersetzt nur ältere Fassungen seiner selbst.
    ///
    /// <c>AllowSameVersionUpgrades</c> nimmt die eigene Fassung in den
    /// Bereich der zu ersetzenden Fassungen auf. Die Obergrenze ist dann
    /// nicht mehr kleiner als die eigene Version – ICE61 meldet „This
    /// product should remove only older versions of itself“. Ein Paket, das
    /// sich selbst für seinen Vorgänger hält, kann sich bei einer
    /// Neuinstallation selbst wieder entfernen.
    /// </summary>
    [Fact]
    public void DasPaketErsetztNurÄltereFassungen()
    {
        XElement upgrade = Assert.Single(Package().Elements(Wxs + "MajorUpgrade"));

        Assert.Null(upgrade.Attribute("AllowSameVersionUpgrades"));
        Assert.NotNull(upgrade.Attribute("DowngradeErrorMessage"));
    }

    /// <summary>
    /// Jede veröffentlichte Produktversion behält ihren festen ProductCode.
    /// Der Code von 0.1.0 ist bereits veröffentlicht und deshalb unveränderlich;
    /// 0.2.0 braucht einen anderen, ebenfalls festen Code.
    /// </summary>
    [Fact]
    public void DieProductCodesDerVeröffentlichtenFassungenBleibenStabil()
    {
        Dictionary<string, string> mappings = ProductCodeMappings();

        Assert.Equal(2, mappings.Count);
        Assert.Equal(
            "723d8a8e-cb3d-4ec0-81d2-3821a56be91d",
            mappings["0.1.0"],
            ignoreCase: true);
        Assert.Equal(
            "f69b7118-58e7-4bb9-b4ff-411056aa3776",
            mappings["0.2.0"],
            ignoreCase: true);
        Assert.False(
            string.Equals(mappings["0.1.0"], mappings["0.2.0"], StringComparison.OrdinalIgnoreCase));
        Assert.Equal("$(ProductCode)", Package().Attribute("ProductCode")?.Value);
    }

    /// <summary>
    /// Eine neue Produktversion ohne feste Zuordnung darf nicht mit einem von
    /// WiX automatisch erzeugten ProductCode weitergebaut werden.
    /// </summary>
    [Fact]
    public void UnbekannteProduktversionBrichtVorDemWixBauAb()
    {
        XElement project = Document("EInvoiceSender.Setup.wixproj").Root!;
        XElement target = Assert.Single(
            project.Elements(),
            e => e.Name.LocalName == "Target"
                 && e.Attribute("Name")?.Value == "ValidateProductIdentity");
        XElement error = Assert.Single(target.Elements(), e => e.Name.LocalName == "Error");

        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(ProductCode)", error.Attribute("Condition")?.Value ?? string.Empty,
                        StringComparison.Ordinal);
        Assert.Contains("$(VersionPrefix)", error.Attribute("Text")?.Value ?? string.Empty,
                        StringComparison.Ordinal);
    }

    /// <summary>
    /// Der UpgradeCode ist die Kennung, an der Windows eine spätere Fassung
    /// als Aktualisierung erkennt. Ändert er sich, steht die neue Fassung
    /// neben der alten statt an ihrer Stelle – und der Anwender hat die
    /// Anwendung zweimal installiert.
    /// </summary>
    [Fact]
    public void DerUpgradeCodeBleibtUnverändert()
        => Assert.Equal(
            "7f3c1d92-4b6a-4d21-9d0e-2a5c8b1e7f40",
            Package().Attribute("UpgradeCode")?.Value,
            ignoreCase: true);

    /// <summary>
    /// Der Startmenüeintrag gehört unverändert zur Hauptfunktion, die
    /// Desktopverknüpfung zu einem eigenen Feature. Beide stehen auf Level 1:
    /// Das ist der wirksame Default für stille und sichtbare Erstinstallationen;
    /// die sichtbare Checkbox kann ausschließlich das Desktopfeature abwählen.
    /// </summary>
    [Fact]
    public void DerStartmenüeintragIstFestUndDerDesktopeintragWählbar()
    {
        XElement haupt = Feature("Hauptfunktion");
        XElement desktop = Feature(DesktopFeatureId);

        Assert.Equal("1", haupt.Attribute("Level")?.Value);
        Assert.Equal("1", desktop.Attribute("Level")?.Value);
        Assert.Equal(["AnwendungsDateien", StartmenuComponentId],
            KomponentenUndGruppenIn(haupt));
        Assert.Equal([DesktopComponentId], KomponentenUndGruppenIn(desktop));

        Assert.Equal(StartmenuComponentId, KomponenteFür("StartmenuOrdner"));
        Assert.Equal(DesktopComponentId, KomponenteFür("DesktopFolder"));

        XElement startmenuShortcut = Shortcut("StartmenuEintrag");
        XElement desktopShortcut = Shortcut("DesktopEintrag");
        Assert.Equal("[INSTALLFOLDER]EInvoiceSender.exe", startmenuShortcut.Attribute("Target")?.Value);
        Assert.Equal("[INSTALLFOLDER]EInvoiceSender.exe", desktopShortcut.Attribute("Target")?.Value);

        XElement desktopKeyPath = Assert.Single(
            Component(DesktopComponentId).Elements(Wxs + "RegistryValue"));
        Assert.Equal("HKCU", desktopKeyPath.Attribute("Root")?.Value);
        Assert.Equal("Desktop", desktopKeyPath.Attribute("Name")?.Value);
        Assert.Equal("yes", desktopKeyPath.Attribute("KeyPath")?.Value);
    }

    [Fact]
    public void DieDesktopoptionVerwendetNurNativeMsiFeatureereignisse()
        => Assert.Empty(DesktopOptionViolations(Document("Package.wxs"), Document("InstallerUI.wxs")));

    [Fact]
    public void DieStandardnavigationDesInstallDirDialogsatzesBleibtErhalten()
    {
        XDocument ui = Document("InstallerUI.wxs");
        (string Dialog, string Control, string Event, string Value, string? Condition)[] expected =
        [
            ("ExitDialog", "Finish", "EndDialog", "Return", null),
            ("WelcomeDlg", "Next", "NewDialog", "LicenseAgreementDlg", "NOT Installed"),
            ("WelcomeDlg", "Next", "NewDialog", "VerifyReadyDlg", "Installed AND PATCH"),
            ("LicenseAgreementDlg", "Back", "NewDialog", "WelcomeDlg", null),
            ("LicenseAgreementDlg", "Next", "NewDialog", "InstallDirDlg", "LicenseAccepted = \"1\""),
            ("InstallDirDlg", "Back", "NewDialog", "LicenseAgreementDlg", null),
            ("MaintenanceWelcomeDlg", "Next", "NewDialog", "MaintenanceTypeDlg", null),
            ("MaintenanceTypeDlg", "RepairButton", "NewDialog", "VerifyReadyDlg", null),
            ("MaintenanceTypeDlg", "RemoveButton", "NewDialog", "VerifyReadyDlg", null),
            ("MaintenanceTypeDlg", "Back", "NewDialog", "MaintenanceWelcomeDlg", null),
            ("VerifyReadyDlg", "Back", "NewDialog", "MaintenanceTypeDlg", "Installed AND NOT PATCH"),
            ("VerifyReadyDlg", "Back", "NewDialog", "WelcomeDlg", "Installed AND PATCH"),
        ];

        foreach ((string dialog, string control, string eventName, string value, string? condition) in expected)
        {
            XElement publish = Publish(ui, dialog, control, eventName, value);
            Assert.Equal(condition, publish.Attribute("Condition")?.Value);
        }

        string[] builtInActions =
        [
            .. ui.Descendants(Wxs + "Publish")
                .Where(element => element.Attribute("Event")?.Value == "DoAction")
                .Select(element => element.Attribute("Value")?.Value ?? string.Empty)
                .OrderBy(value => value, StringComparer.Ordinal),
        ];
        Assert.Equal(
            ["WixUIPrintEula_X64", "WixUIValidatePath_X64", "WixUIValidatePath_X64"],
            builtInActions);
        Assert.Contains(ui.Descendants(Wxs + "UIRef"),
            element => element.Attribute("Id")?.Value == "WixUI_Common");
        Assert.Equal("1", Property(ui, "ARPNOMODIFY").Attribute("Value")?.Value);
    }

    /// <summary>
    /// Negativnachweis: Die Strukturprüfung muss typische Drifts einzeln
    /// erkennen. Die mutierten Dokumente existieren ausschließlich im
    /// Arbeitsspeicher; produktive Dateien und MSI werden nicht verändert.
    /// </summary>
    [Theory]
    [InlineData("Level")]
    [InlineData("Default")]
    [InlineData("Startmenu")]
    [InlineData("UpgradeCondition")]
    [InlineData("FeatureEvent")]
    [InlineData("CustomAction")]
    public void DieDesktopstrukturprüfungErkenntUnzulässigeAbweichungen(string mutation)
    {
        var package = new XDocument(Document("Package.wxs"));
        var ui = new XDocument(Document("InstallerUI.wxs"));

        switch (mutation)
        {
            case "Level":
                Feature(package, DesktopFeatureId).SetAttributeValue("Level", "2");
                break;
            case "Default":
                Property(ui, "INSTALLDESKTOPSHORTCUT").SetAttributeValue("Value", "0");
                break;
            case "Startmenu":
                Feature(package, "Hauptfunktion")
                    .Elements(Wxs + "ComponentRef")
                    .Single(element => element.Attribute("Id")?.Value == StartmenuComponentId)
                    .Remove();
                Feature(package, DesktopFeatureId)
                    .Add(new XElement(Wxs + "ComponentRef", new XAttribute("Id", StartmenuComponentId)));
                break;
            case "UpgradeCondition":
                Publish(ui, "InstallDirDlg", "Next", "NewDialog", "DesktopShortcutDlg")
                    .SetAttributeValue("Condition",
                        "(WIXUI_DONTVALIDATEPATH OR WIXUI_INSTALLDIR_VALID=\"1\") AND NOT Installed");
                break;
            case "FeatureEvent":
                Publish(ui, "DesktopShortcutDlg", "Next", "AddLocal", DesktopFeatureId)
                    .SetAttributeValue("Condition", "1");
                break;
            case "CustomAction":
                ui.Root!.Element(Wxs + "Fragment")!
                    .Add(new XElement(Wxs + "CustomAction", new XAttribute("Id", "DesktopCustomAction")));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        Assert.NotEmpty(DesktopOptionViolations(package, ui));
    }

    private static List<string> DesktopOptionViolations(XDocument packageDocument, XDocument uiDocument)
    {
        var violations = new List<string>();
        XElement package = packageDocument.Root!.Element(Wxs + "Package")!;
        XElement ui = Assert.Single(uiDocument.Descendants(Wxs + "UI"));

        void Require(bool condition, string message)
        {
            if (!condition)
            {
                violations.Add(message);
            }
        }

        XElement haupt = Feature(packageDocument, "Hauptfunktion");
        XElement desktop = Feature(packageDocument, DesktopFeatureId);
        string[] hauptRefs = KomponentenUndGruppenIn(haupt);
        string[] desktopRefs = KomponentenUndGruppenIn(desktop);

        Require(haupt.Attribute("Level")?.Value == "1", "Hauptfunktion muss Level 1 behalten.");
        Require(desktop.Attribute("Level")?.Value == "1", "Desktopverknüpfung muss standardmäßig lokal sein.");
        Require(hauptRefs.SequenceEqual(["AnwendungsDateien", StartmenuComponentId]),
            "Startmenü und Anwendung müssen ausschließlich in der Hauptfunktion bleiben.");
        Require(desktopRefs.SequenceEqual([DesktopComponentId]),
            "Das Desktopfeature darf ausschließlich die Desktopkomponente enthalten.");
        Require(package.Elements(Wxs + "UIRef").SingleOrDefault()?.Attribute("Id")?.Value == "BorstWerk_InstallDir",
            "Das Paket muss genau die eigene vollständige UI-Sequenz referenzieren.");

        Require(ui.Attribute("Id")?.Value == "BorstWerk_InstallDir", "Die UI-Kennung wurde verändert.");
        Require(Property(uiDocument, "WIXUI_INSTALLDIR").Attribute("Value")?.Value == "INSTALLFOLDER",
            "Die InstallDir-Eigenschaft muss erhalten bleiben.");
        Require(Property(uiDocument, "INSTALLDESKTOPSHORTCUT").Attribute("Value")?.Value == "1",
            "Die Desktopoption muss standardmäßig aktiviert sein.");

        XElement checkBox = Assert.Single(
            ui.Descendants(Wxs + "Control"),
            control => control.Attribute("Id")?.Value == "DesktopShortcutCheckBox");
        Require(checkBox.Attribute("Type")?.Value == "CheckBox", "Die Desktopoption muss eine native Checkbox sein.");
        Require(checkBox.Attribute("Property")?.Value == "INSTALLDESKTOPSHORTCUT",
            "Die Checkbox muss ausschließlich ihre Auswahlproperty setzen.");
        Require(checkBox.Attribute("CheckBoxValue")?.Value == "1", "Der gesetzte Checkboxwert muss 1 sein.");
        Require(checkBox.Attribute("Text")?.Value == "Desktop-Verknüpfung erstellen",
            "Der sichtbare Text der Desktopoption wurde verändert.");

        XElement forward = Publish(uiDocument, "InstallDirDlg", "Next", "NewDialog", "DesktopShortcutDlg");
        XElement backward = Publish(uiDocument, "VerifyReadyDlg", "Back", "NewDialog", "DesktopShortcutDlg");
        const string firstInstall = "NOT Installed AND NOT WIX_UPGRADE_DETECTED";
        Require((forward.Attribute("Condition")?.Value ?? string.Empty).Contains(firstInstall, StringComparison.Ordinal),
            "Der Dialog darf vorwärts nur bei echter Erstinstallation erscheinen.");
        Require(backward.Attribute("Condition")?.Value == firstInstall,
            "Der Zurück-Pfad darf nur bei echter Erstinstallation zum Desktopdialog führen.");
        Require(ui.Descendants(Wxs + "Publish").Count(p => p.Attribute("Value")?.Value == "DesktopShortcutDlg") == 2,
            "Maintenance und Upgrade dürfen keinen weiteren Pfad zum Desktopdialog besitzen.");

        XElement addLocal = Publish(uiDocument, "DesktopShortcutDlg", "Next", "AddLocal", DesktopFeatureId);
        XElement remove = Publish(uiDocument, "DesktopShortcutDlg", "Next", "Remove", DesktopFeatureId);
        Require(addLocal.Attribute("Condition")?.Value == "INSTALLDESKTOPSHORTCUT = \"1\"",
            "Aktiviert muss das Desktopfeature lokal anfordern.");
        Require(remove.Attribute("Condition")?.Value == "NOT INSTALLDESKTOPSHORTCUT",
            "Abgewählt muss das Desktopfeature absent anfordern.");
        Require(ui.Descendants(Wxs + "Publish").Count(p =>
                p.Attribute("Event")?.Value is "AddLocal" or "Remove") == 2,
            "Nur die beiden nativen Desktop-Featureereignisse sind zulässig.");
        Require(!packageDocument.Descendants(Wxs + "CustomAction").Any()
                && !uiDocument.Descendants(Wxs + "CustomAction").Any(),
            "Für die Desktopoption darf keine Custom Action verfasst werden.");

        return violations;
    }

    /// <summary>Kennung der Komponente, die in diesen Ordner installiert.</summary>
    private static string KomponenteFür(string ordner)
        => Assert.Single(
            Package().Elements(Wxs + "Component"),
            c => c.Attribute("Directory")?.Value == ordner)
            .Attribute("Id")!.Value;

    private static string[] KomponentenUndGruppenIn(XElement feature)
        =>
        [
            .. feature.Elements()
                .Where(element => element.Name == Wxs + "ComponentRef"
                                  || element.Name == Wxs + "ComponentGroupRef")
                .Select(element => element.Attribute("Id")?.Value ?? string.Empty),
        ];

    private static XElement Component(string id)
        => Assert.Single(Package().Elements(Wxs + "Component"),
            element => element.Attribute("Id")?.Value == id);

    private static XElement Shortcut(string id)
        => Assert.Single(Package().Descendants(Wxs + "Shortcut"),
            element => element.Attribute("Id")?.Value == id);

    private static XElement Feature(string id)
        => Feature(Document("Package.wxs"), id);

    private static XElement Feature(XDocument document, string id)
        => Assert.Single(document.Descendants(Wxs + "Feature"),
            element => element.Attribute("Id")?.Value == id);

    private static XElement Property(XDocument document, string id)
        => Assert.Single(document.Descendants(Wxs + "Property"),
            element => element.Attribute("Id")?.Value == id);

    private static XElement Publish(
        XDocument document,
        string dialog,
        string control,
        string eventName,
        string value)
        => Assert.Single(document.Descendants(Wxs + "Publish"),
            element => element.Attribute("Dialog")?.Value == dialog
                       && element.Attribute("Control")?.Value == control
                       && element.Attribute("Event")?.Value == eventName
                       && element.Attribute("Value")?.Value == value);

    /// <summary>
    /// Beide Verknüpfungen tragen das BorstWerk-Symbol, und die Symboldatei
    /// ist im Paket angemeldet. Fehlte der Eintrag, zeigte „Apps und
    /// Features“ das Ersatzsymbol von Windows.
    /// </summary>
    [Fact]
    public void DieVerknüpfungenTragenDasBorstWerkSymbol()
    {
        XElement icon = Assert.Single(Package().Elements(Wxs + "Icon"));
        string id = icon.Attribute("Id")?.Value ?? string.Empty;

        Assert.Contains("BorstWerk", icon.Attribute("SourceFile")?.Value ?? string.Empty,
                        StringComparison.Ordinal);

        Assert.Contains(
            Package().Elements(Wxs + "Property"),
            p => p.Attribute("Id")?.Value == "ARPPRODUCTICON"
                 && p.Attribute("Value")?.Value == id);

        XElement[] verknüpfungen = [.. Package().Descendants(Wxs + "Shortcut")];

        Assert.Equal(2, verknüpfungen.Length);
        Assert.All(verknüpfungen, s => Assert.Equal(id, s.Attribute("Icon")?.Value));
    }

    /// <summary>
    /// Die Dateien werden weiterhin eingelesen. Eine von Hand gepflegte Liste
    /// über hunderte Laufzeitdateien vergisst früher oder später eine.
    /// </summary>
    [Fact]
    public void DieDateienWerdenWeiterhinEingelesen()
    {
        XElement files = Assert.Single(
            Document("Files.wxs").Descendants(Wxs + "Files"),
            element => (element.Attribute("Include")?.Value ?? string.Empty)
                .Contains("$(PublishDir)", StringComparison.Ordinal));

        Assert.Contains("$(PublishDir)", files.Attribute("Include")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Möglichkeiten, eine MSI-Prüfung stumm zu schalten.
    ///
    /// Gesucht wird nach dem Abschalten, nicht nach der Erwähnung: In den
    /// Kommentaren steht durchaus „ICE38“, und das soll auch so bleiben – dort
    /// ist erklärt, warum das Paket aussieht, wie es aussieht.
    /// </summary>
    private static readonly string[] Suppressions =
    [
        "SuppressIces", "SuppressValidation", "-sice", "-sval", "SuppressSpecificWarnings",
    ];

    /// <summary>
    /// Keine MSI-Prüfung wird unterdrückt. Eine stummgeschaltete Prüfung
    /// meldet den nächsten Fehler derselben Art auch nicht mehr.
    /// </summary>
    [Fact]
    public void KeineMsiPrüfungWirdUnterdrückt()
    {
        string[] treffer =
        [
            .. from path in ProjectFiles.With(".wxs", ".wixproj", ".ps1", ".yml")
               let text = File.ReadAllText(path)
               from suppression in Suppressions
               where text.Contains(suppression, StringComparison.OrdinalIgnoreCase)
               select $"{ProjectFiles.Relative(path)}: {suppression}",
        ];

        Assert.True(
            treffer.Length == 0,
            $"Diese Stellen schalten eine MSI-Prüfung stumm: {string.Join(", ", treffer)}.");
    }

    private static string Scope()
        => Package().Attribute("Scope")?.Value ?? string.Empty;

    private static IEnumerable<string> StandardDirectories()
        => Package()
            .Elements(Wxs + "StandardDirectory")
            .Select(d => d.Attribute("Id")?.Value ?? string.Empty);

    private static XElement Package()
        => Document("Package.wxs").Root!.Element(Wxs + "Package")!;

    private static XDocument Document(string file)
        => XDocument.Load(
            ProjectFiles.With(".wxs", ".wixproj").Single(p => Path.GetFileName(p) == file));

    private static Dictionary<string, string> ProductCodeMappings()
    {
        const string conditionPrefix = "'$(VersionPrefix)' == '";
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        XElement project = Document("EInvoiceSender.Setup.wixproj").Root!;

        foreach (XElement element in project.Descendants().Where(e => e.Name.LocalName == "ProductCode"))
        {
            string condition = element.Attribute("Condition")?.Value ?? string.Empty;
            string productCode = element.Value.Trim();

            Assert.True(
                condition.StartsWith(conditionPrefix, StringComparison.Ordinal)
                && condition.EndsWith('\''),
                $"ProductCode {productCode} besitzt keine eindeutige VersionPrefix-Zuordnung.");

            string version = condition[conditionPrefix.Length..^1];

            Assert.True(Version.TryParse(version, out _), $"'{version}' ist keine Version.");
            Assert.True(Guid.TryParse(productCode, out _), $"'{productCode}' ist keine GUID.");
            Assert.True(
                mappings.TryAdd(version, productCode),
                $"Version {version} ist mehr als einem ProductCode zugeordnet.");
        }

        Assert.Equal(mappings.Count, mappings.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        return mappings;
    }
}

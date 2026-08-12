# Bauen

## Visual Studio

1. Repository klonen.
2. `EInvoiceSender.sln` öffnen.
3. Visual Studio stellt die Pakete beim Öffnen selbst wieder her.
4. `EInvoiceSender.App` ist das Startprojekt. **F5** startet die Anwendung mit
   Debugger, **Strg+F5** ohne.
5. Der Test-Explorer findet beide Testprojekte ohne weitere Einrichtung.

Benötigt wird die Arbeitslast **.NET-Desktopentwicklung** und das .NET SDK 10.
Die Plattform ist auf **x64** festgelegt; eine andere Auswahl ist nicht nötig.

Es gibt keine absoluten Entwicklerpfade. Wo Tests oder die Anwendung Dateien
im Repository suchen, gehen sie von der Solutiondatei aus und finden sie
selbst.

## PowerShell

Die Skripte in `build/` sind für den Fall gedacht, dass ohne IDE gebaut wird.

| Skript | Zweck |
|---|---|
| `Build.ps1` | Pakete wiederherstellen und Release bauen |
| `Test.ps1` | alle Tests; `-RequireExternalValidators` erzwingt das Prüfgate |
| `Publish.ps1` | eigenständige Fassung für win-x64 |
| `Build-Installer.ps1` | MSI, tragbares ZIP und SHA-256-Prüfsummen |
| `Validate-Reference.ps1` | Gegenprüfung mit CEN-Schematron und veraPDF |

Jedes Skript endet bei einem Fehler mit einem Exitcode ungleich null.

## Ohne Skripte

```powershell
dotnet build EInvoiceSender.sln -c Release
dotnet test  EInvoiceSender.sln -c Release
dotnet format EInvoiceSender.sln --verify-no-changes
```

Diese drei Befehle müssen vor jeder Freigabe fehlerfrei durchlaufen.

## Externe Referenzwerkzeuge

Die Gegenprüfung mit CEN-Schematron und veraPDF läuft über die
Mustangproject-CLI und braucht eine **Java-Laufzeit ab Version 17**. Die
Anwendung selbst braucht kein Java.

Die CLI wird unter `tools/mustang/` erwartet:

```bash
./build/fetch-validators.sh
```

Fehlt sie, werden die betroffenen Tests **übersprungen**, damit ein frisch
geklontes Repository nicht sofort rot ist. In der Pipeline steht
`REQUIRE_EXTERNAL_VALIDATORS=1`; dort **scheitern** sie stattdessen. Ein
Freigabegate darf nicht stillschweigend entfallen.

## Installer

```powershell
.\build\Build-Installer.ps1
```

Das Skript veröffentlicht bei Bedarf vorher, baut das MSI, legt zusätzlich
eine tragbare ZIP-Fassung an und schreibt `SHA256SUMS.txt` nach
`artifacts/release/`. WiX erzeugt MSI-Dateien ausschließlich unter Windows;
unter Linux bricht das Skript mit einer entsprechenden Meldung ab.

Die Versionsnummer steht zentral in `Directory.Build.props` (`VersionPrefix`)
und wird von Anwendung, lokalem Installerbau und CI gemeinsam verwendet. Das
WiX-Projekt gibt sie unmittelbar als MSI-ProductVersion weiter; Buildaufrufe
setzen keine eigene ProductVersion.

Für jede veröffentlichte dreiteilige Version enthält das WiX-Projekt genau
einen festen ProductCode. Der Code von 0.1.0 bleibt erhalten, 0.2.0 besitzt
einen neuen. Eine unbekannte Version ohne Zuordnung bricht vor dem WiX-Bau mit
einer verständlichen Meldung ab.

**Testinstallation derselben Fassung:** Das Paket ersetzt nur *ältere*
Fassungen seiner selbst. Innerhalb einer veröffentlichten Fassung bleibt der
ProductCode fest; ein erneuter Start derselben Fassung öffnet deshalb den
Windows-Installer-Wartungsmodus und erzeugt keine zweite Produktinstanz.
`AllowSameVersionUpgrades` bleibt bewusst ausgeschaltet, damit Builds mit
derselben dreiteiligen MSI-Version einander nicht als Major Upgrade behandeln.

Nach dem Installerbau prüft `build/Test-InstallerMetadata.ps1` Assembly-,
Datei- und Produktversion der veröffentlichten Anwendung sowie Identität,
Upgrade-Tabelle und Aktionsreihenfolge des tatsächlich erzeugten MSI, ohne es
zu installieren.

## Linux und macOS

Der Kern und beide Testprojekte bauen und laufen dort vollständig. Die
WPF-Anwendung **übersetzt** dank `EnableWindowsTargeting`, lässt sich aber
nicht starten. Der Installer lässt sich dort gar nicht bauen.

# Bauen

## Visual Studio

1. Repository klonen.
2. `EInvoiceSender.sln` oeffnen.
3. Visual Studio stellt die Pakete beim Oeffnen selbst wieder her.
4. `EInvoiceSender.App` ist das Startprojekt. **F5** startet die Anwendung mit
   Debugger, **Strg+F5** ohne.
5. Der Test-Explorer findet beide Testprojekte ohne weitere Einrichtung.

Benoetigt wird die Arbeitslast **.NET-Desktopentwicklung** und das .NET SDK 10.
Die Plattform ist auf **x64** festgelegt; eine andere Auswahl ist nicht noetig.

Es gibt keine absoluten Entwicklerpfade. Wo Tests oder die Anwendung Dateien
im Repository suchen, gehen sie von der Solutiondatei aus und finden sie
selbst.

## PowerShell

Die Skripte in `build/` sind fuer den Fall gedacht, dass ohne IDE gebaut wird.

| Skript | Zweck |
|---|---|
| `Build.ps1` | Pakete wiederherstellen und Release bauen |
| `Test.ps1` | alle Tests; `-RequireExternalValidators` erzwingt das Pruefgate |
| `Publish.ps1` | eigenstaendige Fassung fuer win-x64 |
| `Build-Installer.ps1` | MSI, tragbares ZIP und SHA-256-Pruefsummen |
| `Validate-Reference.ps1` | Gegenpruefung mit CEN-Schematron und veraPDF |

Jedes Skript endet bei einem Fehler mit einem Exitcode ungleich null.

## Ohne Skripte

```powershell
dotnet build EInvoiceSender.sln -c Release
dotnet test  EInvoiceSender.sln -c Release
dotnet format EInvoiceSender.sln --verify-no-changes
```

Diese drei Befehle muessen vor jeder Freigabe fehlerfrei durchlaufen.

## Externe Referenzwerkzeuge

Die Gegenpruefung mit CEN-Schematron und veraPDF laeuft ueber die
Mustangproject-CLI und braucht eine **Java-Laufzeit ab Version 17**. Die
Anwendung selbst braucht kein Java.

Die CLI wird unter `tools/mustang/` erwartet:

```bash
./build/fetch-validators.sh
```

Fehlt sie, werden die betroffenen Tests **uebersprungen**, damit ein frisch
geklontes Repository nicht sofort rot ist. In der Pipeline steht
`REQUIRE_EXTERNAL_VALIDATORS=1`; dort **scheitern** sie stattdessen. Ein
Freigabegate darf nicht stillschweigend entfallen.

## Installer

```powershell
.\build\Build-Installer.ps1
```

Das Skript veroeffentlicht bei Bedarf vorher, baut das MSI, legt zusaetzlich
eine tragbare ZIP-Fassung an und schreibt `SHA256SUMS.txt` nach
`artifacts/release/`. WiX erzeugt MSI-Dateien ausschliesslich unter Windows;
unter Linux bricht das Skript mit einer entsprechenden Meldung ab.

Die Versionsnummer steht zentral in `Directory.Build.props` (`VersionPrefix`)
und wird von Anwendung und Installer gemeinsam verwendet.

## Linux und macOS

Der Kern und beide Testprojekte bauen und laufen dort vollstaendig. Die
WPF-Anwendung **uebersetzt** dank `EnableWindowsTargeting`, laesst sich aber
nicht starten. Der Installer laesst sich dort gar nicht bauen.

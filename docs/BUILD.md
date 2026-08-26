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
| `Build-Installer.ps1` | frisch veröffentlichen, MSI bauen und gegen App, Versionen und Lizenzbestand prüfen |
| `Build-Release.ps1` | gemeinsamer lokaler/CI-Weg für MSI, portable ZIP und SHA-256 |
| `Test-ReleasePackaging.ps1` | Positiv- und Negativtests der Paketierungsfunktionen |
| `test-installer-build-guard.sh` | belegt mit echten Buildversuchen, dass sich das WiX-Projekt nicht direkt bauen lässt |
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

## Releasepaket

```powershell
.\build\Test-ReleasePackaging.ps1
.\build\Build-Release.ps1
```

`Build-Release.ps1` ist der gemeinsame maßgebliche Paketierungsweg für lokale
Builds und GitHub Actions. Das Skript erzeugt einen frischen Publish, baut und
prüft das MSI, ergänzt die portable Fassung um die festgelegten
Drittanbieterhinweise und erzeugt ZIP und Prüfsummen zunächst unter
`artifacts/release-staging`.

Erst nach erfolgreicher Inhalts- und SHA-256-Prüfung wird das Staging als
`artifacts/release` veröffentlicht. Kann ein vorhandener Releaseordner nicht
vollständig entfernt werden, bricht der Build vor dem Publish ab. Nach Erfolg
enthält der Ordner ausschließlich:

```text
BorstWerk-E-Rechnung-Setup.msi
BorstWerk-E-Rechnung-portable-win-x64.zip
SHA256SUMS.txt
```

Die portable ZIP enthält die Programmdateien direkt an ihrer Wurzel sowie
`Drittanbieterhinweise.md` und `Drittanbieterhinweise/Lizenzen/...`.
`SHA256SUMS.txt` nennt ausschließlich MSI und ZIP in dieser Reihenfolge und
wird sofort nach ihrer Erstellung und nach der finalen Promotion verifiziert.

WiX erzeugt MSI-Dateien ausschließlich unter Windows; unter Linux bricht der
Releaseweg mit einer entsprechenden Meldung ab.

## Reiner Installerbau

```powershell
.\build\Build-Installer.ps1
```

Dieses Skript veröffentlicht die Anwendung **bei jedem Aufruf** neu, baut das
MSI am festgelegten Pfad und führt `Test-InstallerMetadata.ps1` aus. ZIP,
Releaseordner und Prüfsummen gehören ausschließlich zu `Build-Release.ps1`.

## Das Installerprojekt wird nicht direkt gebaut

Es gibt genau zwei unterstützte Einstiege:

| Weg | Ergebnis |
|---|---|
| `build\Build-Installer.ps1` | frischer Publish, MSI, MSI-Prüfung |
| `build\Build-Release.ps1` | ruft `Build-Installer.ps1` auf und verwendet dessen Publish für ZIP und Prüfsummen |

Ein direkter Bau des WiX-Projekts – aus Visual Studio oder mit
`dotnet build installer/EInvoiceSender.Setup/...` – bricht ab und verweist auf
diese beiden Skripte. Das ist Absicht, kein Versehen.

**Der Grund.** Am 25.08.2026 entstand aus einem direkten Build ein MSI mit der
Produktversion 0.2.0, dessen Programmdateien aus einem älteren Quellstand
stammten: Der alte Publish lag noch in `artifacts/publish/win-x64` und wurde
stillschweigend paketiert. Keine Versionsprüfung konnte das bemerken, weil
auch der alte Bestand bereits `VersionPrefix` 0.2.0 trug – die Angaben
stimmten, der Inhalt nicht. Der offizielle Weg über `Build-Release.ps1` war
nicht betroffen.

Ein Build, der sich bei der Frische seiner Eingaben nicht sicher ist, muss
abbrechen. Stillschweigend Altbestand zu paketieren ist schlimmer, weil das
Ergebnis echt aussieht.

Abgesichert ist das doppelt:

- Das WiX-Projekt verlangt eine interne Eigenschaft, die nur
  `Build-Installer.ps1` setzt, und kennt **keinen** Vorgabewert für
  `PublishDir` mehr. Beide Prüfungen laufen vor jedem anderen Bauschritt.
- `build/test-installer-build-guard.sh` belegt das mit echten Buildversuchen
  statt mit einer Quelltextsuche und läuft in beiden CI-Jobs.

Einen Schalter zum Überspringen des Publish gibt es nicht mehr.

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

Vollständige Tests und externe Referenzvalidatoren bleiben vorgelagerte
Freigabegates. Die CI führt sie vor `Build-Release.ps1` aus; lokal sind sie vor
einem freizugebenden Paket über `Test.ps1 -RequireExternalValidators`
auszuführen.

## Linux und macOS

Der Kern und beide Testprojekte bauen und laufen dort vollständig. Die
WPF-Anwendung **übersetzt** dank `EnableWindowsTargeting`, lässt sich aber
nicht starten. Der Installer lässt sich dort gar nicht bauen.

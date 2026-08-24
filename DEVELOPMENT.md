# BorstWerk E-Rechnung – Entwicklung

Diese Datei richtet sich an Mitwirkende. Was die Anwendung für Anwender tut, steht in
[`README.md`](README.md).

## Verbindlicher Entwicklungsprozess

Dieses Projekt folgt dem zentralen [BorstWerk-Entwicklungsprozess](https://github.com/Borstwerk/.github/blob/main/DEVELOPMENT-PROCESS.md).

Vor größeren Änderungen sind zuerst die für die Arbeit relevanten Unterlagen zu lesen:

- die Anforderungen der Zielversion, aktuell [`docs/REQUIREMENTS-0.2.0.md`](docs/REQUIREMENTS-0.2.0.md),
- [`docs/DECISIONS.md`](docs/DECISIONS.md),
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md),
- [`docs/TESTING.md`](docs/TESTING.md).

Änderungen mit eigener Requirement-ID werden zunächst geplant und erst nach Freigabe des Plans umgesetzt. Bestehende Architekturentscheidungen dürfen dabei nicht stillschweigend ersetzt werden. Nach der Umsetzung ist für jedes Akzeptanzkriterium anzugeben, wodurch es nachgewiesen wird.

## Projektziel und Nicht-Ziele

Ein kleines, nachvollziehbares Windows-Werkzeug: vorhandene PDF-Rechnung hinein, geprüfte
ZUGFeRD-/Factur-X-Rechnung nach EN 16931 heraus.

Ausdrücklich **kein** ERP-, Buchhaltungs- oder CRM-System. Die Architektur soll einfach bleiben;
zusätzliche Projekte, Schnittstellen oder Rahmenwerke gehören nur dann hinein, wenn sie
Wartbarkeit, Testbarkeit oder Plattformtrennung spürbar verbessern.

## Repository-Aufbau

```text
EInvoiceSender.sln

src/
├── EInvoiceSender.App     WPF-Oberfläche, Windows-spezifische Bedienlogik
└── EInvoiceSender.Core    Modelle, Berechnung, Regelprüfung, CII/ZUGFeRD,
                           PDF-Verarbeitung, Berichte, Einstellungen, E-Mail-Entwurf

tests/
├── EInvoiceSender.Core.Tests
└── EInvoiceSender.IntegrationTests

installer/
└── EInvoiceSender.Setup
```

`Core` ist plattformneutral (`net10.0`) und ohne Oberfläche vollständig prüfbar. `App` ist auf
`net10.0-windows` festgelegt; die Prüfprojekte können es deshalb nicht referenzieren. Regeln, die
nur in ViewModels oder XAML stehen können, werden über Prüfungen am Quelltext abgesichert.

## Entwicklungsumgebung

- .NET SDK 10
- Visual Studio 2026 mit der Arbeitslast **.NET-Desktopentwicklung**
- Windows x64 für die vollständige Abnahme (Oberfläche, Publish, Installer)

Bauen und Prüfen des Kerns funktionieren auch unter Linux und macOS. Die normale Anwendung
braucht kein Java.

In Visual Studio: Projektmappe öffnen, `EInvoiceSender.App` als Startprojekt setzen, **F5**. Die
Prüfungen erscheinen im Test-Explorer.

## Bauen und testen

```powershell
.\build\Build.ps1
.\build\Test.ps1
```

Oder unmittelbar mit dem SDK:

```powershell
dotnet build EInvoiceSender.sln -c Release
dotnet test EInvoiceSender.sln -c Release
dotnet format EInvoiceSender.sln --verify-no-changes
```

Die Formatprüfung läuft in der CI mit und bricht den Bau ab. Warnungen gelten als Fehler.

## Publish

```powershell
.\build\Publish.ps1
```

Erzeugt eine eigenständige `win-x64`-Fassung samt .NET-Laufzeit. Auf dem Zielrechner muss nichts
nachinstalliert werden.

## Releasepaket und Installer

```powershell
\.\build\Build-Release.ps1
```

Erzeugt über denselben maßgeblichen Weg wie die Windows-CI das MSI, die
portable ZIP-Fassung und die verifizierte `SHA256SUMS.txt`. Der endgültige
Releaseordner erscheint erst, nachdem alle drei Artefakte vollständig geprüft
wurden.

Für einen reinen Installerbau während der Entwicklung:

```powershell
.\build\Build-Installer.ps1
```

Veröffentlicht die Anwendung bei Bedarf und baut und prüft ausschließlich das MSI.

Das Paket ist ein Dual-Purpose-Paket (`Scope="perUserOrMachine"`): Die Standardinstallation läuft
für den aktuellen Benutzer ohne UAC-Rückfrage, dieselbe Datei lässt sich bei Bedarf für alle
Benutzer installieren. Der `UpgradeCode` bleibt über alle Fassungen unverändert.
`AllowSameVersionUpgrades` ist bewusst nicht gesetzt – eine Testinstallation derselben Fassung ist
über den festen ProductCode dasselbe Produkt und gelangt in den Windows-Installer-Wartungsmodus.

Die Produktversion steht ausschließlich als `VersionPrefix` in `Directory.Build.props`. .NET-
Anwendung, örtlicher Installerbau und CI übernehmen sie von dort; ein eigener ProductVersion-
Override gehört in keinen Buildaufruf. Das WiX-Projekt ordnet jeder veröffentlichten dreiteiligen
Version genau einen festen ProductCode zu. Fehlt diese Zuordnung, bricht der Installerbau vor WiX
ab, statt eine neue Produktidentität zu erfinden.

WiX wird nur zum **Bauen** gebraucht, nicht auf dem Zielrechner. Die ICE-Prüfungen laufen ohne
Unterdrückung; die Vorgeschichte zu ICE38, ICE57 und ICE61 steht in den Kommentaren von
`installer/EInvoiceSender.Setup/Package.wxs`. `wix.exe` bricht auf Nicht-Windows-Systemen ab, der
Installer lässt sich also nur unter Windows bauen.

Der gemeinsame Releaseweg übernimmt `Drittanbieterhinweise.md` und den vollständigen
Lizenzordner aus `installer/Drittanbieterhinweise`, prüft den ZIP-Inhalt gegen den vorbereiteten
Publish und erzeugt Prüfsummen ausschließlich für MSI und ZIP in fester Reihenfolge. Ein alter
oder unvollständiger Bestand in `artifacts/release` wird nicht weiterverwendet.

## Externe Referenzprüfung

Die Anwendung hat eigene Prüfungen. Für Entwicklung, Regressionsprüfung und Freigabe kommen
unabhängige Werkzeuge hinzu:

- CEN-Schematron für EN 16931
- Mustangproject
- veraPDF für PDF/A

Dafür wird Java 17 oder neuer gebraucht – **ausschließlich in Entwicklung und Releaseprüfung**.
Das Installationspaket bringt kein Java mit, und die Anwendung sucht auch keines: In der
Zusammenstellung der Dienste ist kein externer Validator eingetragen.

Diese Werkzeuge sind das Freigabegate. Aus bestandenen eigenen Prüfungen allein folgt keine
Aussage über Normkonformität. Einzelheiten in [`docs/TESTING.md`](docs/TESTING.md).

## PDF-Verarbeitung

Die Eingangsprüfung hält drei Fragen auseinander, und das ist der Kern des Entwurfs:

| Frage | Wo sie beantwortet wird |
|---|---|
| Ist die direkte Aufwertung möglich? | `PdfAnalysisResult.CanBeUpgraded` |
| Ist die sichtbare Kopie technisch möglich? | `PdfPreflightReport.Route` |
| Hat der Anwender dem Qualitätsverlust zugestimmt? | `CreateEInvoiceRequest.RasterFallbackConfirmed` |

Daraus ergeben sich drei Wege (`PdfProcessingRoute`):

- **`Direct`** – die Seiten des Originals werden übernommen und um die fehlenden
  PDF/A-3-Bestandteile ergänzt.
- **`RasterFallback`** – nur wenn die fehlende Schrifteinbettung das **einzige** Hindernis ist,
  keine fremden Anhänge an der Datei hängen und sich jede Seite nachweislich darstellen lässt.
  Wird nie ohne ausdrückliche Zustimmung beschritten; die Sperre sitzt im Kern, nicht in der
  Oberfläche.
- **`Rejected`** – alles andere.

Beide Wege enden in denselben PDF/A-Bestandteilen und durchlaufen dieselbe Ergebnisprüfung. Es
gibt bewusst keine Regel der Art „darstellbar, also erlaubt“; jedes Hindernis ist einzeln
beurteilt.

Ausführlich, samt Begründungen und der Erkennung von Rechteschutz und tatsächlich verwendeten
Schriften: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Teststrategie

- **Unit-Tests** für Berechnung, Regeln, XML, Datenerkennung und Textwerkzeuge
- **Integrationstests** für den vollständigen Ablauf von der PDF bis zur gespeicherten Datei
- **Golden Master** für XML und fertige Dateien; sie werden mit den Referenzwerkzeugen
  gegengeprüft (`build/validate-golden-masters.sh`)
- **Externe Referenzvalidatoren** als Freigabegate; mit `REQUIRE_EXTERNAL_VALIDATORS=1` scheitern
  die betroffenen Prüfungen, statt sich still zu überspringen – so läuft die CI
- **Quelltextprüfungen** für alles, was nur in ViewModels oder XAML stehen kann
- **Windows-Testkoffer und Release-Abnahme**: wiederkehrende manuelle Prüfungen stehen in
  [`docs/RELEASE-CHECKLIST.md`](docs/RELEASE-CHECKLIST.md)

Ein neuer Wächter zählt erst, wenn er gegen absichtlich kaputte Eingabe rot wird. Ein Test, der
immer grün ist, sichert nichts.

## BorstWerk-Gestaltung

Farben, Schriftgrößen, Abstände und Bedienelemente stehen unter
`src/EInvoiceSender.App/UI/Themes/`. In den Ansichten steht kein Farbwert; wer eine Farbe ändern
will, ändert sie dort.

Das BorstWerk-Zeichen und die Windows-Symboldatei entstehen aus einer gemeinsamen Geometrie:

```powershell
dotnet run --project build/icon
```

Das schreibt `src/EInvoiceSender.App/Assets/BorstWerkEInvoice.ico` und eine Vorschau unter
`docs/images/`. Die Pfaddaten stehen in `build/icon/BorstWerkMark.cs` und noch einmal in
`UI/Themes/BorstWerkLogo.xaml`; eine Prüfung vergleicht beide, damit Fenster und Taskleiste nicht
verschiedene Zeichen zeigen. Das Werkzeug gehört nicht zur Projektmappe und läuft im Bau nicht
mit – sein Ergebnis ist eingecheckt.

Die Geometrie ist am verbindlichen Markenblatt vermessen und **nicht** entworfen. Sie wird nicht
nach eigenem Ermessen verändert.

## Entwicklungsgrundsätze

- KISS und DRY
- kleine, klar benannte Verantwortlichkeiten
- Root-Cause-Fixes statt Symptombehandlung
- zu jedem gefundenen Fehler eine Regressionsprüfung
- echte deutsche Umlaute in deutschsprachigen Texten; eine Prüfung wacht darüber
- keine unnötigen neuen Abhängigkeiten

Fachlich kritische Änderungen an CII/XML, PDF/A, Einbettung, Summen oder Steuerlogik sind
zusätzlich gegen die Golden Master und die externen Referenzvalidatoren zu prüfen.

## Weitere Unterlagen

| Datei | Inhalt |
|---|---|
| [`docs/REQUIREMENTS-0.2.0.md`](docs/REQUIREMENTS-0.2.0.md) | Verbindliche Anforderungen für Version 0.2.0 |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Neue wichtige Architektur- und Produktentscheidungen samt Begründung |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Aufbau der Projektmappe, Ablauf, PDF-Wege |
| [`docs/BUILD.md`](docs/BUILD.md) | Bauen mit Visual Studio und PowerShell |
| [`docs/E-INVOICE-STANDARD.md`](docs/E-INVOICE-STANDARD.md) | Norm, Profil und verwendete Fassungen |
| [`docs/TESTING.md`](docs/TESTING.md) | Testebenen und Referenzvalidatoren |
| [`docs/RELEASE-CHECKLIST.md`](docs/RELEASE-CHECKLIST.md) | Wiederkehrende manuelle Windows- und Release-Abnahme |
| [`docs/ACCEPTANCE-0.2.0-WINDOWS.md`](docs/ACCEPTANCE-0.2.0-WINDOWS.md) | Protokoll der manuellen Windows- und Release-Abnahme für Version 0.2.0 |
| [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md) | Bekannte Grenzen |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | Echte noch offene Arbeit außerhalb verbindlicher Versionsanforderungen |
| [`docs/SPIKE-RASTER-FALLBACK.md`](docs/SPIKE-RASTER-FALLBACK.md) | Messungen zum Rasterweg |
| [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md) | Fremdkomponenten und Lizenzen |

`docs/legacy/` enthält Unterlagen aus der Entstehungszeit. Sie bleiben als Historie erhalten, sind
für die aktuelle Entwicklung aber nicht mehr maßgeblich.

## Lizenz

Der eigene Quelltext von BorstWerk E-Rechnung steht unter der **MIT-Lizenz**; der Wortlaut liegt
in [`LICENSE`](LICENSE).

Davon getrennt zu betrachten sind die **Fremdkomponenten**. Ihre Lizenzen sind in
[`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md) einzeln aufgeführt und an der
Primärquelle geprüft; einige stehen unter anderen Bedingungen als MIT, etwa PdfPig unter
Apache-2.0 und das WiX Toolset unter der Microsoft Reciprocal License. Die vollständigen,
geprüften Lizenz- und Notice-Texte der ausgelieferten Bestandteile liegen unter
`installer/Drittanbieterhinweise/Lizenzen`. Die RTF-Datei des Installers ist lediglich eine
kurze, sichtbare Zusammenfassung und verweist auf diese installierten Texte.

Die MIT-Lizenz in `LICENSE` gilt also für diesen Quelltext, nicht für alles, was mit ihm
ausgeliefert wird.

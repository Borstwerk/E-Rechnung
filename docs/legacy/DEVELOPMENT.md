# DEVELOPMENT.md – Anleitung für Entwickler

Stand: 2026-08-05. Diese Anleitung ergänzt `AGENTS.md` (verbindliche
Arbeitsregeln) um die praktische Seite: Voraussetzungen, Befehle,
Projektstruktur.

---

## Voraussetzungen

| Werkzeug | Version | Zweck |
|---|---|---|
| .NET SDK | **10.0** (`global.json` pinnt `10.0.100`, `rollForward: latestFeature`) | Build, Test, Publish |
| Java-Laufzeit | optional, 21 empfohlen (Mindestversion laut Projektkonfiguration 11) | Gegenprüfung mit Mustang-CLI (CEN-Schematron, veraPDF) |

Ohne Java lassen sich Build und die meisten Tests trotzdem ausführen; die
Ende-zu-Ende-Konformitätstests werden dann lokal übersprungen (siehe
`docs/TESTING.md`, Abschnitt „REQUIRE_EXTERNAL_VALIDATORS").

---

## Befehle

```bash
# Wiederherstellen und bauen (gesamte Solution, inkl. WPF – auch unter Linux)
dotnet build EInvoiceSender.slnx -c Release

# Alle Tests
dotnet test EInvoiceSender.slnx -c Release

# Einzelnes Testprojekt
dotnet test tests/EInvoiceSender.Domain.Tests -c Release

# Formatprüfung (schlägt fehl, wenn Formatierung abweicht)
dotnet format EInvoiceSender.slnx --verify-no-changes

# Externe Validatoren beschaffen (einmalig, optional)
./build/fetch-validators.sh

# Gegenprüfung der Golden Master mit CEN-Schematron und veraPDF
./build/validate-golden-masters.sh

# Windows-Veröffentlichung (nur auf einem Windows-Agenten vollständig sinnvoll)
dotnet publish src/EInvoiceSender.Desktop -c Release -r win-x64 --self-contained true
```

Details zu Testebenen und Umgebungsvariablen: `docs/TESTING.md`.

---

## Projektstruktur

### Quellprojekte (`src/`)

| Projekt | Ziel-Framework | Verantwortung |
|---|---|---|
| `EInvoiceSender.Domain` | net10.0, keine Fremdpakete | Rechnungsmodell, Berechnungskern, Werttypen (IBAN, Land, Währung); enthält keine I/O. |
| `EInvoiceSender.Application` | net10.0 | Anwendungsfall (`CreateEInvoiceUseCase`), Ports (Interfaces), Berichtsmodelle, sichere XML-Basis (`SecureXml`). |
| `EInvoiceSender.Formats` | net10.0 | Erzeugt und liest die CII-XML nach EN 16931 (eigener Writer, kein ZUGFeRD-Fremdpaket). |
| `EInvoiceSender.Validation` | net10.0 | EN-16931-Geschäftsregeln, Codelisten, deutsche Fehlertexte, Adapter für externe Validatoren. |
| `EInvoiceSender.Infrastructure` | net10.0 | PDF-Analyse und -Vorprüfung, PDF/A-3-Erzeugung, ICC-Profil, Dateiablage, Einstellungen, Prozessausführung. |
| `EInvoiceSender.Mail` | net10.0 | `.eml`-Entwurf, `mailto:`-Fallback; versendet nichts. |
| `EInvoiceSender.Presentation` | net10.0 (plattformneutral) | Eingabemodell (`InvoiceDraft`) und `ShellViewModel` mit dem Fünf-Schritte-Ablauf – getrennt von der WPF-Ansicht, damit die Ablauflogik auch unter Linux testbar ist. |
| `EInvoiceSender.Desktop` | net10.0-**windows** | WPF-Oberfläche (MVVM), DI-Verdrahtung, Windows-Dienste (Shell, Dialoge, Vorschau). |

### Testprojekte (`tests/`)

| Projekt | Verantwortung |
|---|---|
| `EInvoiceSender.Domain.Tests` | Unit-Tests für Werttypen, Berechnungskern, `SafeFileName`. |
| `EInvoiceSender.Formats.Tests` | Golden-Master-Tests der XML-Erzeugung. |
| `EInvoiceSender.Validation.Tests` | Tests der EN-16931-Geschäftsregeln und Codelisten. |
| `EInvoiceSender.Mail.Tests` | Tests des `.eml`-Entwurfs (Header, Kodierung, Anhang). |
| `EInvoiceSender.Presentation.Tests` | Tests der ViewModel- und Ablauflogik der Oberfläche. |
| `EInvoiceSender.IntegrationTests` | Gesamtablauf, PDF/A, Sicherheitstests (XXE, Billion Laughs), Ende-zu-Ende-Gegenprüfung mit den echten Referenzwerkzeugen. |
| `EInvoiceSender.TestSupport` | Gemeinsame Testszenarien (`InvoiceScenarios`) und eine PDF-Fabrik für Testdateien; kein eigenständiges Testprojekt, sondern Hilfsbibliothek für die übrigen. |
| `EInvoiceSender.Application.Tests` | Projektgerüst für zukünftige Tests der Application-Schicht. |

Weitere Verzeichnisse: `installer/` (Windows-Installer, WiX), `build/`
(Hilfsskripte), `docs/` (diese Dokumentation).

---

## WPF unter Linux: kompiliert, aber nicht ausführbar

Dank `EnableWindowsTargeting=true` lässt sich `EInvoiceSender.Desktop`
(net10.0-windows) auch unter Linux **kompilieren** – `dotnet build
EInvoiceSender.slnx -c Release` schließt die WPF-Anwendung ein und läuft grün.
Die WPF-Oberfläche lässt sich unter Linux jedoch **nicht ausführen**. Für
Laufzeitprüfungen der UI (Fenster, Dialoge, PDF-Vorschau) ist ein
Windows-Agent notwendig; genau dafür existiert `EInvoiceSender.Presentation`
als plattformneutrale Schicht mit eigenen Tests.

Der Windows-Installer (`installer/EInvoiceSender.Setup`, WiX 5.0.2) baut nur
unter Windows – WiX erzeugt MSI-Dateien ausschließlich dort. Deshalb ist das
`.wixproj` bewusst **nicht** Teil von `EInvoiceSender.slnx`: Ein Linux-Build
der Solution soll dadurch nicht brechen. Gebaut wird der Installer
ausschließlich im Windows-Job der CI (`.github/workflows/ci.yml`,
`publish-windows`).

---

## Golden Master neu erzeugen

Nur nach einer bewussten, beabsichtigten Änderung am XML-Writer:

```bash
UPDATE_GOLDEN_MASTERS=1 dotnet test tests/EInvoiceSender.Formats.Tests
```

Das schreibt die neuen Sollfassungen nach
`tests/EInvoiceSender.Formats.Tests/GoldenMasters`.

**Danach ist die Schematron-Gegenprüfung zwingend erneut auszuführen:**

```bash
./build/fetch-validators.sh          # falls noch nicht geschehen
dotnet test EInvoiceSender.slnx -c Release
./build/validate-golden-masters.sh
```

Der Grund: Ein Golden Master, der nur vom eigenen Programm akzeptiert wird,
belegt gar nichts – die eigene Regelprüfung ist ausdrücklich kein Ersatz für
das CEN-Schematron und veraPDF (`docs/DECISIONS.md`, ADR-0002 und ADR-0009).
Erst wenn `validate-golden-masters.sh` alle als gültig erwarteten Dateien
tatsächlich bestätigt und alle als ungültig erwarteten Dateien tatsächlich
beanstandet, gilt die Änderung als abgesichert. Details zum Freigabegate:
`docs/TESTING.md`.

---

## Eine neue Abhängigkeit aufnehmen

Verbindliche Regeln stehen in `AGENTS.md`, Abschnitt 6. Kurz zusammengefasst:

1. Version exakt in `Directory.Packages.props` pinnen (Central Package
   Management).
2. Bewertung in `docs/DEPENDENCIES.md` ergänzen: Name, Version, Lizenz,
   Projektstatus, letzte Veröffentlichung, unterstützte Formate/Profile,
   bekannte Einschränkungen, Wartungsrisiko, Auswahlgrund.
3. Lizenz muss permissiv sein (MIT, Apache-2.0, BSD, MS-PL) oder das Werkzeug
   wird als getrennter Prozess eingebunden (wie Mustang/veraPDF).
4. Zustimmung des Hauptagenten einholen.

Ausdrücklich ausgeschlossen ohne Gegenentscheidung: AGPL-Bibliotheken im
Anwendungsprozess, kommerziell lizenzierte Bibliotheken, alles mit
Umsatzschwelle, sowie jede Bibliothek oder API, die Rechnungsdaten an einen
externen Dienst überträgt. Subagenten dürfen Abhängigkeiten vorschlagen, nie
eigenmächtig aufnehmen.

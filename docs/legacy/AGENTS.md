# AGENTS.md – Arbeitsregeln für dieses Repository

Dieses Dokument ist verbindlich für alle Agenten und Entwickler, die in diesem
Repository arbeiten. Es beschreibt, **wie** hier gearbeitet wird. **Was** gebaut
wird, steht in `docs/SPECIFICATION.md`.

---

## 1. Was dieses Projekt ist – und was nicht

`EInvoiceSender` ist ein **Konverter und Versandhelfer**, kein Rechnungsprogramm.

Der Benutzer besitzt bereits eine fertige PDF-Rechnung aus einem anderen
Programm. Diese Anwendung ergänzt strukturierte Rechnungsdaten, erzeugt daraus
eine ZUGFeRD-/Factur-X-Datei (Profil EN 16931, PDF/A-3), validiert sie und
bereitet einen E-Mail-Entwurf vor.

**Nicht Bestandteil** (Scope-Sperre – Erweiterungen nur nach ausdrücklicher
Freigabe des Hauptagenten): Rechnungserstellung, PDF-Layout, CRM, Artikel-,
Lager-, Finanzbuchhaltung, DATEV, Mahnwesen, Zahlungsüberwachung,
Rechnungsnummernverwaltung, Steuerberatung, Benutzer-/Rollenverwaltung,
Mandantenfähigkeit, Cloud, Web, Mehrbenutzerbetrieb, OCR als verbindliche
Datenquelle, vollautomatischer Versand, Peppol, Behördenportale, Langzeitarchiv.

Wenn eine Aufgabe in Richtung dieser Liste zeigt: **nicht implementieren, sondern
melden.**

---

## 2. Build- und Testbefehle

Voraussetzung: .NET SDK 10.0 (`global.json` pinnt `10.0.100`, `latestFeature`).

```bash
# Wiederherstellen und bauen (gesamte Solution, inkl. WPF – auch unter Linux)
dotnet build EInvoiceSender.slnx -c Release

# Alle Tests
dotnet test EInvoiceSender.slnx -c Release

# Einzelnes Testprojekt
dotnet test tests/EInvoiceSender.Domain.Tests -c Release

# Formatprüfung (schlägt fehl, wenn Formatierung abweicht)
dotnet format EInvoiceSender.slnx --verify-no-changes

# Externe Validatoren beschaffen (einmalig, optional – siehe docs/TESTING.md)
./build/fetch-validators.sh

# Windows-Veröffentlichung (nur auf Windows-Agent vollständig sinnvoll)
dotnet publish src/EInvoiceSender.Desktop -c Release -r win-x64 --self-contained true
```

**Wichtig:** Die WPF-Oberfläche lässt sich dank `EnableWindowsTargeting=true`
auch auf Linux **kompilieren**, aber nicht **ausführen**. Laufzeitprüfungen der
UI gehören auf einen Windows-Agenten.

---

## 3. Architekturregeln

Schichten (Details: `docs/ARCHITECTURE.md`):

```
Desktop (WPF)  ->  Application  ->  Domain
                        ^
     Infrastructure / Formats / Validation / Mail  (implementieren Ports)
```

Verbindlich:

- **A1** – `Domain` hat **keine** Paketabhängigkeiten. Kein PDF, kein XML, kein
  Dateisystem, kein Logging-Framework, keine DI.
- **A2** – `Application` definiert die Ports (Interfaces). Adapter liegen in
  `Infrastructure`, `Formats`, `Validation`, `Mail`.
- **A3** – Keine zyklischen Projektabhängigkeiten.
- **A4** – Keine Geschäftslogik in Code-behind oder ViewModels. ViewModels
  koordinieren, sie rechnen nicht.
- **A5** – Keine `Process.Start`-Aufrufe aus ViewModels. Externe Prozesse laufen
  ausschließlich hinter einem Port in `Infrastructure`/`Validation`.
- **A6** – Kein statischer, veränderlicher globaler Zustand. Alles über DI.
- **A7** – Geldbeträge **ausschließlich** `decimal`. `double`/`float` sind für
  monetäre Werte verboten.
- **A8** – Fachliche Datumswerte als `DateOnly`.
- **A9** – Alle öffentlichen Ports sind `async` und nehmen ein
  `CancellationToken` entgegen, wenn sie I/O oder Prozesse berühren.

---

## 4. Codekonventionen

- Nullable Reference Types aktiv, `TreatWarningsAsErrors=true`.
- File-scoped Namespaces, `using` außerhalb des Namespace.
- Domain-Typen bevorzugt unveränderlich (`record`, `init`).
- Fachbegriffe deutsch in UI-Texten, **Code und Bezeichner englisch**.
- Fehlermeldungen für Anwender liegen zentral in
  `EInvoiceSender.Validation/Messages/` und sind nach Regel-ID auffindbar –
  nie direkt im Code formulieren.
- Öffentliche Domain-Regeln tragen die EN-16931-Regel-ID im Namen bzw. in der
  `RuleId`-Eigenschaft (z. B. `BR-CO-13`), damit Bericht und Code zusammenpassen.
- Kommentare erklären **warum**, nicht **was**. Sprache: Deutsch.

---

## 5. Sicherheitsregeln

Vollständig in `docs/SECURITY.md`. Kurzfassung, immer einzuhalten:

- **S1** – Eingehende PDF- und XML-Dateien sind grundsätzlich nicht
  vertrauenswürdig.
- **S2** – XML nur über `SecureXml` aus `EInvoiceSender.Formats` lesen:
  `DtdProcessing = Prohibit`, `XmlResolver = null`, Grenzen für Größe/Tiefe.
- **S3** – Ausgabepfade immer über `SafePath`/`PathGuard` normalisieren.
  Kein Verlassen des Zielverzeichnisses, keine Reparse-Points blind folgen.
- **S4** – Die Original-PDF wird **niemals** geändert und **niemals**
  stillschweigend überschrieben. Schreiben nur atomar (`.tmp` + `File.Move`).
- **S5** – Externe Prozesse nur mit Argumentliste (`ArgumentList`), niemals mit
  zusammengesetzter Kommandozeile. Immer mit Zeitlimit, Exitcode-Prüfung und
  erfasstem stdout/stderr.
- **S6** – Logs maskieren E-Mail-Adressen, IBAN, Steuernummern und
  Dateiinhalte. Nutze `LogMasking`.
- **S7** – Keine Zugangsdaten im Repository, auch nicht in Testdaten. Lokale
  sensible Einstellungen werden per DPAPI geschützt.
- **S8** – Temporäre Dateien nur unterhalb eines eigenen Arbeitsverzeichnisses,
  Löschung im `finally`.

---

## 6. Regeln für neue Abhängigkeiten

Eine neue Abhängigkeit darf **nur** aufgenommen werden, wenn:

1. sie in `Directory.Packages.props` zentral gepinnt wird (exakte Version),
2. sie in `docs/DEPENDENCIES.md` bewertet ist (Name, Version, Lizenz,
   Projektstatus, letzte Veröffentlichung, unterstützte Formate/Profile,
   bekannte Einschränkungen, Wartungsrisiko, Auswahlgrund),
3. die Lizenz permissiv ist (MIT, Apache-2.0, BSD, MS-PL) oder als
   getrennter Prozess eingebunden wird,
4. der **Hauptagent** zugestimmt hat.

**Ausgeschlossen** ohne ausdrückliche Gegenentscheidung: AGPL-Bibliotheken
(iText, Ghostscript) im Anwendungsprozess, kommerziell lizenzierte
Bibliotheken, alles mit Umsatzschwelle, sowie jede Bibliothek oder API, die
Rechnungsdaten an einen externen Dienst überträgt.

Subagenten dürfen Abhängigkeiten **vorschlagen**, nie eigenmächtig aufnehmen.

---

## 7. Delegationsregeln

Der Hauptagent orchestriert. Er delegiert bevorzugt:

- unabhängige Recherche und Repository-Analyse,
- Testfallentwicklung und Fixtures,
- Dokumentationsarbeit,
- eng abgegrenzte Implementierungsaufgaben (ein Service, ein Adapter, ein
  ViewModel),
- statische Reviews.

Regeln:

- Nie zwei schreibende Subagenten auf denselben Dateien.
- Jeder Auftrag nennt: Ziel, zu lesende Dateien, erlaubte Änderungsbereiche,
  erwartetes Ergebnis, auszuführende Tests, Definition-of-Done, Rückgabeformat.
- Subagenten delegieren nicht weiter.
- Rückgabeformat: siehe Abschnitt 9.

**Nicht delegierbar** (Entscheidung bleibt beim Hauptagenten): endgültige
Architektur, Auswahl zentraler Abhängigkeiten, Interpretation der
Rechnungsstandards, Definition des unterstützten Profils, Freigabe der Steuer-
und Summenlogik, Sicherheitsfreigabe, Installer-/Release-Freigabe,
Scope-Änderungen, Meilenstein-Abschluss.

Der Hauptagent prüft **jede** delegierte Änderung selbst: Diff lesen,
Architekturgrenzen prüfen, Sicherheitswirkung prüfen, Build ausführen, Tests
ausführen, neue Abhängigkeiten prüfen, Dokumentationsbedarf prüfen.
**Ein Bericht ersetzt keinen ausgeführten Test.**

---

## 8. Definition of Done (pro Arbeitspaket)

Ein Arbeitspaket ist fertig, wenn **alle** Punkte zutreffen:

1. `dotnet build EInvoiceSender.slnx -c Release` läuft ohne Warnung und Fehler.
2. `dotnet test EInvoiceSender.slnx -c Release` ist grün.
3. `dotnet format --verify-no-changes` meldet keine Abweichung.
4. Neue Logik ist durch Tests abgedeckt, inklusive mindestens einem Fehlerfall.
5. Keine neue, nicht bewertete Abhängigkeit.
6. Keine Klartextgeheimnisse, keine echten Personendaten in Testdaten.
7. Betroffene Dokumentation ist aktualisiert (mindestens `docs/STATUS.md`).
8. Anwendersichtbare Fehlermeldungen sind auf Deutsch und verständlich.

„Kompiliert" ist **nicht** „fertig".

---

## 9. Rückgabeformat für Subagenten

```text
Ergebnis:
- kurze Zusammenfassung

Geänderte Dateien:
- Pfad

Ausgeführte Prüfungen:
- Befehl: Ergebnis

Offene Risiken:
- Risiko oder "keine erkannt"

Empfehlung:
- Übernehmen | Überarbeiten | Verwerfen
```

Keine Rohprotokolle, keine langen Konsolenausgaben.

---

## 10. Verzeichnisse

| Pfad | Inhalt |
|---|---|
| `src/EInvoiceSender.Domain` | Fachmodell, Berechnung, Regeln – ohne Fremdabhängigkeiten |
| `src/EInvoiceSender.Application` | Anwendungsfälle und Ports |
| `src/EInvoiceSender.Formats` | CII-XML erzeugen/lesen (EN 16931) |
| `src/EInvoiceSender.Validation` | Geschäftsregeln, Codelisten, Validator-Adapter |
| `src/EInvoiceSender.Infrastructure` | Dateisystem, PDF, PDF/A-3, Einstellungen, Prozesse |
| `src/EInvoiceSender.Mail` | E-Mail-Entwürfe (kein Versand) |
| `src/EInvoiceSender.Desktop` | WPF-Oberfläche (MVVM) |
| `tests/…` | Unit-, Integrations- und Golden-Master-Tests |
| `installer/` | Windows-Installer |
| `build/` | Hilfsskripte für Build und Validatoren |
| `docs/` | Spezifikation, Architektur, Standards, Abhängigkeiten, Status |

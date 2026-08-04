# STATUS.md

Letzte Aktualisierung: 2026-08-04 (zweiter Stand)

## Aktueller Meilenstein

**M3 – Datenerfassung (WPF)** (als naechstes)

**Hinweis zur Reihenfolge:** M4 (XML) und M5 (PDF/A) wurden bewusst vor M2/M3
umgesetzt. Beide tragen das gesamte technische Risiko des Projekts; ohne den
Nachweis, dass eine normgerechte Datei ueberhaupt erzeugbar ist, waere jede
Oberflaeche verfrueht gewesen. Der Nachweis liegt jetzt vor.

## Erledigt

### M0 – Recherche und Architektur ✅

- Entwicklungsumgebung geprüft: Linux-Container, .NET SDK **10.0.110** aus dem
  Ubuntu-Archiv installiert, Java 21 vorhanden. Die offiziellen
  .NET-Downloadhosts sind über den Egress-Proxy gesperrt; NuGet, Maven Central,
  GitHub und das Ubuntu-Archiv sind erreichbar.
- Nachgewiesen: Die WPF-Anwendung lässt sich mit `EnableWindowsTargeting=true`
  auch unter Linux **übersetzen** (Gesamtbuild grün), aber nicht ausführen.
- Drei parallele Recherchen ausgewertet und die tragenden Aussagen selbst
  nachgeprüft (Profil-URN gegen zwei Referenzimplementierungen, Mustang-Version
  gegen Maven Central, Paketversionen gegen die NuGet-API).
- Standards festgelegt und dokumentiert: `docs/STANDARDS.md`.
- Abhängigkeits- und Lizenzmatrix erstellt: `docs/DEPENDENCIES.md`.
- Acht Architekturentscheidungen festgehalten: `docs/DECISIONS.md`.
- Solution-Grundgerüst mit 7 Quell- und 6 Testprojekten angelegt, Build grün.

### M1 – Solution und Domain ✅

- Werttypen mit Selbstprüfung (IBAN nach ISO 7064, Währung, Land, Einheit).
- Rechnungsmodell und Berechnungskern nach EN 16931.
- `SafeFileName` gegen Path Traversal und reservierte Windows-Namen.
- Ports der Application-Schicht für alle Adapter.
- CI-Pipeline (Linux-Build/Test, Windows-Veröffentlichung).
- 187 Unit-Tests.

### M4 – XML-Erzeugung ✅

- Eigener CII-Writer für Profil EN 16931, eigener Reader für die Gegenprüfung.
- `SecureXml` als einziger Lesepfad (XXE und Entity-Expansion abgewehrt, mit
  Sicherheitstests belegt).
- Acht Golden-Master-Fälle, drei absichtlich fehlerhafte Fälle.
- **Alle acht werden vom offiziellen CEN-Schematron (Mustang 2.24.0) als
  gültig bestätigt, alle drei fehlerhaften werden beanstandet.**

### M5 – PDF/A-3 und Einbettung ✅

- `SRgbIccProfile`: ICC-v2-Profil programmatisch erzeugt.
- `XmpMetadataBuilder`: PDF/A-Kennzeichnung und Factur-X-Erweiterungsschema.
- `PdfAnalyzer`: Signaturprüfung, Schrifteinbettung, Verschlüsselung, aktive
  Inhalte, Signaturen, bereits eingebettete Rechnungs-XML.
- `PdfAInvoiceComposer`: OutputIntent, Anhang, `/AF`, Dokumentinfo.
- `PdfMetadataOverwriter`: setzt das XMP als inkrementelle Aktualisierung ein,
  weil PDFsharp beim Speichern immer sein eigenes XMP schreibt.
- **Die erzeugte Datei besteht die veraPDF-Prüfung mit Flavour 3b:
  389 Prüfpunkte, `isCompliant=true`.** Die eingebettete XML lässt sich wieder
  extrahieren und ist byte-identisch mit der erzeugten.

## In Arbeit

Nichts offen. Der nächste Schritt beginnt bei null.

## Nächster Schritt

M2/M3: PDF-Vorschau und WPF-Oberfläche, danach M6 (Gesamtablauf und Berichte),
M7 (E-Mail-Entwurf), M8 (Installer).

### Noch nicht umgesetzt

| Baustein | Zustand |
|---|---|
| `En16931RuleValidator` (Geschäftsregeln mit deutschen Meldungen) | Codelisten liegen vor, die Regelprüfung selbst fehlt noch |
| WPF-Oberfläche (M2/M3) | nur Projektgerüst, keine Views |
| `CreateEInvoiceUseCase` und Validierungsbericht (M6) | Ports definiert, Umsetzung offen |
| `EmlDraftService` (M7) | Port definiert, Umsetzung offen |
| `FileStorage`, `SettingsStore`, `ProcessRunner` | Ports definiert, Umsetzung offen |
| Installer (M8) | offen, Werkzeugentscheidung steht noch aus |

## Bekannte Probleme und Einschränkungen

| Thema | Stand |
|---|---|
| **PDF/A-3-Konvertierung** | Beliebige PDFs können nicht nach PDF/A-3 konvertiert werden (keine permissiv lizenzierte .NET-Bibliothek kann das). Die Anwendung wertet geeignete PDFs auf und bricht sonst ab – ADR-0003. |
| **PDF/A-Validierung ohne Java** | Ohne externen Validator prüft die Anwendung PDF/A nur strukturell (Teilmenge). Der Bericht weist das aus – ADR-0004. |
| **„Neues Outlook"** | Verhalten beim Öffnen von `.eml` mit Anhang ist aus dieser Umgebung nicht prüfbar. Muss auf echtem Windows 11 verifiziert werden – ADR-0005. |
| **UI-Laufzeitprüfung** | Die WPF-Oberfläche kann hier nur kompiliert, nicht ausgeführt werden. Smoke-Tests der UI gehören auf einen Windows-Agenten. |
| **Installer-Build** | WiX und Inno Setup erzeugen MSI/EXE nur unter Windows. Der Installer wird deshalb ausschließlich im Windows-CI-Job gebaut. |
| **Offizielle Spezifikationsseiten** | `ferd-net.de`, `fnfe-mpe.org`, `pdflib.com` antworten aus dieser Umgebung mit HTTP 403. Betroffene Angaben sind in `docs/STANDARDS.md` als **[S]** markiert. |

## Blockierende Entscheidungen

Keine. Alle offenen Punkte haben eine dokumentierte konservative Vorgabe.

## Zuletzt erfolgreich ausgeführte Prüfungen

| Befehl | Ergebnis | Zeitpunkt |
|---|---|---|
| `dotnet build EInvoiceSender.slnx -c Release` | 0 Fehler, 0 Warnungen | 2026-08-04 |
| `dotnet test EInvoiceSender.slnx -c Release` | 295 Tests, alle grün | 2026-08-04 |
| `./build/validate-golden-masters.sh` | 12 Dateien geprüft, 0 Abweichungen | 2026-08-04 |
| veraPDF 3b auf der Ergebnisdatei | 389 Prüfpunkte, `isCompliant=true` | 2026-08-04 |
| CEN-Schematron EN 16931 auf allen Golden Mastern | `status="valid"` | 2026-08-04 |

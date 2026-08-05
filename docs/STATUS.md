# STATUS.md

Letzte Aktualisierung: 2026-08-05 (vierter Stand)

## Aktueller Meilenstein

**M9 – Endabnahme** (blockiert: erfordert ein echtes Windows-System)

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

### Kernstabilisierung ✅

- Dauerhafte Ende-zu-Ende-Konformitätstests je Golden Master gegen die echten
  Referenzwerkzeuge (Schematron, veraPDF, Factur-X-Prüfung, Rückextraktion,
  Byte-Gleichheit, Anhangname, MIME-Typ, AFRelationship, Profil, XMP).
- `MustangValidator` bewertet **jede** Teilzusammenfassung einzeln; ein
  unlesbarer oder leerer Bericht gilt als Fehler.
- `ProcessRunner`: Argumentliste, kein Shell-Aufruf, Zeitlimit, Prozessbaum
  beenden, stdout und stderr nebenläufig.
- ICC-Profil vollständig dokumentiert und per SHA-256 gepinnt.

### M6 – Gesamtablauf und Berichte ✅

- `CreateEInvoiceUseCase` mit neun Schritten, Fortschrittsmeldungen,
  `CancellationToken`, strukturierten Fehlerobjekten.
- Bestätigungssperre sitzt im Anwendungsfall, nicht in der Oberfläche.
- Ergebnisprüfung: erneut öffnen, XML extrahieren, Byte-Gleichheit,
  PDF/A-Kennzeichnung, fachliche Gegenprüfung der extrahierten Daten.
- `FileStorage`: atomar über temporäre Datei, kein stillschweigendes
  Überschreiben, Schutz gegen Path Traversal.
- Validierungsbericht als JSON und als Text mit Prüfsumme, Zeitpunkt,
  Standard, Profil und Validator-Versionen. Ein nicht ausgeführter Validator
  steht ausdrücklich als „NICHT AUSGEFUEHRT" im Bericht.
- Zwölf Ablauftests: Erfolg, fehlende Bestätigung, Validierungsfehler,
  beschädigte PDF, nicht eingebettete Schrift, Timeout, Beanstandung,
  Benutzerabbruch, Überschreibschutz, Aufräumen der temporären Dateien.

### M7 – E-Mail-Entwurf ✅

- `EmlDraftService`: RFC-5322-konforme `.eml` mit Anhang, `X-Unsent: 1`,
  ohne `Message-ID`, Textkörper als reiner Text. Alle drei Festlegungen sind
  begründet und durch Tests abgesichert.
- Immer ein Rückfallweg: `mailto:` ohne Anhang plus Hinweis auf den
  Ausgabeordner. Ein fehlender Mailclient blockiert nichts.
- **Nicht behauptet** wird die Verträglichkeit mit dem „neuen Outlook" – siehe
  offene Punkte.

### M2/M3 – Oberfläche ✅ (Windows-Laufzeitprüfung offen)

- Neues Projekt `EInvoiceSender.Presentation` (plattformneutral, `net10.0`):
  `InvoiceDraft` als Eingabemodell und `ShellViewModel` mit dem
  Fünf-Schritte-Ablauf. Damit ist die Ablauflogik der Oberfläche auch auf
  einem Linux-Agenten testbar – 19 Tests laufen dort tatsächlich.
- WPF-Ansicht mit Drag-and-drop, Dateiauswahl, PDF-Vorschau, Positionsraster,
  Live-Summen, Kontrollansicht mit Pflichtbestätigung, Fortschrittsliste,
  Ergebnis mit Prüfsumme und E-Mail-Feldern.
- Barrierefreiheit: `AutomationProperties` an allen Eingabefeldern,
  Statuszeile als `LiveSetting`, Befunde tragen Wort **und** Zeichen, nicht
  nur Farbe.
- `JsonSettingsStore`: Vorlage als JSON, die IBAN unter Windows per DPAPI
  geschützt. Ohne DPAPI wird sie **nicht** gespeichert statt still im
  Klartext abgelegt.
- Die gesamte Anwendung einschließlich WPF **kompiliert** unter Linux.

### M8 – Installer ✅ definiert, ⚠️ ungeprüft

- WiX 5.0.2 (MS-RL, an der Primärquelle geprüft – ADR-0011), MSI, Installation
  **pro Benutzer** ohne Administratorrechte.
- Startmenüeintrag, optionale Desktopverknüpfung, `MajorUpgrade` mit stabilem
  `UpgradeCode`, Downgrade-Schutz mit verständlicher Meldung.
- Benutzerdaten bleiben bei der Deinstallation erhalten.
- Dateiliste wird aus dem Veröffentlichungsverzeichnis eingelesen, damit keine
  Laufzeitdatei vergessen werden kann.
- Drittanbieterhinweise werden mitgeliefert.
- **Ungeprüft:** Der Installer wurde in dieser Umgebung nie gebaut und nie
  ausgeführt. WiX erzeugt MSI-Dateien nur unter Windows. Der Windows-CI-Job
  baut ihn; Installation, Upgrade und Deinstallation muss ein Mensch auf einem
  echten Windows-System durchführen.

## In Arbeit

Nichts offen. Der nächste Schritt beginnt bei null.

## Nächster Schritt

M2/M3: PDF-Vorschau und WPF-Oberfläche, danach M6 (Gesamtablauf und Berichte),
M7 (E-Mail-Entwurf), M8 (Installer).

### Noch nicht umgesetzt

| Baustein | Zustand |
|---|---|
| Windows-Laufzeitprüfung der Oberfläche (M9) | offen, erfordert echtes Windows |
| Installer tatsächlich bauen und ausführen (M8/M9) | offen, erfordert echtes Windows |
| Verhalten des „neuen Outlook" mit `.eml` (M7) | offen, erfordert echtes Windows 11 |
| Vorlagenpflege in der Oberfläche | Speichern der Vorlage ist noch nicht mit einer Schaltfläche verbunden |
| Nachlässe/Zuschläge auf Dokumentebene in der Oberfläche | Modell vorhanden, keine Eingabemaske |

## Bekannte Probleme und Einschränkungen

| Thema | Stand |
|---|---|
| **PDF/A-3-Konvertierung** | Beliebige PDFs können nicht nach PDF/A-3 konvertiert werden (keine permissiv lizenzierte .NET-Bibliothek kann das). Die Anwendung wertet geeignete PDFs auf und bricht sonst ab – ADR-0003. |
| **PDF/A-Validierung ohne Java** | Ohne externen Validator prüft die Anwendung PDF/A nur strukturell (Teilmenge). Der Bericht weist das aus – ADR-0004. |
| **„Neues Outlook"** | Verhalten beim Öffnen von `.eml` mit Anhang ist aus dieser Umgebung nicht prüfbar. Muss auf echtem Windows 11 verifiziert werden – ADR-0005. |
| **UI-Laufzeitprüfung** | Die WPF-Oberfläche kann hier nur **kompiliert**, nicht ausgeführt werden. Die Ablauflogik ist über das plattformneutrale Projekt `EInvoiceSender.Presentation` mit 19 Tests abgedeckt; das Zusammenspiel mit echten Fenstern, Dialogen und der PDF-Vorschau ist **ungeprüft**. Der Windows-CI-Job baut und testet, ersetzt aber keinen manuellen Durchlauf. |
| **Installer-Build** | WiX und Inno Setup erzeugen MSI/EXE nur unter Windows. Der Installer wird deshalb ausschließlich im Windows-CI-Job gebaut. |
| **Offizielle Spezifikationsseiten** | `ferd-net.de`, `fnfe-mpe.org`, `pdflib.com` antworten aus dieser Umgebung mit HTTP 403. Betroffene Angaben sind in `docs/STANDARDS.md` als **[S]** markiert. |

## Blockierende Entscheidungen

Keine. Alle offenen Punkte haben eine dokumentierte konservative Vorgabe.

## Zuletzt erfolgreich ausgeführte Prüfungen

| Befehl | Ergebnis | Zeitpunkt |
|---|---|---|
| `dotnet build EInvoiceSender.slnx -c Release` | 0 Fehler, 0 Warnungen | 2026-08-04 |
| `dotnet test EInvoiceSender.slnx -c Release` | 426 Tests, alle grün | 2026-08-05 |
| `./build/validate-golden-masters.sh` | 12 Dateien geprüft, 0 Abweichungen | 2026-08-04 |
| veraPDF 3b auf der Ergebnisdatei | 389 Prüfpunkte, `isCompliant=true` | 2026-08-04 |
| CEN-Schematron EN 16931 auf allen Golden Mastern | `status="valid"` | 2026-08-04 |

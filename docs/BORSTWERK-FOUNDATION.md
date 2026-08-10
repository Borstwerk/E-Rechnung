# BorstWerk – Grundlagen und Projektverfassung 1.0

**Status:** Arbeitsgrundlage für die künftige Werkzeugfamilie  
**Referenzprojekt:** EInvoiceSender  
**Stand:** 10.08.2026  
**Öffentliche Kurzbeschreibung:** Kostenlose Werkzeuge für kleine Unternehmen.

> BorstWerk ist ein privates, nicht-kommerzielles Open-Source-Projekt. Die Projektidentität steht im Vordergrund, nicht die Person dahinter.

---

## 1. Leitidee

BorstWerk entwickelt kleine, kostenlose Softwarewerkzeuge für Kleinstunternehmen, kleine Unternehmen, Selbstständige und Vereine.

Im Mittelpunkt stehen Aufgaben, die notwendig oder praktisch wichtig sind, für die kleine Betriebe heute jedoch häufig zwischen Papier, Excel, Word und unverhältnismäßig umfangreicher oder teurer Spezialsoftware wählen müssen.

Die zentrale Produktfrage lautet nicht:

> Was könnte die Software noch können?

Sondern:

> Was muss der Benutzer danach nicht mehr mühsam selbst erledigen?

### Grundsätze

- kostenlos nutzbar
- kein Abo
- kein künstliches Funktionslimit
- Local First
- keine Cloudpflicht
- keine Benutzerkonten, wenn der fachliche Zweck sie nicht zwingend benötigt
- kein Tracking
- keine Werbung
- keine Telemetrie
- keine unnötige Plattformkomplexität
- nachvollziehbare Releases
- professionelle Qualitätsprüfung trotz privatem Projektcharakter
- KI-Unterstützung ist erlaubt und wird transparent behandelt

---

## 2. Name und Projektidentität

### Dachname

```text
BorstWerk
```

Die Schreibweise wird in sichtbaren Produktnamen konsistent verwendet.

### Namensherkunft

`Borst` hat eine persönliche Herkunft aus einem privaten Spitznamen. Diese Herkunft gibt dem Projekt eine eigene Identität, soll aber **kein öffentliches Personenbranding** erzeugen.

`Werk` steht für:

- Werkzeug
- praktische Arbeit
- etwas Geschaffenes
- eine Sammlung eigenständiger Werke

Die persönliche Geschichte hinter dem Namen muss öffentlich nicht erklärt werden. BorstWerk soll auch ohne Kenntnis dieser Herkunft als sachlicher Projektname funktionieren.

### Öffentliche Beschreibung

Bevorzugt:

> **Kostenlose Werkzeuge für kleine Unternehmen.**

Diese Beschreibung ist bewusst neutral. Sie enthält keine wirtschaftspolitische Wertung und keine Behauptung darüber, welche gesellschaftliche Rolle eine Zielgruppe einnimmt.

### Keine Selbstdarstellungsmarke

BorstWerk ist ausdrücklich nicht als persönliche Bühne gedacht.

Es gibt keinen Bedarf für:

- Founder-Kommunikation
- persönliche Entwicklerporträts
- Projektmarketing um die Person des Maintainers
- künstliche Unternehmenssprache
- Social-Media-Präsenz als Voraussetzung für die Nutzung

Wenn ein Name des Maintainers technisch oder lizenzrechtlich genannt werden muss, geschieht dies sachlich und zurückhaltend.

---

## 3. Produktfamilie

Jedes Werkzeug ist eigenständig installierbar und nutzbar.

Beispielhafte Produktnamen:

- BorstWerk E-Rechnung
- BorstWerk GoBD-Doku
- BorstWerk IT-Notfall
- BorstWerk Arbeitsschutz
- BorstWerk HACCP
- BorstWerk Datenschutz

Ein Benutzer soll niemals weitere Anwendungen installieren müssen, nur weil sie zur selben Familie gehören.

Gemeinsame Bibliotheken sind erlaubt, wenn sie nachweisbar Wartungsnutzen bringen. Eine künstliche Plattform oder ein zentrales Hauptprogramm wird nicht gebaut.

---

## 4. Rolle von EInvoiceSender

EInvoiceSender ist das erste reale Produkt und damit Referenz für technische Entscheidungen, die sich bereits bewährt haben.

Bestehende Stärken werden bewusst übernommen:

- .NET 10
- WPF für Windows-Desktopwerkzeuge
- zentrale Paketverwaltung
- TreatWarningsAsErrors
- aktivierte .NET-Analyzer
- reproduzierbare Builds
- automatisierte Formatprüfung
- Unit- und Integrationstests
- Linux-CI für schnelle plattformneutrale Prüfung
- Windows-CI für WPF, Publish und Installer
- self-contained win-x64-Ausgabe
- MSI plus portable ZIP-Fassung
- SHA-256-Prüfsummen für Releases
- externe Referenzvalidatoren als Release-Gate, wenn das Fachgebiet solche Werkzeuge anbietet
- klare Trennung zwischen Endbenutzeranforderungen und Entwicklerwerkzeugen
- kleine, nachvollziehbare Architektur statt vorschnellem Framework-Aufbau

### Wichtig für die laufende EInvoice-Entwicklung

Die Einführung von BorstWerk darf den laufenden Produktumbau nicht blockieren.

Deshalb gilt:

1. Fachliche Stabilität vor Branding.
2. Kein großflächiger UI-Umbau parallel zu laufenden Funktionsänderungen.
3. Gemeinsame Komponenten werden erst extrahiert, wenn mindestens zwei Anwendungen dieselbe Abstraktion tatsächlich benötigen.
4. EInvoiceSender wird nicht künstlich in zusätzliche Projekte zerlegt, nur um einer theoretischen Zielarchitektur zu entsprechen.

---

## 5. Produktprinzipien

### 5.1 Klein bleiben

Jedes Werkzeug löst ein klar begrenztes Problem.

BorstWerk baut keine abgespeckten Enterprise-Produkte und kein Mini-ERP.

### 5.2 Problem vor Technik

Die Technologie folgt dem Anwendungsfall.

- typischer Windows-Desktopprozess → WPF
- echter mobiler Prozess → separat prüfen, beispielsweise PWA
- keine Cross-Platform-Komplexität ohne konkreten Nutzen

### 5.3 Verständlichkeit vor Funktionsmenge

Ein Nutzer soll eine Anwendung öffnen und ohne Schulung verstehen, was als Nächstes zu tun ist.

### 5.4 Local First

Geschäftsdaten bleiben standardmäßig beim Benutzer.

### 5.5 Kein Lock-in

Daten sollen exportierbar und sicherbar sein. Eigene Dateiformate dürfen keine Sackgasse werden.

---

## 6. Visuelle Identität

Die Oberfläche soll wie ein gutes Werkzeug wirken:

- ruhig
- verständlich
- freundlich
- professionell
- sachlich
- nicht steril
- nicht verspielt

Kein Startup-Look, keine überladenen Dashboards und keine dekorativen Animationen ohne Bediennutzen.

Die vollständigen Regeln stehen in `BORSTWERK-VISUAL-IDENTITY.md`.

---

## 7. Standard-AppShell

Nicht jede Anwendung benötigt dieselben Menüpunkte, aber dieselbe Bedienlogik.

```text
┌──────────────────────────────────────────────────────┐
│ BorstWerk · Produktname                         ?  ⚙ │
├───────────────┬──────────────────────────────────────┤
│               │                                      │
│ Navigation    │ Seitentitel                           │
│               │ kurze Erklärung                      │
│ Übersicht     │                                      │
│ Hauptprozess  │ Arbeitsbereich                        │
│ Dokumente     │                                      │
│               │                                      │
│───────────────│                                      │
│ Einstellungen │                                      │
│ Über          │                                      │
└───────────────┴──────────────────────────────────────┘
```

### Regeln

- Navigation zeigt Aufgaben, keine technischen Komponenten.
- Ein linearer Workflow darf ein Wizard bleiben, wenn das fachlich besser ist.
- Einstellungen sind vom täglichen Arbeitsablauf getrennt.
- „Über“ enthält Projektcharakter, Version, Lizenz und KI-Hinweis.
- Hilfe ist möglichst kontextbezogen.

EInvoiceSender darf seinen bestehenden Fünf-Schritte-Wizard behalten. BorstWerk erzwingt keine Sidebar nur für ein einheitliches Foto.

---

## 8. Technische Standardarchitektur

### Desktop

Standard:

- .NET 10 LTS
- WPF
- MVVM
- CommunityToolkit.Mvvm, wenn erforderlich
- Microsoft.Extensions.DependencyInjection nur dort, wo es Abhängigkeiten tatsächlich vereinfacht
- Microsoft.Extensions.Logging mit lokaler Ausgabe

### Architekturprinzip

So wenige Schichten wie möglich, so viele wie nötig.

Eine typische Anwendung darf zunächst aus nur zwei Projekten bestehen:

```text
src/
├── Product.App
└── Product.Core

tests/
├── Product.Core.Tests
└── Product.IntegrationTests
```

Weitere Projekte entstehen erst bei einer realen technischen Grenze.

Kein Cargo-Cult-Clean-Architecture-Aufbau mit zehn Projekten für ein Werkzeug mit fünf Formularseiten.

---

## 9. Lokale Daten

Wenn strukturierte persistente Daten erforderlich sind, ist SQLite der bevorzugte Startpunkt.

Anforderungen:

- versionierte Migrationen
- Integritätsprüfung
- Backup
- Wiederherstellung
- dokumentierter Speicherort
- kein proprietärer Lock-in

### Backupformat

Für Anwendungen mit eigener Datenbank wird ein einheitlicher Container angestrebt:

```text
Produktname-Backup-YYYY-MM-DD.bwbackup
```

Möglicher Inhalt:

```text
manifest.json
database.sqlite
documents/
attachments/
```

Das Format wird erst verbindlich, wenn das erste Produkt mit echter Anwendungsdatenbank umgesetzt wird.

---

## 10. Datenschutz und Netzwerkzugriff

Standardzustand:

- keine Telemetrie
- kein Tracking
- keine Crash-Uploads
- keine Cloud-KI für Geschäftsdaten
- keine zentralen Benutzerkonten

Netzwerkzugriff muss fachlich begründet sein.

Wenn ein Produkt Netzwerkzugriff erhält, wird verständlich dokumentiert:

- welcher Dienst angesprochen wird
- welche Daten übertragen werden
- warum dies erforderlich ist
- ob die Funktion optional ist

---

## 11. Fehlerberichte

Standardmäßig gibt es keine automatische Fehlerübertragung.

Eine Anwendung darf optional einen lokalen Fehlerbericht erzeugen.

Beispielinhalt:

- Produktname und Version
- Betriebssystemversion
- technische Ausnahme
- relevantes lokales Anwendungslog
- keine Dokumentinhalte
- keine Adressen
- keine Bankdaten
- keine fachlichen Nutzdaten

Der Benutzer entscheidet selbst, ob er den Bericht weitergibt.

---

## 12. Qualität und CI

Das EInvoice-Repository setzt bereits einen sinnvollen Referenzstandard.

### Basis-Gate für jedes Desktopprodukt

1. Restore
2. Formatprüfung
3. Release-Build
4. Unit Tests
5. Integrationstests
6. Windows-Testlauf für WPF-relevante Bereiche
7. self-contained Publish
8. Installer-Build
9. portable ZIP
10. SHA-256-Prüfsummen

### Zusätzlich anzustreben

- Dependency-Sicherheitsprüfung
- Lizenzinventar
- SBOM für Releaseartefakte
- Architecture Tests nur für tatsächlich relevante Architekturregeln

### Fachspezifische Gates

Wo externe Referenzwerkzeuge existieren, werden sie bevorzugt eingesetzt.

EInvoice-Beispiel:

- CEN-Schematron
- Mustang
- veraPDF
- Golden Master

Derartige Werkzeuge gehören primär in Entwicklung und Releaseprüfung und werden nicht automatisch zur Endbenutzerabhängigkeit.

---

## 13. Abhängigkeiten

Neue Abhängigkeiten werden nur aufgenommen, wenn sie einen echten Vorteil gegenüber BCL/.NET oder bereits vorhandenen Komponenten bringen.

Für jede neue Bibliothek werden dokumentiert:

- Zweck
- Lizenz
- Version
- besondere Einschränkungen

Zentrale Paketverwaltung ist Standard.

---

## 14. Lizenz und Projektcharakter

Vorgesehene Standardlizenz für eigenen Code: MIT.

Das Projekt ist ein privates Freizeitprojekt.

Es besteht kein Anspruch auf:

- Support
- individuelle Anpassungen
- bestimmte Reaktionszeiten
- dauerhafte Pflege jeder Funktion

Veröffentlichte Versionen werden trotzdem mit professionellem Qualitätsanspruch geprüft.

---

## 15. KI-Unterstützung

KI darf umfassend genutzt werden für:

- Analyse
- Planung
- Implementierung
- Tests
- Refactoring
- Dokumentation

KI-generierter Code wird wie jeder andere Code geprüft.

Neutraler Hinweis:

> Mit KI-Unterstützung entwickelt. Konzeption, Anforderungen, Prüfung und Projektpflege erfolgen durch den Projektmaintainer.

KI ist Werkzeug, kein Produktversprechen.

---

## 16. EInvoice-Migrationsplan

Die Integration in BorstWerk erfolgt erst nach fachlicher Stabilisierung.

### Stufe A – ohne sichtbaren Produktumbau

- Projektgrundsätze dokumentieren
- Lizenz festlegen
- Datenschutz-/Projektcharakter dokumentieren
- Dependency- und Lizenzinventar vervollständigen
- Qualitätsgate gegen BorstWerk-Standard vergleichen

### Stufe B – UI-Tokens statt UI-Neubau

Die heute direkt in `App.xaml` definierten Werte werden schrittweise als benannte Ressourcen zentralisiert:

- Farben
- Abstände
- Typografie
- Statusdarstellung
- Buttonvarianten
- Eingabefelder

Das Verhalten der Oberfläche bleibt unverändert.

### Stufe C – dezentes Branding

- Produktkennzeichnung `BorstWerk E-Rechnung`
- gemeinsames App-Icon
- Über-Dialog
- Projekt- und Lizenzhinweise
- README-Ausrichtung

### Stufe D – gemeinsame Komponenten erst bei Bedarf

Erst wenn das zweite Desktopwerkzeug existiert, wird geprüft, welche Bestandteile wirklich gemeinsam sind.

Mögliche Kandidaten:

- Farbtokens
- Typografie
- Statuskarten
- About-Dialog
- lokale Loganzeige
- Datei-/Ordnerdialog-Helfer
- Backup-Hülle

Nicht vorsorglich extrahieren:

- E-Rechnungs-Wizard
- PDF-spezifische UI
- Invoice-Validierung
- fachliche Dialoge

---

## 17. Release-Definition

Ein BorstWerk-Release ist erst veröffentlichungswürdig, wenn:

- CI grün ist
- bekannte fachliche Grenzen dokumentiert sind
- Installer bzw. portable Ausgabe geprüft wurden
- der Benutzer seine Daten ohne Cloud nutzen kann, sofern das Produkt nichts anderes zwingend benötigt
- keine nicht dokumentierte Telemetrie vorhanden ist
- Drittanbieterabhängigkeiten dokumentiert sind
- zentrale Benutzerabläufe manuell geprüft wurden

**Kostenlos bedeutet nicht unfertig.**

---

## 18. Namens- und Veröffentlichungscheck

BorstWerk ist derzeit der bevorzugte Dachname.

Vor dem ersten öffentlichen Release unter dieser Bezeichnung wird separat geprüft:

- relevante Markenregister
- gleichartige Softwareprojekte
- offensichtliche Namenskonflikte im deutschsprachigen Markt

Die technische Planung ist nicht von einer Markenanmeldung abhängig.

---

## 19. Nächste Schritte

1. BorstWerk-Logo und App-Icon visuell festlegen.
2. konkrete Design-Tokens als XAML-Ressourcen prototypisieren.
3. EInvoice nach Abschluss des laufenden Funktionsumbaus gegen dieses Dokument prüfen.
4. danach BorstWerk GoBD-Doku fachlich spezifizieren.
5. beim zweiten Desktopprojekt entscheiden, welche UI- und Infrastrukturteile wirklich als gemeinsame Bibliothek geeignet sind.

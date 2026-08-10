# KlarKram – Grundlagen und Design-System 1.0

**Status:** Arbeitsgrundlage für die künftige Werkzeugfamilie  
**Referenzprojekt:** EInvoiceSender  
**Ziel:** Einheitliche Produktidentität, Bedienlogik und technische Leitplanken für kleine kostenlose Werkzeuge.

> Hinweis: „KlarKram“ ist derzeit ein Arbeitstitel. Vor einer öffentlichen Veröffentlichung wird der Name noch einmal separat auf Kollisionen geprüft. Die technische und gestalterische Struktur dieses Dokuments bleibt davon unabhängig.

---

## 1. Leitidee

KlarKram entwickelt kleine, kostenlose Werkzeuge für Kleinstunternehmen, kleine Unternehmen, Selbstständige und Vereine.

Die Programme lösen klar begrenzte Aufgaben, für die kleine Betriebe heute häufig zwischen Papier, Excel, Word und unverhältnismäßig teurer Spezialsoftware wählen müssen.

Die zentrale Frage lautet nicht:

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
- keine künstliche Plattformkomplexität
- nachvollziehbare Releases
- professionelle Qualitätsprüfung trotz privatem Projektcharakter
- KI-Unterstützung ist erlaubt und wird transparent behandelt

---

## 2. Produktfamilie

Jedes Werkzeug ist eigenständig installierbar und nutzbar.

Beispielhafte Produktnamen:

- KlarKram E-Rechnung
- KlarKram GoBD-Doku
- KlarKram IT-Notfall
- KlarKram Arbeitsschutz
- KlarKram HACCP
- KlarKram Datenschutz

Ein Benutzer soll niemals weitere Anwendungen installieren müssen, nur weil sie zur selben Familie gehören.

Gemeinsame Bibliotheken sind erlaubt, wenn sie echten Wartungsnutzen bringen. Eine künstliche Plattform oder ein zentrales Hauptprogramm wird nicht gebaut.

---

## 3. Rolle von EInvoiceSender

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

Die Einführung von KlarKram darf den laufenden Produktumbau nicht blockieren.

Deshalb gilt:

1. Fachliche Stabilität vor Branding.
2. Kein großflächiger UI-Umbau parallel zu laufenden Funktionsänderungen.
3. Gemeinsame Komponenten werden erst extrahiert, wenn mindestens zwei Anwendungen dieselbe Abstraktion tatsächlich benötigen.
4. EInvoiceSender wird nicht künstlich in zusätzliche Projekte zerlegt, nur um einer theoretischen Zielarchitektur zu entsprechen.

---

## 4. Visuelle Identität

### 4.1 Charakter

Die Oberfläche soll wirken wie ein gutes Werkzeug:

- ruhig
- klar
- freundlich
- professionell
- sachlich
- nicht steril
- nicht verspielt

Kein Start-up-Look, keine überladenen Dashboards und keine dekorativen Animationen ohne Bediennutzen.

### 4.2 Typografie

Standard für Windows-Desktopanwendungen:

- Segoe UI Variable, wenn verfügbar
- Fallback: Segoe UI
- Systemschrift statt mitgelieferter Fontdateien

Richtwerte:

- Fenstertitel / Seitentitel: 24–28 px, SemiBold
- Abschnittsüberschrift: 17–20 px, SemiBold
- Standardtext: 14–15 px
- Hilfetext / Metadaten: 12–13 px

### 4.3 Grundfarben

Die Dachmarke bleibt neutral.

Empfohlene Basiswerte:

- Hintergrund hell: `#F7F8FA`
- Fläche: `#FFFFFF`
- Fläche sekundär: `#F1F3F5`
- Primärtext: `#1F2933`
- Sekundärtext: `#5B6573`
- Rahmen: `#D6DBE1`
- Fokus: `#245EA8`

Jedes Produkt erhält genau eine charakteristische Akzentfarbe. Diese wird sparsam eingesetzt für:

- aktive Navigation
- primäre Aktion
- ausgewählte Elemente
- kleine Produktkennzeichnung
- Dokumentkopf / App-Icon

Die Akzentfarbe darf niemals allein Information transportieren.

### 4.4 Statusfarben

Status wird immer mit Text oder Symbol plus Farbe dargestellt.

- Erfolg: ruhiges Grün
- Warnung: Ocker / Bernstein
- Fehler: Rot
- Information: Blau

Beispiele:

- `✓ Gültig`
- `⚠ Prüfen`
- `✕ Fehler`

EInvoiceSender macht dies bei Befunden bereits richtig: Schweregrad wird nicht nur über Farbe dargestellt.

### 4.5 Abstände

Basiseinheit: 4 px.

Bevorzugte Abstände:

- 4 px: eng zusammengehörige Elemente
- 8 px: Feldinterne Abstände
- 12 px: kleine Gruppen
- 16 px: Standardabstand
- 24 px: Abschnittsabstand
- 32 px: größere Inhaltsblöcke

---

## 5. Standard-AppShell

Nicht jede Anwendung muss dieselben Menüpunkte besitzen, aber dieselbe Bedienlogik.

```text
┌──────────────────────────────────────────────────────┐
│ KlarKram · Produktname                          ?  ⚙ │
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
- Der primäre Arbeitsablauf darf als Wizard umgesetzt werden, wenn eine feste Reihenfolge fachlich sinnvoll ist.
- Einstellungen sind vom täglichen Arbeitsablauf getrennt.
- „Über“ enthält Projektcharakter, Version, Lizenz und KI-Hinweis.
- Hilfe soll kontextbezogen sein und nicht auf ein riesiges Handbuch verweisen.

EInvoiceSender darf seinen bestehenden Fünf-Schritte-Wizard behalten. KlarKram verlangt keine Sidebar, wenn der lineare Ablauf besser ist.

---

## 6. UI-Komponenten

### Primäre Aktion

Pro Ansicht möglichst nur eine visuell dominante Hauptaktion.

Beispiele:

- `Weiter`
- `E-Rechnung erzeugen`
- `Dokumentation exportieren`

### Sekundäre Aktionen

Neutraler dargestellt.

Beispiele:

- Zurück
- Abbrechen
- Vorschau
- Exportordner öffnen

### Eingabefelder

- Beschriftung über dem Feld
- Pflichtangaben nicht nur durch Sternchen erklären
- Validierungsfehler direkt am Feld
- keine Platzhalter als Ersatz für Beschriftungen
- nachvollziehbare Herkunftshinweise sind erlaubt

### Meldungen

Technische Fehlercodes dürfen vorhanden sein, aber der Benutzer erhält zuerst eine verständliche Aussage.

Gut:

> Die PDF ist kennwortgeschützt und kann deshalb nicht verarbeitet werden.

Darunter optional:

> Technische Kennung: PDF-INPUT-004

Nicht gut:

> IOException: object reference …

### Bestätigungsdialoge

Nur einsetzen, wenn eine echte Folge besteht.

Keine Dialoge für harmlose Navigation.

---

## 7. Barrierefreiheit

Barrierefreiheit wird von Beginn an berücksichtigt.

- Tastaturbedienung
- sichtbarer Fokus
- ausreichend große Bedienflächen
- sinnvoller Tab-Index
- AutomationProperties für zentrale Bedienelemente
- Status nicht ausschließlich über Farbe
- lesbare Kontraste
- keine Information nur über Tooltip

Die bereits vorhandene Befunddarstellung in EInvoiceSender ist hierfür ausdrücklich Referenz.

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

Weitere Projekte entstehen erst bei realer technischer Grenze.

Kein Cargo-Cult-Clean-Architecture-Aufbau mit zehn Projekten für ein Werkzeug mit fünf Formularseiten.

### Mobile Prozesse

Wenn ein fachlicher Prozess wirklich am Smartphone oder Tablet stattfindet, wird eine mobile bzw. PWA-Lösung separat bewertet.

Die Desktoptechnologie wird nicht aus Prinzip auf mobile Abläufe gezwungen.

---

## 9. Lokale Daten

Standardmäßig bleiben Geschäftsdaten auf dem Rechner des Benutzers.

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
Produktname-Backup-YYYY-MM-DD.kkbackup
```

Inhalt beispielsweise:

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

Wenn ein Produkt Netzwerkzugriff erhält, wird in der Anwendung verständlich dokumentiert:

- welcher Dienst angesprochen wird
- welche Daten übertragen werden
- warum dies erforderlich ist
- ob die Funktion optional ist

---

## 11. Fehlerberichte

Standardmäßig keine automatische Fehlerübertragung.

Stattdessen soll eine Anwendung optional einen lokalen Fehlerbericht erzeugen können.

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

Trotzdem werden veröffentlichte Versionen mit professionellem Qualitätsanspruch geprüft.

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

Empfohlener neutraler Hinweis:

> Mit KI-Unterstützung entwickelt. Konzeption, Anforderungen, Prüfung und Projektpflege erfolgen durch den Projektmaintainer.

KI ist Werkzeug, kein Produktversprechen.

---

## 16. EInvoice-Migrationsplan

Die Integration in die Familie erfolgt erst nach fachlicher Stabilisierung.

### Stufe A – ohne sichtbaren Produktumbau

- Projektgrundsätze dokumentieren
- Lizenz festlegen
- Datenschutz-/Projektcharakter dokumentieren
- Dependency- und Lizenzinventar vervollständigen
- Qualitätsgate gegen KlarKram-Standard vergleichen

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

- Produktkennzeichnung „KlarKram E-Rechnung“
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

Ein KlarKram-Release ist erst veröffentlichungswürdig, wenn:

- CI grün ist
- bekannte fachliche Grenzen dokumentiert sind
- Installer bzw. portable Ausgabe geprüft wurden
- der Benutzer seine Daten ohne Cloud nutzen kann, sofern das Produkt nichts anderes zwingend benötigt
- keine nicht dokumentierte Telemetrie vorhanden ist
- Drittanbieterabhängigkeiten dokumentiert sind
- zentrale Benutzerabläufe manuell geprüft wurden

Kostenlos bedeutet nicht unfertig.

---

## 18. Nächste Schritte

1. Arbeitstitel final prüfen und ggf. ersetzen.
2. Logo und App-Icon entwickeln.
3. konkrete Design-Tokens als XAML-Ressourcen prototypisieren.
4. EInvoice nach Abschluss des laufenden Funktionsumbaus gegen dieses Dokument prüfen.
5. erst danach KlarKram GoBD-Doku fachlich spezifizieren.
6. beim zweiten Desktopprojekt entscheiden, welche UI-/Infrastrukturteile wirklich als gemeinsame Bibliothek geeignet sind.

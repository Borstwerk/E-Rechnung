# BorstWerk E-Rechnung – Architektur- und Produktentscheidungen

Diese Datei hält neue, nicht offensichtliche oder langfristig wichtige Entscheidungen fest, deren Begründung auch in späteren Entwicklungszyklen erhalten bleiben soll.

Sie ersetzt weder [`ARCHITECTURE.md`](ARCHITECTURE.md) noch die Versionsanforderungen. Bereits vorhandene Begründungen werden nicht rückwirkend aus anderen Dokumenten hierher kopiert. Die bestehende Architektur- und Testdokumentation bleibt für bereits getroffene Entscheidungen maßgeblich.

Ein neuer Eintrag ist vor allem dann sinnvoll, wenn ein späterer Entwickler berechtigterweise fragen könnte:

> Warum wurde das nicht einfacher beziehungsweise anders gelöst?

Nicht jede Implementierungsentscheidung benötigt einen eigenen Eintrag.

## Verwendung

Neue Einträge erhalten eine fortlaufende ID:

```text
DEC-001
DEC-002
...
```

Wenn eine Entscheidung zu einer konkreten Versionsanforderung gehört, wird deren Requirement-ID zusätzlich genannt.

## Vorlage

```markdown
## DEC-001 – Titel

**Status:** gültig
**Bezug:** ER-020-XXX-00

### Kontext

Welches Problem oder welche Wahl bestand?

### Entscheidung

Was wurde entschieden?

### Grund

Warum wurde diese Variante gewählt?

### Konsequenzen

Welche Folgen hat die Entscheidung für spätere Änderungen?
```

## Entscheidungen ab Version 0.2.0

## DEC-001 – Fester ProductCode je veröffentlichter Fassung

**Status:** gültig

**Bezug:** ER-020-INS-01

### Kontext

WiX erzeugt ohne explizite Angabe bei jedem Bau einen neuen ProductCode. Zwei
MSI-Pakete mit derselben ProductVersion, aber verschiedenen ProductCodes gelten
für Windows Installer als verschiedene Produkte. Die Major-Upgrade-Regel
erfasst gleiche Versionen bewusst nicht; dadurch konnte Version 0.1.0 aus zwei
verschiedenen Builds mehrfach installiert werden.

### Entscheidung

Jede veröffentlichte dreiteilige Produktversion erhält genau einen festen
ProductCode. Alle Builds derselben Fassung verwenden diesen Code. Mit einer
neuen Produktversion wird einmalig ein neuer ProductCode vergeben. Der
UpgradeCode bleibt dagegen über alle Fassungen stabil.

Die festgelegten Zuordnungen lauten:

- 0.1.0 → `{723D8A8E-CB3D-4EC0-81D2-3821A56BE91D}`
- 0.2.0 → `{F69B7118-58E7-4BB9-B4FF-411056AA3776}`

`AllowSameVersionUpgrades` bleibt ausgeschaltet. Dieselbe Fassung wird über den
festen ProductCode als dasselbe Produkt erkannt und gelangt in den normalen
Windows-Installer-Wartungsmodus.

Der Dual-Purpose-Scope bleibt für 0.2.0 bestehen. Ein Wechsel auf ausschließlich
per-user würde bereits per-machine installierte 0.1.0-Fassungen ohne regulären
kontextgleichen Upgradepfad zurücklassen.

### Grund

Die Lösung beseitigt die zweite Produktidentität an ihrer Ursache und nutzt den
nativen MSI-Upgrademechanismus. Sie braucht weder Custom Actions noch eine Suche
nach alten Programmdateien oder Deinstallationsprogrammen.

### Konsequenzen

- ProductCode und dreiteilige Produktversion müssen bei einer neuen Fassung
  gemeinsam geändert werden.
- Ein erneuter Build derselben Fassung darf den ProductCode nicht ändern.
- Per-user- und per-machine-Upgrades müssen getrennt im jeweils gleichen
  Installationskontext geprüft werden; Windows Installer wechselt den Kontext
  bei einem Major Upgrade nicht.
- Eine automatisierte Prüfung liest ProductCode, UpgradeCode, ProductVersion
  und Upgrade-Tabelle aus dem tatsächlich gebauten MSI.

## DEC-002 – Eine aktive Produktversionsquelle

**Status:** gültig

**Bezug:** ER-020-VER-01

### Kontext

Die .NET-Projekte bezogen ihre Version bereits aus `VersionPrefix` in
`Directory.Build.props`. Der Windows-Job der CI übergab dem Installer jedoch
zusätzlich eine fest eingetragene ProductVersion. Bei einer Versionsänderung
konnten Anwendung und MSI dadurch auseinanderlaufen.

### Entscheidung

`VersionPrefix` in `Directory.Build.props` ist die einzige aktive
Produktversionsquelle. Die .NET-Projekte verwenden die Standardableitungen des
SDK für Version, AssemblyVersion, FileVersion und InformationalVersion. WiX
erhält die MSI-ProductVersion unmittelbar aus `VersionPrefix`; lokale und
CI-Buildaufrufe setzen keine eigene ProductVersion.

Die ProductCode-Zuordnungen sind davon getrennte historische
Installeridentitäten: `VersionPrefix` wählt genau einen festen Code aus. Fehlt
eine Zuordnung, bricht der Installerbau verständlich vor WiX ab.

### Grund

Ein Releasewechsel ändert damit nur eine aktive Versionsangabe. Anwendung,
Installer und Buildwege können nicht durch mehrere gepflegte Versionswerte
auseinanderlaufen. Die SDK-Bordmittel reichen aus; ein eigenes
Versionsframework oder eine neue Abhängigkeit ist nicht nötig.

### Konsequenzen

- Für eine neue veröffentlichte dreiteilige Version wird vor der Umschaltung
  einmalig ein neuer ProductCode hinterlegt.
- Unbekannte Produktversionen können kein installierbares Paket erzeugen.
- Die InformationalVersion darf den Commit-Hash als Buildmetadatum tragen; die
  sichtbare Über-Anzeige zeigt den Versionsanteil vor dem Pluszeichen.
- Eine automatisierte Prüfung vergleicht die gebaute Anwendung und das MSI mit
  demselben `VersionPrefix`.

## DEC-003 – Technische Hauptquelle für Drittanbieterhinweise

**Status:** gültig

**Bezug:** ER-020-LIC-01

### Kontext

Die technische Dokumentation, die mit der portablen Fassung ausgelieferte
Markdown-Datei und die RTF-Anzeige des Installers wurden unabhängig gepflegt.
Dadurch nannten die Anwenderdarstellungen entfernte Pakete und ließen direkte,
transitive sowie native Laufzeitbestandteile aus. Eine vollständig generierte
Lizenzdatenbank wäre für das kleine Projekt unverhältnismäßig und könnte eine
fachliche Lizenzprüfung ohnehin nicht ersetzen.

### Entscheidung

`docs/THIRD-PARTY-NOTICES.md` ist die fachlich-technische Hauptquelle. Sie
ordnet direkte Pakete, transitive und native Laufzeitbestandteile,
self-contained Runtimepacks, Entwicklungswerkzeuge sowie externe Prüfwerkzeuge
getrennt zu und nennt die geprüften Primärquellen.

`installer/Drittanbieterhinweise/README.md` und
`installer/EInvoiceSender.Setup/Lizenzhinweise.rtf` bleiben handgeschriebene,
zielgruppengerechte Anwenderdarstellungen. Sie enthalten keine unnötig
duplizierten Paketfassungen. Quelltext- und Artefaktprüfungen gleichen ihre
Pflichtangaben mit der Hauptquelle und dem tatsächlichen `deps.json` ab.

Vollständige Lizenz- und Notice-Texte werden unverändert unter
`installer/Drittanbieterhinweise/Lizenzen` abgelegt. Das MSI bezieht Übersicht
und Texte unmittelbar aus diesem Verzeichnis; der gemeinsame Releasebau
kopiert dieselben Dateien in die portable Fassung. Die RTF bleibt eine kurze
Zusammenfassung und wird nicht als vollständiger Lizenztext bezeichnet.

### Grund

Diese Aufteilung hält fachliche Zuordnung, tatsächlich ausgelieferte Dateien
und lesbare Anwenderinformation auseinander, ohne eine Generatorarchitektur
einzuführen. Gleichzeitig machen die Prüfungen jede neue oder entfernte
Runtimeabhängigkeit sichtbar, bevor ein Artefakt freigegeben wird.

### Konsequenzen

- Änderungen produktiver Pakete erfordern eine bewusste Aktualisierung der
  technischen Hauptquelle.
- Änderungen transitiver Pakete werden spätestens am frischen Publish rot.
- Lizenzangaben werden an Paketdateien oder Primärquellen geprüft und nicht aus
  Paketnamen abgeleitet.
- Das MSI muss `Drittanbieterhinweise.md` und die vorgesehenen Lizenztexte als
  installierte Dateien enthalten.
- Der gemeinsame lokale/CI-Releaseweg kopiert dieselben Quellen in die
  portable Fassung und prüft deren vollständigen Inhalt.

## DEC-004 – Ein maßgeblicher Releasepaketierungsweg

**Status:** gültig

**Bezug:** ER-020-REL-01

### Kontext

Der örtliche Installerbau und die Windows-CI formulierten Publish,
Drittanbieterhinweise, ZIP, Releaseordner und Prüfsummen getrennt. Der lokale
Weg ließ die Hinweise im portablen ZIP aus und übernahm vorhandene Dateien aus
`artifacts/release` in die Prüfsummendatei.

### Entscheidung

`build/Build-Release.ps1` ist der einzige maßgebliche Paketierungsweg für
lokale Releasebauten und GitHub Actions. `Build-Installer.ps1` bleibt auf
Installerbau und die bestehende MSI-/Publish-Prüfung begrenzt. Checkout,
Werkzeuge, Tests und Artifact-Upload bleiben Aufgaben der CI.

Der Releaseweg entfernt einen vorhandenen finalen Releasebestand, arbeitet
anschließend in einem separaten Staging und veröffentlicht diesen erst nach
vollständiger ZIP-, Dateisatz- und Prüfsummenprüfung. Kann kein sauberer
Ausgangszustand hergestellt werden oder schlägt ein Schritt fehl, wird kein
fertiger Releasebestand gemeldet.

Der reguläre Satz besteht genau aus MSI, portabler ZIP und
`SHA256SUMS.txt`. Die portable Fassung behält Dateien direkt an ihrer
ZIP-Wurzel und übernimmt Übersicht und Lizenzordner aus den durch DEC-003
festgelegten Quellen. Die Prüfsummendatei nennt ausschließlich MSI und ZIP in
fester Reihenfolge und wird nach ihrer Erstellung erneut verifiziert.

### Grund

Ein gemeinsamer, kleiner Repositoryweg verhindert Drift zwischen lokalem und
CI-Bau. Staging und exakte Dateisätze verhindern, dass Altdateien oder
unvollständige Ergebnisse als Release erscheinen. Wiederverwendete
PowerShell-Funktionen erlauben Positiv- und Negativtests ohne ein zusätzliches
Buildframework.

### Konsequenzen

- Änderungen an ZIP-Inhalt, Artefaktnamen oder Prüfsummen erfolgen nur im
  gemeinsamen Releaseweg.
- Die CI darf keine parallele MSI-Auswahl, Lizenzkopie, ZIP- oder SHA-Logik
  enthalten.
- Reproduzierbarkeit bezeichnet gleiche Struktur, Quellen und Prüfungen;
  MSI-PackageCode, Zeitstempel und Binärhashes dürfen zwischen Bauten
  abweichen.
- Code Signing kann später vor ZIP und Prüfsummenerzeugung ergänzt werden,
  gehört aber nicht zu dieser Entscheidung.

## DEC-005 – Desktopverknüpfung als natives optionales MSI-Feature

**Status:** gültig

**Bezug:** ER-020-INS-02

### Kontext

Die Desktopverknüpfung war bereits ein eigenes WiX-Feature, stand aber auf
Installationslevel 2 und war im verwendeten `WixUI_InstallDir`-Dialogsatz
nicht sichtbar. Eine normale Installation wählte sie deshalb weder aus noch
bot sie verständlich an. Ein generischer Featurebaum wäre für die einzige
Anwenderoption unnötig technisch gewesen.

### Entscheidung

Das vorhandene Feature für die Desktopverknüpfung bleibt getrennt von der
`Hauptfunktion` und steht wie diese auf Level 1. Dadurch ist es auch bei einer
stillen Erstinstallation standardmäßig ausgewählt. Eine native MSI-Checkbox
„Desktop-Verknüpfung erstellen“ kann ausschließlich dieses Feature über die
Windows-Installer-ControlEvents `AddLocal` und `Remove` ab- oder anwählen.

Der zusätzliche Dialog ist Teil einer eigenen vollständigen, an
`WixUI_InstallDir` aus WiX 5.0.2 angelehnten UI-Sequenz. Er erscheint nur bei
`NOT Installed AND NOT WIX_UPGRADE_DETECTED`. Repair und Major Upgrade zeigen
die Option nicht erneut; dort bleibt der vorhandene beziehungsweise über
`MigrateFeatureStates` übernommene Featurezustand maßgeblich.

### Grund

Die Lösung bietet Nicht-IT-Anwendern genau die eine verständliche Auswahl,
ohne Bootstrapper, Custom Action oder Shortcutverwaltung in der Anwendung.
Der unabhängige Startmenüeintrag und die vorhandenen MSI-Komponenten bleiben
unverändert. Eine vollständige eigene Sequenz vermeidet konkurrierende
Publish-Ereignisse am eingebauten WiX-Dialogsatz.

### Konsequenzen

- Feature-, Component-, Shortcut- und Registry-Identitäten bleiben stabil.
- Änderungen an der WiX-Version erfordern einen Abgleich der eigenen Sequenz
  mit dem dann aktuellen `WixUI_InstallDir`-Ablauf.
- Ein Upgrade übernimmt den bisherigen Desktop-Featurezustand und erzwingt
  nicht den neuen Erstinstallationsdefault.
- Per-user- und per-machine-Verhalten der profilbezogenen Verknüpfungen bleibt
  unverändert.
- Quelltests und die Prüfung der gebauten MSI-Tabellen sichern Featurezustand,
  Dialogpfade und native ControlEvents ab.

## DEC-006 – Lokales Diagnoselog ohne Nutzdaten

**Status:** gültig

**Bezug:** ER-020-LOG-01

### Kontext

Die Anwendung verwendete bereits `Microsoft.Extensions.Logging` und
quellgenerierte Logevents, hatte aber keinen persistierenden Provider. Einige
dieser Ereignisse enthielten Rechnungsnummern, Dokumenthashes, Datei- oder
Werkzeugpfade. Eine unveränderte Dateipersistenz hätte damit genau die Daten
gesammelt, die das Diagnoselog ausschließen soll.

### Entscheidung

Ein kleiner eigener `ILoggerProvider` schreibt ausschließlich lokale
Sitzungslogs nach `%LOCALAPPDATA%\EInvoiceSender\Diagnose`. Eine zufällige
Session-ID gilt nur für den jeweiligen Programmlauf. Es gibt keine dauerhafte
Benutzer-, Rechner- oder Installationskennung und keine Netzwerkfunktion.

Vor der Provideraktivierung wurden alle bestehenden Logevents geprüft.
Nutzdaten werden nicht nachträglich geschwärzt, sondern gar nicht erst an den
Logger übergeben. Dateipfade und -namen werden weggelassen oder durch feste
technische Kategorien ersetzt. Persistierte Exceptions bestehen nur aus der
Typkette und höchstens 40 Methodennamen; Message, Data, Argumentwerte,
Quelldateien und Zeilennummern werden nicht gelesen.

Jeder Programmlauf erhält eine eigene UTF-8-Datei. Eine Datei ist auf ein MiB
begrenzt, zehn abgeschlossene Dateien werden aufbewahrt. Aktive oder gesperrte
Dateien werden bei der Rotation übersprungen. Jeder Initialisierungs-, Schreib-,
Rotations- oder Dispose-Fehler schaltet höchstens das Logging ab und läuft nie
in den Anwendungspfad zurück.

### Grund

Die bestehende Logging-Abstraktion genügt; eine weitere Bibliothek und deren
Lizenz-/Runtimepflege wären für Dateiausgabe und einfache Rotation unnötig.
Sitzungsdateien trennen einzelne Fehlerläufe verständlich. Harte Mengen- und
Größengrenzen verhindern unbegrenztes Wachstum. Datenminimierung an der Quelle
ist belastbarer als eine unvollständige Liste regulärer Ausdrücke.

### Konsequenzen

- Neue Logevents dürfen nur kontrollierte technische Werte erhalten.
- Exceptionobjekte werden ausschließlich vom sicheren Formatter des Providers
  ausgewertet.
- Der vollständige Privacy-Test durchsucht tatsächlich erzeugte Logs nach
  markanten Rechnungs-, Kunden-, Bank-, Mail-, PDF-, XML- und Pfadwerten.
- „Diagnoseordner öffnen“ zeigt nur den zentral berechneten lokalen Ordner; es
  gibt keinen Upload, Versand oder Freigabemechanismus.
- Die Anwendung bleibt bei jedem Fehler des Loggings uneingeschränkt nutzbar.

## DEC-007 – Unternehmensvorlage nur aus ausdrücklich manuellen Angaben

**Status:** gültig

**Bezug:** ER-020-SET-01

### Kontext

Ohne gespeicherte Firmenvorlage kann die Anwendung den Rechnungsteller
absichtlich nicht aus einer PDF erraten. Anwender erfassen ihre Verkäuferdaten
dann in Schritt 2, mussten dieselben Angaben bisher aber noch einmal in den
Einstellungen eingeben. Der aktuelle Formularinhalt kann zugleich erkannte
Verkäufer-, Bank-, Käufer- und Rechnungsdaten enthalten und darf deshalb nicht
pauschal zur Unternehmensvorlage werden.

### Entscheidung

Eine ausdrückliche Aktion „Als eigene Unternehmensdaten speichern“ plant die
Speicherung über eine feste Allowlist aus Verkäufer- und Bankfeldern. Neu
übernommen werden nur Felder mit Herkunft `Manual`. Das sichtbare
Programmstandardland `DE` ist die einzige Ausnahme, solange es nicht aus einer
PDF-Erkennung stammt. Zuverlässig und unsicher erkannte Werte werden
gleichermaßen ausgeschlossen; Käufer- und Rechnungsfelder kommen in der
Allowlist nicht vor.

Vor jedem Plan wird `firmenvorlage.json` frisch gelesen. Die Existenz der Datei
gilt nicht als Unternehmensvorlage: Maßgeblich sind inhaltliche Verkäufer- oder
Bankdaten. Beim Merge bleiben nicht manuell geänderte Unternehmensfelder,
Komfortvorgaben und das letzte Ausgabeverzeichnis unverändert. Eine vorhandene
inhaltliche Vorlage verlangt eine Inline-Bestätigung; Abbruch und identische
Kandidaten schreiben nichts.

Nach erfolgreichem Speichern synchronisiert die Ablaufsteuerung nur ihren
Vorlagen-Snapshot. Der laufende Rechnungsentwurf und seine Feldherkünfte werden
nicht erneut befüllt oder verändert. Die regulären Einstellungen bleiben der
zentrale Ort für spätere Bearbeitung.

### Grund

Die bestehende Herkunftsverfolgung liefert eine belastbare technische Grenze
zwischen eigener Eingabe und PDF-Erkennung. Eine kleine reine Planerfunktion
ist einfacher zu prüfen als eine allgemeine Objektkopie oder automatisches
Lernen und benötigt weder Datenbank noch neue Synchronisationsinfrastruktur.

### Konsequenzen

- Neue Unternehmensfelder müssen bewusst in Modell, Allowlist, Merge und Tests
  aufgenommen werden; unbeabsichtigt werden sie nicht mitgespeichert.
- Die Speicheraktion darf ausschließlich an den sichtbaren Benutzerbefehl und
  seine Bestätigung gebunden sein.
- Erkannte Werte müssen vom Anwender tatsächlich bearbeitet werden, bevor sie
  als eigene Unternehmensangabe gelten können.
- Eine beschädigte oder fehlende Vorlagendatei bleibt ein kontrollierter leerer
  Ausgangszustand und kann durch die ausdrückliche Aktion neu angelegt werden.

Entscheidungen werden erst ergänzt, wenn sie im Rahmen der Planung oder Umsetzung tatsächlich getroffen und freigegeben wurden.

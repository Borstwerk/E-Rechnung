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

Entscheidungen werden erst ergänzt, wenn sie im Rahmen der Planung oder Umsetzung tatsächlich getroffen und freigegeben wurden.

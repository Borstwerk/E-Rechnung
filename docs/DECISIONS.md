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

Entscheidungen werden erst ergänzt, wenn sie im Rahmen der Planung oder Umsetzung tatsächlich getroffen und freigegeben wurden.

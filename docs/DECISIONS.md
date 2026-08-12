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

Noch keine neuen Entscheidungen eingetragen.

Entscheidungen werden erst ergänzt, wenn sie im Rahmen der Planung oder Umsetzung tatsächlich getroffen und freigegeben wurden.

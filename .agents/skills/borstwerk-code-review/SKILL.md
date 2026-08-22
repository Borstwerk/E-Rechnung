---
name: borstwerk-code-review
description: Gate-4-Code-Review für BorstWerk. Verwenden, um einen Diff gegen festen Ausgangspunkt, freigegebene Requirement/Planung und Repository-Standards zu prüfen.
---

# BorstWerk Code Review

Dieser Skill unterstützt Gate 4. Ein grüner Testlauf ersetzt dieses Review nicht.

## 1. Ausgangspunkt festnageln

Vor dem Review festhalten:

- Basis-Commit beziehungsweise Merge-Base;
- Head;
- Requirement-ID;
- freigegebener Gate-2-Plan beziehungsweise Spec-Quelle;
- relevante Architektur-, Entscheidungs- und Testdokumente.

Wenn Basis oder Spec unklar ist: STOPP und klären.

## 2. Diff tatsächlich lesen

Nicht nur Abschlussbericht oder Testzahl bewerten.

Prüfen:

- welche Dateien wirklich geändert wurden;
- ob verbotene beziehungsweise nicht geplante Bereiche berührt wurden;
- ob neue Abhängigkeiten, öffentliche Schnittstellen, Dateiformate oder Persistenzpfade entstanden sind;
- ob Migration, Snapshot, Ableitung, Backup/Restore und Löschschutz betroffen sind;
- ob UI, Dokumentprojektion, Findings und Publish-Verhalten konsistent bleiben.

## 3. Zwei getrennte Achsen

### A – Anforderung / Spec

Für jedes Akzeptanzkriterium prüfen:

- vollständig umgesetzt;
- teilweise umgesetzt;
- fehlt;
- Verhalten implementiert, aber fachlich anders als bestellt;
- zusätzliches Verhalten ohne Auftrag beziehungsweise Scope Creep.

Akzeptanzkriterien jeweils einem konkreten Nachweis zuordnen.

### B – Code / Standards

Prüfen:

- KISS und bestehende Architektur;
- keine unnötige Generalisierung;
- keine zweite fachliche Wahrheit;
- keine Duplikation zentraler Definitionen;
- keine versteckten Heuristiken;
- keine Validierungsabschwächung;
- sprechende Begriffe aus dem Fachmodell;
- keine unnötigen Repository-/Service-/Manager-Schichten;
- keine Seiteneffekte außerhalb des Slices.

Mögliche Code-Smells als Hinweise benennen, aber nicht automatisch zu Blockern erklären. Repository-Entscheidungen und Requirements haben Vorrang vor allgemeinen Stilregeln.

## 4. Tests prüfen, nicht nur zählen

Für kritische Zusicherungen prüfen:

- testet der Test das Verhalten oder nur die Implementierungsform;
- könnte er trotz gebrochener Anforderung grün bleiben;
- stammen erwartete Werte aus einer unabhängigen Quelle;
- decken Persistenz-/Migrations-/Snapshot-Tests echte Pfade ab;
- wurde bei riskanten Eigenschaften eine Brechprobe durchgeführt und hat sie tatsächlich angeschlagen.

Wenn eine Brechprobe im Bericht behauptet wird, aber der zugehörige Test die Mutation nicht erkennen würde, als Review-Fund melden.

## 5. Daten- und Historienrisiken

Besonders streng bei:

- Schemaänderungen;
- Migrationen;
- veröffentlichten Snapshots;
- Ableitung neuer Arbeitsstände;
- Backup/Restore;
- Hashes/Artefakten;
- Nutzerangaben, die still überschrieben werden könnten.

Keine historische Neuinterpretation ohne ausdrückliche Anforderung.

## 6. Ausgabe

Funde getrennt ausgeben als:

- **Blocker** – Requirement verletzt, Daten-/Sicherheitsrisiko oder falsches Verhalten;
- **Sollte korrigiert werden** – relevante Qualitäts-/Wartbarkeitsabweichung;
- **Hinweis** – kein Freigabehindernis.

Danach kurze Nachweismatrix:

```text
Akzeptanzkriterium
→ Code/Verhalten
→ automatischer Test
→ manuelle Prüfung, falls nötig
```

Abschließend ausdrücklich sagen, ob Gate 4 aus Sicht des Reviews bestanden ist oder welche Punkte offen bleiben.

Keine Freigabe behaupten, solange notwendige manuelle Prüfungen noch offen sind.

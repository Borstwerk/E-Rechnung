---
name: borstwerk-tdd
description: Testgetriebene Umsetzung für BorstWerk. Verwenden, wenn nach Gate-2-Freigabe ein Feature oder Bugfix in kleinen, überprüfbaren Schnitten umgesetzt wird.
---

# BorstWerk TDD

Dieser Skill unterstützt Gate 3. Er ersetzt weder Requirement noch freigegebenen Plan.

## Vorbedingung

Bevor Code geändert wird:

1. Requirement-ID und freigegebenen Gate-2-Plan identifizieren.
2. Die dort vereinbarten fachlichen Testgrenzen beziehungsweise Seams übernehmen.
3. Falls Gate 2 fehlt, widersprüchlich ist oder eine notwendige Architekturentscheidung offenlässt: STOPP und melden statt implementieren.

## Arbeitsweise

Für jeden kleinen fachlichen Schnitt:

1. Einen Test schreiben, der das gewünschte Verhalten an einer belastbaren öffentlichen Grenze beschreibt.
2. Den engsten passenden Testlauf ausführen und bestätigen, dass der neue Test aus dem erwarteten Grund rot ist.
3. Nur die kleinste fachlich richtige Änderung implementieren, die diesen Schnitt erfüllt.
4. Den engen Testlauf erneut ausführen und grün bestätigen.
5. Erst danach den nächsten Schnitt beginnen.

Nicht erst alle Tests und anschließend die gesamte Implementierung schreiben. Der letzte rote/grüne Zyklus soll die nächste Entscheidung informieren.

## Gute BorstWerk-Tests

Ein Test soll:

- Verhalten prüfen, nicht private Implementierungsdetails;
- einen unabhängigen erwarteten Wert besitzen und nicht die Produktionslogik im Test nachbauen;
- bei Refactoring grün bleiben, solange das zugesicherte Verhalten unverändert bleibt;
- bei Persistenzänderungen einen echten Roundtrip oder eine echte Migration prüfen;
- bei Snapshots und Ableitungen sowohl Einfrieren als auch Wiederherstellung prüfen, wenn der Slice diese Pfade berührt;
- bei UI-Wiring möglichst Verhalten oder belastbare Source-/Markup-Gates prüfen, ohne XAML-Struktur unnötig festzufrieren.

Keine Tests lockern, löschen oder umformulieren, nur damit neue Implementierung grün wird. Wenn eine alte Zusicherung fachlich nicht mehr gilt, muss das aus Requirement oder freigegebenem Plan hervorgehen.

## Brechprobe

Wenn eine kritische Eigenschaft leicht versehentlich umgangen werden kann, nach dem grünen Testlauf eine gezielte Brechprobe durchführen:

- Schutzbedingung probeweise entfernen oder falschen Zustand einführen;
- bestätigen, dass mindestens der dafür gedachte Test rot wird;
- Probe vollständig zurückbauen;
- Endstand erneut grün ausführen.

Brechproben sind besonders wertvoll bei Migrationen, Heuristikverboten, Deduplizierung, Rollback, Zustandskopplung und Persistenzpfaden.

## Refactoring und Scope

Refactoring nur, wenn es für den freigegebenen Slice notwendig ist oder direkt aus dem letzten Zyklus als lokale Vereinfachung folgt. Kein Architekturputz nebenbei. Keine neue Abhängigkeit ohne Gate-2-Grund.

## Abschluss

Vor Abschluss mindestens:

- relevante enge Tests grün;
- vollständige Testsuite grün;
- Format-/Lint-/Build-Prüfungen des Repos grün;
- `git diff --check` oder gleichwertige Diff-Prüfung grün;
- Diff gegen die freigegebene Scope-Grenze prüfen;
- Testzahl vorher/nachher und Brechproben im Bericht nennen.

Grün bedeutet: Umsetzung technisch belegt. Die Freigabe erfolgt erst in Gate 4/5.

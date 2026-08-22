---
name: borstwerk-diagnose
description: Disziplinierte Fehlerdiagnose für BorstWerk. Verwenden bei Bugs, Regressionen, sporadischem Verhalten oder Performanceproblemen, bevor ein Fix geraten wird.
---

# BorstWerk Fehlerdiagnose

Dieser Skill unterstützt Gate 2/3 bei Fehlerkorrekturen. Ziel ist ein Root-Cause-Fix mit belastbarem Nachweis.

## 1. Reproduzierbaren Nachweis bauen

Vor Hypothesen einen möglichst engen, agent-runnbaren Pass/Fail-Nachweis erstellen, der exakt das gemeldete Symptom treffen kann.

Bevorzugte Reihenfolge:

1. fehlender automatisierter Regressionstest an einer echten fachlichen Grenze;
2. bestehender Test mit minimalem neuen Fixture;
3. kleiner CLI-/Datei-/DB-Harness;
4. reproduzierbarer UI-/Windows-Schritt, wenn Automatisierung nicht sinnvoll möglich ist;
5. für Performance: Messharness statt bloßer Logausgabe.

Wenn kein belastbarer Repro möglich ist: STOPP, bisherige Versuche nennen und benötigte Information oder Umgebung benennen. Nicht aus Code-Lektüre allein einen Fix erraten.

## 2. Repro minimieren

Den Fehlerfall auf das kleinste noch fehlschlagende Szenario reduzieren. Eingaben, Daten, Schritte und Abhängigkeiten einzeln entfernen und nach jedem Schritt erneut prüfen.

## 3. Hypothesen bilden

Drei bis fünf falsifizierbare Ursachen priorisieren. Für jede Hypothese angeben, welche Beobachtung sie bestätigen oder widerlegen würde.

Keine Einzelhypothese als Tatsache behandeln, bevor der Repro sie stützt.

## 4. Gezielt instrumentieren

Nur Messungen oder Logs hinzufügen, die konkrete Hypothesen unterscheiden. Keine flächendeckende Debug-Ausgabe.

- Keine Secrets, personenbezogenen Daten oder Geschäftsdaten in Logs übernehmen.
- Temporäre Instrumentierung eindeutig markieren und vor Abschluss entfernen.
- Bei Performanceproblemen messen/profilen statt Logmengen zu erzeugen.

## 5. Regressionstest vor Fix

Wenn eine geeignete Testgrenze existiert:

1. minimalen Repro als Regressionstest festhalten;
2. rot bestätigen;
3. Root Cause beheben;
4. Test grün bestätigen;
5. ursprünglichen, breiteren Repro erneut ausführen.

Existiert keine sinnvolle Testgrenze, das ausdrücklich als Architektur-/Nachweisgrenze dokumentieren statt einen wertlosen Test gegen Interna zu erzwingen.

## 6. Fix-Regeln

- Ursache beheben, nicht Symptom verdecken.
- Keine Validierung abschalten.
- Keine Daten still reparieren oder heuristisch umdeuten, sofern Requirement das nicht ausdrücklich verlangt.
- Keine fremden Bereiche refactoren.
- Führt die Diagnose zu einer größeren Verhaltens- oder Architekturänderung, zurück zu Gate 2 und Plan freigeben lassen.

## 7. Brechprobe und Abschluss

Bei kritischer Regression den Fix oder Schutz probeweise wieder entfernen und bestätigen, dass der Regressionstest rot wird.

Im Abschlussbericht nennen:

- reproduziertes Symptom;
- Root Cause;
- verworfene wesentliche Hypothesen, wenn relevant;
- Regressionstest/Nachweis;
- Brechprobe;
- geänderte Dateien;
- vollständigen Prüflauf;
- verbleibende manuelle Prüfungen.

---
name: borstwerk-domain-modeling
description: Fachmodellierungs-Disziplin für BorstWerk. Verwenden in Gate 1/2, wenn Begriffe, wiederholbare Fachobjekte, Zuständigkeiten oder Modulgrenzen geklärt werden müssen.
---

# BorstWerk Domain Modeling

Dieser Skill unterstützt Gate 1/2. Während dieses Skills wird nicht implementiert.

## 1. Quellen zuerst

Vor Modellentscheidungen lesen:

- konkrete Requirement-ID und Akzeptanzkriterien;
- relevante Fachplanung/Quellenmatrix;
- `ARCHITECTURE.md`;
- `DECISIONS.md`;
- bestehenden Domaincode und Persistenzmodell;
- passende Testfälle und Szenarien.

Alte Planung ist ein Hinweis, keine automatisch gültige Architekturentscheidung.

## 2. Begriffe schärfen

Jeden wichtigen Begriff gegen Dokumentation und Code prüfen.

Wenn dasselbe Wort zwei Dinge meint oder zwei Wörter denselben Sachverhalt beschreiben, Widerspruch offenlegen und einen kanonischen Begriff vorschlagen.

Beispiele für typische Fragen:

- Ist das eine grobe Scope-Antwort oder ein echtes Fachobjekt?
- Ist der Wert aktuelle Wahrheit oder nur Legacyinformation?
- Ist etwas eine Verantwortung, eine Kontrolle, ein Systemmerkmal oder ein Datenfluss?
- Ist ein Objekt wiederholbar oder existiert es im MVP genau einmal?

## 3. Konkrete Szenarien statt abstrakter Kästen

Das geplante Modell mindestens an folgenden Arten von Fällen stressen:

- einfachster Solo-Betrieb;
- mehrere reale Wege/Objekte nebeneinander;
- externer Dienstleister;
- Unknown/Open;
- historischer Altbestand;
- Objekt wird gelöscht oder ersetzt;
- Snapshot wird veröffentlicht und später abgeleitet.

Das Modell muss diese Fälle ohne erfundene Enterprise-Struktur erklären können.

## 4. Antworten versus Fachobjekte

Leitregel:

> Eine beantwortete Frage ist kein Ersatz für ein Fachobjekt, wenn derselbe reale Sachverhalt wiederholbar, referenzierbar, historisierbar oder mit anderen Objekten verknüpft werden muss.

Umgekehrt gilt:

> Nicht jede Antwort braucht einen neuen Record oder eine neue Tabelle.

Ein neues persistiertes Fachobjekt nur vorschlagen, wenn mindestens ein echter fachlicher Grund besteht, etwa:

- mehrere Instanzen müssen parallel beschrieben werden;
- andere Fachobjekte müssen stabil darauf verweisen;
- eigener Lebenszyklus/Löschschutz;
- Snapshot-/Historienbedeutung;
- eine einzelne Projektantwort würde verschiedene reale Sachverhalte vermischen.

## 5. Keine zweite Wahrheit

Vor jedem neuen Feld oder Objekt prüfen, ob der Sachverhalt bereits verbindlich existiert in:

- Systemmodell;
- Rollen/Verantwortlichkeiten;
- Dateninterfaces;
- Migration;
- Aufbewahrung;
- Findings;
- anderer Prozessprojektion.

Wenn ja: wiederverwenden oder klare Abgrenzung formulieren. Keine parallele Freitextwahrheit aufbauen.

## 6. Grenzen des Slices

Explizit festhalten:

- was dieser Slice übernimmt;
- was bewusst bei einem anderen Requirement bleibt;
- was nicht Teil des MVP ist;
- welche vorhandenen Objekte nur referenziert werden;
- welche alten Fragen/Legacyfelder weiter existieren und warum.

Keine Nachbarmodule vorsorglich mitbauen.

## 7. Persistenzentscheidung

Falls ein neues Fachobjekt vorgeschlagen wird, Gate 2 muss beantworten:

- braucht es wirklich eine Schemaänderung;
- Defaultzustand bestehender Dateien;
- ob deterministische Migration möglich ist;
- welche Heuristiken ausdrücklich verboten sind;
- Snapshot-Kompatibilität;
- `DeriveWorkingDraft`;
- Backup/Restore;
- Löschschutz/Fremdschlüssel.

Keine Schemaerhöhung nur, weil ein neuer Begriff schöner aussieht.

## 8. Entscheidungsformat

Gate-2-Ergebnis mindestens:

1. Bestandsmatrix: was existiert bereits?
2. zentrale Modellentscheidung mit Begründung;
3. finaler Scope und Abgrenzungen;
4. Domain-/Persistenzvorschlag nur soweit nötig;
5. Finding- und Publish-Semantik;
6. Dokumentwirkung;
7. Altprojekt-/Snapshot-/Derive-Strategie;
8. positive und negative Szenarien;
9. Testplan;
10. harte Diff-Grenze.

Wenn eine Entscheidung langfristig, überraschend und schwer umkehrbar ist, einen Eintrag in `DECISIONS.md` vorschlagen. Nicht für jede lokale Implementierungswahl eine Entscheidung erzeugen.

Nach Gate-2-Analyse STOPP. Umsetzung beginnt erst nach ausdrücklicher Freigabe.

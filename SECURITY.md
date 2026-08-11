# Sicherheitsrichtlinie

## Unterstützte Versionen

BorstWerk E-Rechnung wird derzeit nur in der jeweils aktuellen veröffentlichten
Version unterstützt.

| Version | Unterstützt |
|---------|-------------|
| 0.1.x   | ✅ |
| älter   | ❌ |

Nach Veröffentlichung einer neueren Version kann die Unterstützung älterer
Versionen eingestellt werden.

## Sicherheitslücke melden

Wenn Sie eine mögliche Sicherheitslücke in BorstWerk E-Rechnung gefunden haben,
melden Sie diese bitte **nicht als öffentliches GitHub-Issue**.

Verwenden Sie stattdessen die private Sicherheitsmeldung des Repositorys:

**Security → Report a vulnerability**

Bitte geben Sie nach Möglichkeit an:

- verwendete Version von BorstWerk E-Rechnung
- betroffene Programmfunktion
- Schritte, mit denen sich das Problem nachvollziehen lässt
- mögliche Auswirkungen
- gegebenenfalls eine möglichst kleine, anonymisierte Beispieldatei

Bitte laden Sie **keine echten Rechnungen oder Dateien mit vertraulichen,
personenbezogenen oder Bankdaten** hoch.

Eine Sicherheitsmeldung wird zunächst geprüft und nach Möglichkeit vertraulich
behandelt, bis eine Korrektur verfügbar ist.

## Was als Sicherheitsproblem gilt

Dazu gehören beispielsweise:

- unbeabsichtigtes Offenlegen oder Übertragen von Rechnungs- oder Firmendaten
- unsichere Speicherung sensibler Einstellungen
- Manipulationsmöglichkeiten an erzeugten E-Rechnungen
- Ausführen unerwarteter Inhalte aus verarbeiteten Dateien
- Umgehen vorgesehener Schutzmechanismen bei problematischen PDFs
- Schwachstellen in mitgelieferten Abhängigkeiten, die BorstWerk E-Rechnung
  tatsächlich betreffen

Normale Programmfehler, Darstellungsprobleme oder Verbesserungsvorschläge können
über die regulären GitHub-Issues gemeldet werden.

## Datenschutz bei Sicherheitsmeldungen

BorstWerk E-Rechnung verarbeitet Rechnungen grundsätzlich lokal. Auch bei einer
Fehlermeldung sollen keine Rechnungsinhalte an das Projekt übertragen werden,
wenn dies nicht zwingend erforderlich ist.

Verwenden Sie für reproduzierbare Beispiele bevorzugt künstliche oder vollständig
anonymisierte Daten.

## Keine Sicherheitsprämien

BorstWerk ist ein privates, nichtkommerzielles Open-Source-Projekt. Ein
Bug-Bounty- oder Prämienprogramm besteht nicht.

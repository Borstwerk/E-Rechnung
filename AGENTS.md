# BorstWerk E-Rechnung – Agent Instructions

BorstWerk-Agent-Skills: `2026-08-22-01`

## Vor jeder relevanten Änderung

1. Die konkrete Requirement-ID in den projektbezogenen Requirements unter `docs/` identifizieren.
2. Relevante Teile aus `DEVELOPMENT.md`, Architektur-/Entscheidungs-/Testdokumenten und vorhandenen Tests lesen.
3. Bestehenden Code prüfen, bevor eine Lösung geplant wird.
4. Den BorstWerk-Fünf-Gate-Prozess einhalten:

```text
Anforderung
→ Planung
→ Umsetzung
→ Review und Nachweis
→ Freigabe
```

Größere Änderungen werden vor Gate-2-Freigabe nicht implementiert.

## Repo-lokale Skills

Unter `.agents/skills/` stehen:

- `borstwerk-domain-modeling` – Gate 1/2 für Begriffe, Fachmodell und Slice-Grenzen
- `borstwerk-tdd` – Gate 3 für kleine red/green-Umsetzungsschnitte
- `borstwerk-diagnose` – reproduzierbare Fehlerdiagnose vor Root-Cause-Fixes
- `borstwerk-code-review` – Gate 4 gegen festen Ausgangspunkt, Requirement und Repository-Standards

Skills sind Hilfsmittel. Bei Widerspruch haben Requirements, gültige Decisions und der freigegebene Gate-2-Plan Vorrang.

## E-Rechnung-spezifisch

- BorstWerk E-Rechnung ist **kein Rechnungsprogramm**: Es vergibt keine Rechnungsnummer, führt keine Buchhaltung und verwaltet keine Kunden.
- Die Anwendung verarbeitet eine bereits vorhandene PDF und erzeugt daraus ZUGFeRD/Factur-X EN 16931.
- Die Original-PDF wird gelesen und nicht verändert.
- Ein optionaler `.eml`-Entwurf ist kein automatischer Versand; versendet wird außerhalb der Anwendung durch den Nutzer.
- Prüfberichte und Validierung dürfen keine Prüfung als bestanden darstellen, die tatsächlich nicht ausgeführt wurde.
- Keine Cloud-/Telemetry-/Account-Funktion oder neue externe Abhängigkeit ohne ausdrückliches Requirement.
- Release- und Installeränderungen benötigen ihre vorgesehenen Windows-/Artefakt-Nachweise; grüne Unit-Tests reichen dafür nicht.

# Windows-Abnahme 0.2.0

**Status: FREIGEGEBEN – Windows-Abnahme für den Releasekandidaten bestanden**

Durchgeführt am **24.08.2026** auf dem eingefrorenen Produktstand
`458e90ca313f7c33ca05cf896ea456370d64e167`.

**Was diese Freigabe bedeutet – und was nicht.** Sie besagt, dass die manuelle
Windows-, Installer- und Upgrade-Abnahme für den Releasekandidaten bestanden
ist. Sie besagt **nicht**, dass Version 0.2.0 veröffentlicht wäre: Es gibt
weder einen Tag `v0.2.0` noch ein GitHub-Release. Beides entsteht erst nach dem
Releaseabschluss nach den Bedingungen in
[`REQUIREMENTS-0.2.0.md`](REQUIREMENTS-0.2.0.md).

Grüne Tests, eine bestandene Codereview und eine bestandene Windows-Abnahme
sind drei verschiedene Zustände. Diese Datei hält ausschließlich den dritten
fest.

Wo für einen bestandenen Punkt kein eigenes Artefakt und keine eigene Prüfsumme
vorliegt, steht das auch so da. Es wurden keine Prüfsummen, Buildnummern,
Zeiten oder Dateinamen erfunden.

## Prüfling

| Angabe | Wert |
|---|---|
| Commit | `458e90ca313f7c33ca05cf896ea456370d64e167` |
| Version | 0.2.0 |
| Datum der Abnahme | 24.08.2026 |
| Windows-Version und Build | nicht erfasst |
| Tester | Auftraggeber (Projektverantwortlicher) |
| Verwendetes MSI (Dateiname) | nicht erfasst |
| SHA-256 des MSI | nicht erfasst |
| Verwendete portable ZIP (Dateiname) | nicht erfasst |
| SHA-256 der ZIP | nicht erfasst |
| SHA-256 von `SHA256SUMS.txt` | nicht erfasst |

## A – Releasepaket

| # | Prüfung | Sollresultat | Ergebnis |
|---|---|---|---|
| A1 | `.\build\Test-ReleasePackaging.ps1` | läuft fehlerfrei durch | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A2 | `.\build\Build-Release.ps1` ohne erhöhte Rechte | läuft fehlerfrei durch | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A3 | Zweiter Bau über vorhandenen Artefakten | keine Altdateien im Ergebnis | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A4 | MSI vorhanden | genau eine Datei | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A5 | Portable ZIP vorhanden | genau eine Datei | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A6 | `SHA256SUMS.txt` vorhanden | vorhanden, enthält sich nicht selbst | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A7 | Prüfsummen verifizieren | alle aufgeführten Dateien stimmen | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A8 | Drittanbieterhinweise in der ZIP | vorhanden und vollständig | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A9 | Drittanbieterhinweise im MSI | vorhanden und vollständig | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A10 | Versionsangaben | Anwendung, MSI und Artefakte melden 0.2.0 | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| A11 | `artifacts/release` insgesamt | exakt MSI, ZIP, `SHA256SUMS.txt` | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

## B – Installation

| # | Prüfung | Sollresultat | Ergebnis |
|---|---|---|---|
| B1 | MSI ohne erhöhte Rechte starten | keine UAC-Rückfrage bei Benutzerinstallation | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B2 | Desktopoption bei Erstinstallation | standardmäßig aktiviert | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B3 | Installation **mit** Desktopverknüpfung | genau eine Verknüpfung auf dem Desktop | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B4 | Installation **ohne** Desktopverknüpfung | keine Verknüpfung auf dem Desktop | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B5 | Startmenüeintrag | in beiden Fällen vorhanden | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B6 | Programmeinträge | genau ein Eintrag unter „Installierte Apps“ | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B7 | Repair nach aktivierter Option | Auswahl erscheint nicht erneut, weiterhin genau eine Verknüpfung | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B8 | Repair nach abgewählter Option | Auswahl erscheint nicht erneut, keine Verknüpfung | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B9 | Anwendung starten | startet ohne Nachinstallation einer Laufzeit | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B10 | „Über“-Dialog | meldet 0.2.0, nicht „unbekannt“ | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| B11 | Symbol | in Taskleiste, Startmenü, Explorer und „Installierte Apps“ korrekt | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

## C – ER-020-SELL-ID-01

Der fachliche Schwerpunkt dieser Abnahme. Für die Fälle A bis C liegen erzeugte
Artefakte mit Prüfsumme vor; sie wurden zusätzlich extern mit Mustang-CLI
**2.24.0** (Java OpenJDK 21) gegengeprüft. Die externe Prüfung umfasst veraPDF
(Abschnitt `<pdf>` des Berichts) und das CEN-Schematron des Zielprofils.

### Fall A – Verkäufer mit USt-IdNr. (BT-31)

| Angabe | Wert |
|---|---|
| Erzeugte Datei | `RE-CTRL-0001_Nordlicht_Handel_GmbH_ZUGFeRD.pdf` (62 222 Bytes) |
| SHA-256 | `4b85abf57b4b27506a2f4291b08eb937da1cab36eebce5faa91099afbc7c4e45` |
| Mustang / CEN-Schematron | valid |
| veraPDF (PDF/A-3b) | valid |
| Mustang-Exitcode | 0 |
| BR-CO-26 | keine Verletzung |
| Gesamtstatus | **Bestanden** |

### Fall B – keine USt-IdNr., dafür Steuernummer und Registerkennung (BT-30)

| Angabe | Wert |
|---|---|
| Erzeugte Datei | `RE-CTRL-0001_Nordlicht_Handel_GmbH_ZUGFeRD (2).pdf` (62 210 Bytes) |
| SHA-256 | `b902cebcdffdf1b952165ae2343faa89548606ba32b44a3f78b022520da2c9dc` |
| BT-30 in der eingebetteten XML | `SpecifiedLegalOrganization/ram:ID` = `HRB 12345` |
| BT-29 | nicht vorhanden |
| BT-31 beim Verkäufer | nicht vorhanden |
| BT-32 | zusätzlich vorhanden (`schemeID="FC"`) |
| Mustang / CEN-Schematron | valid |
| veraPDF (PDF/A-3b) | valid |
| Mustang-Exitcode | 0 |
| BR-CO-26 | keine Verletzung; erfüllt allein über BT-30 |
| Gesamtstatus | **Bestanden** |

### Fall C – keine USt-IdNr., dafür Steuernummer und Lieferantennummer (BT-29)

| Angabe | Wert |
|---|---|
| Erzeugte Datei | `RE-CTRL-0001_Nordlicht_Handel_GmbH_ZUGFeRD.pdf` (62 125 Bytes) |
| SHA-256 | `c9ab1477877843546507e5d0ccd77131b6207088bfdb5ef63f4191696f48cea9` |
| BT-29 in der eingebetteten XML | `SellerTradeParty/ram:ID` = `LIEF-4711` |
| BT-29 als unmittelbares Kind von `SellerTradeParty` | ja |
| BT-29 vor `ram:Name` | ja |
| BT-29 ohne `schemeID` | ja |
| BT-30 | nicht vorhanden |
| BT-31 beim Verkäufer | nicht vorhanden |
| BT-32 | zusätzlich vorhanden (`schemeID="FC"`) |
| Mustang / CEN-Schematron | valid |
| veraPDF (PDF/A-3b) | valid |
| Mustang-Exitcode | 0 |
| BR-CO-26 | keine Verletzung; erfüllt allein über BT-29 |
| Gesamtstatus | **Bestanden** |

### Fall D – nur Steuernummer (BT-32)

Die Gegenprobe. Hier ist das Ausbleiben einer Datei das erwartete Ergebnis; ein
Artefakt gibt es deshalb nicht.

| # | Prüfung | Sollresultat | Ergebnis |
|---|---|---|---|
| D1 | USt-IdNr., Registerkennung und Lieferantennummer leer, Steuernummer gesetzt | Ausgangslage hergestellt | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D2 | Ausgabe | **keine** Datei entsteht | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D3 | Befund | `APP-SEL-004` mit Normregel `BR-CO-26` | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D4 | Verständlichkeit | Der Befund wird verständlich angezeigt | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

### Oberfläche, Speicher- und Rücksetzverhalten

| # | Prüfung | Sollresultat | Ergebnis |
|---|---|---|---|
| C5 | ToolTip am Feld „Lieferantennummer“ | erscheint mit der Maus und ist verständlich | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| C6 | Einstellungen | führen die Registerkennung | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| C7 | Einstellungen | führen **keine** Lieferantennummer | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| C8 | Registerkennung (BT-30) | wird als Firmenstamm gespeichert und ist nach „Neue Rechnung“ wieder vorhanden | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| C9 | Lieferantennummer (BT-29) | wird **nicht** global gespeichert und ist nach „Neue Rechnung“ leer | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| C10 | Tastaturreihenfolge im Verkäuferblock | läuft in sichtbarer Reihenfolge durch die neuen Felder | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

**ER-020-SELL-ID-01 ist damit Windows-abgenommen.** Alle drei nach BR-CO-26
zulässigen Wege sind extern belegt, und die Gegenprobe hält den unzulässigen
vierten auf.

## D – ER-020-POS-01

| # | Prüfung | Sollresultat | Ergebnis |
|---|---|---|---|
| D1 | Kontroll-PDF mit HUR, HUR und C62 einlesen | Tabelle erkannt, alle Positionen mit korrekter Einheit; keine steht pauschal auf „Stück“ | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D2 | Drei Positionen | Netto 600,00, Steuer 114,00, Brutto und Zahlbetrag 714,00 | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D3 | Manuell erfasste Positionswerte | bleiben geschützt | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D4 | Rechnung ohne Mengeneinheit | Positionen übernommen, Einheitenfeld bleibt leer | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D5 | Validierung bei leerer Einheit | blockiert bis zur manuellen Ergänzung | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D6 | Nach Ergänzen einer gültigen Einheit | korrekte Berechnung | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D7 | „Neue Rechnung“ | räumt vorbefüllte Positionen weg | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| D8 | Erzeugte E-Rechnung extern geprüft | Mustang/CEN und veraPDF bestehen | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

**Die Freigabeschranke aus ER-020-POS-01 ist damit erfüllt.**

## E – Erkennung, Käufer und Einstellungen

| # | Prüfung | Ergebnis |
|---|---|---|
| E1 | DET-01: eindeutiger Seller ohne Firmenvorlage | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E2 | DET-01: zwei gleich starke Firmen, Seller bleibt leer | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E3 | DET-02: Kleinunternehmer-Referenzrechnung | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E4 | SET-01: „Als meine Unternehmensdaten speichern“ | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E5 | SET-01: „Nicht als eigene Daten speichern“ schreibt nichts | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E6 | BUY-01: Käuferland | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E7 | BUY-02: Käufer-USt-IdNr. | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E8 | BUY-03: Käufer-E-Mail | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E9 | Keine Vertauschung von Seller und Buyer | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| E10 | DUE-01: Fälligkeit folgt dem Rechnungsdatum | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

## F – Windows-spezifische Funktionen

| # | Prüfung | Ergebnis |
|---|---|---|
| F1 | „Über“ → „Diagnoseordner öffnen“ | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F2 | Sitzungslog enthält Start, Verarbeitung und reguläres Ende | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F3 | Sitzungslog enthält keine Rechnungs- oder Kundendaten | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F4 | Keine automatische Übertragung der Protokolle | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F5 | Logrotation auf zehn Sitzungslogs | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F6 | IBAN geschützt abgelegt (DPAPI) | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F7 | Tastaturnavigation, Fokus sichtbar | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F8 | Zugriffstasten | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F9 | Skalierung 100 % | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F10 | Skalierung 150 % | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F11 | Skalierung 200 % | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F12 | Fenstergröße 940 × 640 | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F13 | `.eml` im klassischen Outlook | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| F14 | `.eml` im neuen Outlook | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

## G – Upgrade

| # | Prüfung | Ergebnis |
|---|---|---|
| G1 | 0.1.0 installieren | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G2 | Firmendaten anlegen und Rechnung erzeugen | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G3 | Upgrade auf 0.2.0 | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G4 | Genau ein Programmeintrag nach dem Upgrade | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G5 | Keine doppelten Startmenü- oder Desktopeinträge | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G6 | Desktopoption erscheint beim Upgrade nicht erneut | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G7 | Firmendaten nach dem Upgrade vollständig erhalten | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G8 | `%LOCALAPPDATA%\EInvoiceSender` bleibt erhalten | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G9 | Same-Version erzeugt keine zweite Produktinstanz | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G10 | Downgrade auf 0.1.0 sauber verhindert | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G11 | Deinstallation entfernt Verknüpfung und Startmenüeintrag | Bestanden – manuelle Windows-Abnahme 24.08.2026 |
| G12 | Benutzerdaten bleiben nach der Deinstallation erhalten | Bestanden – manuelle Windows-Abnahme 24.08.2026 |

## Abschluss

| Requirement | Ergebnis | Nachweis | Restpunkt |
|---|---|---|---|
| ER-020-INS-01 | Bestanden | Abschnitte B und G | – |
| ER-020-INS-02 | Bestanden | Abschnitte B3, B4, B7, B8, G5, G6 | – |
| ER-020-VER-01 | Bestanden | A10, B10 | – |
| ER-020-LIC-01 | Bestanden | A8, A9 | – |
| ER-020-REL-01 | Bestanden | Abschnitt A | – |
| ER-020-LOG-01 | Bestanden | F1 bis F5 | – |
| ER-020-SET-01 | Bestanden | E4, E5 | – |
| ER-020-DET-01 | Bestanden | E1, E2 | – |
| ER-020-DET-02 | Bestanden | E3 | – |
| ER-020-DUE-01 | Bestanden | E10 | – |
| ER-020-BUY-01 | Bestanden | E6, E9 | – |
| ER-020-BUY-02 | Bestanden | E7, E9 | – |
| ER-020-BUY-03 | Bestanden | E8 | – |
| ER-020-POS-01 | Bestanden | Abschnitt D; Freigabeschranke erfüllt | – |
| ER-020-SELL-ID-01 | Bestanden | Abschnitt C; Fälle A bis C extern mit Mustang 2.24.0 belegt, Fall D als Gegenprobe | – |
| ER-020-SIGN-01 | entfällt | Optional laut Requirement; ohne vertrauenswürdiges Zertifikat wird transparent unsigniert veröffentlicht | kein Release-Blocker |

### Gesamturteil

- [x] **FREIGEGEBEN**
- [ ] **FREIGABE MIT RESTPUNKT**
- [ ] **NICHT FREIGEGEBEN**

Freigegeben ist der Produktstand `458e90ca313f7c33ca05cf896ea456370d64e167`
für den Releasekandidaten. Tag und Veröffentlichung entstehen dadurch **nicht**
automatisch; sie folgen erst nach dem Releaseabschluss.

### Beobachtung ohne Blockerwirkung

Bei Fall D fiel auf, dass Steuernummer und EN-16931-Verkäuferkennung im
Formular fachlich leicht zu verwechseln sind. Die Prüfung selbst ist richtig –
BT-32 allein erfüllt BR-CO-26 nicht, und der Befund sagt das auch. Verbessern
ließe sich die Beschriftung. Der Punkt steht als künftige Arbeit in
[`BACKLOG.md`](BACKLOG.md) und blockiert 0.2.0 nicht.

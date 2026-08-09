# SPECIFICATION.md – Verbindliche fachliche Spezifikation

Version 1.0 · Stand 2026-08-04

## 1. Zweck

`EInvoiceSender` wandelt eine **bereits vorhandene** PDF-Rechnung zusammen mit
manuell erfassten und ausdrücklich bestätigten strukturierten Rechnungsdaten in
eine validierte ZUGFeRD-/Factur-X-Rechnung (Profil EN 16931, PDF/A-3) um und
bereitet einen E-Mail-Entwurf vor.

Die Anwendung **erstellt keine Rechnungen**. Sie ersetzt kein
Rechnungsprogramm und keine Buchhaltung. Die inhaltliche und steuerliche
Richtigkeit verantwortet der Benutzer.

## 2. Nichtziele

Siehe `AGENTS.md`, Abschnitt 1. Die dortige Liste ist Teil dieser Spezifikation.

## 3. Ablauf

| Schritt | Inhalt |
|---|---|
| 1 | PDF auswählen (Dateidialog oder Drag-and-drop), prüfen, Vorschau anzeigen |
| 2 | Rechnungsdaten erfassen oder aus Vorlage übernehmen |
| 3 | Kontrollansicht, Abgleich PDF ↔ Daten, **Pflichtbestätigung** |
| 4 | Erzeugen und validieren, Fortschritt je Prüfschritt |
| 5 | Speichern, E-Mail-Entwurf, Ausgabeordner öffnen |

## 4. Eingangsprüfung (Schritt 1)

- Dateiendung **und** tatsächlicher Dateityp (`%PDF-` Signatur) werden geprüft.
- Maximale Dateigröße ist konfigurierbar; Vorgabe **20 MB**.
- Angezeigt werden Dateiname, vollständiger Pfad und Größe.
- Enthält die Datei bereits eine eingebettete Rechnungs-XML, wird das erkannt,
  das Profil ausgelesen und **gewarnt**. Die Verarbeitung ist nur nach
  ausdrücklicher Bestätigung möglich.
- Verschlüsselte, beschädigte oder passwortgeschützte PDFs werden mit
  verständlicher Begründung abgelehnt.
- Die Originaldatei wird **niemals** verändert oder überschrieben.

## 5. Rechnungsdaten (Schritt 2)

### 5.1 Dokument
Rechnungsnummer (BT-1), Rechnungsdatum (BT-2), Rechnungsart (BT-3, Vorgabe 380),
Währung (BT-5, Vorgabe EUR), Leistungsdatum oder -zeitraum (BT-72 / BG-14),
Zahlungsziel (BT-9) oder Zahlungsbedingungen (BT-20), Bestellreferenz (BT-13),
Kundenreferenz (BT-10), Bemerkung (BT-22).

### 5.2 Verkäufer
Name (BT-27), Anschrift (BG-5: Straße, PLZ, Ort), Land (BT-40), E-Mail (BT-43),
Steuernummer und/oder USt-IdNr. (BT-31/BT-32), Kontoinhaber, IBAN (BT-84),
BIC (BT-86, optional).

### 5.3 Käufer
Name (BT-44), Anschrift (BG-8), Land (BT-55), E-Mail (BT-58), USt-IdNr. (BT-48,
optional), elektronische Adresse / Routing (BT-49, optional).

### 5.4 Positionen (BG-25, mindestens eine)
Positionsnummer (BT-126), Beschreibung (BT-153), Menge (BT-129),
Einheit (BT-130), Netto-Einzelpreis (BT-146), optionaler Positionsrabatt
(BT-136/BT-138), Netto-Positionssumme (BT-131, **berechnet**),
Steuerkategorie (BT-151), Steuersatz (BT-152).

### 5.5 Summen (alle **berechnet**, nicht eingegeben)
BT-106 Summe der Positionen, BT-107 Nachlässe, BT-108 Zuschläge,
BT-109 Nettosumme, BT-110 Gesamtsteuer, BT-112 Bruttosumme,
BT-113 bereits gezahlt, BT-114 Rundung, BT-115 offener Zahlbetrag,
je Steuersatz BT-116 Basis / BT-117 Steuerbetrag.

**Regel:** Vom Benutzer eingegebene Gesamtsummen werden nicht als Wahrheit
übernommen. Der Benutzer kann eine Kontrollsumme eintragen; weicht sie von der
berechneten ab, ist das ein Fehler, kein stillschweigender Vorrang.

## 6. Prüfungen vor der Erzeugung

Pflichtfelder, Rechnungsnummer nicht leer, plausible Datumswerte
(Rechnungsdatum nicht in der Zukunft, Fälligkeit nicht vor Rechnungsdatum),
Vorzeichen passend zum Dokumenttyp (380 positiv, 381 Gutschrift),
Positionssummen, Steuersummen je Satz, Nettosumme, Bruttosumme, Zahlbetrag,
ISO-4217-Währung, ISO-3166-Land, gültige Einheiten- und Steuerkategoriecodes,
E-Mail-Syntax, IBAN-Prüfziffer nach ISO 7064, erforderliche Referenzen,
Rundungsabweichungen über 0,01 EUR.

Für die Steuerkategorien `Z`, `E`, `AE`, `K`, `G`, `O` ist eine Begründung
(BT-120) erforderlich; für `S` muss der Steuersatz größer null sein.

Jeder Befund trägt Schweregrad, Regel-ID, deutschen Klartext und Feldbezug.

## 7. Bestätigung der Übereinstimmung (Schritt 3)

Die Kontrollansicht zeigt links die PDF-Vorschau, rechts die strukturierten
Kerndaten: Verkäufer, Käufer, Rechnungsnummer, Rechnungsdatum, Netto, Steuer,
Brutto, Zahlbetrag.

Der Benutzer muss ausdrücklich bestätigen:

> Ich habe geprüft, dass die strukturierten Rechnungsdaten mit der sichtbaren
> PDF-Rechnung übereinstimmen.

Ohne diese Bestätigung ist die Erzeugung technisch gesperrt – nicht nur in der
Oberfläche, sondern im Anwendungsfall selbst.

## 8. Erzeugung (Schritt 4)

Ausgabeformat: ZUGFeRD 2.3 / Factur-X 1.07, Profil EN 16931, PDF/A-3b,
Anhang `factur-x.xml`. Exakte Werte: `docs/STANDARDS.md`.

Ist das Eingangs-PDF nicht zu PDF/A-3 aufwertbar, wird der Vorgang abgebrochen,
die Ursache verständlich erklärt, das Original unverändert gelassen und
**keine** Datei ausgegeben (ADR-0003).

## 9. Validierung

Verpflichtend, in dieser Reihenfolge: Wohlgeformtheit der XML, Struktur gegen
das Profil, EN-16931-Geschäftsregeln, Codelisten, Summenkonsistenz,
PDF/A-3-Struktur, korrekte Einbettung und Dateibeziehung, PDF-Metadaten,
Profilübereinstimmung, erneutes Öffnen des Ergebnisses mit Extraktion und
Wiederholung der XML-Prüfung.

Zusätzlich, sofern konfiguriert: externe Validatoren (Schematron, veraPDF).
Ohne sie meldet der Bericht ausdrücklich, dass die PDF/A-Prüfung nur strukturell
erfolgte.

Eine Datei gilt nur als erfolgreich erzeugt, wenn alle verpflichtenden
Prüfungen bestanden wurden.

## 10. Ergebnis (Schritt 5)

Erzeugt werden: die ZUGFeRD-PDF, ein maschinenlesbarer Bericht (JSON), eine
menschenlesbare Zusammenfassung, SHA-256-Prüfsumme, Erzeugungszeitpunkt,
Standard- und Profilangabe sowie die Versionen aller beteiligten Validatoren.

Dateinamensvorschlag: `<Rechnungsnummer>_<Empfänger>_ZUGFeRD.pdf`, unzulässige
Zeichen sicher bereinigt. Vorhandene Dateien werden nie stillschweigend
überschrieben.

## 11. E-Mail-Vorbereitung

Erzeugt wird ein `.eml`-Entwurf (Empfänger, Betreff, Text, Anhang), der im
Mailclient des Benutzers geöffnet wird. **Die Anwendung versendet nichts.**
Immer verfügbar: „Ausgabeordner öffnen" und ein `mailto:`-Fallback ohne Anhang.

## 12. Vorlagen

Lokal gespeichert werden: eigene Firmendaten, Standardbankverbindung,
Standardzahlungsziel, Standard-Betreff, Standard-E-Mail-Text, zuletzt
verwendetes Ausgabeverzeichnis. Format JSON unter
`%LOCALAPPDATA%\EInvoiceSender`. Sensible Werte werden per DPAPI geschützt.
Kennwörter werden nicht gespeichert.

## 13. Datenschutz

Die Verarbeitung erfolgt vollständig lokal. Ohne ausdrückliche Benutzeraktion
verlässt kein PDF, kein Rechnungsdatum, keine E-Mail-Adresse, keine
Bankverbindung, keine Steuerangabe und kein Validierungsergebnis den Rechner.
Onlinekonverter und Cloud-APIs sind im Kernprozess ausgeschlossen.

## 14. Sprache und Bedienung

Primärsprache Deutsch; Texte liegen zentral und sind für spätere Lokalisierung
vorbereitet. Vollständige Tastaturbedienung, sinnvolle Tabreihenfolge,
erkennbarer Fokus, DPI-Skalierung, Screenreader-Bezeichnungen. Fehler werden
nie allein durch Farbe angezeigt. Irreversible Aktionen sind
bestätigungsbedürftig.

## 15. Spätere Ausbaustufen (nicht MVP)

Zusätzliche reine XRechnung als XML, PDF-Texterkennung ausschließlich zur
Vorbelegung mit Pflichtbestätigung, weitere Mailanbieter über
`IEmailDraftService`.

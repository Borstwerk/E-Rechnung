# BorstWerk E-Rechnung

**Kostenlose Werkzeuge für kleine Unternehmen.**

**BorstWerk E-Rechnung** ist ein kostenloses Open-Source-Windows-Werkzeug für kleine Unternehmen,
Selbstständige und Vereine. Es erzeugt aus einer **bereits vorhandenen PDF-Rechnung** eine
**ZUGFeRD-/Factur-X-E-Rechnung** im Profil **EN 16931**. Das Ergebnis ist eine PDF/A-3-Datei mit
eingebetteten strukturierten Rechnungsdaten; die Verarbeitung läuft vollständig lokal auf dem
eigenen Rechner.

BorstWerk E-Rechnung ist ein Werkzeug der [BorstWerk-Werkzeugfamilie](https://github.com/Borstwerk).

Sie ist ausdrücklich **kein Rechnungsprogramm**. Sie schreibt keine Rechnung, vergibt keine
Rechnungsnummer, führt keine Buchhaltung, verwaltet keine Kunden, erstellt keine Mahnungen und
versendet keine E-Mail von sich aus.

Gedacht ist sie für den Fall, dass die Rechnung schon fertig ist – geschrieben mit Word, Excel,
LibreOffice, einer Branchenlösung oder sonst einem Programm – und daraus jetzt eine E-Rechnung
werden soll.

> **Status:** Version 0.1.0 ist veröffentlicht.

## Download

Für Windows 10 und Windows 11 (64 Bit):

- **Empfohlen:** MSI-Installer für die normale Installation
- **Alternativ:** portable ZIP-Fassung ohne Installation

[**Aktuelle Version herunterladen**](https://github.com/Borstwerk/E-Rechnung/releases/latest)

## Was die Anwendung kann

- eine vorhandene PDF-Rechnung auswählen und örtlich prüfen
- vorhandenen PDF-Text auslesen und das Formular damit vorbelegen
- die erkannten Angaben kontrollieren, korrigieren und ergänzen
- wiederkehrende eigene Firmendaten einmalig hinterlegen
- eine ZUGFeRD-/Factur-X-Rechnung im Profil EN 16931 erzeugen
- die strukturierten Rechnungsdaten als XML in eine PDF/A-3-Datei einbetten
- das Ergebnis erneut öffnen und gegenprüfen
- einen Prüfbericht mit SHA-256-Prüfsumme schreiben
- einen E-Mail-Entwurf als `.eml`-Datei für das vorhandene Mailprogramm vorbereiten

Die Original-PDF wird dabei ausschließlich gelesen und niemals verändert.

## Was die Anwendung bewusst nicht kann

- kein Rechnungsprogramm: Sie schreibt keine Rechnungen und vergibt keine Rechnungsnummern
- keine Buchhaltung, keine Kundenverwaltung, kein Mahnwesen
- kein automatischer E-Mail-Versand – der Entwurf wird vorbereitet, abgeschickt wird er von Hand
- keine Texterkennung (OCR) für eingescannte Rechnungen
- keine zuverlässige automatische Übernahme von Rechnungspositionen aus beliebigen Tabellen
- keine Steuer- oder Rechtsberatung: Ob eine Rechnung inhaltlich und steuerlich richtig ist,
  kann die Anwendung nicht beurteilen
- keine Zusicherung, dass sich jede beliebige PDF verarbeiten lässt
- keine Verarbeitung kennwortgeschützter oder rechtebeschränkter PDFs
- keine Veränderung digital signierter PDFs

## So läuft es ab

1. **PDF auswählen und prüfen.** Die vorhandene Rechnung wird geprüft und als Vorschau angezeigt.
2. **Rechnungsdaten prüfen und ergänzen.** Erkannte Angaben stehen im Formular; fehlende oder
   unsichere werden ergänzt.
3. **Sichtbare Rechnung und strukturierte Daten vergleichen.** Vor der Erzeugung ist ausdrücklich
   zu bestätigen, dass beides übereinstimmt.
4. **E-Rechnung erzeugen und prüfen.** Die strukturierte XML wird erstellt, gegengeprüft und in
   die PDF/A-3-Datei eingebettet.
5. **Speichern und E-Mail vorbereiten.** Die fertige Datei und der Prüfbericht werden abgelegt,
   auf Wunsch dazu ein `.eml`-Entwurf.

## Screenshots

**PDF auswählen und analysieren**

![Schritt 1 – PDF-Rechnung auswählen und analysieren](docs/images/01-pdf-auswahl.png)

**Rechnungsdaten prüfen und ergänzen**

![Schritt 2 – Rechnungsdaten erfassen und prüfen](docs/images/02-rechnungsdaten.png)

**E-Rechnung speichern und E-Mail-Entwurf vorbereiten**

![Schritt 5 – E-Rechnung speichern und E-Mail-Entwurf vorbereiten](docs/images/03-speichern-und-versenden.png)

## Welche Daten aus der PDF gelesen werden

Bei digital erzeugten PDFs liest die Anwendung den bereits vorhandenen Text örtlich aus und füllt
das Formular damit vor. Erkannt werden unter anderem Rechnungsnummer, Rechnungs-, Leistungs- und
Fälligkeitsdatum, Währung, die Käuferangaben aus dem Adressblock, die eigenen Angaben aus der
gespeicherten Firmenvorlage, IBAN und BIC sowie Netto-, Steuer-, Brutto- und Zahlbetrag samt
Steuersätzen.

Jeder erkannte Wert bleibt sichtbar und änderbar. Unsichere Werte werden gekennzeichnet oder gar
nicht erst übernommen. **Rechnungspositionen werden derzeit noch nicht zuverlässig aus Tabellen
übernommen** und müssen häufig von Hand erfasst werden.

## Welche PDFs sich verwenden lassen

**Der direkte Weg.** Geeignete PDFs werden unverändert übernommen und um die fehlenden
PDF/A-3-Bestandteile ergänzt. Der Text der Rechnung bleibt dabei Text. Dieser Weg hat immer
Vorrang.

**Die sichtbare Kopie.** Fehlt einer Datei nur die Einbettung der verwendeten Schriftarten und
lassen sich ihre Seiten zuverlässig darstellen, kann die Anwendung stattdessen eine sichtbare
Kopie erzeugen: Sie stellt jede Seite örtlich dar und baut daraus ein neues Dokument. Das
geschieht **nie automatisch** – Sie müssen ausdrücklich zustimmen, und vorher steht dort, was es
kostet:

- Das Original bleibt unverändert.
- Der sichtbare Seiteninhalt bleibt erhalten.
- Der Text der neuen Datei ist danach nicht mehr markierbar und in der Anzeige nicht mehr
  durchsuchbar.
- Verknüpfungen und Formularfunktionen gehen verloren.
- Die Datei kann größer werden.
- Die Rechnungsdaten selbst bleiben über die eingebettete XML vollständig maschinenlesbar – für
  den Empfänger einer E-Rechnung ist das der Teil, der zählt.

**Was abgelehnt wird.** Die sichtbare Kopie ist kein Rettungsanker für jede problematische Datei.
Abgelehnt werden weiterhin beschädigte PDFs, solche mit Öffnungs- oder Besitzerkennwort, digital
signierte, solche mit aktiven Inhalten wie JavaScript – und solche, an denen weitere Anhänge
hängen, die in einer sichtbaren Kopie verloren gingen. In jedem Fall nennt die Anwendung den
Grund und, wo möglich, was sich im Ausgangsprogramm umstellen lässt.

**Eingescannte Rechnungen.** Eine reine Bild-PDF lässt sich verarbeiten, sofern sie sonst geeignet
ist. Ihre Rechnungsdaten werden dabei aber **nicht** aus dem Bild gelesen – eine Texterkennung
gibt es nicht. Die Anwendung sagt das geradeheraus und erfindet nichts; die Angaben sind von Hand
zu erfassen.

## Was mit Ihren Daten geschieht

Die gesamte Verarbeitung läuft auf Ihrem Rechner.

- Rechnungsdaten bleiben örtlich.
- Keine Cloud-Verarbeitung, kein Webdienst, keine Cloud-KI.
- Kein Benutzerkonto, keine Anmeldung.
- Keine Telemetrie, keine Werbung.
- Rechnungen, Anschriften und Bankverbindungen werden nicht übertragen.
- Die Original-PDF wird nur gelesen.
- Eine E-Mail wird nie ohne Ihr Zutun versendet.

## Eigene Firmendaten

Wiederkehrende Angaben lassen sich einmalig hinterlegen: Firmenname und Anschrift, USt-IdNr. oder
Steuernummer, E-Mail-Adresse, Kontoinhaber mit IBAN und BIC, Standardwährung,
Zahlungsbedingungen, ein Standardtext für die E-Mail und das Ausgabeverzeichnis. Sensible Angaben
wie die IBAN werden unter Windows geschützt abgelegt.

## Der Prüfbericht

Zu jeder erzeugten E-Rechnung entsteht ein Prüfbericht. Er hält fest:

- die erzeugte Datei mit Größe und SHA-256-Prüfsumme
- Rechnungsnummer, Beteiligte und Standardangaben
- die festgestellten Fehler und Warnungen
- welche Prüfungen tatsächlich gelaufen sind
- auf welchem Weg die PDF verarbeitet wurde – direkt übernommen oder als sichtbare Kopie

Die installierte Anwendung führt ihre eingebauten Prüfungen aus. Unabhängige Referenzwerkzeuge
gehören zur Entwicklung und zur Freigabe und werden **nicht** mitinstalliert. Hat keine externe
Prüfung stattgefunden, schreibt der Bericht genau das – eine nicht durchgeführte Prüfung wird nie
als bestandene dargestellt.

## Installation

- Windows 10 oder Windows 11, 64 Bit
- eine vorhandene PDF-Rechnung

Mehr wird nicht gebraucht. Die Anwendung bringt alles Nötige mit; weder .NET noch Java müssen
nachinstalliert werden.

Für die normale Nutzung ist das MSI-Installationspaket vorgesehen. Es installiert
standardmäßig **nur für den aktuellen Benutzer** und kommt ohne Administratorrechte aus. Es legt
einen Eintrag im Startmenü an, auf Wunsch zusätzlich eine Desktopverknüpfung, und lässt
persönliche Einstellungen bei der Deinstallation unangetastet. Alternativ gibt es eine portable
ZIP-Fassung, die sich ohne Installation starten lässt.

## Bekannte Grenzen

Die wichtigsten stehen oben unter „Was die Anwendung bewusst nicht kann“ und „Welche PDFs sich
verwenden lassen“. Ausführlich: [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md).

## Fehler und Verbesserungsvorschläge

Einen Fehler gefunden oder eine Idee für BorstWerk E-Rechnung?

- [Fehler melden](https://github.com/Borstwerk/E-Rechnung/issues/new?template=bug_report.yml)
- [Verbesserung vorschlagen](https://github.com/Borstwerk/E-Rechnung/issues/new?template=feature_request.yml)

Bitte **keine echten Rechnungen oder andere Dateien mit vertraulichen, personenbezogenen oder
Bankdaten hochladen**. Verwenden Sie nach Möglichkeit anonymisierte oder künstliche Beispieldaten.

## Entwicklung und Transparenz

**KI-gestützte Entwicklung:** Bei der Entwicklung von BorstWerk E-Rechnung
werden generative KI-Werkzeuge unterstützend eingesetzt. Anforderungen,
Architekturentscheidungen, Code, Tests, Validierung und Releases werden durch
den Projektverantwortlichen geprüft. Die Verantwortung für die veröffentlichten
Versionen liegt beim Projektverantwortlichen.

## BorstWerk

BorstWerk ist eine Familie kleiner, eigenständiger Werkzeuge für kleine Unternehmen,
Selbstständige und Vereine. Die Werkzeuge verarbeiten Geschäftsdaten standardmäßig örtlich,
brauchen kein Benutzerkonto, enthalten keine Werbung und keine Telemetrie – und jedes lässt sich
unabhängig von den anderen installieren und benutzen.

Derzeit gehören dazu:

- **BorstWerk E-Rechnung** – dieses Werkzeug
- **BorstWerk GoBD-Doku**

Übersicht: [github.com/Borstwerk](https://github.com/Borstwerk)

---

Entwicklerinnen, Entwickler und Mitwirkende finden Hinweise zu Aufbau, Bauen und Tests in
[`DEVELOPMENT.md`](DEVELOPMENT.md).

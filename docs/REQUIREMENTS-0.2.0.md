# BorstWerk E-Rechnung – Anforderungen Version 0.2.0

## Status

**Planung**

Diese Datei beschreibt die verbindlichen Anforderungen für Version 0.2.0.

Sie ist keine Implementierungsanweisung. Die technische Umsetzung wird pro Anforderung separat geplant und erst nach Freigabe des jeweiligen Plans begonnen.

## Produktgrenzen

Version 0.2.0 verändert das Grundkonzept von BorstWerk E-Rechnung nicht.

Der Ablauf bleibt:

```text
vorhandene PDF-Rechnung
→ Daten erkennen
→ Daten prüfen und ergänzen
→ ZUGFeRD-/Factur-X EN 16931 erzeugen
→ prüfen
→ speichern
→ E-Mail-Entwurf vorbereiten
```

Weiterhin ausdrücklich nicht Bestandteil:

- Rechnungen schreiben,
- Rechnungsnummern vergeben,
- Buchhaltung,
- CRM,
- Mahnwesen,
- automatischer E-Mail-Versand,
- Cloud-Verarbeitung,
- Cloud-KI,
- Benutzerkonto,
- Telemetrie.

## ER-020-INS-01 – Bestehende Installation sauber aktualisieren

### Problem / Grund

Während der Abnahme von Version 0.1.0 konnte dieselbe Anwendung durch unterschiedliche MSI-Produktinstanzen mehrfach installiert werden.

Für veröffentlichte Updates muss ein eindeutiger Upgradepfad bestehen.

### Anforderung

Version 0.2.0 muss eine vorhandene Installation von Version 0.1.0 regulär erkennen und ersetzen.

### Akzeptanzkriterien

- 0.1.0 → 0.2.0 ergibt genau eine installierte Anwendung.
- Es existiert danach genau ein Eintrag unter „Installierte Apps“.
- Startmenüeinträge werden nicht dupliziert.
- Desktopverknüpfungen werden nicht dupliziert.
- Gespeicherte Firmendaten bleiben erhalten.
- Gespeicherte Einstellungen bleiben erhalten.
- `%LOCALAPPDATA%\EInvoiceSender` wird beim Upgrade nicht entfernt.
- Installation derselben 0.2.0-Fassung erzeugt keine zweite Produktinstanz.
- Downgrade von 0.2.0 auf 0.1.0 wird verhindert oder verständlich abgelehnt.
- Der bestehende UpgradeCode bleibt stabil.
- Es wird kein eigener EXE-Such- oder Deinstallationsmechanismus gebaut, wenn Windows Installer das regulär lösen kann.

### Nachweis

Noch festzulegen:

- automatisierte Installer-Prüfungen soweit sinnvoll,
- Windows-Abnahme 0.1.0 → 0.2.0,
- Same-Version-Test,
- Downgrade-Test,
- Deinstallationstest.

## ER-020-VER-01 – Einheitliche Versionsführung

### Problem / Grund

Anwendungs-, Installer- und CI-Version dürfen bei zukünftigen Releases nicht auseinanderlaufen.

### Anforderung

Version 0.2.0 soll aus einer zentralen maßgeblichen Versionsquelle abgeleitet werden.

### Akzeptanzkriterien

- Anwendung meldet 0.2.0.
- MSI meldet 0.2.0.
- Release-Artefakte gehören nachweisbar zu derselben Version.
- Keine zusätzliche fest eingetragene `0.1.0` oder `0.2.0` in Build- oder CI-Dateien, wenn der Wert aus der zentralen Quelle gelesen werden kann.
- Release-Build und lokaler Installer-Build verwenden dieselbe Versionsquelle.

### Nachweis

- Prüfung der Buildkonfiguration,
- automatisierter Build,
- Versionsanzeige auf dem installierten Windows-Paket.

## ER-020-LOG-01 – Lokales Diagnoselogging

### Problem / Grund

Fehler auf fremden Rechnern sollen nachvollziehbar sein, ohne Rechnungs- oder Kundendaten zu sammeln.

### Anforderung

Die Anwendung erhält ein begrenztes lokales Diagnoselog.

### Akzeptanzkriterien

Logs dürfen enthalten:

- Programmversion,
- Zeitpunkt,
- Workflow-Schritt,
- PDF-Verarbeitungsroute,
- technische Prüfresultate,
- technische Fehlercodes,
- Laufzeiten relevanter Schritte,
- Exception-Typ,
- Stacktrace.

Logs dürfen insbesondere nicht enthalten:

- Rechnungsnummer,
- Verkäufer- oder Käufername,
- Anschriften,
- E-Mail-Adressen,
- IBAN,
- BIC,
- USt-IdNr.,
- Steuernummer,
- Rechnungspositionen,
- extrahierten PDF-Text,
- XML-Inhalte,
- PDF-Inhalte.

Weitere Kriterien:

- Logging erfolgt ausschließlich lokal.
- Es erfolgt keine automatische Übertragung.
- Logs wachsen nicht unbegrenzt.
- Alte Logs werden über eine einfache Rotation begrenzt.
- Die Oberfläche bietet „Diagnoseordner öffnen“.
- Dateipfade werden so behandelt, dass personenbezogene Bestandteile nicht unnötig protokolliert werden.

### Nachweis

- automatisierte Tests gegen bekannte sensible Beispieldaten,
- Prüfung der erzeugten Logs,
- Windows-Abnahme „Diagnoseordner öffnen“.

## ER-020-SET-01 – Manuell eingegebene Verkäuferdaten als Firmenvorlage speichern

### Problem / Grund

Auf einer Erstinstallation kann die Anwendung den Rechnungsteller bewusst nicht automatisch bestimmen, solange keine eigene Firmenvorlage vorhanden ist.

Benutzer können die Verkäuferdaten zwar in Schritt 2 manuell eingeben, müssen sie derzeit aber zusätzlich separat in den Einstellungen hinterlegen.

### Anforderung

Wenn keine Firmenvorlage vorhanden ist und Verkäuferdaten manuell erfasst wurden, soll die Anwendung optional anbieten, diese Angaben als eigene Firmendaten zu speichern.

### Akzeptanzkriterien

- Es wird niemals automatisch gespeichert.
- Die Speicherung benötigt eine ausdrückliche Benutzeraktion.
- Nur Verkäuferdaten werden übernommen.
- Käuferdaten werden niemals in die eigene Firmenvorlage übernommen.
- Eine bereits vorhandene Firmenvorlage wird nicht ungefragt überschrieben.
- Die gespeicherten Daten stehen bei einer neuen Rechnung als normale Firmenvorlage zur Verfügung.
- Die regulären Einstellungen bleiben der zentrale Ort zum späteren Bearbeiten der Firmenvorlage.
- Manuelle Eingaben der aktuellen Rechnung bleiben unverändert.

### Nachweis

- Test ohne vorhandene Firmenvorlage,
- Test mit vorhandener Firmenvorlage,
- Test, dass Käuferdaten nicht übernommen werden,
- Windows-UI-Abnahme.

## ER-020-BUY-01 – Käuferland erkennen

### Problem / Grund

Der Käuferadressblock wird bereits erkannt, das Land jedoch noch nicht zuverlässig übernommen.

### Anforderung

Ein Käuferland darf aus dem Käuferkontext erkannt und vorbelegt werden.

### Akzeptanzkriterien

- Land wird nur aus einem ausreichend eindeutig dem Käufer zugeordneten Kontext übernommen.
- Das Verkäuferland darf nicht versehentlich zum Käuferland werden.
- Globale Fundstellen ohne belastbare Zuordnung reichen nicht aus.
- Unsichere Werte bleiben leer oder werden entsprechend der bestehenden Confidence-Logik behandelt.
- Manuelle Eingaben werden nicht überschrieben.

### Nachweis

- Käuferblock mit Land,
- Käuferblock ohne Land,
- Verkäufer- und Käuferland unterschiedlich,
- mehrdeutige PDF.

## ER-020-BUY-02 – Käufer-USt-IdNr. erkennen

### Problem / Grund

Eine PDF kann mehrere USt-IdNrn. enthalten. Eine globale Suche könnte deshalb die USt-IdNr. des Verkäufers fälschlich dem Käufer zuordnen.

### Anforderung

Die Käufer-USt-IdNr. darf nur aus einem ausreichend starken Käuferkontext übernommen werden.

### Akzeptanzkriterien

- Käufer-USt-ID aus dem Käuferblock wird erkannt.
- Verkäufer-USt-ID wird nicht zum Käufer.
- Eine isolierte globale USt-ID wird nicht ohne Kontext als Käufer-USt-ID übernommen.
- Ungültige beziehungsweise offensichtlich ungeeignete Werte werden nicht übernommen.
- Manuelle Werte bleiben geschützt.

### Nachweis

- PDF mit zwei USt-IdNrn.,
- PDF nur mit Verkäufer-USt-ID,
- PDF mit Käufer-USt-ID direkt im Käuferblock,
- Regressionstest gegen vertauschte Parteien.

## ER-020-BUY-03 – Käufer-E-Mail-Adresse erkennen

### Problem / Grund

Auch Verkäufer und Käufer können jeweils eine E-Mail-Adresse in derselben PDF besitzen.

### Anforderung

Eine Käufer-E-Mail-Adresse darf nur aus einem ausreichend eindeutig dem Käufer zugeordneten Bereich übernommen werden.

### Akzeptanzkriterien

- E-Mail im Käuferblock wird erkannt.
- Verkäufer-E-Mail wird nicht zum Käufer.
- Globale E-Mail-Fundstellen reichen ohne Kontext nicht aus.
- Manuelle Werte werden nicht überschrieben.

### Nachweis

- Käufer und Verkäufer mit unterschiedlichen E-Mail-Adressen,
- nur Verkäufer-E-Mail vorhanden,
- Käufer-E-Mail im adressbezogenen Kontext.

## ER-020-POS-01 – Rechnungspositionen aus digitalen PDFs erkennen

**Status:** Feature mit Freigabeschranke

Diese Anforderung darf auf eine spätere Version verschoben werden, wenn keine ausreichend zuverlässige Lösung für 0.2.0 erreicht wird.

### Problem / Grund

Rechnungspositionen müssen derzeit überwiegend manuell erfasst werden.

Einfache zeilenbasierte Parser sind für unterschiedliche Rechnungstabellen nicht zuverlässig genug.

### Anforderung

Bei geeigneten digital erzeugten PDFs sollen Rechnungspositionen soweit zuverlässig möglich automatisch vorbelegt werden.

### Leitplanken

Die Erkennung soll vorhandene räumliche PDF-Informationen verwenden.

Insbesondere nicht zulässig:

- jede Textzeile mit Zahlen als Position interpretieren,
- Tabellen ausschließlich anhand von Leerzeichen zerlegen,
- Werte raten, nur um möglichst viele Felder zu füllen.

### Zu prüfende Informationen

Soweit zuverlässig ableitbar:

- Beschreibung,
- Menge,
- Einheit,
- Einzelpreis,
- Gesamtpreis,
- Steuersatz beziehungsweise Steuerkategorie.

Mehrzeilige Beschreibungen und unterschiedliche Tabellenlayouts müssen berücksichtigt werden.

### Akzeptanzkriterien

- Sicher erkannte Positionen können vorbelegt werden.
- Erkannte Werte bleiben vollständig editierbar.
- Unsichere Tabellen führen nicht zu erfundenen Positionen.
- Bei nicht zuverlässig erkennbarer Struktur bleibt die manuelle Eingabe erhalten.
- Summen müssen nach Übernahme der Positionen mit den bestehenden Berechnungsregeln konsistent sein.
- Bestehende manuelle Positionsbearbeitung darf nicht verschlechtert werden.

### Testlayouts

Mindestens:

- klassische Tabelle,
- mehrzeilige Beschreibung,
- unterschiedliche Spaltenbreiten,
- rechtsbündige Preise,
- zusätzliche Tabellenspalten,
- unterschiedliche Einheiten,
- keine Positionstabelle,
- bewusst mehrdeutiges Layout.

### Release-Gate

Die Funktion wird in 0.2.0 nur aktiviert beziehungsweise veröffentlicht, wenn die Tests und manuelle Abnahme ausreichende Zuverlässigkeit zeigen.

Andernfalls bleibt das Verhalten von 0.1.0 bestehen und die Anforderung wird verschoben.

## ER-020-SIGN-01 – Öffentliche Binärdateien signieren

**Status:** Optional für 0.2.0

### Problem / Grund

Version 0.1.0 ist nicht mit einem öffentlich vertrauenswürdigen Code-Signing-Zertifikat signiert.

Windows kann deshalb vor einem unbekannten Herausgeber warnen.

### Anforderung

Wenn rechtzeitig ein geeigneter Code-Signing-Dienst beziehungsweise ein öffentlich vertrauenswürdiges Zertifikat verfügbar ist, sollen die Release-Binärdateien signiert werden.

### Akzeptanzkriterien

Bei verfügbarem Signing:

1. Anwendung veröffentlichen.
2. relevante EXE- und DLL-Dateien signieren.
3. Signaturen prüfen.
4. MSI aus signierten Dateien bauen.
5. MSI signieren.
6. portable ZIP erzeugen.
7. Signaturen der enthaltenen Dateien prüfen.
8. erst danach SHA-256-Prüfsummen erzeugen.

Weitere Kriterien:

- keine privaten Schlüssel im Repository,
- keine selbstsignierten Zertifikate für öffentliche Releases,
- keine Signatur vortäuschen, wenn kein vertrauenswürdiges Zertifikat verfügbar ist.

### Release-Gate

Fehlendes Code Signing blockiert 0.2.0 nicht.

Ist kein geeigneter Signing-Dienst verfügbar, wird die Version weiterhin transparent unsigned veröffentlicht.

## Allgemeine Qualitätsanforderungen

Für sämtliche Anforderungen gelten:

- bestehende Tests bleiben grün,
- Regressionstests für behobene Fehler,
- keine unnötigen neuen Abhängigkeiten,
- keine Abschwächung bestehender PDF-, XML- oder EN-16931-Prüfungen,
- manuelle Benutzereingaben bleiben geschützt,
- Original-PDF bleibt unverändert,
- externe Validatoren bleiben Entwicklungs- und Releasewerkzeuge,
- ausgelieferte Anwendung benötigt weiterhin weder Java noch .NET SDK,
- KISS bleibt verbindlich.

## Release-Gate 0.2.0

Version 0.2.0 darf erst veröffentlicht werden, wenn:

- alle als Pflicht markierten Anforderungen erfüllt sind,
- vollständige CI grün ist,
- externe Referenzprüfungen bestanden sind,
- Windows-Abnahme bestanden ist,
- Upgrade 0.1.0 → 0.2.0 bestanden ist,
- Same-Version-Test bestanden ist,
- Downgrade-Verhalten geprüft ist,
- Firmendaten nach Upgrade erhalten bleiben,
- MSI und portable ZIP funktionieren,
- `SHA256SUMS.txt` zu den tatsächlich veröffentlichten Artefakten passt,
- README und weitere Dokumentation dem tatsächlichen Funktionsstand entsprechen.

Der bestehende Tag `v0.1.0` bleibt unverändert.

Erst nach vollständiger Abnahme wird `v0.2.0` erzeugt.

## Geplante Bearbeitungsreihenfolge

1. `ER-020-INS-01` – Installer-Upgrade
2. `ER-020-VER-01` – zentrale Versionsführung
3. `ER-020-LOG-01` – Diagnoselogging
4. `ER-020-SET-01` – Firmendaten-Onboarding
5. `ER-020-BUY-01` bis `ER-020-BUY-03` – Käufererkennung
6. `ER-020-POS-01` – Positionserkennung
7. `ER-020-SIGN-01` – Signing, sofern verfügbar
8. vollständige Abnahme
9. Dokumentation und Release

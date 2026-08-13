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

## ER-020-INS-02 – Desktopverknüpfung optional anbieten

### Problem / Grund

BorstWerk E-Rechnung legt bereits einen Startmenüeintrag an. Eine Desktopverknüpfung ist im Installer technisch vorbereitet, wird im normalen Installationsablauf jedoch nicht verständlich als Benutzeroption angeboten.

Anwender sollen während der Installation selbst entscheiden können, ob zusätzlich eine Desktopverknüpfung angelegt wird.

### Anforderung

Der Installer soll eine verständliche Option „Desktop-Verknüpfung erstellen“ anbieten.

Die Option soll standardmäßig aktiviert sein, aber vom Benutzer abgewählt werden können.

### Akzeptanzkriterien

- Die Installation legt immer den bestehenden Startmenüeintrag an.
- Die Desktopverknüpfung ist eine getrennte Benutzeroption.
- Die Option ist bei einer normalen Erstinstallation standardmäßig aktiviert.
- Wird die Option abgewählt, wird keine Desktopverknüpfung angelegt.
- Wird sie gewählt, verweist die Verknüpfung auf die installierte `EInvoiceSender.exe`.
- Produktname und Programmsymbol entsprechen BorstWerk E-Rechnung.
- Upgrade von 0.1.0 beziehungsweise bestehender 0.2.0-Testinstallation erzeugt keine doppelten Desktopverknüpfungen.
- Repair erzeugt keine doppelten Desktopverknüpfungen.
- Deinstallation entfernt eine vom Installer angelegte Desktopverknüpfung.
- Benutzerdaten unter `%LOCALAPPDATA%\EInvoiceSender` bleiben davon unberührt.
- Per-User- und Per-Machine-Installation dürfen durch die Änderung nicht beschädigt werden.
- Es wird keine eigene Shortcut-Verwaltung in der Anwendung gebaut, wenn Windows Installer/WiX dies regulär lösen kann.

### Nachweis

Noch festzulegen:

- automatisierte WiX-/Strukturprüfung,
- Windows-Erstinstallation mit aktivierter Option,
- Windows-Erstinstallation mit abgewählter Option,
- Repair-Test,
- Upgrade-Test,
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

## ER-020-LIC-01 – Drittanbieterhinweise konsistent halten

### Problem / Grund

Die aktuellen Entwicklungsunterlagen und Paketdefinitionen nennen die tatsächlich verwendeten Bibliotheken. Die ausgelieferten Drittanbieterhinweise sind dagegen nicht vollständig synchron: Sie nennen noch Serilog und `Microsoft.Extensions.Hosting`, obwohl diese nicht mehr Bestandteil der Anwendung sind, und lassen unter anderem PdfPig aus.

Drittanbieterhinweise sind Bestandteil der Auslieferung und dürfen nicht von der tatsächlichen Release-Zusammenstellung abweichen.

### Anforderung

Die mit Installer und portabler Fassung ausgelieferten Drittanbieterhinweise müssen den tatsächlich enthaltenen Fremdkomponenten entsprechen und untereinander konsistent sein.

### Akzeptanzkriterien

- PdfPig wird in den ausgelieferten Hinweisen aufgeführt, solange es Bestandteil der Anwendung ist.
- Nicht ausgelieferte Serilog-Komponenten werden nicht aufgeführt.
- `Microsoft.Extensions.Hosting` wird nicht als ausgelieferte Abhängigkeit genannt, solange es nicht tatsächlich enthalten ist.
- Die verwendeten `Microsoft.Extensions.*`-Pakete werden sachlich korrekt bezeichnet.
- Markdown-Hinweise, Installerhinweise und RTF widersprechen sich nicht.
- Die sichtbare Produktbezeichnung lautet BorstWerk E-Rechnung und nicht ein überholter interner Produktname.
- Neue oder entfernte Laufzeitabhängigkeiten müssen zu einer nachvollziehbaren Aktualisierung der Drittanbieterhinweise führen.
- Nach Möglichkeit gibt es eine maßgebliche Quelle oder eine automatisierte Konsistenzprüfung, statt mehrere dauerhaft manuell gepflegte Listen ohne Abgleich.
- Ein vorgesehener Release darf nicht stillschweigend ohne die vorgesehenen Drittanbieterhinweise paketiert werden.

### Nachweis

Noch festzulegen:

- Abgleich gegen Paketdefinitionen und veröffentlichte `deps.json`,
- Prüfung der ausgelieferten Markdown-/RTF-Hinweise,
- Prüfung von MSI und portabler ZIP,
- automatisierte Konsistenzprüfung, sofern mit vertretbarem Aufwand möglich.

## ER-020-REL-01 – Einheitlichen und reproduzierbaren Release-Paketbau herstellen

### Problem / Grund

Der lokale Installer-/Releaseweg und die GitHub-CI paketieren die portable Fassung derzeit nicht identisch. Die CI kopiert Drittanbieterhinweise vor dem ZIP-Bau in den Publish-Ordner, der lokale `Build-Installer.ps1` dagegen nicht.

Zusätzlich wird das lokale Release-Ausgabeverzeichnis vor einem neuen Build nicht vollständig bereinigt. Dadurch können Artefakte älterer Builds im Ordner verbleiben und anschließend sogar in `SHA256SUMS.txt` aufgenommen werden.

### Anforderung

Lokaler Releasebau und CI müssen einen klar definierten, möglichst gemeinsamen Paketierungsweg verwenden und aus einem frischen Zustand ausschließlich die Artefakte des aktuellen Builds erzeugen.

### Akzeptanzkriterien

- Lokaler Releasebau und GitHub-CI verwenden denselben Paketierungsablauf oder nachweislich dieselben relevanten Schritte.
- Das Release-Ausgabeverzeichnis enthält nach einem Build keine Altartefakte aus vorherigen Builds.
- Die portable ZIP enthält die vorgesehenen Drittanbieterhinweise.
- Das MSI enthält die vorgesehenen Drittanbieterhinweise.
- Release-Artefakte werden erst nach erfolgreichem Build und vollständiger Paketierung als fertig betrachtet.
- `SHA256SUMS.txt` wird nach Erzeugung aller endgültigen Artefakte frisch erstellt.
- `SHA256SUMS.txt` enthält ausschließlich Dateien des aktuellen Release-Builds und niemals sich selbst.
- Jede in `SHA256SUMS.txt` aufgeführte Datei existiert und ihre Prüfsumme lässt sich verifizieren.
- Für den regulären 0.2.0-Release besteht der erwartete veröffentlichte Satz aus MSI, portabler ZIP und `SHA256SUMS.txt`, sofern der freigegebene Releaseplan keine weiteren Artefakte vorsieht.
- Der Buildprozess scheitert verständlich, wenn ein verpflichtender Paketbestandteil fehlt, statt ein unvollständiges Release als erfolgreich zu melden.

### Nachweis

Noch festzulegen:

- lokaler Releasebau aus sauberem Zustand,
- zweiter lokaler Releasebau über vorhandenen Buildartefakten zur Prüfung auf Altlasten,
- CI-Artefakte gegen lokalen Build vergleichen,
- Inhalt der portablen ZIP prüfen,
- MSI-Inhalt beziehungsweise installierte Drittanbieterhinweise prüfen,
- `SHA256SUMS.txt` vollständig gegen die erzeugten Dateien verifizieren.

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

## ER-020-DET-01 – Verkäufer ohne Firmenvorlage konservativ erkennen

### Problem / Grund

Ohne vorhandene Firmenvorlage bleiben Verkäuferdaten derzeit leer, obwohl typische Rechnungen Rechnungsaussteller und Rechnungsempfänger ausreichend eindeutig voneinander trennen. Gerade kleine Unternehmen und Einzelunternehmer sollen die eigene Rechnung deshalb als Ausgangspunkt für ihre Unternehmensvorlage nutzen können, ohne Käufer und Verkäufer zu vertauschen oder erkannte Daten automatisch dauerhaft zu speichern.

### Anforderung

Die Anwendung soll einen Verkäufer auch ohne vorhandene Firmenvorlage nur dann vorausfüllen, wenn feste belastbare Signalkombinationen, der Ausschluss von Käufer- und Lieferblöcken sowie eine Eindeutigkeitsprüfung zusammen einen einzigen Seller ergeben. Die erkannten Verkäuferdaten bleiben ihrer PDF-Herkunft zugeordnet. Ein konkretes `DetectedOwnCompanyProposal` enthält ausschließlich die zusammengehörigen Verkäufer- und eindeutig zuordenbaren Bankfelder samt Werten, Confidence und Evidenz.

Mit der ausdrücklichen Aktion „Als meine Unternehmensdaten speichern“ bestätigt der Benutzer gleichzeitig, dass der vorgeschlagene Seller das eigene Unternehmen ist, und stößt die bestehende lokale Speicherung als Unternehmensvorlage an. Eine Ablehnung verwirft nur das Angebot und verändert weder den aktuellen Rechnungsentwurf noch den lokalen Store.

### Akzeptanzkriterien

- Es gibt keine automatische Speicherung und kein stilles Lernen aus Rechnungen.
- `FieldOrigin` bleibt unverändert; erkannte Werte werden nicht zu manuellen Werten umetikettiert.
- Ohne Firmenvorlage wird ein eindeutiger Seller aus einer typischen Rechnung mit getrenntem Käuferblock sinnvoll vorausgefüllt.
- Die Erkennung verwendet keine erste-Adresse-Regel und keine reine Punktesumme aus schwachen Signalen.
- Käufer- und Lieferblöcke sind als Sellerquelle ausgeschlossen.
- Bei mehreren gleich belastbaren Seller-Kandidaten bleibt der Seller leer.
- Einzelunternehmer ohne Rechtsform können bei einer belastbaren Signalkombination erkannt werden.
- Zweispaltige Layouts sowie Käuferblöcke oben oder links führen nicht zur Vertauschung von Seller und Buyer.
- Verkäufer- und Käufer-USt-IdNr. beziehungsweise Steuernummer werden nicht vertauscht.
- Bankdaten identifizieren keinen Seller. Sie dürfen nur einem bereits eindeutig erkannten Seller-Proposal hinzugefügt werden.
- Bei mehreren gültigen IBANs wird keine eigene Bankverbindung automatisch vorgeschlagen.
- Das Proposal enthält nur die konkret erkannten und bestätigbaren Allowlist-Felder mit ihrem unveränderten Wert, ihrer Confidence und ihrer Evidenz.
- Die Speicheraktion übernimmt weiterhin manuelle Allowlist-Felder und zusätzlich unveränderte Felder des aktuellen Proposal.
- Beliebige andere erkannte Werte, Käuferdaten, nicht zum Proposal gehörende Bankdaten und gegenüber dem Proposal veränderte erkannte Werte werden nicht gespeichert.
- „Nicht als eigene Daten speichern“ verursacht keinen Store-Schreibzugriff und löscht keine erkannten Werte aus dem Rechnungsentwurf.
- Nach erfolgreicher ausdrücklicher Speicherung verwendet eine neue Rechnung die gespeicherte Unternehmensvorlage.
- Es werden weder eine zusätzliche Herkunftsstufe noch eine neue Layout- oder Installertechnologie eingeführt.

### Nachweis

- automatisierte Regression für eine typische Rechnung ohne Firmenvorlage mit eindeutigem Seller und Käuferblock,
- Tests für eindeutigen Seller, Einzelunternehmer, zweispaltiges Layout, Käufer oben/links und Lieferanschrift,
- Negativtest mit zwei gleich starken Firmen,
- Tests gegen Vertauschung von Seller-/Buyer-Steuermerkmalen und gegen mehrere IBANs,
- Tests für die Proposal-Allowlist, unveränderte PDF-Herkünfte, ausdrückliche Speicherung und Ablehnung ohne Schreibzugriff,
- Test, dass eine neue Rechnung anschließend die gespeicherte Vorlage verwendet,
- Windows-UI-Abnahme der Aktionen zum Speichern und Ablehnen.

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

Große Klassen oder Dateien allein sind kein Änderungsgrund. Refactorings brauchen einen konkreten Nutzen für Wartbarkeit, Testbarkeit oder Risikoreduktion und dürfen fachliche Änderungen nicht unnötig vergrößern.

## Release-Gate 0.2.0

Version 0.2.0 darf erst veröffentlicht werden, wenn:

- alle als Pflicht markierten Anforderungen erfüllt sind,
- vollständige CI grün ist,
- externe Referenzprüfungen bestanden sind,
- die für 0.2.0 relevanten Punkte aus [`RELEASE-CHECKLIST.md`](RELEASE-CHECKLIST.md) bestanden sind,
- Upgrade 0.1.0 → 0.2.0 bestanden ist,
- Same-Version-Test bestanden ist,
- Downgrade-Verhalten geprüft ist,
- Firmendaten nach Upgrade erhalten bleiben,
- MSI und portable ZIP funktionieren,
- Drittanbieterhinweise zu den tatsächlich ausgelieferten Komponenten passen,
- `SHA256SUMS.txt` zu den tatsächlich veröffentlichten Artefakten passt,
- README und weitere Dokumentation dem tatsächlichen Funktionsstand entsprechen.

Der bestehende Tag `v0.1.0` bleibt unverändert.

Erst nach vollständiger Abnahme wird `v0.2.0` erzeugt.

## Geplante Bearbeitungsreihenfolge

1. `ER-020-INS-01` – Installer-Upgrade
2. `ER-020-VER-01` – zentrale Versionsführung
3. `ER-020-LIC-01` – Drittanbieterhinweise
4. `ER-020-REL-01` – einheitlicher Release- und Paketbau
5. `ER-020-LOG-01` – Diagnoselogging
6. `ER-020-SET-01` – Firmendaten-Onboarding
7. `ER-020-BUY-01` bis `ER-020-BUY-03` – Käufererkennung
8. `ER-020-POS-01` – Positionserkennung
9. `ER-020-SIGN-01` – Signing, sofern verfügbar
10. vollständige Release-Abnahme
11. Dokumentation und Release

# Offene Punkte

Nur echte, noch nicht erledigte Aufgaben. Abgeschlossenes gehört nicht
hierher, sondern in die Commit-Historie.

## Funktion

- **Positionserkennung aus Tabellen.** Die PDF-Erkennung liest Kopfdaten,
  Parteien, Bankangaben und Summen, aber keine Rechnungspositionen. Die
  Tabellenstruktur ist zwischen Rechnungsvorlagen zu uneinheitlich, um sie mit
  den bisherigen zeilenbasierten Regeln zuverlässig zu treffen. Dafür braucht
  es Spaltenerkennung über die Wortpositionen, die der Extraktor bereits
  liefert.
- **Käuferangaben weiter erkennen.** USt-IdNr., E-Mail und Land des Käufers
  werden noch nicht gelesen. Das Land ist der wichtigste davon: Es steuert
  unter anderem, ob eine innergemeinschaftliche Lieferung vorliegt.

## Code

- **Wiederholung im Eingabeformular auflösen.** Die Karten für Verkäufer und
  Käufer haben denselben Aufbau, unterscheiden sich aber in jedem
  Bindungspfad. Sauber wäre ein gemeinsames `PartyDraft` im Entwurf, an das
  beide Karten mit einer gemeinsamen Vorlage binden - dann verschwinden rund
  200 Zeilen XAML. Der Umbau greift in `InvoiceDraft`, in die Vorbefüllung und
  in die Regelprüfung; er gehört nicht in eine Fehlerbehebung.
- **Herkunftshinweis als eigenes Bedienelement.** Beschriftung, Feld und
  Hinweis stehen heute je Feld ausgeschrieben da. Ein kleines
  ContentControl mit einer Vorlage würde das zusammenfassen. Zurückgestellt,
  weil eine fehlerhafte ControlTemplate erst zur Laufzeit auffällt und die
  Oberfläche hier nicht ausgeführt werden kann.
- **InvoiceDraft verkleinern.** Mit 592 Zeilen die größte Klasse des Kerns.
  Die Umwandlung in das Domänenmodell (`TryBuildInvoice` und die
  Bau-Hilfsmethoden, rund 300 Zeilen) gehört in eine eigene Klasse. Ein erster
  Versuch wurde zurückgenommen: Die mechanische Umschreibung der 55
  unqualifizierten Feldzugriffe veränderte Zeichenketten - unter anderem die
  FieldPath-Angaben der Befunde - und Namen in Objektinitialisierern. Der
  Umbau braucht einen werkzeuggestützten Rename, keine Textersetzung.

## Nur auf einem Windows-Rechner prüfbar

- **Durchlauf durch alle fünf Schritte** mit einer echten PDF: Dateidialog,
  Drag-and-drop, PDF-Vorschau, Erkennungsübersicht, Feldkennzeichnung,
  Summenabgleich.
- **„Neue Rechnung“ im laufenden Programm:** Nach einem vollständigen
  Durchlauf muss das Formular leer sein, die eigene Firma aber wieder
  dastehen. Der Ablauf ist automatisiert geprüft – am Entwurf zur Laufzeit,
  an der Verdrahtung im Quelltext –, aber nicht am laufenden Fenster.
- **Einstellungen ändern und schließen:** Die neuen Vorgaben müssen ohne
  Neustart im nächsten Vorgang stehen, ein bereits ausgefülltes Formular
  dagegen unberührt bleiben. Ist Schritt 2 schon offen, erscheint stattdessen
  die Rückfrage über dem Formular; „Für diese Rechnung übernehmen“ aktualisiert
  ausschließlich die Verkäufer- und Bankangaben.
- **Summen während der Eingabe** (die Positionstabelle lässt sich ohne
  laufendes Fenster nicht bedienen):

  1. Drei Positionen eintragen – 4 × 85,00, 2 × 95,00, 1 × 70,00, je 19 %.
  2. Nach dem Verlassen der letzten Zelle müssen die Summen von selbst auf
     600,00 / 114,00 / 714,00 / 714,00 stehen.
  3. Den Steuersatz der letzten Position ändern und **ohne** die Zelle zu
     verlassen auf „Summen neu berechnen“ klicken: Die Summen müssen sofort
     zum neuen Satz passen.
  4. Dasselbe mit Menge und Einzelpreis als zuletzt bearbeiteter Zelle.
  5. „Weiter“ darf an einer bereits angezeigten Summe nichts mehr ändern.
- **Installer-Abnahme auf einem sauberen Windows 11 x64** – ohne .NET SDK,
  ohne Java, ohne Mustang, ohne veraPDF. Der vollständige Ablauf muss laufen,
  ohne dass irgendeine Laufzeit nachinstalliert werden muss:

  1. MSI installieren
  2. Anwendung starten
  3. PDF laden
  4. Rechnung erfassen
  5. ZUGFeRD-PDF erzeugen
  6. speichern
  7. E-Mail-Entwurf erzeugen
  8. deinstallieren

  Automatisiert vorbereitet ist das, soweit es ohne Windows geht: Die
  ausgelieferte Zusammenstellung trägt keinen externen Validator ein
  (`ProductionWithoutJavaTests`), und der vollständige Ablauf ohne jeden
  Validator ist als Ende-zu-Ende-Test abgedeckt
  (`OhneJedenExternenValidatorEntstehtEineVollständigeDatei`).
- **Installer:** MSI bauen, aktualisieren, deinstallieren.
- **`.eml` im klassischen und im neuen Outlook** praktisch öffnen.
- **DPAPI-Schutz der IBAN** in den Einstellungen.

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

## Vor der nächsten Freigabe

- **Sollfassungen erneut gegen das CEN-Schematron prüfen.** Mit der Umstellung
  auf echte Umlaute hat sich der deutsche Text in den Testdaten geändert
  (Musterstraße, März, Gebäude) und damit auch die abgelegten Sollfassungen.
  Der Aufbau der XML ist unverändert, die externe Gegenprüfung ist damit aber
  nicht mehr die zuletzt bestätigte:
  `build/validate-golden-masters.sh`.

## Nur auf einem Windows-Rechner prüfbar

- **Durchlauf durch alle fünf Schritte** mit einer echten PDF: Dateidialog,
  Drag-and-drop, PDF-Vorschau, Erkennungsübersicht, Feldkennzeichnung,
  Summenabgleich.
- **Installer:** MSI bauen, installieren, aktualisieren, deinstallieren.
- **`.eml` im klassischen und im neuen Outlook** praktisch öffnen.
- **DPAPI-Schutz der IBAN** in den Einstellungen.

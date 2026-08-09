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
- **Installer:** MSI bauen, installieren, aktualisieren, deinstallieren.
- **`.eml` im klassischen und im neuen Outlook** praktisch öffnen.
- **DPAPI-Schutz der IBAN** in den Einstellungen.

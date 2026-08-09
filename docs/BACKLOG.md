# Offene Punkte

Nur echte, noch nicht erledigte Aufgaben. Abgeschlossenes gehoert nicht
hierher, sondern in die Commit-Historie.

## Funktion

- **Positionserkennung aus Tabellen.** Die PDF-Erkennung liest Kopfdaten,
  Parteien, Bankangaben und Summen, aber keine Rechnungspositionen. Die
  Tabellenstruktur ist zwischen Rechnungsvorlagen zu uneinheitlich, um sie mit
  den bisherigen zeilenbasierten Regeln zuverlaessig zu treffen. Dafuer braucht
  es Spaltenerkennung ueber die Wortpositionen, die der Extraktor bereits
  liefert.
- **Kaeuferangaben weiter erkennen.** USt-IdNr., E-Mail und Land des Kaeufers
  werden noch nicht gelesen. Das Land ist der wichtigste davon: Es steuert
  unter anderem, ob eine innergemeinschaftliche Lieferung vorliegt.

## Code

- **InvoiceDraft verkleinern.** Mit 592 Zeilen die groesste Klasse des Kerns.
  Die Umwandlung in das Domaenenmodell (`TryBuildInvoice` und die
  Bau-Hilfsmethoden, rund 300 Zeilen) gehoert in eine eigene Klasse. Ein erster
  Versuch wurde zurueckgenommen: Die mechanische Umschreibung der 55
  unqualifizierten Feldzugriffe veraenderte Zeichenketten - unter anderem die
  FieldPath-Angaben der Befunde - und Namen in Objektinitialisierern. Der
  Umbau braucht einen werkzeuggestuetzten Rename, keine Textersetzung.

## Nur auf einem Windows-Rechner pruefbar

- **Durchlauf durch alle fuenf Schritte** mit einer echten PDF: Dateidialog,
  Drag-and-drop, PDF-Vorschau, Erkennungsuebersicht, Feldkennzeichnung,
  Summenabgleich.
- **Installer:** MSI bauen, installieren, aktualisieren, deinstallieren.
- **`.eml` im klassischen und im neuen Outlook** praktisch oeffnen.
- **DPAPI-Schutz der IBAN** in den Einstellungen.

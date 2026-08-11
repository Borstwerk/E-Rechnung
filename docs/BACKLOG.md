# Offene Punkte

Nur echte, noch nicht erledigte Aufgaben. Abgeschlossenes gehört nicht
hierher, sondern in die Commit-Historie.

## Version 0.2.0

- **Lokales Diagnoselogging für die Fehleranalyse.** Fehlerberichte sollen sich
  besser nachvollziehen lassen, ohne Rechnungs- oder Kundendaten zu sammeln.
  Das Logging bleibt vollständig lokal und wird standardmäßig in einem
  begrenzten, rotierenden Satz von Logdateien abgelegt. Erfasst werden dürfen
  insbesondere Programmversion, Workflow-Schritt, gewählter PDF-Verarbeitungsweg,
  technische Prüfresultate, Dauer relevanter Verarbeitungsschritte und
  Ausnahmeinformationen. Rechnungsinhalte, Namen und Anschriften der Beteiligten,
  Rechnungsnummern, IBAN/BIC, E-Mail-Adressen sowie eingebettete XML- oder
  PDF-Inhalte gehören ausdrücklich **nicht** ins Log. Die Oberfläche soll einen
  einfachen Weg anbieten, den Logordner für einen Fehlerbericht zu öffnen.

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

- **Abnahme der sichtbaren Kopie mit dem Windows-Testkoffer.** Der Weg ist
  automatisiert geprüft, bis hin zu veraPDF und dem CEN-Schematron auf der
  gerasterten Datei. Was sich hier nicht prüfen lässt, ist, wie das Ergebnis
  **aussieht** – und darum geht es bei einer sichtbaren Kopie.

  1. `01_Kontrolle_eingebettete_Schriften.pdf` – geht wie bisher den direkten
     Weg, ohne Angebot einer Kopie. Der Prüfbericht nennt „Direkte Übernahme
     der Originalseiten“.
  2. `02_Nicht_eingebettete_Schrift.pdf` – „Weiter“ ist zunächst gesperrt, das
     Angebot erscheint als Warnung und nicht als Fehler.
  3. Ohne Zustimmung lässt sich Schritt 1 nicht verlassen.
  4. Nach „Mit visueller Kopie fortfahren“ steht dort „Visuelle Kopie wird
     verwendet“, und „Weiter“ ist frei.
  5. Die Datenerkennung hat vorher auf dem **Original** gelaufen: Die
     Erkennungsübersicht zeigt Werte, obwohl die Kopie später keinen Text mehr
     enthält.
  6. Die erzeugte Datei öffnen und die Seiten mit dem Original vergleichen:
     nichts beschnitten, nichts verschoben, Schrift bei 100 % lesbar.
  7. Im Ergebnis lässt sich kein Text markieren – das ist der erwartete Preis.
  8. Der Prüfbericht nennt „Sichtbare PDF/A-Kopie (Raster-Fallback, 300 dpi)“.
  9. `03_QR_Code_300dpi_Kandidat.pdf` – den QR-Code im **Ergebnis** mit einem
     Mobiltelefon einlesen. Er muss denselben Inhalt liefern wie der im
     Original.
  10. `04_Mehrseitig_gemischte_Orientierung.pdf` – Seitenzahl und
      Seitenausrichtung bleiben erhalten, auch bei gedrehten Seiten.
  11. `05_Scan_only_kein_PDF_Text.pdf` – **kein** Befund zur Schrifteinbettung:
      Die Seite trägt eine Schriftressource, zeichnet damit aber nichts. Die
      Datei geht den direkten Weg. Die Erkennung meldet, dass sie keinen Text
      gefunden hat, und erfindet nichts.
  12. `06_Dicht_10_Seiten.pdf` – Dauer und Dateigröße notieren.
  13. `07_Besitzerkennwort_Rechte_eingeschraenkt.pdf` – wird **schon in
      Schritt 1** abgelehnt, nicht erst beim Erzeugen und nicht stillschweigend
      gerastert. Die Meldung nennt die Berechtigungseinschränkung und
      bezeichnet die Datei **nicht** als beschädigt.
  14. `08_Oeffnungspasswort_test123.pdf` – wird ohne Kennwortabfrage sauber
      abgelehnt. Es erscheint **kein** zusätzliches Störungsfenster mit
      englischem Bibliothekstext; der deutsche Befund in Schritt 1 steht
      allein.
  15. Nach jedem Durchlauf: **Original-PDF unverändert** (Dateigröße und
      Änderungsdatum, im Zweifel die Prüfsumme).

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
- **Installer, Benutzerinstallation** (die Vorgabe):

  1. MSI ohne erhöhte Rechte starten – es darf **keine** Rückfrage von
     Windows kommen.
  2. Anwendung startet.
  3. Die Dateien liegen unter
     `%LOCALAPPDATA%\Programs\BorstWerk\BorstWerk E-Rechnung`.
  4. Startmenüeintrag vorhanden, Desktopverknüpfung nur bei Auswahl.
  5. Aktualisierung derselben Installation mit gleicher und höherer Version.
  6. Deinstallation entfernt Programm und Verknüpfungen; die Daten unter
     `%LOCALAPPDATA%\EInvoiceSender` bleiben erhalten. Dieser Ordner behält
     seinen alten Namen – er wurde mit der Umbenennung auf BorstWerk
     bewusst nicht mit umgezogen, weil sonst vorhandene Firmenvorlagen nach
     einer Aktualisierung verschwunden wären.
  7. Symbol prüfen: in der Taskleiste, im Startmenü, im Explorer und im
     Eintrag unter „Apps und Features“.

- **Installer, Installation für alle Benutzer** (der zweite Weg desselben
  Pakets): `msiexec /i BorstWerk-E-Rechnung-Setup.msi MSIINSTALLPERUSER=""` – hier
  ist die Rückfrage von Windows erwartet, und die Dateien landen unter
  `C:\Program Files`.

- **Der Bau selbst** muss aus einer gewöhnlichen PowerShell-Sitzung laufen,
  ohne erhöhte Rechte, und die MSI-Prüfung fehlerfrei durchlaufen:
  `.\build\Build-Installer.ps1`.
- **Sichtprüfung der BorstWerk-Oberfläche** (WPF lässt sich nur unter Windows
  ausführen; automatisiert geprüft sind Farbwerte, Kontraste, Fokusstile und
  Ressourcenschlüssel, nicht ihr Aussehen):

  1. Skalierung 100 %, 150 % und 200 % – der Ablauf muss in allen dreien
     vollständig bedienbar bleiben.
  2. Kleinste zulässige Fenstergröße (940 × 640): Kopf, Schritt, Statuszeile
     und Navigation dürfen sich nicht überlagern.
  3. Mit der Tabulatortaste durch jeden Schritt gehen: Der Fokus muss überall
     sichtbar sein – auch auf der eingefärbten Schaltfläche „Weiter“.
  4. Zugriffstasten prüfen: Alt+W, Alt+Z, Alt+R, Alt+E, Alt+Ü.
  5. Alle vier Statusarten ansehen – Fehler, Warnung, Hinweis, Erfolg. Jede
     muss Zeichen, Wort und Farbe zeigen.
  6. Einstellungen und „Über“ öffnen.
  7. Fenstersymbol, Taskleistensymbol und Startmenüeintrag.

- **Fassungsnummer im Info-Bereich:** Sie wird aus der Programmdatei gelesen.
  Am gebauten Installationspaket prüfen, dass dort nicht „unbekannt“ steht.

- **`.eml` im klassischen und im neuen Outlook** praktisch öffnen.
- **DPAPI-Schutz der IBAN** in den Einstellungen.

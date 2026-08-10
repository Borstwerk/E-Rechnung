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
  Pakets): `msiexec /i EInvoiceSender-Setup.msi MSIINSTALLPERUSER=""` – hier
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

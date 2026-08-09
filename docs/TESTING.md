# Tests

Zwei Testprojekte, klare Aufgabenteilung. Beide laufen auf jedem System, auch
ohne Windows – der Kern ist bewusst plattformneutral.

| Projekt | Tests | Schwerpunkt |
|---|---|---|
| `tests/EInvoiceSender.Core.Tests` | 433 | Werttypen, Berechnung, Regelwerk, Codelisten, CII-Writer und -Reader, Golden Master, E-Mail-Entwurf, Dateinamen, Eingabeformular, Quelltextregeln der Oberflaeche |
| `tests/EInvoiceSender.IntegrationTests` | 48 | Gesamtablauf, PDF/A-3, Einbettung und Rueckextraktion, externe Gegenpruefung, sichere XML-Verarbeitung, Prozess-Zeitlimit, atomare Speicherung |
| **Summe** | **481** | |

## Ebenen

### Unit-Tests

Werttypen mit Selbstpruefung (IBAN nach ISO 7064, Waehrung, Land, Einheit),
der Berechnungskern (BR-CO-10 bis BR-CO-25, BR-S-08/09, BR-DEC-09…17),
`SafeFileName` gegen Path Traversal und reservierte Windows-Namen sowie das
Regelwerk EN 16931. Alles ohne externe Werkzeuge.

### Golden Master

`Zugferd/GoldenMasterTests.cs` erzeugt fuer jedes Szenario die CII-XML und
vergleicht sie **byte-genau** mit der abgelegten Sollfassung unter
`tests/EInvoiceSender.Core.Tests/GoldenMasters`. Ein zweiter Test stellt
sicher, dass zwei Laeufe desselben Szenarios identische Ausgabe liefern –
Voraussetzung fuer nachvollziehbare Pruefsummen.

Sollfassungen werden **nur nach bewusster Aenderung** neu erzeugt:

```bash
UPDATE_GOLDEN_MASTERS=1 dotnet test tests/EInvoiceSender.Core.Tests
```

Danach ist die Gegenpruefung mit dem Schematron zwingend erneut auszufuehren.
Ein Golden Master, den nur das Programm selbst akzeptiert, belegt nichts.

### Ende-zu-Ende und Referenzpruefung

Die Integrationstests pruefen den kompletten Weg und lassen das Ergebnis von
den **echten Referenzwerkzeugen** gegenlesen: CEN-Schematron und veraPDF ueber
die Mustangproject-CLI. Geprueft werden unter anderem Schematron- und
PDF/A-Ergebnis, Rueckextraktion der eingebetteten XML mit Byte-Gleichheit,
Anhangname `factur-x.xml`, MIME-Typ, `/AFRelationship`, Profilkennung und XMP.

### Quelltextregeln der Oberflaeche

`Core.Tests/App/` prueft die WPF-Anwendung als **Text**. Das klingt ungewoehnlich
und hat einen konkreten Grund: Die ViewModels liegen im WPF-Projekt, und ein
Testprojekt mit Verweis darauf liefe nur unter Windows. Als Textpruefung
laufen diese Regeln bei **jedem** Lauf.

Drei Regeln, alle drei aus Fehlern entstanden, die im laufenden Programm
auftraten und die kein bestehender Test finden konnte:

1. **Kein `ConfigureAwait(false)` in der Oberflaeche.** Sonst laeuft die
   Fortsetzung nach dem `await` auf einem Threadpool-Thread und WPF bricht beim
   naechsten Zugriff auf ein gebundenes Bedienelement ab. Ein Laufzeittest kann
   das nicht finden: Ein Testlauf hat keinen Synchronisierungskontext, und ohne
   Kontext verhalten sich `ConfigureAwait(true)` und `ConfigureAwait(false)`
   gleich.
2. **Jede Eigenschaft, die eine Freigabepruefung liest, benachrichtigt ihren
   Befehl.** Sonst bleibt die Schaltflaeche im zuletzt bewerteten Zustand
   haengen. Auch das findet kein gewoehnlicher Test: `CanExecute` wertet die
   Bedingung bei jedem Aufruf frisch aus und liefert deshalb immer die richtige
   Antwort – auch dann, wenn der Knopf auf dem Bildschirm falsch aussieht.
   Geprueft werden muss die Verdrahtung, nicht der Wert.
3. **Die Anzeigevorlage fuer Befunde passt zum ViewModel.** Fehlt sie, zeigt WPF
   den Klassennamen; ist ein Bindungspfad falsch geschrieben, bleibt das Feld
   leer. Beides meldet WPF nur im Ausgabefenster des Debuggers oder gar nicht.

Beim ersten Lauf hat Regel 2 sofort einen echten Fehler in neu geschriebenem
Code gefunden.

### Datenerkennung

Die Tests der PDF-Datenerkennung pruefen ueberwiegend, was **nicht** erkannt
werden darf. Das ist Absicht: Ein leeres Feld kostet ein paar Tastenanschlaege,
ein falsch gefuelltes Feld, das jemand uebersieht, kostet eine fehlerhafte
Rechnung.

Ausdruecklich abgedeckte Fehlzuordnungen:

- eine Telefon- oder Kundennummer wird nicht zur Rechnungsnummer,
- eine Postleitzahl wird nicht als Betrag gelesen,
- ein Rabatt- oder Skontoprozentsatz wird nicht zum Steuersatz,
- der Prozentwert aus "Umsatzsteuer 19 % 190,00" wird nicht zum Steuerbetrag,
- eine IBAN mit falscher Pruefsumme wird verworfen, nicht uebernommen,
- ein unmoegliches Datum wie 32.13.2026 wird verworfen,
- die eigene Firma wird nicht zum Kaeufer,
- ohne belastbares Signal wird kein Verkaeufer geraten,
- unsichere Werte und Positionen fuellen das Formular nicht aus,
- eine PDF ohne Text bleibt von Hand erfassbar.

Die Testvorgaben entstehen mit `TextPdfBuilder`, der PDF-Dateien mit echtem,
maschinenlesbarem Text erzeugt. Er baut die Datei von Hand mit der
Standardschrift Helvetica – so braucht der Build-Agent keine Schriftausstattung
und es kommt keine Schriftdatei fremder Lizenz ins Repository. Fuer die
Textextraktion ist eine nicht eingebettete Schrift gleichgueltig, und diese
Dateien durchlaufen die PDF/A-Aufwertung nie.

## Externe Validatoren sind das Freigabegate

**Eine positive oberste Zusammenfassung genuegt nie.** Mustang meldet je
Teilpruefung eine eigene `<summary>`. Die oberste kann `valid` lauten, obwohl
die PDF/A-Pruefung im Detail fehlgeschlagen ist – genau dieser Fall ist
waehrend der Entwicklung aufgetreten. Deshalb bewertet `MustangValidator`
**jede** Teilzusammenfassung einzeln, und ein unlesbarer oder leerer Bericht
gilt als Fehler. Andernfalls saehe ein abgestuerztes Werkzeug wie eine
bestandene Pruefung aus.

Die eigene Regelpruefung ist ausdruecklich **kein Ersatz**. Sie meldet Fehler
frueh und verstaendlich; die Freigabe erteilen Schematron und veraPDF.

## REQUIRE_EXTERNAL_VALIDATORS

Fehlt die Mustang-JAR unter `tools/mustang/`:

- **Oertlich:** Die betroffenen Tests werden uebersprungen, damit ein frisch
  geklontes Repository nicht sofort rot ist.
- **Mit `REQUIRE_EXTERNAL_VALIDATORS=1`** (so laeuft die Pipeline): Sie
  **scheitern**. In einer Freigabe darf das Pruefgate nicht stillschweigend
  entfallen.

```bash
./build/fetch-validators.sh
REQUIRE_EXTERNAL_VALIDATORS=1 dotnet test EInvoiceSender.sln -c Release
```

Unter Windows:

```powershell
.\build\Test.ps1 -RequireExternalValidators
.\build\Validate-Reference.ps1
```

## Was nicht automatisiert geprueft ist

- Das Zusammenspiel mit echten Fenstern, Dateidialogen, Drag-and-drop und der
  PDF-Vorschau zur Laufzeit.
- Der Installer: Neuinstallation, Startmenue, Upgrade, Deinstallation.
- Das Verhalten der `.eml`-Datei im klassischen und im neuen Outlook.
- Der DPAPI-Schutz der IBAN, der nur unter Windows greift.

Diese Punkte brauchen einen echten Windows-Rechner und einen Menschen davor.

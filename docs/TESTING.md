# Tests

Zwei Testprojekte, klare Aufgabenteilung. Beide laufen auf jedem System, auch
ohne Windows – der Kern ist bewusst plattformneutral.

| Projekt | Tests | Schwerpunkt |
|---|---|---|
| `tests/EInvoiceSender.Core.Tests` | 433 | Werttypen, Berechnung, Regelwerk, Codelisten, CII-Writer und -Reader, Golden Master, E-Mail-Entwurf, Dateinamen, Eingabeformular, Quelltextregeln der Oberfläche |
| `tests/EInvoiceSender.IntegrationTests` | 48 | Gesamtablauf, PDF/A-3, Einbettung und Rückextraktion, externe Gegenprüfung, sichere XML-Verarbeitung, Prozess-Zeitlimit, atomare Speicherung |
| **Summe** | **481** | |

## Ebenen

### Unit-Tests

Werttypen mit Selbstprüfung (IBAN nach ISO 7064, Währung, Land, Einheit),
der Berechnungskern (BR-CO-10 bis BR-CO-25, BR-S-08/09, BR-DEC-09…17),
`SafeFileName` gegen Path Traversal und reservierte Windows-Namen sowie das
Regelwerk EN 16931. Alles ohne externe Werkzeuge.

### Golden Master

`Zugferd/GoldenMasterTests.cs` erzeugt für jedes Szenario die CII-XML und
vergleicht sie **byte-genau** mit der abgelegten Sollfassung unter
`tests/EInvoiceSender.Core.Tests/GoldenMasters`. Ein zweiter Test stellt
sicher, dass zwei Läufe desselben Szenarios identische Ausgabe liefern –
Voraussetzung für nachvollziehbare Prüfsummen.

Sollfassungen werden **nur nach bewusster Änderung** neu erzeugt:

```bash
UPDATE_GOLDEN_MASTERS=1 dotnet test tests/EInvoiceSender.Core.Tests
```

Danach ist die Gegenprüfung mit dem Schematron zwingend erneut auszuführen.
Ein Golden Master, den nur das Programm selbst akzeptiert, belegt nichts.

### Ende-zu-Ende und Referenzprüfung

Die Integrationstests prüfen den kompletten Weg und lassen das Ergebnis von
den **echten Referenzwerkzeugen** gegenlesen: CEN-Schematron und veraPDF über
die Mustangproject-CLI. Geprüft werden unter anderem Schematron- und
PDF/A-Ergebnis, Rückextraktion der eingebetteten XML mit Byte-Gleichheit,
Anhangname `factur-x.xml`, MIME-Typ, `/AFRelationship`, Profilkennung und XMP.

### Quelltextregeln der Oberfläche

`Core.Tests/App/` prüft die WPF-Anwendung als **Text**. Das klingt ungewöhnlich
und hat einen konkreten Grund: Die ViewModels liegen im WPF-Projekt, und ein
Testprojekt mit Verweis darauf liefe nur unter Windows. Als Textprüfung
laufen diese Regeln bei **jedem** Lauf.

Drei Regeln, alle drei aus Fehlern entstanden, die im laufenden Programm
auftraten und die kein bestehender Test finden konnte:

1. **Kein `ConfigureAwait(false)` in der Oberfläche.** Sonst läuft die
   Fortsetzung nach dem `await` auf einem Threadpool-Thread und WPF bricht beim
   nächsten Zugriff auf ein gebundenes Bedienelement ab. Ein Laufzeittest kann
   das nicht finden: Ein Testlauf hat keinen Synchronisierungskontext, und ohne
   Kontext verhalten sich `ConfigureAwait(true)` und `ConfigureAwait(false)`
   gleich.
2. **Jede Eigenschaft, die eine Freigabeprüfung liest, benachrichtigt ihren
   Befehl.** Sonst bleibt die Schaltfläche im zuletzt bewerteten Zustand
   hängen. Auch das findet kein gewöhnlicher Test: `CanExecute` wertet die
   Bedingung bei jedem Aufruf frisch aus und liefert deshalb immer die richtige
   Antwort – auch dann, wenn der Knopf auf dem Bildschirm falsch aussieht.
   Geprüft werden muss die Verdrahtung, nicht der Wert.
3. **Die Anzeigevorlage für Befunde passt zum ViewModel.** Fehlt sie, zeigt WPF
   den Klassennamen; ist ein Bindungspfad falsch geschrieben, bleibt das Feld
   leer. Beides meldet WPF nur im Ausgabefenster des Debuggers oder gar nicht.

Beim ersten Lauf hat Regel 2 sofort einen echten Fehler in neu geschriebenem
Code gefunden.

### Datenerkennung

Die Tests der PDF-Datenerkennung prüfen überwiegend, was **nicht** erkannt
werden darf. Das ist Absicht: Ein leeres Feld kostet ein paar Tastenanschläge,
ein falsch gefülltes Feld, das jemand übersieht, kostet eine fehlerhafte
Rechnung.

Ausdrücklich abgedeckte Fehlzuordnungen:

- eine Telefon- oder Kundennummer wird nicht zur Rechnungsnummer,
- eine Postleitzahl wird nicht als Betrag gelesen,
- ein Rabatt- oder Skontoprozentsatz wird nicht zum Steuersatz,
- der Prozentwert aus "Umsatzsteuer 19 % 190,00" wird nicht zum Steuerbetrag,
- eine IBAN mit falscher Prüfsumme wird verworfen, nicht übernommen,
- ein unmögliches Datum wie 32.13.2026 wird verworfen,
- die eigene Firma wird nicht zum Käufer,
- ohne belastbares Signal wird kein Verkäufer geraten,
- unsichere Werte und Positionen füllen das Formular nicht aus,
- eine PDF ohne Text bleibt von Hand erfassbar.

Die Testvorgaben entstehen mit `TextPdfBuilder`, der PDF-Dateien mit echtem,
maschinenlesbarem Text erzeugt. Er baut die Datei von Hand mit der
Standardschrift Helvetica – so braucht der Build-Agent keine Schriftausstattung
und es kommt keine Schriftdatei fremder Lizenz ins Repository. Für die
Textextraktion ist eine nicht eingebettete Schrift gleichgültig, und diese
Dateien durchlaufen die PDF/A-Aufwertung nie.

## Externe Validatoren sind das Freigabegate

**Eine positive oberste Zusammenfassung genügt nie.** Mustang meldet je
Teilprüfung eine eigene `<summary>`. Die oberste kann `valid` lauten, obwohl
die PDF/A-Prüfung im Detail fehlgeschlagen ist – genau dieser Fall ist
während der Entwicklung aufgetreten. Deshalb bewertet `MustangValidator`
**jede** Teilzusammenfassung einzeln, und ein unlesbarer oder leerer Bericht
gilt als Fehler. Andernfalls sähe ein abgestürztes Werkzeug wie eine
bestandene Prüfung aus.

Die eigene Regelprüfung ist ausdrücklich **kein Ersatz**. Sie meldet Fehler
früh und verständlich; die Freigabe erteilen Schematron und veraPDF.

## REQUIRE_EXTERNAL_VALIDATORS

Fehlt die Mustang-JAR unter `tools/mustang/`:

- **Örtlich:** Die betroffenen Tests werden übersprungen, damit ein frisch
  geklontes Repository nicht sofort rot ist.
- **Mit `REQUIRE_EXTERNAL_VALIDATORS=1`** (so läuft die Pipeline): Sie
  **scheitern**. In einer Freigabe darf das Prüfgate nicht stillschweigend
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

## Was nicht automatisiert geprüft ist

- Das Zusammenspiel mit echten Fenstern, Dateidialogen, Drag-and-drop und der
  PDF-Vorschau zur Laufzeit.
- Der Installer: Neuinstallation, Startmenü, Upgrade, Deinstallation.
- Das Verhalten der `.eml`-Datei im klassischen und im neuen Outlook.
- Der DPAPI-Schutz der IBAN, der nur unter Windows greift.

Diese Punkte brauchen einen echten Windows-Rechner und einen Menschen davor.

## Installer-Metadaten

Der Windows-Bau führt nach dem Erzeugen des MSI automatisch
`build/Test-InstallerMetadata.ps1` aus. Das Skript öffnet die MSI-Datenbank
ausschließlich lesend und vergleicht das gebaute Paket mit den verbindlichen
Projektangaben. Geprüft werden insbesondere:

- ProductVersion und der feste ProductCode der Fassung,
- der fassungsübergreifend stabile UpgradeCode,
- ein gültiger PackageCode,
- die Dual-Purpose-Eigenschaften `ALLUSERS=2` und `MSIINSTALLPERUSER=1`,
- Upgrade- und Downgradezeilen ohne Same-Version-Major-Upgrade,
- die Reihenfolge von `FindRelatedProducts`, `MigrateFeatureStates`,
  `RemoveExistingProducts` und `InstallInitialize`.

Das belegt die Paketmetadaten, ersetzt aber nicht die Windows-Abnahme eines
echten Upgrades. Installationskontext, Apps-&-Features-Einträge, Verknüpfungen
und erhaltene Benutzerdaten werden weiterhin praktisch geprüft.

# Tests

Zwei Testprojekte, klare Aufgabenteilung. Beide laufen auf jedem System, auch
ohne Windows – der Kern ist bewusst plattformneutral.

| Projekt | Tests | Schwerpunkt |
|---|---|---|
| `tests/EInvoiceSender.Core.Tests` | 771 | Werttypen, Berechnung, Regelwerk, Codelisten, CII-Writer und -Reader, Golden Master, E-Mail-Entwurf, Dateinamen, Eingabeformular, Quelltextregeln der Oberfläche |
| `tests/EInvoiceSender.IntegrationTests` | 91 | Gesamtablauf, PDF/A-3, Einbettung und Rückextraktion, externe Gegenprüfung, sichere XML-Verarbeitung, Prozess-Zeitlimit, atomare Speicherung |
| **Summe** | **862** | |

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
- Der Installer zur Laufzeit: Neuinstallation, sichtbare Checkbox,
  Verknüpfungen, Repair, Upgrade und Deinstallation.
- Das Verhalten der `.eml`-Datei im klassischen und im neuen Outlook.
- Der DPAPI-Schutz der IBAN, der nur unter Windows greift.

Diese Punkte brauchen einen echten Windows-Rechner und einen Menschen davor.

## Installer-Metadaten

Der Windows-Bau führt nach dem Erzeugen des MSI automatisch
`build/Test-InstallerMetadata.ps1` aus. Das Skript öffnet die MSI-Datenbank
ausschließlich lesend und vergleicht das gebaute Paket mit den verbindlichen
Projektangaben. Zuvor prüft es an der veröffentlichten EXE und der verwalteten
Anwendungsassembly, dass ProductVersion, FileVersion und AssemblyVersion von
demselben zentralen `VersionPrefix` stammen. Geprüft werden insbesondere:

- ProductVersion und der für diese Fassung fest zugeordnete ProductCode,
- der fassungsübergreifend stabile UpgradeCode,
- ein gültiger PackageCode,
- die Dual-Purpose-Eigenschaften `ALLUSERS=2` und `MSIINSTALLPERUSER=1`,
- Upgrade- und Downgradezeilen ohne Same-Version-Major-Upgrade,
- die getrennten Features für Hauptfunktion und Desktopverknüpfung mit
  Installationslevel 1,
- die standardmäßig aktivierte native Checkbox und ihre ausschließliche
  Zuordnung zum Desktopfeature über `AddLocal` und `Remove`,
- die Dialogpfade, die die Desktopoption nur bei
  `NOT Installed AND NOT WIX_UPGRADE_DETECTED` erreichen,
- die unveränderten Shortcutziele, Symbole und HKCU-KeyPaths,
- das Migrationsbit für den vorhandenen Featurezustand,
- die Reihenfolge von `FindRelatedProducts`, `MigrateFeatureStates`,
  `RemoveExistingProducts` und `InstallInitialize`,
- den Paketbestand des gebauten `EInvoiceSender.deps.json` gegen die
  Runtimetabelle in `docs/THIRD-PARTY-NOTICES.md`,
- die beiden self-contained Runtimepacks und ihre Fassungen,
- `Drittanbieterhinweise.md` sowie die vorgesehenen vollständigen Lizenz- und
  Notice-Texte in der MSI-Dateitabelle,
- den tatsächlichen RTF-Inhalt des Installer-Lizenzdialogs einschließlich
  Produktname und Pflichtkomponenten.

Das belegt die Paketmetadaten, ersetzt aber nicht die Windows-Abnahme eines
echten Upgrades. Installationskontext, Apps-&-Features-Einträge, Verknüpfungen
und erhaltene Benutzerdaten werden weiterhin praktisch geprüft.

Plattformneutrale Quelltextprüfungen sichern zusätzlich ab, dass
`Directory.Build.props` die einzige aktive Produktversionsquelle bleibt, CI
und Buildskripte keinen ProductVersion-Override einführen, jede veröffentlichte
Version genau einen gültigen ProductCode besitzt und die Über-Anzeige ihre
Fassung aus der gebauten Assembly liest. Die Strukturprüfung der
Desktopoption besitzt Negativfälle für einen falschen Installationslevel,
fehlenden Default, Kopplung des Startmenüs, fehlende Upgradebedingung,
abweichende Feature-ControlEvents und eine unzulässige Custom Action.

## Drittanbieterhinweise

`ThirdPartyNoticeTests` sichert die dokumentarische Seite auch auf Linux ab:

- jedes direkte produktive Paket steht mit der zentralen Fassung in der
  technischen Hauptquelle,
- die Anwender-Markdown-Datei und die RTF enthalten die vorgesehenen
  Runtimefamilien,
- veraltete Laufzeitangaben werden abgewiesen,
- der sichtbare Produktname lautet `BorstWerk E-Rechnung`,
- `Files.wxs` nimmt die Anwenderübersicht und die Lizenztexte unabhängig vom
  Publish-Verzeichnis in das MSI auf,
- die CI legt dem portablen Publish dieselben Texte bei.

Die Prüfregeln besitzen eigene Negativtests: Ein neues undokumentiertes Paket,
ein entferntes, aber weiter behauptetes Paket, fehlendes PdfPig und eine erneut
eingetragene veraltete Runtimeangabe müssen jeweils einen Fehler erzeugen. Die
Prüfung erzeugt keine Lizenzzuordnung aus NuGet-Metadaten. Die fachliche
Zuordnung bleibt in `docs/THIRD-PARTY-NOTICES.md`; der Test verhindert nur,
dass sie unbemerkt vom technischen Bestand oder den Anwenderdarstellungen
abweicht.

## Releasepaketierung

`build/Test-ReleasePackaging.ps1` prüft die von lokalem Releasebau und CI
gemeinsam verwendeten PowerShell-Funktionen mit temporären Dateien. Abgedeckt
sind insbesondere:

- vollständige Bereinigung eines schmutzigen Releaseordners,
- Abbruch bei einer gesperrten Datei unter Windows,
- fehlendes MSI und fehlendes ZIP,
- unerwartete zusätzliche Release-Datei,
- vollständige Übernahme der Drittanbieterhinweise und Lizenztexte,
- fehlender Lizenztext,
- exakte ZIP-Struktur ohne zusätzlichen obersten Ordner,
- exakt zwei Prüfsummenzeilen in fester Reihenfolge,
- manipuliertes MSI und manipuliertes ZIP.

`ReleasePackagingTests` verhindert plattformneutral, dass die CI erneut eine
eigene ZIP-, Lizenzkopier-, MSI-Auswahl- oder SHA-Logik erhält. Der reale
`Build-Release.ps1` prüft das portable ZIP bytegenau gegen den vorbereiteten
Publish, den finalen Drei-Dateien-Satz und alle Prüfsummen vor und nach der
Promotion nach `artifacts/release`.

Reproduzierbarkeit bedeutet für Releaseartefakte gleiche Schritte,
Dateiauswahl, Struktur, Namen und Validierungen. Byteidentische MSI- oder
ZIP-Dateien werden nicht verlangt; insbesondere darf WiX pro Bau einen neuen
PackageCode erzeugen.

## Lokales Diagnoselog

`LocalFileLoggerTests` prüft das tatsächliche UTF-8-Dateiformat, das
Ein-MiB-Limit, die Begrenzung abgeschlossener Sitzungen, gesperrte aktive Logs,
parallele Provider sowie nicht beschreibbare Verzeichnisse und einen absichtlich
fehlschlagenden Writer. Kein Fehler des Providers darf beim Aufrufer ankommen.

Der Exception-Test baut Message, Data, InnerException und einen Pfad mit
markanten Geheimwerten. Im Log müssen Exception-Typkette und Methodenstack
stehen; Geheimwerte, Quelldateiname und Zeilennummer dürfen nicht vorkommen.

Der maßgebliche Privacy-Test fährt den echten Erzeugungs- und E-Mail-Weg mit
markanten Werten für Namen, Rechnungsnummer, Anschrift, E-Mail, IBAN/BIC,
USt-ID, Steuernummer, Rechnungsposition, PDF-/XML-Inhalt sowie Ein- und
Ausgabedateinamen. Anschließend liest er sämtliche erzeugten Sitzungslogs und
weist nach, dass keines dieser Merkmale vorkommt. Positive Zusicherungen auf
die Events von Preflight, Speichern, Erzeugung und E-Mail verhindern, dass ein
leeres Log versehentlich als Datenschutzbeleg gilt.

`DiagnosticLoggingSourceTests` ist eine zusätzliche, bewusst nachgeordnete
Schutzschicht: verdächtige Platzhalternamen, rohe Exceptiontexte,
Quelldatei-Stacks, abweichende Pfad-/Grenzdefinitionen und Netzwerk- oder
Uploadbausteine werden abgewiesen. Diese Quellregeln ersetzen den Test der
tatsächlich persistierten Dateien nicht.

## Unternehmensvorlage aus bestätigten Angaben

`CompanyTemplateSavePlannerTests` prüft die feste Allowlist und den Merge gegen
markante Daten. Manuell geänderte Verkäufer- und Bankfelder werden übernommen;
zuverlässig oder unsicher erkannte Werte werden ohne ein exakt passendes
`DetectedOwnCompanyProposal` verworfen. Mit Proposal werden ausschließlich
dessen unveränderte Allowlist-Werte übernommen. Markante Käufer-, Rechnungs-,
Zahlungs- und Positionswerte dürfen weder im Kandidaten noch in der
gespeicherten JSON-Datei erscheinen.

Gemischte Zustände sind ausdrücklich abgedeckt: Bei vorhandener Vorlage und
genau einem manuell geänderten Feld bleibt jedes andere Unternehmensfeld
erhalten. Währung, Zahlungsziel, Zahlungsbedingungen, E-Mail-Vorgaben und
`LastOutputDirectory` werden wertgleich weitergeführt. `Template` und
`TemplateDefault` zählen nicht als Benutzereingabe. Das unberührte
Programmstandardland `DE` wird nur mit Herkunft `Default` zugelassen, nie mit
einer Erkennungsherkunft.

`JsonSettingsStoreRecoveryTests` prüft fehlende und beschädigte
`firmenvorlage.json` sowie den anschließenden ausdrücklichen Schreib- und
Ladevorgang. `CompanyTemplateSaveFlowTests` sichert die WPF-Verdrahtung:
Property-Changes, Reset, Navigation, Erzeugung und Abbruch rufen den neuen
Schreibweg nicht auf. Eine vorhandene Vorlage wird inline bestätigt, ein
identischer Kandidat wird nicht geschrieben und `MainViewModel` synchronisiert
nach Erfolg ausschließlich seinen Vorlagen-Snapshot.

`SellerDetectionTests` bildet die konservativen Mindestkombinationen als echte
PDFs nach. Abgedeckt sind der manuell gefundene Erstnutzerfall, ein
Einzelunternehmer ohne Rechtsform, ein zweispaltiges Layout mit Käufer oben und
links, eine Lieferanschrift, getrennte Seller-/Buyer-USt-IdNrn. sowie ein
ausdrücklicher Seller-Block. Eine bloße erste Adresse, zwei gleich starke Firmen
und mehrere gültige IBANs sind Negativfälle: Seller beziehungsweise eigene
Bankverbindung bleiben dann leer. Proposal-Tests sichern konkrete Werte,
Confidence, Evidenz und unveränderte PDF-Herkünfte.

# TESTING.md – Teststrategie

Stand: 2026-08-05. Beschreibt die Teststrategie, wie sie im Repository
tatsächlich existiert – keine Zielvorstellung.

---

## Überblick: Testprojekte

| Projekt | Tests (Stand 2026-08-05) | Schwerpunkt |
|---|---|---|
| `tests/EInvoiceSender.Domain.Tests` | 187 | Werttypen, Berechnungskern, `SafeFileName` |
| `tests/EInvoiceSender.Formats.Tests` | 40 | CII-XML-Erzeugung, Golden Master, `SecureXml` |
| `tests/EInvoiceSender.Validation.Tests` | 122 | EN-16931-Geschäftsregeln, Codelisten |
| `tests/EInvoiceSender.Mail.Tests` | 10 | `.eml`-Entwurf, Kodierung, Header |
| `tests/EInvoiceSender.Presentation.Tests` | 32 | ViewModel- und Ablauflogik der Oberfläche, Thread-Zugehörigkeit |
| `tests/EInvoiceSender.IntegrationTests` | 48 | Gesamtablauf, PDF/A, externe Gegenprüfung |
| **Summe** | **439** | laut `docs/STATUS.md`, alle grün |

Daneben bestehen `tests/EInvoiceSender.TestSupport` (gemeinsame Testszenarien
und PDF-Fabrik, kein eigenes Testprojekt im Sinn von zählbaren Tests) und
`tests/EInvoiceSender.Application.Tests` (Projektgerüst vorhanden).

---

## Ebenen

### Unit-Tests

Der grösste Teil der 439 Tests: Werttypen mit Selbstprüfung (IBAN nach
ISO 7064, Währung, Land, Einheit), Berechnungskern (`docs/STANDARDS.md`,
Abschnitt 3: BR-CO-10 bis BR-CO-25, BR-S-08/09, BR-DEC-09…17),
`SafeFileName` gegen Path Traversal und reservierte Windows-Namen, sowie die
EN-16931-Geschäftsregeln in `EInvoiceSender.Validation`. Diese Tests laufen
vollständig ohne externe Werkzeuge und auf jedem Build-Agenten, auch unter
Linux (`docs/DECISIONS.md`, ADR-0008).

### Golden-Master-Tests

`tests/EInvoiceSender.Formats.Tests/GoldenMasterTests.cs` erzeugt für jedes
Szenario aus `EInvoiceSender.TestSupport.InvoiceScenarios` die CII-XML und
vergleicht sie byte-genau mit der abgelegten Sollfassung unter
`tests/EInvoiceSender.Formats.Tests/GoldenMasters`. Eine zweite Prüfung
(`ErzeugteXmlIstZwischenLaeufenIdentisch`) stellt sicher, dass zwei Läufe
desselben Szenarios identische Ausgabe liefern – notwendig für nachvollziehbare
Prüfsummen.

Die Sollfassungen werden **nur nach bewusster Änderung** neu erzeugt:

```bash
UPDATE_GOLDEN_MASTERS=1 dotnet test tests/EInvoiceSender.Formats.Tests
```

**Wichtig:** Nach einer Neuerzeugung ist die Schematron-Gegenprüfung
(`./build/validate-golden-masters.sh`) zwingend erneut auszuführen – siehe
`docs/DEVELOPMENT.md`. Ein Golden Master, der nur „vom Programm selbst
akzeptiert" wird, belegt gar nichts.

### Ende-zu-Ende-Konformitätstests

`tests/EInvoiceSender.IntegrationTests` prüft je Golden Master gegen die
**echten Referenzwerkzeuge**: CEN-Schematron und veraPDF, beide über die
Mustang-CLI (`MustangValidator`). Geprüft werden unter anderem: Schematron- und
PDF/A-Validierung, Rückextraktion der eingebetteten XML mit Byte-Gleichheit,
Anhangname (`factur-x.xml`), MIME-Typ, `/AFRelationship`, Profilkennung, XMP.
Ebenfalls hier: Sicherheitstests für `SecureXml` (XXE, Billion Laughs) und die
zwölf Ablauftests des Gesamtprozesses (Erfolg, fehlende Bestätigung,
Validierungsfehler, beschädigte PDF, nicht eingebettete Schrift, Timeout,
Beanstandung, Benutzerabbruch, Überschreibschutz, Aufräumen der temporären
Dateien).

### ViewModel-Tests

`tests/EInvoiceSender.Presentation.Tests` prüft die Ablauflogik des
plattformneutralen Projekts `EInvoiceSender.Presentation`
(`InvoiceDraft`, `ShellViewModel`, Fünf-Schritte-Ablauf). Diese Tests laufen
auf jedem Agenten, auch unter Linux, weil `Presentation` bewusst nicht auf
`net10.0-windows` zielt (`docs/STATUS.md`, M2/M3).

### Thread-Zugehörigkeit der Oberfläche

`UiThreadAffinityTests` schliesst eine Lücke, die diese Testanlage von sich aus
hat: Ein gewöhnlicher Testlauf besitzt **keinen Synchronisierungskontext**, und
ohne Kontext verhalten sich `ConfigureAwait(true)` und `ConfigureAwait(false)`
gleich. Ein `ConfigureAwait(false)` im ViewModel ist unter WPF trotzdem ein
Fehler: Die Fortsetzung nach dem `await` läuft dann auf einem
Threadpool-Thread, meldet von dort an gebundene Bedienelemente und lässt WPF
mit „Der aufrufende Thread kann nicht auf dieses Objekt zugreifen" abbrechen.
Genau das ist beim ersten echten Start passiert, ohne dass ein einziger Test
angeschlagen hätte.

Der Test stellt deshalb einen echten Ein-Thread-Kontext mit Nachrichtenschleife
bereit – den kleinsten Nachbau des WPF-Dispatchers – führt dort jeden der vier
abwartenden Befehle aus und prüft, dass **jede** Änderungsmeldung
(`PropertyChanged` des ViewModels und des `InvoiceDraft`, `CollectionChanged`
von `Findings` und `Progress`) vom Oberflächen-Thread kommt.

Zwei Fallstricke sind im Testcode ausdrücklich vermerkt, weil sie den Test
lautlos wirkungslos machen:

- Die Stubs müssen **wirklich** auf einem fremden Thread fertig werden.
  `Task.FromResult` läuft synchron weiter, `Task.Yield` kehrt selbst über den
  Kontext zurück – beides bliebe auf dem Oberflächen-Thread.
- Jeder Befehl braucht seine Vorbereitung, sonst kehrt er an seiner
  Eingangsprüfung sofort zurück. Der Test schlägt in diesem Fall mit einer
  eigenen Meldung fehl, statt stillschweigend nichts zu prüfen.

Gegenprobe: Vor der Behebung schlagen alle vier Fälle fehl, danach laufen sie
durch.

### Freigabe der Schaltflächen

`CommandEnablementTests` deckt eine zweite Lücke ab, die derselben Ursache
entspringt – die Oberfläche zeigt etwas anderes an, als der Zustand hergibt.

Eine WPF-Schaltfläche fragt einen `RelayCommand` **nicht** laufend nach seiner
Freigabe. Sie fragt einmal beim Binden und danach nur noch, wenn der Befehl
`CanExecuteChanged` meldet. Liest eine Freigabeprüfung also eine Eigenschaft,
die den Befehl nicht benachrichtigt, bleibt der Knopf im zuletzt bewerteten
Zustand hängen. Beim ersten Durchlauf blieb „Weiter" deshalb dauerhaft
gesperrt, obwohl die Statuszeile die PDF als verarbeitbar meldete.

**Der entscheidende Punkt für den Testentwurf:** Ein Test, der einfach
`CanExecute(null)` aufruft, findet diesen Fehler **nie**. Dieser Aufruf wertet
die Bedingung jedes Mal frisch aus und liefert deshalb immer die richtige
Antwort – auch dann, wenn der Knopf auf dem Bildschirm seit Minuten falsch
aussieht. Geprüft werden muss das *Ereignis*, nicht der Wert. Der erste Anlauf
dieses Tests war genau deshalb wirkungslos und bestand gegen den fehlerhaften
Stand.

`ButtonSpy` bildet daher nach, wie ein echter Knopf seinen Zustand führt: einmal
beim Binden fragen, danach nur auf Meldung. Nach jedem realistischen Ablauf
vergleicht der Test, was der Anwender sähe, mit dem, was gälte. Der Test kennt
die Verdrahtung nicht und findet damit auch künftige vergessene
Benachrichtigungen. Gegenprobe: Er meldet gegen den fehlerhaften Stand beide
betroffenen Befehle namentlich.

### Anzeigevorlagen gegen ihr ViewModel

`FindingTemplateBindingTests` prüft die XAML-Datei als Text gegen das ViewModel,
das sie anzeigt. Hintergrund sind zwei Fehler, die WPF **stillschweigend**
begeht:

- Fehlt die `DataTemplate` ganz, zeigt WPF das Ergebnis von `ToString()`, also
  den Klassennamen. Genau das stand beim ersten Durchlauf in der Befundliste.
- Ist ein Bindungspfad falsch geschrieben, bleibt das Feld leer. WPF schreibt
  eine Meldung ins Ausgabefenster des Debuggers und macht weiter; ohne
  angehängten Debugger bemerkt das niemand.

Der Test liest `src/EInvoiceSender.Desktop/App.xaml`, sucht die Vorlage für
`FindingViewModel` und prüft, dass jeder gebundene Pfad dort auch existiert –
in beiden Schreibweisen (`{Binding Pfad}` und `<Binding Path="Pfad" />`). Er
läuft auf jedem Agenten, weil er die Datei als Text liest und kein WPF braucht.

### Fortschrittsmeldungen in Tests

`Progress<T>` stellt jede Meldung über den Synchronisierungskontext zu, in einem
Test also über den Threadpool. Die Meldung trifft damit **irgendwann** ein,
möglicherweise erst nach dem Ende des Tests. In den Integrationstests hatte das
zwei Folgen: eine Zusicherung prüfte eine Liste, bevor sie gefüllt war, und ein
Rückruf rief `Cancel` auf einer bereits entsorgten `CancellationTokenSource`.
Letzteres erzeugte eine unbeobachtete `ObjectDisposedException` auf einem
Threadpool-Thread, die xunit als „Catastrophic failure" meldet und den ganzen
Lauf rot färbt – sporadisch, ohne Änderung am Programm.

Tests verwenden deshalb `ImmediateProgress<T>`, das sofort und im meldenden
Thread zustellt. In der Oberfläche bleibt `Progress<T>` richtig: Dort ist das
Zustellen über den Kontext genau der Zweck.

---

## Grundprinzip: externe Validatoren sind das Freigabegate

**Externe Validatoren sind das Freigabegate; eine positive oberste
Zusammenfassung genügt nie.**

Mustang meldet je Teilprüfung eine eigene `<summary>`. Die oberste
Zusammenfassung kann `valid` lauten, obwohl zum Beispiel die PDF/A-Prüfung im
Detail fehlgeschlagen ist – dieser Fall ist während der Entwicklung
tatsächlich aufgetreten (`build/validate-golden-masters.sh`). Deshalb zählt das
Skript **jede einzelne** `<summary status="...">` im Ausgabetext, nicht nur die
oberste Zeile. Ebenso bewertet `MustangValidator` jede Teilzusammenfassung
einzeln; ein unlesbarer oder leerer Bericht gilt als Fehler
(`docs/STATUS.md`, Kernstabilisierung).

Die eigene Regelprüfung (`En16931RuleValidator`) ist dabei ausdrücklich **kein
Ersatz** für die externen Validatoren. Sie meldet Fehler in verständlichem
Deutsch, bevor überhaupt etwas erzeugt wird – aber was sie durchlässt, gilt
nicht als normkonform bestätigt. Die Freigabe erteilen ausschließlich CEN-
Schematron und veraPDF (`docs/DECISIONS.md`, ADR-0009).

---

## REQUIRE_EXTERNAL_VALIDATORS

`ExternalValidatorFixture`
(`tests/EInvoiceSender.IntegrationTests/ExternalValidatorFixture.cs`) sucht die
Mustang-JAR unter `tools/mustang/`. Fehlt sie:

- **Lokal (Standardfall):** Die betroffenen Tests werden mit `Assert.Skip`
  übersprungen, damit ein frisch geklontes Repository nicht sofort rot ist.
- **Wenn `REQUIRE_EXTERNAL_VALIDATORS=1` gesetzt ist** (so läuft es in der
  CI, siehe `.github/workflows/ci.yml`): Die Tests **scheitern** statt
  übersprungen zu werden. Nur so bleibt die Aussage belastbar – in der
  Pipeline gibt es kein stilles Überspringen des Freigabegates.

In der CI wird die Umgebungsvariable in beiden Jobs (Linux und Windows) gesetzt
und **vor** dem Testlauf `./build/fetch-validators.sh` ausgeführt, damit das
Werkzeug tatsächlich vorliegt.

---

## Befehle

```bash
# Alle Tests
dotnet test EInvoiceSender.slnx -c Release

# Einzelnes Testprojekt
dotnet test tests/EInvoiceSender.Domain.Tests -c Release

# Mit verpflichtenden externen Validatoren (wie in der CI)
./build/fetch-validators.sh
REQUIRE_EXTERNAL_VALIDATORS=1 dotnet test EInvoiceSender.slnx -c Release

# Golden Master neu erzeugen (nur nach bewusster Änderung)
UPDATE_GOLDEN_MASTERS=1 dotnet test tests/EInvoiceSender.Formats.Tests

# Gegenprüfung der Golden Master mit CEN-Schematron und veraPDF
./build/validate-golden-masters.sh
```

---

## Was NICHT automatisiert geprüft ist

- **UI-Laufzeit auf Windows.** Die WPF-Oberfläche lässt sich unter Linux dank
  `EnableWindowsTargeting=true` kompilieren, aber nicht ausführen. Das
  Zusammenspiel mit echten Fenstern, Dialogen und der PDF-Vorschau ist laut
  `docs/STATUS.md` ungeprüft; der Windows-CI-Job baut und testet, ersetzt aber
  keinen manuellen Durchlauf.
- **Installer.** Neuinstallation, Upgrade über eine ältere Fassung,
  Deinstallation und Startmenüeintrag sind laut `docs/DECISIONS.md`
  (ADR-0011) und `docs/STATUS.md` in dieser Umgebung nicht ausführbar. WiX
  erzeugt MSI-Dateien nur unter Windows.
- **Verhalten des „neuen Outlook".** Ob eine `.eml`-Datei mit Anhang im „neuen
  Outlook" wie erwartet geöffnet wird, ist laut `docs/DECISIONS.md` (ADR-0005)
  aus dieser Umgebung nicht prüfbar und muss auf einem echten Windows-11-System
  verifiziert werden.

Alle drei Punkte stehen ebenso in `docs/STATUS.md` unter „Bekannte Probleme und
Einschränkungen" beziehungsweise „Noch nicht umgesetzt".

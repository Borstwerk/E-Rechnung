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
| `tests/EInvoiceSender.Presentation.Tests` | 19 | ViewModel- und Ablauflogik der Oberfläche |
| `tests/EInvoiceSender.IntegrationTests` | 48 | Gesamtablauf, PDF/A, externe Gegenprüfung |
| **Summe** | **426** | laut `docs/STATUS.md`, alle grün |

Daneben bestehen `tests/EInvoiceSender.TestSupport` (gemeinsame Testszenarien
und PDF-Fabrik, kein eigenes Testprojekt im Sinn von zählbaren Tests) und
`tests/EInvoiceSender.Application.Tests` (Projektgerüst vorhanden).

---

## Ebenen

### Unit-Tests

Der grösste Teil der 426 Tests: Werttypen mit Selbstprüfung (IBAN nach
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

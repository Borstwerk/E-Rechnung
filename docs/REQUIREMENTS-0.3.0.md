# BorstWerk E-Rechnung – Anforderungen Version 0.3.0

## Status

**In Entwicklung – nicht veröffentlicht**

Diese Datei ist die verbindliche Anforderungsbasis für Version 0.3.0. Bereits
auf `main` liegende 0.3.0-Slices sind als umgesetzt gekennzeichnet. Geplante
Slices beginnen erst nach ausdrücklicher Freigabe ihres Gate-2-Plans.

Die Produktversion bleibt bis zu einem eigenen Versions- und Releasegate bei
`0.2.0`. Aus dieser Datei folgt weder eine Versionsumschaltung noch eine
Freigabe von Installer, Tag oder GitHub Release.

## Produktgrenzen

BorstWerk E-Rechnung bleibt ein ausschließlich örtlich arbeitendes
Windows-Werkzeug für zwei klar getrennte Anwendungsfälle:

```text
vorhandene PDF-Rechnung
→ Rechnungsdaten erfassen und prüfen
→ ZUGFeRD-/Factur-X EN 16931 erzeugen
→ speichern
```

und, schrittweise ab 0.3.0:

```text
vorhandene fertige E-Rechnung
→ ausschließlich lesen
→ technischen Inhalt aufnehmen
→ verständlich anzeigen, was tatsächlich geprüft wurde
```

Weiterhin ausdrücklich nicht Bestandteil:

- Rechnungen schreiben oder Rechnungsnummern vergeben,
- Buchhaltung, CRM oder Mahnwesen,
- Reparatur oder Veränderung einer geprüften E-Rechnung,
- automatischer E-Mail-Versand,
- Cloud-Verarbeitung, Telemetrie oder Benutzerkonten,
- eine nicht tatsächlich ausgeführte Norm- oder PDF/A-Prüfung als bestanden
  ausgeben,
- neue externe Laufzeitabhängigkeiten ohne eigenes freigegebenes Requirement.

## ER-030-STD-01a – Standardstand und Normaussagen aktualisieren

**Status:** Umgesetzt auf `main`

### Problem / Grund

Die Dokumentation nannte noch ZUGFeRD 2.3 / Factur-X 1.07 mit Syntaxfassung
D16B, obwohl die Erzeugung bereits gegen ZUGFeRD 2.5.2 / Factur-X 1.09.2 und
D22B geprüft wurde. Gleichzeitig behandelten einzelne interne Produktgrenzen
einen von BorstWerk nicht angebotenen Code fälschlich wie einen Normverstoß.

### Anforderung

Der dokumentierte Standardstand muss der tatsächlich geprüften Erzeugung
entsprechen. Aussagen über das Angebot von BorstWerk und Aussagen über die
Gültigkeit nach EN 16931 müssen getrennt bleiben.

### Akzeptanzkriterien

- ZUGFeRD 2.5.2 / Factur-X 1.09.2 und D22B sind als Erzeugungsziel
  dokumentiert.
- Profilkennung, CII-Namensräume, Anhangname, MIME-Typ, Dateibeziehung und das
  XMP-Extension-Schema bleiben ohne konkreten Normbefund unverändert.
- Nicht angebotene Rechnungsarten oder Mengeneinheiten werden nicht allein
  deshalb als normwidrig bezeichnet.
- Falsche oder unbelegte Normverweise werden entfernt, nicht durch geratene
  Verweise ersetzt.
- Writer, Golden Master und externe Validatorversion bleiben unverändert,
  solange kein konkreter Befund eine Änderung verlangt.

### Nachweis

- `StandardRefreshTests`, `CodeListTests` und die Regeltests sichern die
  Trennung von Produktangebot und Normaussage.
- Alle Golden Master blieben byteidentisch und bestanden die externe
  Gegenprüfung mit Mustang 2.24.0.
- Core-, Integrations-, Build-, Format- und Diff-Prüfungen waren beim Merge
  grün.

## ER-030-STD-01b – Währungen positiv gegen den Normbestand prüfen

**Status:** Umgesetzt auf `main`

### Problem / Grund

Eine Negativliste zurückgezogener Währungen kann nicht unterscheiden, ob ein
beliebiger Code gültig ist. Damit würden ein erfundener Code und eine
normgültige, von BorstWerk lediglich nicht angebotene Währung gleich
behandelt.

### Anforderung

Währungscodes müssen für Normaussagen positiv gegen den vollständigen
EN-16931-Codebestand v17b geprüft werden. Die kuratierte Auswahlliste der
Anwendung bleibt davon getrennt.

### Akzeptanzkriterien

- `CurrencyCodeList.NormCodes` enthält die 178 Codes des belegten Bestands
  v17b.
- Eine feste Prüfsumme sichert den vollständigen Bestand und nicht nur
  Stichproben.
- Ein nicht normgültiger Code erzeugt `APP-DOC-008` mit `BR-CL-04`.
- Ein normgültiger, nicht angebotener Code erzeugt nur `APP-DOC-011` ohne
  Normverweis.
- Die angebotenen Währungen sind eine echte Teilmenge des Normbestands.
- Weitere Codelisten werden nicht aus Gedächtnis oder alten Werkzeugdaten
  ergänzt; ihre noch fehlenden Normbestände bleiben sichtbar dokumentiert.

### Nachweis

- Prüfsummentest über alle 178 Codes,
- Positiv- und Negativtests für gültige, nicht angebotene, zurückgezogene und
  erfundene Codes,
- unveränderte Golden Master und externe Gegenprüfung.

## ER-030-REL-01 – Installerbau nur aus frischem Publishbestand

**Status:** Umgesetzt auf `main`

### Problem / Grund

Ein direkter Build des WiX-Projekts konnte einen liegengebliebenen
Publishbestand paketieren. Ein solches MSI trägt plausible Versionsangaben,
enthält aber möglicherweise veralteten Anwendungscode.

### Anforderung

Ein Installer darf ausschließlich über einen unterstützten Weg und aus einem
frisch erzeugten, kanonischen Publishverzeichnis gebaut werden. Ist die
Frische oder Herkunft der Eingaben nicht belegt, muss der Build vor WiX
abbrechen.

### Akzeptanzkriterien

- `Build-Installer.ps1` veröffentlicht bei jedem Aufruf frisch und baut und
  prüft danach das MSI.
- `Build-Release.ps1` verwendet genau diesen Publishbestand für ZIP und
  Prüfsummen und erzeugt keinen zweiten Publish.
- Ein direkter Build des WiX-Projekts bricht verständlich ab.
- Ohne ausdrücklich übergebenes Publishverzeichnis bricht der autorisierte
  Installerbau ab.
- Als Publishziel ist ausschließlich das normalisierte
  `artifacts/publish/win-x64` innerhalb des Repositorys zulässig.
- Ein fremder oder über `..` aus dem Repository führender Pfad wird vor jeder
  Bereinigung abgewiesen und bleibt unverändert.
- Produktversion, ProductCode, UpgradeCode sowie die bestehende
  Releasepaketierung bleiben von diesem Hardening unberührt.

### Nachweis

- `build/test-installer-build-guard.sh` führt echte positive und negative
  Buildversuche aus.
- `build/Test-ReleasePackaging.ps1` belegt mit Sentinel-Dateien, dass fremde
  Verzeichnisse nicht bereinigt oder als Publishziel verwendet werden.
- Quelltests sichern den einen Publishweg; beide CI-Jobs führen den
  Installerwächter aus.

## ER-030-CHK-01 – Vorhandene E-Rechnung technisch prüfen

**Status:** Umgesetzt auf `main`

### Problem / Grund

Die Anwendung kann eine vorhandene PDF-Rechnung in eine neue
ZUGFeRD-/Factur-X-Rechnung überführen. Sie bietet aber noch keinen eigenen,
verständlichen Ablauf, um eine bereits fertige Hybridrechnung ausschließlich
lesend zu untersuchen.

Anwender müssen erkennen können, welche Rechnungsdaten und technischen
Merkmale tatsächlich in einer Datei vorhanden sind. Dabei darf eine
Bestandsaufnahme nicht als vollständige EN-16931- oder PDF/A-Konformitätsprüfung
ausgegeben werden.

### Anforderung

Die Anwendung erhält einen klar vom Erzeugungsablauf getrennten Prüfmodus für
fertige PDF-Hybridrechnungen. Die Quelldatei wird ausschließlich gelesen. Der
Prüfmodus zeigt technische Feststellungen, Kerndaten und Befunde an und nennt
sichtbar die Grenzen der ausgeführten Prüfung.

### Übergreifende Akzeptanzkriterien

- Der Prüfmodus ist ein eigener Anwendungsfall neben der Erzeugung.
- Die Quelldatei bleibt byteweise unverändert; es entsteht ohne ausdrückliche
  spätere Exportanforderung keine Datei neben ihr.
- Es gibt keine Reparatur, kein Ersetzen der XML und keine Aufwertung der PDF.
- Fehlerhafte, ungewöhnliche und fremde Dateien werden kontrolliert als
  Befund behandelt.
- Mehrere rechnungsartige Anhänge werden nicht nach „erster gewinnt“
  aufgelöst.
- Fremde Anhänge werden nicht unnötig materialisiert; der ausgewählte
  Rechnungsanhang wird bereits beim Entpacken begrenzt.
- Das Ergebnis unterscheidet eine vollständig ausgeführte Bestandsaufnahme
  von einer gültigen Rechnung. Es gibt kein irreführendes allgemeines
  `Succeeded`.
- PDF/A-Angaben aus XMP werden als Deklaration der Datei bezeichnet, nicht als
  bestandene PDF/A-Prüfung.
- Kein Befund behauptet „normkonform“, „gültige E-Rechnung“ oder
  „PDF/A-konform“, wenn die dazu notwendige Prüfung nicht gelaufen ist.
- Der ausgelieferte Prüfmodus benötigt kein Java, kein Netzwerk und keine
  neue externe Laufzeitabhängigkeit.

### Nicht Bestandteil von ER-030-CHK-01

- Reparatur oder Konvertierung der geprüften Datei,
- Unterstützung von UBL-XRechnung oder Order-X als auswertbares Format,
- OCR,
- automatische fachliche Gleichheitsbehauptung zwischen sichtbarer PDF und
  eingebetteter XML,
- Einbau von Mustangproject oder veraPDF in die ausgelieferte Anwendung,
- Export oder Versand eines Prüfberichts ohne eigenes Requirement,
- vollständige interne EN-16931-Prüfung fremder Rechnungen, solange die dafür
  nötigen Normbestände und Regeln nicht nachweisbar vollständig vorliegen.

### Phase A – Read-only Core-Fundament

**Status:** Umgesetzt auf `main`

`IEInvoiceCheckService` steht als eigener Anschluss neben
`IEInvoiceService`. Er liest PDF-Metadaten und genau einen eindeutig
auswertbaren ZUGFeRD-/Factur-X-Anhang, ermittelt eine SHA-256-Prüfsumme und
liefert technische Dokumentinformationen, eine optionale
`CiiInvoiceSummary` und einen `ValidationReport`.

Die Phase unterscheidet insbesondere:

- fehlende, Nicht-PDF-, beschädigte und kennwortgeschützte Dateien,
- fehlende, nicht unterstützte und mehrdeutige Rechnungsanhänge,
- leere, zu große, nicht begrenzt entpackbare, nicht wohlgeformte und
  strukturell nicht als CII lesbare XML,
- vorhandene und fehlende Profilkennung,
- reine PDF/A-Deklaration, Signatur- und Rechteeinschränkungsfeststellung.

#### Nachweis Phase A

- `EInvoiceCheckServiceTests` prüfen Kerndaten, Befunde und zurückhaltende
  Aussagen.
- `AttachmentMaterialisationTests` prüfen Auswahl vor Materialisierung und
  begrenzte Dekomprimierung einschließlich Entfaltungsbombe.
- `ExistingInvoiceGateTests` sichern, dass Anwesenheit und Lesbarkeit einer
  vorhandenen Rechnungs-XML nicht verwechselt werden.
- SHA-256, Bytevergleich, Zeitstempel und Dateisatz belegen die
  Read-only-Zusage auch im Fehlerfall.

### Phase B – Prüfmodus in der Anwendung bedienbar machen

**Status:** Umgesetzt auf `main`

#### Zentrale Modellentscheidung

Der Prüfmodus wird nicht als Sonderzustand des vorhandenen
Fünf-Schritt-Wizards modelliert. Ein eigenes modales Fenster verwendet den
vorhandenen `IEInvoiceCheckService` und zeigt dessen Ergebnis an. Der
Erzeugungsvorgang bleibt währenddessen erhalten und wird weder zurückgesetzt
noch fachlich verändert.

Der UI-Zustand ist flüchtig. Es entsteht kein neues persistiertes Fachobjekt,
keine Einstellungsdatei und keine zweite Wahrheit neben
`CheckEInvoiceResult`, `CheckedDocumentInfo`, `CiiInvoiceSummary` und
`ValidationReport`.

#### Variantenentscheidung

| Variante | Bewertung |
|---|---|
| Prüfmodus in den Erzeugungswizard einbauen | Vermischt gegensätzliche Abläufe, erzeugt Sondernavigation und gefährdet den bestehenden Fünf-Schritt-Prozess. Nicht empfohlen. |
| Eigenes modales Prüffenster | Klare fachliche Grenze, geringe Eingriffe, bestehender Vorgang bleibt erhalten. **Empfohlen.** |
| Neue Startseite mit allgemeinem Workflow-Router | Für zwei Abläufe unnötig groß und erst bei weiteren gleichrangigen Modi begründbar. Zurückgestellt. |

#### Geplante Bedienung

- Das Hauptfenster bietet eine sichtbare Aktion „E-Rechnung prüfen …“.
- Die Aktion öffnet ein eigenes Fenster mit Dateiauswahl. Sie deutet einen
  bereits laufenden Erzeugungsvorgang nicht um.
- Jede neu ausgewählte Datei leert zuerst den vorherigen Prüfzustand.
- Während der Prüfung sind widersprüchliche Mehrfachaufrufe gesperrt.
- Abbruch und unerwartete Lesefehler führen zu einem kontrollierten Zustand;
  das Hauptfenster und ein dort begonnener Vorgang bleiben nutzbar.
- Angezeigt werden mindestens:
  - Dateiname, Größe und SHA-256,
  - PDF-Version und Seitenzahl,
  - deklarierte PDF/A-Stufe,
  - eingebettete Dateien und ausgewerteter Rechnungsanhang,
  - Profilkennung,
  - Rechnungsnummer, Rechnungsdatum, Rechnungsart und Währung,
  - Positionsanzahl und die gelesenen Summen,
  - sämtliche Befunde mit Kennung und Schweregrad.
- Eine gut sichtbare Erläuterung sagt, dass die technische Bestandsaufnahme
  keine vollständige EN-16931- oder PDF/A-Konformitätsprüfung ist.
- `Completed` wird ausschließlich als „Bestandsaufnahme vollständig
  durchgeführt“ dargestellt. Weder Fenster noch ViewModel leiten daraus
  „gültig“ oder „bestanden“ ab.
- Das Fenster bietet in dieser Phase weder Reparatur noch Speichern, Export,
  Upload oder Versand an.

#### Finding- und Ergebnissemantik

- Die stabilen `APP-CHK-*`-Kennungen und Schweregrade aus dem Core bleiben
  maßgeblich.
- Die Oberfläche verwendet die vorhandene Darstellung von
  `FindingViewModel`; sie erfindet keine zweite Befundbewertung.
- Der verständliche Befundtext bleibt die Hauptaussage. Darunter stehen
  interne Kennung und eine tatsächlich vorhandene Normregel als ausdrücklich
  beschriftete technische Details. Für `BR-CO-26` darf die aus der bestehenden
  Regeldefinition belegte Bezeichnung „Verkäuferidentifikation“ erscheinen;
  bei rein internen Befunden wird kein Normbezug erfunden.
- Dokumentinformationen und Rechnungsübersicht sind Feststellungen über den
  gelesenen Inhalt. Fehlende Werte bleiben sichtbar unbekannt und werden
  nicht ergänzt oder geraten.
- BT-2 wird in der deutschen Oberfläche deterministisch als `dd.MM.yyyy`
  ohne Uhrzeit dargestellt. BT-106, BT-109, BT-110, BT-112 und BT-115 werden
  mit zwei deutschen Nachkommastellen und der gelesenen Rechnungswährung
  dargestellt; weder Windows-Kultur noch eine fest eingebaute EUR-Annahme
  dürfen die Anzeige bestimmen.
- Der Dateiname darf in der unmittelbaren lokalen Anzeige erscheinen. Das
  Diagnoselog bleibt bei seinen strengeren Datenschutzregeln und erhält
  weder Dateiname, Pfad noch Rechnungsdaten.

#### Akzeptanzkriterien Phase B

- Der Prüfmodus ist aus dem Hauptfenster eindeutig erreichbar.
- Öffnen und Schließen des Prüffensters verändert den Zustand des
  Erzeugungswizards nicht.
- Eine normale Factur-X-/ZUGFeRD-Hybridrechnung zeigt Dokumentinformationen,
  Kerndaten, SHA-256 und Befunde aus Phase A.
- Eine PDF ohne Rechnungsanhang, eine beschädigte XML, XRechnung, Order-X,
  mehrere Rechnungsanhänge und ein begrenzt abgewiesener Anhang werden ohne
  Anwendungsabsturz verständlich angezeigt.
- Eine zweite Dateiauswahl zeigt ausschließlich das neue Ergebnis.
- Ein abgebrochener Vorgang wird nicht als vollständig oder bestanden
  dargestellt.
- Alle sichtbaren Konformitätsaussagen bleiben auf die tatsächlich
  ausgeführten Prüfungen begrenzt.
- Befundtexte bleiben fachlich unverändert; technische Kennungen sind
  weiterhin vollständig sichtbar, aber verständlich und nachrangig
  beschriftet.
- Rechnungsdatum und die fünf gelesenen Summen erscheinen in der deutschen
  Oberfläche lokalisiert, ohne gelesene Werte oder Parsing zu verändern.
- Es gibt keine Schreib-, Reparatur-, Export-, Upload- oder Netzwerklogik.
- Der bestehende Erzeugungsablauf, Einstellungen, Firmenvorlage und
  Releasepaketierung bleiben unverändert.

#### Teststrategie Phase B

Automatisiert:

- bestehende Phase-A-Coretests vollständig,
- DI-Strukturtest für genau einen wiederverwendeten `PdfAnalyzer` als
  `IPdfAnalyzer` und `IPdfAttachmentReader` sowie einen wiederverwendeten
  `CiiInvoiceReader` als `IInvoiceXmlReader` und `ICiiInvoiceInspector`,
- Quell- und XAML-Tests für Einstieg, Bindungen, Befunddarstellung,
  Busy-/Abbruchzustand und den sichtbaren Begrenzungshinweis,
- ausgeführte Präsentationstests unter `en-US` für `dd.MM.yyyy`, deutsches
  Dezimalformat, übernommene Fremdwährung sowie Normbefunde und rein interne
  Befunde,
- Negativtest gegen die Wörter und Bedeutungen „gültig“, „normkonform“,
  „PDF/A-konform“ und ein aus `Completed` abgeleitetes „bestanden“,
- Strukturtest, dass das Prüffenster keinen Erzeugungs-, Speicher-,
  Einstellungs-, Mail-, Upload- oder Netzwerkdienst verwendet,
- Core- und Integrationstests einschließlich Mustang/veraPDF als
  Regressionsgate für die Erzeugung,
- Solution-Build, Formatprüfung und `git diff --check`.

Windows-Abnahme:

- normale Factur-X-Datei,
- gewöhnliche PDF ohne Rechnungs-XML,
- beschädigte und übergroße beziehungsweise ungewöhnlich gepackte XML,
- XRechnung und Order-X,
- mehrere rechnungsartige Anhänge,
- Abbruch und anschließende Prüfung einer anderen Datei,
- SHA-256- und Bytevergleich der Quelle vor und nach der Bedienung,
- Prüffenster während eines begonnenen Erzeugungsvorgangs öffnen und schließen;
  Eingaben und aktueller Schritt bleiben erhalten,
- Tastaturbedienung, verständliche Fokusreihenfolge, Sprachausgabenamen und
  Darstellung bei Mindestfenstergröße,
- der unveränderte blockierende Verkäuferbefund `APP-SEL-004`/`BR-CO-26` mit
  verständlichem Haupttext und nachrangiger technischer Detailzeile,
- deutsches Rechnungsdatum ohne Uhrzeit sowie deutsche Beträge mit der
  tatsächlich gelesenen Währung.

#### Windows-Abnahme der UX-Nachbesserung am 30.08.2026

Die zwei in der ersten manuellen Abnahme beanstandeten Darstellungen wurden
im lokalen Entwicklungsbuild erneut sichtbar geprüft:

- Im Erfassungsworkflow mit `01_Kontrolle_eingebettete_Schriften.pdf` blieb
  die fehlende Verkäuferidentifikation genau ein blockierender Befund. Der
  bestehende vollständige Fachtext wurde einmal angezeigt; die nachrangige
  Zeile lautete `Technische Details: EN 16931 – Verkäuferidentifikation
  (BR-CO-26) · interne Kennung APP-SEL-004`.
- Im read-only Prüfmodus zeigte der lokale Golden Master
  `artifacts/golden-masters/valid/20-zugferd-ergebnis.pdf` das Rechnungsdatum
  `15.03.2026` ohne Uhrzeit. BT-106/109 erschienen als `1.000,00 EUR`, BT-110
  als `190,00 EUR` und BT-112/115 als `1.190,00 EUR`.
- Die dort sichtbaren rein internen Befunde `APP-CHK-010` und `APP-CHK-040`
  wurden als „Interne Kennung“ beschriftet und erhielten keinen erfundenen
  EN-16931-Regelbezug.

Damit ist die gezielte Windows-Nachprüfung dieser beiden UX-Punkte grün. Der
vollständige Phase-B-Stand wurde anschließend unabhängig geprüft, bestand die
Linux- und Windows-CI einschließlich CEN-Schematron und veraPDF sowie die
vollständige Windows-Abnahme und wurde danach in `main` integriert.

#### Geplante Dateien und harte Diff-Grenze Phase B

Voraussichtlich betroffen:

- `src/EInvoiceSender.App/App.xaml.cs` ausschließlich für DI-Aliase und die
  Registrierung des Prüfdienstes, ViewModels und Fensters,
- `src/EInvoiceSender.App/Views/MainWindow.xaml` und `.xaml.cs` ausschließlich
  für die sichtbare Einstiegsaktion,
- neues `src/EInvoiceSender.App/ViewModels/EInvoiceCheckViewModel.cs`,
- neues `src/EInvoiceSender.App/Views/Dialogs/EInvoiceCheckWindow.xaml` und
  `.xaml.cs`,
- neue App-Strukturtests unter `tests/EInvoiceSender.Core.Tests/App/`,
- `README.md` für den tatsächlich verfügbaren Anwenderumfang,
- `docs/ARCHITECTURE.md`, `docs/TESTING.md` und
  `docs/RELEASE-CHECKLIST.md` für den tatsächlich umgesetzten Stand.

Nur bei einem durch einen konkreten Test belegten Fehler betroffen:

- `src/EInvoiceSender.Core/Checking/`,
- `src/EInvoiceSender.Core/Zugferd/CiiInvoiceReader.cs`,
- vorhandene Phase-A-Tests.

Ausdrücklich außerhalb des Diffs:

- `Directory.Build.props`, Produktversion und Assemblyversion,
- ProductCode, UpgradeCode und WiX-Definition,
- `build/Build-Installer.ps1`, `build/Build-Release.ps1` und CI-Paketierung,
- Erzeugungswizard und seine fünf vorhandenen Schritt-ViewModels,
- Firmenvorlage, Datenerkennung, Buyer/Seller, Positionen und CII-Writer,
- externe Validatorintegration in der ausgelieferten Anwendung,
- neue NuGet- oder Runtimeabhängigkeiten.

## ER-030-UX-01 – Verkäuferidentifikation und steuerliche Angaben verständlich trennen

**Status:** Umsetzung auf `feature/0.3.0-seller-id-ux`, Review ausstehend

### Problem / Grund

USt-IdNr., Steuernummer, Registerkennung und Lieferantenkennung stehen im
Verkäuferformular optisch nahezu gleichrangig nebeneinander. Dadurch kann die
Steuernummer wie eine für BR-CO-26 ausreichende Verkäuferkennung wirken,
obwohl ausschließlich BT-29, BT-30 oder BT-31 den Verkäufer in diesem Sinne
identifizieren. BT-32 bleibt eine getrennte steuerliche Angabe.

### Anforderung

Schritt 2 trennt steuerliche Angaben sichtbar von der
Verkäuferidentifikation für die E-Rechnung. Die Oberfläche erklärt bereits
bei der Eingabe, dass die vorhandene USt-IdNr. gleichzeitig als BT-31 zählt,
eine Steuernummer allein aber nicht genügt. Es bleibt genau ein Eingabefeld
für die USt-IdNr.; bestehende Bindungen, Herkunftsanzeigen, Vorbefüllung und
Firmenvorlage bleiben unverändert.

Die Lieferantenkennung wird als „Lieferanten-/Kreditorennummer“ bezeichnet und
weiterhin als eine vom Kunden vergebene Kennung erklärt. Es entsteht keine
neue fachliche Bedeutung für BT-29.

### Akzeptanzkriterien

- „Steuerliche Angaben“ und „Verkäuferidentifikation für die E-Rechnung“ sind
  im Verkäuferformular sichtbar getrennt.
- Der Hilfetext nennt USt-IdNr., Registerkennung und eine vom Kunden vergebene
  Lieferanten-/Kreditorennummer als Identifikationswege und sagt ausdrücklich,
  dass die Steuernummer allein nicht genügt.
- Die USt-IdNr. erscheint nur einmal und behält ihren bisherigen Binding-Pfad.
- Steuernummer, Registerkennung und Lieferantenkennung behalten ebenfalls
  ihre bisherigen Binding- und Herkunftspfade.
- `APP-SEL-004`, `BR-CO-26`, Severity und Regeltext bleiben fachlich
  unverändert: BT-32 allein blockiert; BT-29, BT-30 oder BT-31 erfüllen die
  Verkäuferidentifikation jeweils allein.
- PDF-Erkennung, Firmenvorlage, CII-Reader/-Writer, Checker-Core, Installer,
  Releaseweg und Produktversion bleiben unverändert.

### Nachweis

- XAML-Strukturtests sichern Gruppierung, Hilfetext, eindeutiges
  USt-IdNr.-Feld, unveränderte Bindungen und Herkunftsanzeigen.
- Die bestehenden Regeltests prüfen BT-32 allein sowie BT-29, BT-30 und BT-31
  einzeln und in Kombination mit BT-32.
- Die Windows-Abnahme prüft manuelle, aus Vorlage stammende und aus PDF
  erkannte Werte sowie Mindestfenstergröße und Tastaturreihenfolge.
- Core-, Integrations-, Validator-, Build-, Format- und Diff-Prüfungen bleiben
  unverändert grün; Golden Master und externe Validatorintegration werden
  nicht verändert.

## Allgemeine Qualitätsanforderungen 0.3.0

Für sämtliche Anforderungen gelten:

- bestehende Tests und Referenzvalidatoren bleiben grün,
- jede neue Schutzregel erhält Positiv-, Negativ- und Brechprobe,
- die Original-PDF bleibt unverändert,
- ausgeführte und nicht ausgeführte Prüfungen bleiben unterscheidbar,
- keine unnötigen neuen Abhängigkeiten,
- keine Cloud-, Konto-, Upload- oder Telemetriefunktion,
- keine Abschwächung bestehender PDF-, XML-, Datenschutz-, Installer- oder
  Releasewächter,
- KISS und die Trennung von Produktangebot und Normaussage bleiben
  verbindlich.

## Noch nicht freigegebene spätere Arbeit

Diese Punkte benötigen jeweils ein eigenes Requirement und einen eigenen
Gate-2-Plan:

- vollständige fachliche EN-16931-Prüfung fremder Rechnungen einschließlich
  der dafür noch fehlenden positiven Normbestände,
- belastbarer Vergleich zwischen sichtbarem PDF-Inhalt und eingebetteter XML,
- optionaler Export eines Prüfberichts,
- Versionsumschaltung auf 0.3.0 samt festem ProductCode,
- Releaseabnahme, Tag und Veröffentlichung von 0.3.0,
- Code Signing, sofern dafür eine freigegebene Lösung vorliegt.

## Geplante Bearbeitungsreihenfolge

1. `ER-030-STD-01a/b` – Standardstand und Währungsbestand – **umgesetzt**
2. `ER-030-REL-01` – Installerbau gegen Altbestand – **umgesetzt**
3. `ER-030-CHK-01` Phase A – read-only Core-Fundament – **umgesetzt**
4. `ER-030-CHK-01` Phase B – bedienbarer technischer Prüfmodus – **umgesetzt**
5. `ER-030-UX-01` – Verkäuferidentifikation verständlich trennen – **Gate 3**
6. weitere Anforderungen erst nach eigener Planungsaufnahme und Freigabe
7. getrenntes Versions-, Windows-Abnahme- und Releasegate

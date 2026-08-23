# Aufbau

Die Projektmappe besteht aus vier Projekten plus dem Installer. Mehr braucht
eine Desktopanwendung dieser Größe nicht.

```
EInvoiceSender.sln
├── src/EInvoiceSender.Core   – der fachliche Kern     (net10.0, kein WPF)
├── src/EInvoiceSender.App    – die Oberfläche        (net10.0-windows, WPF, x64)
├── tests/EInvoiceSender.Core.Tests
├── tests/EInvoiceSender.IntegrationTests
└── installer/EInvoiceSender.Setup                     (WiX, nur unter Windows baubar)
```

## EInvoiceSender.Core

Alles Fachliche. Kennt kein WPF und lässt sich deshalb vollständig
automatisiert prüfen – auch auf einem Build-Agenten ohne Bildschirm.

| Ordner | Inhalt |
|---|---|
| `Models` | Rechnung, Parteien, Beträge, Werttypen (IBAN, Währung, Land, Einheit), das Eingabeformular `InvoiceDraft` |
| `Calculation` | Summen- und Steuerberechnung nach EN 16931, ausschließlich `decimal` |
| `Validation` | Befunde und Prüfberichte |
| `Validation/Rules` | die EN-16931-Regeln, nach Dokument, Parteien, Positionen, Umsatzsteuer, Summen und Zahlung gruppiert |
| `Zugferd` | CII-XML erzeugen und zurücklesen |
| `Pdf` | PDF-Analyse, Eingangsprüfung, PDF/A-3-Aufwertung, sichtbare Kopie (Rasterweg), XMP, ICC-Profil, Einbettung |
| `Pdf/Detection` | örtliche Datenerkennung, je Aufgabe ein Detektor: Dokument, Parteien, Zahlung, Summen; dazu Vertrauensstufen, Vorbefüllung und Summenabgleich |
| `Reports` | Validierungsbericht als JSON und als Text |
| `Storage` | atomare Dateiausgabe, sichere Dateinamen, temporäre Arbeitsverzeichnisse |
| `Security` | sichere XML-Verarbeitung, Prozessausführung mit Zeitlimit |
| `Settings` | Firmenvorlage als JSON, IBAN unter Windows per DPAPI geschützt |
| `Mail` | `.eml`-Entwurf und `mailto:`-Rückfallweg |
| `Services` | der zentrale Dienst und die Anbindung der externen Validatoren |

### Der zentrale Dienst

Die Oberfläche kennt genau eine Schnittstelle:

```csharp
public interface IEInvoiceService
{
    Task<PdfPreflightReport> AnalyzePdfAsync(string pdfPath, CancellationToken ct = default);
    ValidationReport ValidateInvoice(Invoice invoice);
    Task<CreateEInvoiceResult> CreateAsync(CreateEInvoiceRequest request,
                                           IProgress<PipelineProgress>? progress = null,
                                           CancellationToken ct = default);
}
```

Die Umsetzung `EInvoiceService` führt dahinter die spezialisierten Klassen
zusammen – `CiiInvoiceWriter`, `CiiInvoiceReader`, `En16931RuleValidator`,
`PdfPreflightService`, `PdfAInvoiceComposer`, `ValidationReportWriter`,
`FileStorage`. Diese Klassen bleiben getrennt lesbar, liegen aber in **einer**
Assembly.

`MustangValidator` gehört ebenfalls in diese Assembly, wird von der
ausgelieferten Anwendung aber **nicht eingetragen**: Er ruft die
Mustangproject-CLI auf und braucht dafür eine Java-Laufzeit. Verwendet wird er
in den Tests und in der Pipeline. `EInvoiceService` nimmt die Validatoren als
Aufzählung entgegen; ist sie leer, entfällt der Schritt, und der Bericht sagt
das.

`CreateAsync` selbst enthält keinen Detailalgorithmus mehr, sondern liest
sich als Ablauf: bestätigt, geeignet, gültig, erzeugen, gegenprüfen,
aufbauen, auslesen, extern prüfen, speichern. Der Zustand eines Laufs steht
in einem `CreationContext`; er trägt auch die Fortschrittsmeldungen.

## EInvoiceSender.App

Nur Oberfläche: Fenster, Ansichten, ViewModels, Windows-Dialoge,
Drag-and-drop, PDF-Vorschau, Shell-Aufrufe und das Zusammensetzen der
Abhängigkeiten. Keine Steuer-, PDF/A-, XML- oder Rechnungslogik.

```
App.xaml(.cs)              Composition Root: eine ServiceCollection, kein Generic Host
Views/MainWindow           Rahmen, Schrittanzeige, Statuszeile, Navigation, Störungsanzeige
Views/Steps/               die fünf Schritte als eigene UserControls
Views/Dialogs/             Einstellungen
ViewModels/                je Schritt ein ViewModel plus MainViewModel
Services/                  PDF-Vorschau, Windows-Shell, Systemuhr
```

Zwei Regeln gelten in der Oberfläche ausnahmslos und werden von Tests bewacht:

- **`ConfigureAwait(true)` an jedem `await`.** Sonst läuft die Fortsetzung auf
  einem Threadpool-Thread und WPF bricht beim nächsten Zugriff auf ein
  gebundenes Bedienelement ab.
- **Jede Eigenschaft, die eine Freigabeprüfung liest, benachrichtigt ihren
  Befehl** (`[NotifyCanExecuteChangedFor]`). Sonst bleibt die Schaltfläche im
  zuletzt bewerteten Zustand hängen.

Beide Regeln stammen aus Fehlern, die im laufenden Programm aufgetreten sind.

## Die Datenerkennung

Nach der Eingangsprüfung liest die Anwendung den bereits vorhandenen Text der
PDF und versucht, das Formular vorauszufüllen.

```
PDF  →  PdfTextExtractor  →  InvoiceDataDetector  →  InvoiceDetectionResult
                                                          ↓  DraftPrefiller
                                                     InvoiceDraft
                                                          ↓  Bestätigung
                                                       Invoice
```

`InvoiceDataDetector` koordiniert nur. Die Regeln liegen in vier fachlich
getrennten Detektoren – `DocumentFieldDetector`, `PartyDetector`,
`PaymentDetector`, `TotalsDetector` – und die kleinen Umwandlungen in
`DetectionParsers`.

Drei Eigenschaften machen das ungefährlich:

1. **`InvoiceDetectionResult` ist kein Rechnungsmodell** und lässt sich auch
   nicht in eines verwandeln. Es füllt nur das Formular vor. Damit ist durch
   die Bauart ausgeschlossen, dass ein gelesener Wert ungeprüft in der
   E-Rechnung landet.
2. **Jeder Wert trägt eine Vertrauensstufe** und die Zeile, aus der er stammt.
   Nur `High` und `Medium` füllen ein Feld; `Low` wird angezeigt, aber nie
   eingetragen.
3. **Jedes vorausgefüllte Feld ist gekennzeichnet** (`FieldOrigin`). Sobald
   der Anwender es anfasst, gilt es als von Hand erfasst und die Kennzeichnung
   verschwindet.

**Rechnungspositionen werden erkannt, aber nur geschlossen.** Die Erkennung
nimmt eine Positionstabelle nur an, wenn sie auf einer Seite steht, einen
eindeutigen Tabellenkopf hat, ausschließlich bekannte Einheiten und Steuersätze
verwendet und ihre Summe die im Dokument gefundene Netto- oder Steuer- und
Bruttosumme trifft. Hält eine dieser Bedingungen nicht, entsteht **keine
einzige** Position – nie eine Teilmenge. Eine halb übernommene Tabelle sähe
vollständig aus und wäre falsch.

Die erkannten Positionen wandern über eine bewusst **interne** Eigenschaft von
`InvoiceDetectionResult` zur Vorbefüllung. Eine öffentliche Erkennungs-API
entsteht dafür nicht; alle Verbraucher liegen im Kern.

Enthält das Formular bereits Positionen, wird keine erkannte übernommen –
weder ergänzt noch ersetzt noch vermischt. Gemeldet wird das trotzdem.

Kein OCR, keine externen Dienste. Der Text wird im Arbeitsspeicher ausgewertet
und danach verworfen.

## Der Ablauf

```
1. PDF auswählen      →  AnalyzePdfAsync   →  geeignet? Gründe nennen
                       →  DetectAsync       →  Formular vorausfüllen
2. Daten erfassen      →  InvoiceDraft      →  ValidateInvoice
3. Vergleichen         →  Pflichtbestätigung des Anwenders
4. Erzeugen            →  CreateAsync       →  neun Schritte mit Fortschritt
5. Ergebnis            →  Datei, Bericht, Prüfsumme, E-Mail-Entwurf
```

Der Kern führt in Schritt 4 neun Arbeitsschritte aus: Eingangsprüfung,
Datenprüfung, XML erzeugen, XML prüfen, PDF/A-3 aufbauen, Ergebnis erneut
öffnen und zurücklesen, extern gegenprüfen, Bericht schreiben, Datei
speichern. Der siebte Schritt läuft nur, wenn ein Referenzvalidator
eingerichtet ist – in der ausgelieferten Anwendung ist das nie der Fall.

Drei Eigenschaften sind dabei bindend:

- **Ohne die Bestätigung des Anwenders entsteht nichts.** Die Sperre sitzt im
  Kern, nicht in der Oberfläche.
- **Keine halb fertige Ausgabedatei.** Gespeichert wird erst, wenn alle
  verpflichtenden Prüfungen bestanden sind.
- **Das Original bleibt unverändert.** Es wird ausschließlich gelesen.

### Die zwei Wege zur fertigen Datei

Die Eingangsprüfung beantwortet drei Fragen, und sie bleiben getrennt:

| Frage | Wo sie beantwortet wird |
|---|---|
| Ist die direkte Aufwertung möglich? | `PdfAnalysisResult.CanBeUpgraded` |
| Ist die sichtbare Kopie technisch möglich? | `PdfPreflightReport.Route` |
| Hat der Anwender dem Qualitätsverlust zugestimmt? | `CreateEInvoiceRequest.RasterFallbackConfirmed` |

Zusammengefasst wären sie nicht mehr auseinanderzuhalten – „technisch möglich“
sähe aus wie „gewollt“.

`PdfProcessingRoute` kennt drei Werte:

- **`Direct`** – die Seiten des Originals werden unverändert übernommen.
  `PdfAInvoiceComposer`, unverändert.
- **`RasterFallback`** – ausschließlich für Dateien, deren **einziges**
  Hindernis die fehlende Schrifteinbettung ist, die **keine fremden Anhänge**
  tragen und deren Seiten sich nachweislich darstellen lassen
  (`IPdfRenderProbe` stellt jede Seite probeweise bei 72 dpi dar).
  `RasterFallbackComposer` über `RasterizedPdfBuilder`, fest 300 dpi.
  Die Anhangsbedingung ist keine Vorsicht auf Verdacht: Der Rasterweg baut ein
  neues Dokument und übernimmt nur Seiten und Rechnungs-XML. Für alles andere,
  was an der Vorlage hing, wäre das ein unbemerkter Verlust.
- **`Rejected`** – alles andere.

Es gibt bewusst **keine** Regel der Art „PDFium kann rendern, also ist alles
erlaubt“. Jedes Hindernis ist einzeln beurteilt; die Begründungen stehen im
Kommentar von `PdfPreflightService.ChooseRouteAsync`.

Beide Wege enden in denselben PDF/A-3-Bestandteilen (`PdfAInvoiceParts`) und
durchlaufen dieselbe Ergebnisprüfung. Der gegangene Weg steht im Prüfbericht.

Zum Rechteschutz: PDFsharp meldet für eine Datei mit Besitzerkennwort
`IsEncrypted == false` und öffnet sie anstandslos. Der Eintrag `/Encrypt` steht
im Trailer, den PDFsharp nicht offenlegt. `PdfAnalyzer` befragt deshalb
zusätzlich PdfPig – ohnehin für die Textauswertung vorhanden – und unterscheidet
so Öffnungskennwort, Rechteeinschränkung und ungeschützt. Kann PdfPig die Frage
nicht beantworten, wird sie so gestellt, wie der Ablauf sie braucht: Lässt sich
die Datei mit `PdfDocumentOpenMode.Modify` öffnen? Bleibt die Frage offen, gilt
sie als offen und nicht als „ungeschützt“.

Zur Schrifteinbettung: Geprüft wird, ob eine nicht eingebettete Schrift
tatsächlich Text zeichnet, nicht ob sie unter `/Resources /Font` steht.
`PdfAnalyzer` liest dazu den Inhaltsstrom (`ContentReader`), führt den
Grafikzustand über `q`/`Q` mit und steigt in aufgerufene Form-XObjects ab –
viele Erzeuger legen den gesamten sichtbaren Inhalt dorthin. Ein Formular ohne
eigene Schriftliste greift auf die der umgebenden Ebene zurück; auch das wird
mitgeführt, weil sich dort sonst eine nicht eingebettete Schrift verstecken
ließe.

Zwei Stellen bleiben bewusst streng: Lässt sich ein Inhaltsstrom nicht lesen,
zählt für ihn wieder jede erklärte Schrift als verwendet. Und sind Formulare
tiefer verschachtelt als `MaxFormDepth`, gilt die Einbettung als **nicht
bestätigt** – nicht als in Ordnung. Eine Tiefengrenze, die nach außen öffnet,
wäre selbst der Weg an der Prüfung vorbei.

## Lokales Diagnoselog

`Microsoft.Extensions.Logging` bleibt die einzige Logging-Abstraktion. Der
kleine `LocalFileLoggerProvider` im Core schreibt pro Programmlauf eine lokale
UTF-8-Datei nach `%LOCALAPPDATA%\EInvoiceSender\Diagnose`. Die Oberfläche
bezieht denselben `DiagnosticLogDirectory` aus der Dependency Injection und
kann genau diesen Ordner über den vorhandenen Windows-Shelldienst öffnen.

Die Datenschutzgrenze liegt vor dem Provider: Logevents erhalten nur feste
technische Kategorien, Zähler, Statuswerte, Fassungen und Laufzeiten. Namen,
Pfade, Rechnungsdaten und Dokumentinhalte werden nicht übergeben. Für eine
übergebene Exception ignoriert der Provider Message und Data vollständig und
schreibt nur Typkette und Methodennamen ohne PDB-Datei- oder Zeilenangaben.

Eine Sitzung endet spätestens bei einem MiB. Beim nächsten Start bleiben bis
zu zehn abgeschlossene Logs erhalten. Gesperrte Dateien anderer laufender
Instanzen werden übersprungen. Der Provider enthält absichtlich keinen
Netzwerkbaustein, keine Warteschlange und keinen Fehler-Rückkanal: Kann er
nicht schreiben oder rotieren, deaktiviert er nur sich selbst.

## Abhängigkeiten

`App` verweist auf `Core`. `Core` verweist auf nichts aus der Projektmappe.
Damit gibt es keine Kreise, und der Kern bleibt ohne Oberfläche prüfbar.

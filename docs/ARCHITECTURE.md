# ARCHITECTURE.md

## 1. Überblick

Klassische Schichtenarchitektur mit Ports und Adaptern. Die Fachlogik steht in
der Mitte und kennt weder PDF noch XML noch das Dateisystem.

```
┌──────────────────────────────────────────────────────────────┐
│  EInvoiceSender.Desktop  (WPF, MVVM, net10.0-windows)        │
│  Views · ViewModels · Konverter · DI-Verdrahtung             │
└───────────────────────────┬──────────────────────────────────┘
                            │ nutzt Anwendungsfälle
┌───────────────────────────▼──────────────────────────────────┐
│  EInvoiceSender.Application  (net10.0)                       │
│  Anwendungsfälle · PORTS (Interfaces) · Berichtsmodelle      │
└───────────────────────────┬──────────────────────────────────┘
                            │ nutzt
┌───────────────────────────▼──────────────────────────────────┐
│  EInvoiceSender.Domain  (net10.0, ohne Fremdpakete)          │
│  Rechnungsmodell · Summen-/Steuerberechnung · Werttypen      │
└──────────────────────────────────────────────────────────────┘

Adapter implementieren die Ports (Abhängigkeit zeigt nach innen):

  Formats        → IInvoiceXmlWriter, IInvoiceXmlReader
  Validation     → IBusinessRuleValidator, IExternalDocumentValidator
  Infrastructure → IPdfAnalyzer, IPdfAInvoiceComposer, IFileStorage,
                   ISettingsStore, IProcessRunner, IClock
  Mail           → IEmailDraftService
  Desktop        → IUserInteraction, IShellService, IPdfPreviewRenderer
```

## 2. Projekte und Verantwortung

| Projekt | Ziel-Framework | Darf abhängen von | Verantwortung |
|---|---|---|---|
| `Domain` | net10.0 | – | Rechnungsmodell, Beträge, Steuer, Rundung, Werttypen (IBAN, Land, Währung). Keine I/O. |
| `Application` | net10.0 | Domain | Anwendungsfälle (`CreateEInvoiceUseCase`, `PrepareEmailUseCase`), Ports, Berichtsmodelle, Fortschrittsmeldungen |
| `Formats` | net10.0 | Domain, Application | CII-XML nach EN 16931 erzeugen und lesen, sichere XML-Verarbeitung |
| `Validation` | net10.0 | Domain, Application | EN-16931-Geschäftsregeln, Codelisten, deutsche Fehlertexte, Adapter für externe Validatoren |
| `Infrastructure` | net10.0 | Domain, Application | PDF-Analyse, PDF/A-3-Erzeugung, ICC-Profil, Dateiablage, Einstellungen, Prozessausführung |
| `Mail` | net10.0 | Domain, Application | `.eml`-Entwurf, `mailto:`-Fallback |
| `Desktop` | net10.0-windows | alle | WPF-Oberfläche, DI-Verdrahtung, Windows-Dienste (Shell, Dialoge, Vorschau) |

Keine dieser Kanten darf umgekehrt werden. `Domain` referenziert nichts.

## 3. Der Hauptablauf

`CreateEInvoiceUseCase` orchestriert und meldet jeden Schritt einzeln an die
Oberfläche (`IProgress<PipelineStep>`):

1. **PDF prüfen** – `IPdfAnalyzer`: Signatur, Verschlüsselung, Seitenzahl,
   Schrifteinbettung, vorhandene Anhänge, bereits eingebettete Rechnungs-XML.
2. **Fachdaten prüfen** – `IBusinessRuleValidator` auf dem Domänenmodell.
   Bricht bei Fehlern ab, bevor irgendetwas erzeugt wird.
3. **XML erzeugen** – `IInvoiceXmlWriter` → `factur-x.xml` im Speicher.
4. **XML gegenprüfen** – Wohlgeformtheit, Struktur, Regeln erneut auf dem
   erzeugten XML (nicht nur auf dem Modell).
5. **PDF/A-3 erzeugen** – `IPdfAInvoiceComposer`: OutputIntent, XMP, Anhang,
   `/AF`. Bricht ab, wenn das Eingangs-PDF nicht aufwertbar ist.
6. **Ergebnis gegenprüfen** – erzeugte Datei erneut öffnen, XML extrahieren,
   erneut validieren, PDF/A-Struktur prüfen.
7. **Externe Validatoren** – sofern konfiguriert (`IExternalDocumentValidator`).
8. **Speichern** – `IFileStorage`, atomar, mit SHA-256 und Bericht.

Schlägt ein Schritt fehl, wird **keine** Ausgabedatei zurückgelassen und das
Original bleibt unverändert.

## 4. Verbotene Konstruktionen

- Geschäftslogik in Code-behind oder ViewModel (Regel A4).
- `Process.Start` außerhalb von `IProcessRunner` (Regel A5).
- Dateisystemzugriff in `Domain` (Regel A1).
- `double`/`float` für Geld (Regel A7).
- Statischer veränderlicher Zustand (Regel A6).
- Direkte Abhängigkeit der Anwendung von einem konkreten Kommandozeilenwerkzeug –
  externe Validatoren liegen immer hinter `IExternalDocumentValidator`.

## 5. Fehler- und Meldungsfluss

Fachliche Befunde sind `ValidationFinding` mit:

- `Severity` (Fehler / Warnung / Hinweis),
- `RuleId` (z. B. `BR-CO-13`) – technisch, im Detailbereich sichtbar,
- `Message` – deutscher, für Anwender verständlicher Satz,
- `FieldPath` – Feldbezug für die Oberfläche,
- `TechnicalDetail` – optional, aufklappbar.

Ausnahmen werden nie roh angezeigt. `Application` übersetzt sie an der Grenze in
`ValidationFinding` bzw. `OperationFailure`.

## 6. Nebenläufigkeit

- Die gesamte Pipeline läuft asynchron außerhalb des UI-Threads.
- PDF-Rendering (PDFtoImage) ist nicht threadsicher und läuft serialisiert hinter
  `PdfPreviewRenderer`.
- Jeder Port mit I/O nimmt ein `CancellationToken`; der Benutzer kann abbrechen.
- Temporäre Dateien liegen in einem Arbeitsverzeichnis pro Vorgang und werden im
  `finally` gelöscht.

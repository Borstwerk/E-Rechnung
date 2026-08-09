# Aufbau

Die Projektmappe besteht aus vier Projekten plus dem Installer. Mehr braucht
eine Desktopanwendung dieser Groesse nicht.

```
EInvoiceSender.sln
├── src/EInvoiceSender.Core   – der fachliche Kern     (net10.0, kein WPF)
├── src/EInvoiceSender.App    – die Oberflaeche        (net10.0-windows, WPF, x64)
├── tests/EInvoiceSender.Core.Tests
├── tests/EInvoiceSender.IntegrationTests
└── installer/EInvoiceSender.Setup                     (WiX, nur unter Windows baubar)
```

## EInvoiceSender.Core

Alles Fachliche. Kennt kein WPF und laesst sich deshalb vollstaendig
automatisiert pruefen – auch auf einem Build-Agenten ohne Bildschirm.

| Ordner | Inhalt |
|---|---|
| `Models` | Rechnung, Parteien, Betraege, Werttypen (IBAN, Waehrung, Land, Einheit), das Eingabeformular `InvoiceDraft` |
| `Calculation` | Summen- und Steuerberechnung nach EN 16931, ausschliesslich `decimal` |
| `Validation` | Regelwerk EN 16931, Codelisten, Befunde und Pruefberichte |
| `Zugferd` | CII-XML erzeugen und zurueclesen |
| `Pdf` | PDF-Analyse, Eingangspruefung, PDF/A-3-Aufwertung, XMP, ICC-Profil, Einbettung |
| `Reports` | Validierungsbericht als JSON und als Text |
| `Storage` | atomare Dateiausgabe, sichere Dateinamen, temporaere Arbeitsverzeichnisse |
| `Security` | sichere XML-Verarbeitung, Prozessausfuehrung mit Zeitlimit |
| `Settings` | Firmenvorlage als JSON, IBAN unter Windows per DPAPI geschuetzt |
| `Mail` | `.eml`-Entwurf und `mailto:`-Rueckfallweg |
| `Services` | der zentrale Dienst und die Anbindung der externen Validatoren |

### Der zentrale Dienst

Die Oberflaeche kennt genau eine Schnittstelle:

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

Die Umsetzung `EInvoiceService` fuehrt dahinter die spezialisierten Klassen
zusammen – `CiiInvoiceWriter`, `CiiInvoiceReader`, `En16931RuleValidator`,
`PdfPreflightService`, `PdfAInvoiceComposer`, `MustangValidator`,
`ValidationReportWriter`, `FileStorage`. Diese Klassen bleiben getrennt
lesbar, liegen aber in **einer** Assembly.

## EInvoiceSender.App

Nur Oberflaeche: Fenster, Ansichten, ViewModels, Windows-Dialoge,
Drag-and-drop, PDF-Vorschau, Shell-Aufrufe und das Zusammensetzen der
Abhaengigkeiten. Keine Steuer-, PDF/A-, XML- oder Rechnungslogik.

```
App.xaml(.cs)              Composition Root: eine ServiceCollection, kein Generic Host
Views/MainWindow           Rahmen, Schrittanzeige, Statuszeile, Navigation, Stoerungsanzeige
Views/Steps/               die fuenf Schritte als eigene UserControls
Views/Dialogs/             Einstellungen
ViewModels/                je Schritt ein ViewModel plus MainViewModel
Services/                  PDF-Vorschau, Windows-Shell, Systemuhr
```

Zwei Regeln gelten in der Oberflaeche ausnahmslos und werden von Tests bewacht:

- **`ConfigureAwait(true)` an jedem `await`.** Sonst laeuft die Fortsetzung auf
  einem Threadpool-Thread und WPF bricht beim naechsten Zugriff auf ein
  gebundenes Bedienelement ab.
- **Jede Eigenschaft, die eine Freigabepruefung liest, benachrichtigt ihren
  Befehl** (`[NotifyCanExecuteChangedFor]`). Sonst bleibt die Schaltflaeche im
  zuletzt bewerteten Zustand haengen.

Beide Regeln stammen aus Fehlern, die im laufenden Programm aufgetreten sind.

## Der Ablauf

```
1. PDF auswaehlen      →  AnalyzePdfAsync   →  geeignet? Gruende nennen
2. Daten erfassen      →  InvoiceDraft      →  ValidateInvoice
3. Vergleichen         →  Pflichtbestaetigung des Anwenders
4. Erzeugen            →  CreateAsync       →  neun Schritte mit Fortschritt
5. Ergebnis            →  Datei, Bericht, Pruefsumme, E-Mail-Entwurf
```

Der Kern fuehrt in Schritt 4 neun Arbeitsschritte aus: Eingangspruefung,
Datenpruefung, XML erzeugen, XML pruefen, PDF/A-3 aufbauen, Ergebnis erneut
oeffnen und zurueclesen, extern gegenpruefen, Bericht schreiben, Datei
speichern.

Drei Eigenschaften sind dabei bindend:

- **Ohne die Bestaetigung des Anwenders entsteht nichts.** Die Sperre sitzt im
  Kern, nicht in der Oberflaeche.
- **Keine halb fertige Ausgabedatei.** Gespeichert wird erst, wenn alle
  verpflichtenden Pruefungen bestanden sind.
- **Das Original bleibt unveraendert.** Es wird ausschliesslich gelesen.

## Abhaengigkeiten

`App` verweist auf `Core`. `Core` verweist auf nichts aus der Projektmappe.
Damit gibt es keine Kreise, und der Kern bleibt ohne Oberflaeche pruefbar.

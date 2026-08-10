# BorstWerk – UI-Referenz E-Rechnung

**Stand:** 10.08.2026  
**Status:** Designreferenz, keine Implementierungsfreigabe  
**Referenz:** aktueller EInvoiceSender auf dem BorstWerk-Planungsbranch

---

## 1. Ziel

EInvoiceSender bleibt fachlich und strukturell das erste Referenzprodukt der BorstWerk-Familie.

Die spätere visuelle Anpassung soll **keinen neuen Workflow erfinden**, sondern die vorhandene Oberfläche ordnen, vereinheitlichen und dezent als BorstWerk-Produkt kennzeichnen.

Der laufende Funktionsumbau wird nicht parallel durch Branding- oder Theme-Arbeiten gestört.

---

## 2. Was am heutigen EInvoiceSender bereits richtig ist

Der aktuelle `MainWindow.xaml` enthält bewusst nur:

- aktuellen Schritttitel
- globale Fehlermeldung
- aktuellen Wizard-Schritt
- Statuszeile
- Einstellungen / Neue Rechnung / Zurück / Weiter

Diese Reduktion ist für BorstWerk grundsätzlich passend.

Der aktuelle Fünf-Schritte-Prozess bleibt erhalten:

1. PDF auswählen
2. Rechnungsdaten prüfen/ergänzen
3. Prüfung
4. Erzeugung
5. Ergebnis

Es wird **keine Sidebar nur wegen des Corporate Designs** eingeführt.

---

## 3. Zielbild der Fensterstruktur

```text
┌───────────────────────────────────────────────────────────────┐
│ [BW] BorstWerk · E-Rechnung                         ?    ⚙   │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  Rechnungsdaten prüfen                                        │
│  Erkannte Angaben kontrollieren und fehlende Werte ergänzen.  │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │                                                         │  │
│  │                 aktueller Wizard-Schritt                │  │
│  │                                                         │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                               │
│  Status / fachlicher Hinweis                                  │
│                                                               │
│                         Neue Rechnung  Zurück  [Weiter]        │
└───────────────────────────────────────────────────────────────┘
```

### Kopfzeile

Dezente Produktidentität:

```text
[BorstWerk-Symbol] BorstWerk · E-Rechnung
```

Das BorstWerk-Zeichen ist klein. Der Produktname bleibt wichtiger als die Dachmarke.

Keine Claims und keine Prinzipienliste in der Kopfzeile.

---

## 4. Produktfarbe

Für E-Rechnung:

```text
ProductAccent = #176B87
```

Einsatz nur für:

- primäre Aktion
- sichtbaren Tastaturfokus, wenn kontrastreich geeignet
- kleine aktive Kennzeichnungen
- optional dünne Produktmarkierung in der Kopfzeile

Nicht verwenden für:

- große dekorative Flächen
- komplette Fensterhintergründe
- Status wie Fehler oder Warnung

Statusfarben bleiben semantisch und familienweit getrennt.

---

## 5. Bestehende Styles → spätere Tokens

Der heutige `App.xaml` hat bereits zentrale Styles. Das ist ein guter Ausgangspunkt, aber Werte sollen später semantische Namen erhalten.

### Typografie

Aktuell:

- `Überschrift`
- `Abschnitt`
- `Feldbeschriftung`
- `Herkunftshinweis`
- `Betrag`
- `Betragsbeschriftung`

Ziel:

```text
BorstWerk.Text.PageTitle
BorstWerk.Text.SectionTitle
BorstWerk.Text.FieldLabel
BorstWerk.Text.Helper
BorstWerk.Text.Technical
BorstWerk.Text.Amount
```

Deutsche Style-Namen im bestehenden Produkt müssen nicht während des laufenden Umbaus umbenannt werden. Die semantischen Namen sind Zielbild für spätere Extraktion bzw. neue Produkte.

---

## 6. Farb-Tokens

Zielstruktur:

```text
BorstWerk.Color.Neutral950
BorstWerk.Color.Neutral700
BorstWerk.Color.Neutral300
BorstWerk.Color.Neutral100
BorstWerk.Color.Surface

BorstWerk.Color.ProductAccent

BorstWerk.Color.Status.Success
BorstWerk.Color.Status.Warning
BorstWerk.Color.Status.Error
BorstWerk.Color.Status.Info
```

Dazu passende Brushes:

```text
BorstWerk.Brush.Text.Primary
BorstWerk.Brush.Text.Secondary
BorstWerk.Brush.Surface.Default
BorstWerk.Brush.Surface.Subtle
BorstWerk.Brush.Border.Default
BorstWerk.Brush.ProductAccent
BorstWerk.Brush.Status.Error
...
```

Keine Farbwerte mehr direkt in einzelnen Views, sobald die spätere Theme-Migration beginnt.

---

## 7. Befunde / Validierung

Die aktuelle Befunddarstellung ist eine Stärke und bleibt fachlich erhalten.

Heute werden bereits kombiniert:

- Symbol
- ausgeschriebener Schweregrad
- Meldung
- technische Detailangabe
- Farbe
- `AutomationProperties.Name`

Das entspricht dem BorstWerk-Grundsatz, dass Farbe niemals allein Information transportiert.

### Zielbild

```text
┌─────────────────────────────────────────────────────┐
│ × Fehler   Verkäufername fehlt                      │
│             Regel: BR-DE-...                        │
└─────────────────────────────────────────────────────┘
```

bzw.

```text
┌─────────────────────────────────────────────────────┐
│ ! Prüfen  Zahlungsziel konnte nicht sicher erkannt  │
│             Quelle: Dokumentanalyse                 │
└─────────────────────────────────────────────────────┘
```

Keine Ampel-Dashboards und keine künstlichen Prozent-Scores.

---

## 8. Eingabefelder

Das bestehende Muster wird beibehalten:

```text
Rechnungsnummer
┌─────────────────────────────────┐
│ 2026-0042                       │
└─────────────────────────────────┘
Aus der PDF erkannt
```

Regeln:

- Label bleibt sichtbar über dem Feld
- Placeholder ersetzt kein Label
- Herkunftshinweise bleiben dezent
- Validierungsfehler stehen direkt am betroffenen Feld, wenn fachlich möglich
- technische Details werden nicht zur Hauptmeldung

---

## 9. Buttons

### Primär

Pro Wizard-Schritt genau eine visuell dominante Hauptaktion.

Typischerweise:

```text
[Weiter]
```

bzw. im letzten fachlichen Schritt:

```text
[E-Rechnung erstellen]
```

### Sekundär

- Zurück
- Neue Rechnung
- Einstellungen
- optionale Hilfsaktionen

„Neue Rechnung“ darf bewusst weniger dominant sein als „Weiter“.

### Keine Branding-Buttons

Nicht vorgesehen:

- „Mehr über BorstWerk“ im Hauptworkflow
- „Open Source entdecken“
- „Unsere Prinzipien“
- Spenden-/Sponsoraktion

---

## 10. Fenstermaße und Skalierung

Aktuell:

```text
1200 × 860
Minimum 940 × 640
```

Diese Größen sind als Ausgangspunkt plausibel und werden nicht aus Designgründen verändert.

Vor späterem UI-Release prüfen:

- 100 % Skalierung
- 125 % Skalierung
- 150 % Skalierung
- 200 % Skalierung
- Minimumfenstergröße
- lange deutsche Texte
- Tastaturnavigation

---

## 11. Einstellungen

Der bestehende separate Einstellungsdialog bleibt konzeptionell sinnvoll.

Einstellungen gehören nicht in den normalen Rechnungsworkflow.

Späterer BorstWerk-Stil:

- gleiche Typografie
- gleiche Input-Styles
- gleiche Buttonhierarchie
- dieselben Statusmuster
- dezente Dachmarke

Kein zusätzlicher „BorstWerk“-Bereich notwendig.

---

## 12. „Über“-Dialog

Der spätere Info-Dialog darf enthalten:

```text
BorstWerk E-Rechnung
Version x.y.z

Kostenloses Open-Source-Werkzeug.
MIT License

Die Verarbeitung der Rechnungsdaten erfolgt lokal.

Mit KI-Unterstützung entwickelt.
Quellcode / Dokumentation: GitHub
```

Der Text bleibt kurz. Keine Projektgeschichte und kein Manifest.

---

## 13. Was ausdrücklich nicht verändert wird

Die BorstWerk-Migration rechtfertigt **keine** Änderung an:

- Rechnungslogik
- PDF-Erkennung
- ZUGFeRD-/Factur-X-Erzeugung
- Validatoren
- Datenmodellen
- Reihenfolge des bewährten Workflows
- fachlichen Regeln

Design und Fachlogik bleiben getrennte Änderungsgründe.

---

## 14. Geplanter technischer Migrationsschnitt

Erst nach fachlicher Stabilisierung des parallel bearbeiteten EInvoiceSender.

### Phase UI-A – Tokenisieren

Nur visuelle Konstanten zentralisieren:

- Farben
- Brushes
- Abstände
- Schriftgrößen
- Radien

Keine Layout-Neukonzeption.

### Phase UI-B – Basiscontrols

Styles vereinheitlichen:

- Button Primary / Secondary / Danger
- TextBox / ComboBox
- CheckBox
- Status-/Befundkarte
- Überschriften

### Phase UI-C – Branding

- BorstWerk-Logo/Symbol
- Produktbezeichnung
- App-Icon
- Info-Dialog

### Phase UI-D – Regression

- bestehende UI-/Layouttests
- manuelle Skalierungstests
- Tastaturtest
- Screenreader-/Automation-Smoke-Test
- visueller Vergleich des kompletten Fünf-Schritte-Workflows

---

## 15. Wann gemeinsame BorstWerk-UI entsteht

Noch **nicht**.

EInvoiceSender bleibt zunächst autonom.

Erst beim Bau von BorstWerk GoBD-Doku wird verglichen:

```text
Wird Style/Control in beiden Produkten wirklich identisch gebraucht?
                  │
          nein ───┴───> lokal lassen
                  │
                 ja
                  ▼
      als gemeinsame Komponente prüfen
```

Damit wächst die gemeinsame Bibliothek aus echten Anforderungen statt aus Architekturvorfreude.

---

## 16. Freigabekriterien vor Implementierung

- [x] BorstWerk-Grundidentität festgelegt
- [x] neutrale öffentliche Positionierung festgelegt
- [x] Produktfarbe für E-Rechnung festgelegt
- [x] bestehender Wizard als Referenz bestätigt
- [x] Prinzip „Prinzipien umsetzen, nicht plakatieren“ festgelegt
- [ ] finales BorstWerk-Symbol ausgewählt
- [ ] finales App-Icon-System ausgewählt
- [ ] EInvoice-Funktionsumbau fachlich stabil
- [ ] konkreter visueller Vorher-/Nachher-Abgleich freigegeben

Bis dahin bleibt dieses Dokument ausschließlich Referenz und verändert keinen Produktivcode.

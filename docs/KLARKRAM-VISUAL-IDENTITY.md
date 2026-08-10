# KlarKram – Visuelle Identität 1.0

**Status:** Arbeitsgrundlage · 10.08.2026  
**Projekt:** private, nicht-kommerzielle Open-Source-Werkzeugfamilie für kleine Unternehmen  
**Arbeitstitel:** KlarKram

> Ziel: ruhig, klar, freundlich und professionell wirken – ohne Startup-Theater, Enterprise-Optik oder Hobbyprojekt-Anmutung.

---

## 1. Namensentscheidung

`KlarKram` bleibt vorerst der bevorzugte Arbeitstitel.

Die normale Websuche am 10.08.2026 ergab keine erkennbare etablierte Softwaremarke oder Produktfamilie dieses Namens. Es existieren ältere Nutzernamen und private Verkaufsaccounts mit der Zeichenfolge `klarkram`; daraus wird derzeit keine relevante Kollision für eine Software-Werkzeugfamilie abgeleitet.

Wichtig: Das ist **keine Markenrecherche und keine Freigabe zur Markenanmeldung**. Vor einer öffentlichen Veröffentlichung unter diesem Namen wird separat geprüft, ob relevante Marken oder gleichartige Softwareangebote bestehen. Solange bleibt KlarKram ein Arbeitstitel.

### Schreibweise

Immer:

```text
KlarKram
```

Nicht:

```text
Klarkram
KLARKRAM
Klar Kram
klarKram
```

In technischen Bezeichnern darf `KlarKram` verwendet werden, z. B. `KlarKram.UI` oder `KlarKram.GoBD`.

---

## 2. Positionierung

### Kurzbeschreibung

> Kostenlose Werkzeuge für den Kram, der eben gemacht werden muss.

### Sachliche Variante

> Kostenlose, lokale Softwarewerkzeuge für kleine Unternehmen.

### Was KlarKram nicht sein soll

- keine Firma und kein Startup-Auftritt
- keine persönliche Selbstdarstellungsmarke
- kein ERP- oder Office-Komplettpaket
- keine Cloudplattform
- kein SaaS-Angebot
- kein Freemium-Modell
- keine Werbeplattform
- kein KI-Marketinglabel

Die Projektidentität steht vor der Person des Maintainers.

---

## 3. Markencharakter

KlarKram soll sich anfühlen wie ein ordentlich sortierter Werkzeugkasten:

- **klar:** verständliche Sprache, eindeutige Bedienung
- **ruhig:** keine optische Reizüberflutung
- **verlässlich:** Status und Konsequenzen sind sichtbar
- **freundlich:** keine Behörden- oder Enterprise-Kälte
- **bodenständig:** kein Marketing-Sprech
- **professionell:** kostenlos ist kein Synonym für unfertig

### Tonalität in der Anwendung

Bevorzugt:

- „Rechnung prüfen“
- „2 Angaben fehlen noch“
- „Backup wurde erstellt“
- „Diese Datei kann nicht verarbeitet werden“

Vermeiden:

- „Processing failed unexpectedly“
- „Awesome! Your task was completed successfully!“
- „Compliance Score 98 %“ ohne belastbare fachliche Bedeutung
- kryptische Fehlercodes ohne verständlichen Begleittext

---

## 4. Logo-Richtung

### Primäres Zeichen

Das KlarKram-Zeichen kombiniert drei Gedanken:

1. **K** als Wiedererkennung des Namens,
2. **Haken** als „erledigt / geklärt“,
3. **Dokument- oder Werkzeugform** als Hinweis auf praktische Arbeit.

Das Symbol muss auch bei 16×16 bzw. 24×24 Pixeln noch als einfache Form funktionieren.

### Gestaltungsregeln

- geometrisch, aber nicht technisch-kalt
- maximal zwei bis drei visuelle Grundelemente
- keine Verläufe im Primärlogo
- keine Schatten
- keine feinen Linien, die in kleinen Größen verschwinden
- keine Büroklammer, Glühbirne, Wolke, Handschlag oder Rakete
- kein Maskottchen
- kein Monogramm in einem generischen Hexagon

### Wortmarke

`KlarKram` in einer neutralen, kräftigen Sans-Serif. Für Produktoberflächen wird die Windows-Systemtypografie genutzt; das Logo selbst soll typografisch ebenfalls nüchtern und gut lesbar bleiben.

### Logo-Varianten

1. Symbol + Wortmarke – Standard
2. Symbol allein – App-Icon, Taskleiste, kleine Flächen
3. monochrom hell
4. monochrom dunkel

Produktnamen stehen **nicht im eigentlichen Logo**. Beispiel:

```text
[KlarKram-Zeichen] KlarKram
E-Rechnung
```

statt eines eigenen Logos für jedes Werkzeug.

---

## 5. Farb-System

Die Dachmarke bleibt neutral. Anwendungen erhalten eine einzige eigene Akzentfarbe.

### Neutrale Basis

| Token | Wert | Einsatz |
|---|---|---|
| `Neutral.950` | `#172027` | primärer dunkler Text |
| `Neutral.800` | `#26333C` | starke Sekundärflächen |
| `Neutral.700` | `#40505B` | sekundärer Text |
| `Neutral.500` | `#71808A` | Hinweise, Icons |
| `Neutral.300` | `#C9D0D5` | Rahmen |
| `Neutral.200` | `#DDE2E6` | Trenner |
| `Neutral.100` | `#EEF1F3` | ruhige Hintergründe |
| `Neutral.050` | `#F7F8F8` | App-Hintergrund |
| `White` | `#FFFFFF` | Karten / Eingabeflächen |

### Dachmarken-Akzent

`KlarKram.Teal = #167D7F`

Das Blaugrün wirkt sachlich und freundlich, ohne nach Bank, Windows-Standardblau oder „Öko-App“ auszusehen.

Passende Zustände:

| Token | Wert |
|---|---|
| `Brand.700` | `#0F5F61` |
| `Brand.600` | `#167D7F` |
| `Brand.500` | `#27989A` |
| `Brand.100` | `#DDF1F1` |
| `Brand.050` | `#EFF8F8` |

### Produkt-Akzente

| Werkzeug | Akzent | Hex |
|---|---|---|
| E-Rechnung | Petrol/Blau | `#176B87` |
| GoBD-Doku | Ocker | `#A66A12` |
| IT-Notfall | Ziegelrot | `#A94735` |
| Arbeitsschutz | Orange | `#B85F16` |
| HACCP | Grün | `#3F7B55` |
| Datenschutz | Violett | `#6C5A93` |

Diese Farben dürfen **nicht als alleiniger Informationsträger** dienen.

---

## 6. Statusfarben

Statusfarben sind familienweit identisch und semantisch reserviert.

| Status | Text/Bordüre | Hintergrund | Symbol |
|---|---|---|---|
| Erfolg | `#2F6B45` | `#EDF6F0` | ✓ |
| Information | `#315F78` | `#EEF6FA` | i |
| Warnung | `#8A6512` | `#FBF6E8` | ! |
| Fehler | `#A13B32` | `#FBEFEE` | × |
| Neutral | `#58656D` | `#F2F4F5` | • |

Jeder Status besteht mindestens aus **Text + Symbol**. Farbe ist zusätzliche Orientierung, nie die einzige.

---

## 7. Typografie

### Windows-Desktop

Primär:

```text
Segoe UI Variable
```

Fallback:

```text
Segoe UI
```

Keine separat mitgelieferten Schriftdateien.

### Größenraster

| Rolle | Größe | Gewicht |
|---|---:|---|
| Seitentitel | 24 px | SemiBold |
| Abschnittstitel | 18 px | SemiBold |
| Kartentitel | 15 px | SemiBold |
| Standardtext | 14 px | Regular |
| Beschriftung | 13 px | Regular / SemiBold |
| Hinweis | 12 px | Regular |
| technische Details | 11 px | Regular |

Keine unnötige Großschreibung. Überschriften bleiben normal geschrieben.

---

## 8. Abstands-System

Basis ist ein 4-Pixel-Raster.

```text
Space.1 = 4
Space.2 = 8
Space.3 = 12
Space.4 = 16
Space.5 = 20
Space.6 = 24
Space.8 = 32
Space.10 = 40
Space.12 = 48
```

Regel:

- innerhalb einer logisch zusammengehörenden Gruppe: 4–12 px
- zwischen Gruppen: 16–24 px
- zwischen Hauptabschnitten: 24–40 px

Abstände sollen Struktur erzeugen; zusätzliche Rahmen werden nur eingesetzt, wenn sie wirklich nötig sind.

---

## 9. Radien und Linien

```text
Radius.Small  = 4
Radius.Medium = 6
Radius.Large  = 10
```

Standardrahmen:

```text
1 px Neutral.300
```

Keine Pillenoptik für jede Schaltfläche. Stark gerundete Controls bleiben Statuschips und besonderen Elementen vorbehalten.

---

## 10. App-Shell

Desktopwerkzeuge verwenden grundsätzlich eine gemeinsame visuelle Sprache, aber nicht zwingend dieselbe Navigation.

### Standardaufbau

```text
┌──────────────────────────────────────────────────────────┐
│  KlarKram  ·  Produktname                         ?  ⚙  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Seitentitel                                             │
│  kurze verständliche Erklärung                           │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │ Arbeitsinhalt                                      │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

Produkte mit linearem Arbeitsablauf – etwa EInvoiceSender – dürfen ihren Wizard behalten. Produkte mit mehreren unabhängigen Bereichen dürfen eine linke Navigation verwenden.

**Die Informationsarchitektur folgt dem Problem, nicht dem Branding.**

---

## 11. Buttons

### Primär

- Produkt-Akzent als Hintergrund
- heller Text
- nur eine echte Primäraktion pro Bereich

Beispiele:

- „Weiter“
- „E-Rechnung erzeugen“
- „Backup erstellen“

### Sekundär

- neutraler Hintergrund oder transparente Fläche
- 1-px-Rahmen

### Destruktiv

- nicht dauerhaft rot gefüllte Oberfläche
- Rot nur bei wirklich destruktiver Aktion
- zusätzliche Bestätigung, wenn Datenverlust möglich ist

### Sprache

Buttons benennen eine konkrete Handlung:

Gut:

```text
PDF auswählen
Rechnung prüfen
Bericht speichern
```

Schlechter:

```text
OK
Ausführen
Bestätigen
```

wenn der Kontext nicht eindeutig ist.

---

## 12. Eingabefelder

Beschriftung grundsätzlich **über dem Feld**, nicht nur als Placeholder.

```text
Rechnungsnummer
┌───────────────────────────┐
│ 2026-0042                 │
└───────────────────────────┘
Aus der PDF erkannt
```

Fehler:

```text
Rechnungsnummer
┌───────────────────────────┐
│                           │  ← Fehlerbordüre
└───────────────────────────┘
× Rechnungsnummer fehlt
```

Technische Details werden nicht in die Hauptmeldung gedrückt.

---

## 13. Karten und Gruppen

Karten sparsam verwenden.

Eine Karte ist sinnvoll, wenn sie einen eigenständigen Informationsblock bildet. Nicht jedes Formularfeld bekommt eine eigene Karte.

Standard:

- weißer Hintergrund
- 1-px-Rahmen `Neutral.200`
- `Radius.Medium`
- 16–24 px Innenabstand
- keine oder nur extrem zurückhaltende Schatten

---

## 14. Icons

Bevorzugt werden einfache Outline-Icons mit konsistenter Strichstärke.

Regeln:

- Icon unterstützt Text, ersetzt ihn bei wichtigen Funktionen nicht
- keine Emoji als UI-Icons
- keine Mischung aus gefüllten, Outline- und 3D-Symbolen
- Statussymbole bleiben zusätzlich textlich benannt

Für Standardfunktionen soll möglichst auf eine etablierte, lizenzkompatible Iconquelle oder Windows-eigene Symbolik zurückgegriffen werden statt eigene hundertteilige Iconsets zu pflegen.

---

## 15. App-Icons

Das App-Icon kombiniert das gemeinsame KlarKram-Zeichen mit der Produktakzentfarbe.

Beispielprinzip:

```text
KlarKram-Zeichen
+ E-Rechnung-Akzent = E-Rechnung-App-Icon
+ GoBD-Akzent       = GoBD-App-Icon
```

Keine zusätzlichen Mini-Symbole wie Rechnung + Paragraph + Haken + Eurozeichen in einem 32×32-Icon. Wiedererkennbarkeit vor Inhaltsillustration.

---

## 16. Barrierefreiheit

Verbindliche Grundregeln:

- verständliche Fokusreihenfolge
- sichtbarer Tastaturfokus
- ausreichende Kontraste
- Status nie nur über Farbe
- relevante Controls mit Automation-Namen
- verständliche Beschriftungen
- keine kritischen Informationen nur in Tooltips
- Touch-Ziele und Buttons nicht unnötig klein
- 200-%-Skalierung darf keinen Kernworkflow unbenutzbar machen

EInvoiceSender enthält bereits gute Ansätze, etwa Status mit Text, Symbol und AutomationProperties. Diese Muster gelten als Referenz und werden nicht durch rein dekorative Gestaltung zurückgebaut.

---

## 17. Dark Mode

**Nicht Bestandteil von Design-System 1.0.**

Begründung: Ein sauberer heller Modus ist wertvoller als zwei halb gepflegte Themes. Die Tokens werden trotzdem semantisch benannt, damit ein späteres Dark Theme möglich bleibt.

---

## 18. XAML-Tokenstruktur

Künftige WPF-Projekte sollen visuelle Werte nicht quer durch einzelne Views hart codieren.

Zielstruktur:

```text
UI/
└── Themes/
    ├── Colors.xaml
    ├── Brushes.xaml
    ├── Typography.xaml
    ├── Spacing.xaml
    ├── Controls.Buttons.xaml
    ├── Controls.Input.xaml
    ├── Controls.Status.xaml
    └── Theme.xaml
```

Beispielnamen:

```text
KlarKram.Color.Neutral950
KlarKram.Brush.Text.Primary
KlarKram.Brush.Surface.Default
KlarKram.Brush.Border.Default
KlarKram.Brush.ProductAccent
KlarKram.Brush.Status.Error
KlarKram.FontSize.Body
KlarKram.Space.4
```

Die konkrete Implementierung wird erst nach einem visuellen Mockup und einem Abgleich mit EInvoiceSender vorgenommen.

---

## 19. EInvoiceSender – spätere visuelle Migration

EInvoiceSender ist Referenzprodukt und wird nicht parallel zu seiner laufenden fachlichen Überarbeitung umgebaut.

Nach fachlicher Stabilisierung:

### Stufe 1 – Tokens

- hart codierte neutrale Farben ersetzen
- Statusfarben vereinheitlichen
- Abstände und Typografie tokenisieren
- bestehende Accessibility erhalten

### Stufe 2 – Branding

- KlarKram-Zeichen
- Produktname `E-Rechnung`
- Produktakzent `#176B87`
- About-/Info-Bereich

### Stufe 3 – Controls

Nur tatsächlich wiederverwendete Styles auslagern.

### Stufe 4 – gemeinsame Bibliothek

Erst wenn mindestens ein zweites KlarKram-WPF-Projekt dieselben Komponenten real benötigt.

---

## 20. Definition of Done für Visual Identity 1.0

Die visuelle Identität gilt als 1.0, wenn:

- [x] Markencharakter definiert ist
- [x] Farbpalette definiert ist
- [x] Produktakzente definiert sind
- [x] Statussystem definiert ist
- [x] Typografie definiert ist
- [x] Abstands- und Radius-System definiert ist
- [x] App-Shell-Grundsätze definiert sind
- [x] UI-Komponenten-Grundsätze definiert sind
- [x] Accessibility-Regeln definiert sind
- [x] XAML-Tokenstruktur geplant ist
- [ ] Logo visuell geprüft und ausgewählt ist
- [ ] App-Icon visuell geprüft ist
- [ ] ein neutrales UI-Mockup geprüft ist
- [ ] EInvoice-Mockup mit bestehendem Workflow geprüft ist
- [ ] Arbeitstitel vor öffentlichem Release abschließend auf relevante Namenskollisionen geprüft ist

---

## 21. Nächster Schritt

Nicht sofort XAML umbauen.

Zuerst werden drei Dinge visuell geprüft:

1. KlarKram-Logo + Symbol
2. App-Icon-System
3. Beispieloberfläche mit E-Rechnung-Akzent

Erst nach Auswahl dieser Richtung werden konkrete WPF-Ressourcen umgesetzt.

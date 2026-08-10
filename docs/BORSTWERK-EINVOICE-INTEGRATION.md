# BorstWerk – Integrationsauftrag für EInvoiceSender

**Status:** verbindliche Umsetzungsvorgabe nach fachlicher Stabilisierung des laufenden EInvoice-Umbaus  
**Produkt:** EInvoiceSender / künftige sichtbare Produktbezeichnung „BorstWerk E-Rechnung“  
**Dachmarke:** BorstWerk  
**Stand:** 10.08.2026

---

## 1. Ziel

EInvoiceSender soll nach Abschluss der laufenden fachlichen Arbeiten visuell und strukturell in die BorstWerk-Werkzeugfamilie integriert werden.

Die Integration ist **kein funktionaler Neubau** und **kein UX-Redesign des Fachprozesses**. Der bestehende E-Rechnungsworkflow bleibt erhalten. Ziel ist, das bereits funktionierende Produkt mit einer einheitlichen, ruhigen BorstWerk-Identität auszustatten und seine derzeit verstreuten visuellen Konstanten in ein wartbares Design-System zu überführen.

Die Umsetzung muss sich an folgenden Dokumenten orientieren:

- `docs/BORSTWERK-FOUNDATION.md`
- `docs/BORSTWERK-VISUAL-IDENTITY.md`
- `docs/BORSTWERK-PRESENTATION-GUIDELINES.md`
- `docs/BORSTWERK-UI-REFERENCE-EINVOICE.md`

Zusätzlich wird dem Umsetzungs-Chat das freigegebene BorstWerk-Logo-/App-Icon-Design als Bildreferenz mitgegeben.

---

## 2. Zentrale Leitplanke

> **Prinzipien umsetzen, nicht plakatieren.**

Die Anwendung soll durch ruhige Bedienung, lokale Verarbeitung, gute Lesbarkeit, fehlende Telemetrie und klare Statusmeldungen überzeugen. Diese Eigenschaften werden nicht als Werbebanner im Hauptworkflow dargestellt.

Im Vordergrund stehen:

1. Produkt
2. Aufgabe
3. Bedienung

Projektphilosophie, Open-Source-Hinweise, KI-Unterstützung und technische Hintergründe gehören nur in passende Informationsbereiche oder Dokumentation.

---

## 3. Was ausdrücklich erhalten bleibt

Folgende bestehende Eigenschaften dürfen durch die BorstWerk-Integration nicht verschlechtert oder ohne fachlichen Grund umgebaut werden:

- der lineare Fünf-Schritte-Workflow
- bestehende ViewModels und Fachlogik
- PDF-Auswahl und PDF-Erkennung
- Rechnungsdatenbearbeitung
- Review-/Prüfschritt
- ZUGFeRD-/Factur-X-Erzeugung
- PDF/A-Erzeugung
- CII-/EN-16931-Validierung
- externe Referenzvalidatoren
- Datei-/Mail-/Exportlogik
- bestehende Settings-Funktionalität
- Tastaturbedienung
- AutomationProperties und Accessibility
- Statusdarstellung mit Text und Symbol statt reiner Farbcodierung
- vorhandene Unit-, Integrations- und Layouttests
- Installer-/Releasepipeline

Keine fachliche Funktion darf allein für das Branding geändert werden.

---

## 4. Nicht umsetzen

Nicht Bestandteil dieses Auftrags sind:

- keine Sidebar nur wegen des Corporate Designs
- kein Dashboard
- keine Startseite vor dem bestehenden Workflow
- kein Login
- kein Benutzerkonto
- keine Cloudanbindung
- keine Telemetrie
- kein Tracking
- kein automatischer Crash-Upload
- kein News-/Updatefeed im Hauptfenster
- keine Werbung für BorstWerk
- kein großer Mission-/Prinzipienbereich
- kein Dark Mode in dieser Ausbaustufe
- keine neue gemeinsame `BorstWerk.UI`-Bibliothek nur für dieses eine Produkt
- keine Architekturzerlegung ohne konkreten Wartungsnutzen
- keine Änderungen an fachlichen Standards oder Validatorversionen im Rahmen dieses Designauftrags

---

## 5. Sichtbare Produktidentität

Die Anwendung soll künftig als BorstWerk-Produkt erkennbar sein, ohne dass die Dachmarke die eigentliche Aufgabe überdeckt.

Bevorzugte sichtbare Bezeichnung:

```text
BorstWerk E-Rechnung
```

Fenstertitel beispielsweise:

```text
BorstWerk E-Rechnung
```

oder, wenn zusätzlicher Kontext benötigt wird:

```text
BorstWerk E-Rechnung – E-Rechnung erstellen
```

`EInvoiceSender` darf intern in Assembly-, Namespace- oder Projektbezeichnungen zunächst erhalten bleiben, wenn eine Umbenennung keinen konkreten Nutzer- oder Wartungsnutzen bringt. Eine große technische Rename-Aktion ist ausdrücklich nicht Teil dieses Auftrags.

---

## 6. Logo und App-Icon

Das freigegebene BorstWerk-Zeichen wird als gemeinsame Markenbasis verwendet.

### Dachmarke

- geometrisches `B`
- Graphit als Hauptfarbe
- warmer Ocker-/Werkstatt-Akzent
- subtile Werkzeug-/Haken-Anspielung
- kein Cartoon-Schwein
- kein Maskottchen
- keine dekorativen Effekte oder Verläufe im Primärzeichen

### App-Icon

Für E-Rechnung soll das gemeinsame BorstWerk-Zeichen verwendet werden.

Es muss in den benötigten Windows-Größen sauber funktionieren, insbesondere:

- 256×256
- 128×128
- 64×64
- 48×48
- 32×32
- 16×16

Das Icon soll in Anwendung, Taskleiste, Startmenü und Installer konsistent sein.

Falls die technische Asset-Erstellung aus der Bildreferenz ein vektorisiertes bzw. sauber nachgebautes Primärzeichen erfordert, soll das Logo kontrolliert geometrisch rekonstruiert werden; kein blindes Hochskalieren eines Raster-Mockups.

---

## 7. Produktfarbe E-Rechnung

Der E-Rechnungs-Akzent ist:

```text
#176B87
```

Die Produktfarbe dient für:

- Primäraktionen
- aktiven bzw. aktuellen Schritt
- dezente Hervorhebungen
- produktbezogene Icons oder Akzentlinien

Sie darf nicht als alleiniger Statusindikator dienen.

Die Dachmarkenfarbe Ocker wird nur sehr zurückhaltend für das BorstWerk-Zeichen bzw. die Markenidentität genutzt. Im eigentlichen Arbeitsbereich dominiert die Produktfarbe.

---

## 8. Semantische XAML-Tokens

Der derzeitige `App.xaml` enthält mehrere direkt eingetragene visuelle Werte. Diese sollen in semantisch benannte Ressourcen überführt werden.

Ziel ist keine überkomplizierte Theme-Engine, sondern eine nachvollziehbare zentrale Ressourcenstruktur.

Empfohlene Struktur innerhalb des bestehenden App-Projekts:

```text
Themes/
├── Colors.xaml
├── Brushes.xaml
├── Typography.xaml
├── Spacing.xaml
├── Buttons.xaml
├── Inputs.xaml
├── Status.xaml
└── Theme.xaml
```

Wenn für den aktuellen Projektumfang weniger Dateien klarer sind, darf sinnvoll zusammengefasst werden. Wartbarkeit ist wichtiger als die Dateizahl.

### Beispielhafte semantische Ressourcen

```text
BorstWerk.Color.Neutral950
BorstWerk.Color.Neutral700
BorstWerk.Color.Neutral300
BorstWerk.Color.Background
BorstWerk.Color.ProductAccent

BorstWerk.Brush.Text.Primary
BorstWerk.Brush.Text.Secondary
BorstWerk.Brush.Surface.Default
BorstWerk.Brush.Surface.Subtle
BorstWerk.Brush.Border.Default
BorstWerk.Brush.ProductAccent
BorstWerk.Brush.Status.Success
BorstWerk.Brush.Status.Warning
BorstWerk.Brush.Status.Error
BorstWerk.Brush.Status.Information

BorstWerk.FontSize.PageTitle
BorstWerk.FontSize.SectionTitle
BorstWerk.FontSize.Body
BorstWerk.FontSize.Label
BorstWerk.FontSize.Hint

BorstWerk.Space.1
BorstWerk.Space.2
BorstWerk.Space.3
BorstWerk.Space.4
BorstWerk.Space.6
BorstWerk.Space.8
```

Nicht jeder mögliche Token muss eingeführt werden. Nur Werte tokenisieren, die tatsächlich Designbedeutung oder Wiederverwendung besitzen.

---

## 9. Typografie

Primär:

```text
Segoe UI Variable
```

Fallback:

```text
Segoe UI
```

Keine externen Schriftdateien mitliefern.

Orientierung:

- Seitentitel: ca. 24 px, SemiBold
- Abschnittstitel: ca. 18 px, SemiBold
- Kartentitel: ca. 15 px, SemiBold
- Standardtext: ca. 14 px
- Beschriftung: ca. 13 px
- Hinweis/Metatext: ca. 12 px
- technische Detailinformation: ca. 11 px

Die tatsächlichen Größen dürfen an bestehende WPF-Skalierung angepasst werden, solange die Hierarchie erhalten bleibt und die Anwendung bei 125 %, 150 % und 200 % Skalierung sinnvoll nutzbar bleibt.

---

## 10. Neutrale Farbwelt

Orientierung laut BorstWerk Design-System:

```text
Neutral.950  #172027
Neutral.800  #26333C
Neutral.700  #40505B
Neutral.500  #71808A
Neutral.300  #C9D0D5
Neutral.200  #DDE2E6
Neutral.100  #EEF1F3
Neutral.050  #F7F8F8
White        #FFFFFF
```

Die Oberfläche bleibt hell, ruhig und kontrastreich.

Keine großflächigen dunklen Arbeitsflächen im Hauptworkflow. Dunkle Flächen sind höchstens für kleine Markenbereiche oder den Info-Dialog zulässig, wenn dies visuell und hinsichtlich Kontrast überzeugt.

---

## 11. Statussystem

Bestehende semantische Unterscheidungen bleiben erhalten.

Empfohlene Statusfarben:

### Erfolg

```text
Text/Bordüre: #2F6B45
Hintergrund:  #EDF6F0
Symbol:       ✓
```

### Information

```text
Text/Bordüre: #315F78
Hintergrund:  #EEF6FA
Symbol:       i
```

### Warnung

```text
Text/Bordüre: #8A6512
Hintergrund:  #FBF6E8
Symbol:       !
```

### Fehler

```text
Text/Bordüre: #A13B32
Hintergrund:  #FBEFEE
Symbol:       ×
```

Verbindlich:

> Status niemals nur über Farbe kommunizieren.

Die bestehende Finding-Darstellung mit Severity-Text, Symbol, Meldung, technischem Detail und AutomationProperties ist als gute Referenz zu erhalten bzw. visuell zu harmonisieren.

---

## 12. Hauptfenster und Wizard

Das bestehende Hauptfenster bleibt konzeptionell bestehen:

```text
Kopf / aktueller Schritt
Fehler- oder Hinweisbereich
aktueller Wizard-Inhalt
Statusmeldung
Navigation
```

Der fünfstufige Ablauf bleibt sichtbar und verständlich.

Wenn ein visueller Fortschrittsindikator ergänzt oder modernisiert wird, muss er den bestehenden Prozess widerspiegeln und darf nicht zusätzliche Navigation vortäuschen.

Die Schrittnamen richten sich nach dem realen Workflow. Keine Änderung nur zur optischen Symmetrie.

---

## 13. Buttons und Eingaben

### Buttons

- nur eine echte Primäraktion je Kontext
- Produktfarbe für Primäraktion
- neutrale Sekundäraktionen
- klare aktive/fokussierte Zustände
- bestehende Tastatur-Mnemonics erhalten, soweit sinnvoll
- Mindestgrößen nicht verkleinern

Buttontexte bleiben konkret:

```text
PDF auswählen
Weiter
Zurück
Neue Rechnung
Einstellungen
E-Rechnung erzeugen
```

### Eingaben

- sichtbare Labels bleiben erhalten
- Placeholder ersetzen keine Beschriftung
- Herkunftshinweise bleiben sichtbar, wenn fachlich vorhanden
- Fehler erhalten verständlichen Text
- keine roten Rahmen ohne Erklärung

---

## 14. Spacing und Radien

Basis: 4-Pixel-Raster.

Orientierung:

```text
4
8
12
16
20
24
32
40
48
```

Radien zurückhaltend:

```text
Small   4
Medium  6
Large  10
```

Keine Pillenoptik für Standardbuttons und Standardfelder.

Keine starken Schatten. Karten und Gruppen werden bevorzugt über Abstand, Hintergrund und feine Rahmen strukturiert.

---

## 15. Info-/Über-Bereich

Ein kleiner, bewusst unaufdringlicher Info-Bereich darf ergänzt oder angepasst werden.

Inhalt:

- BorstWerk-Zeichen
- Produktname `E-Rechnung`
- Version
- kurze Beschreibung
- Open-Source-Lizenz
- Hinweis auf lokale Verarbeitung / keine Telemetrie, sofern technisch weiterhin korrekt
- sachlicher KI-Hinweis
- ggf. Link bzw. Verweis zum Repository, sofern im finalen Release gewünscht
- Drittanbieterhinweise bzw. Verweis darauf

### KI-Hinweis

Bevorzugte Formulierung:

> Mit KI-Unterstützung entwickelt. Konzeption, Anforderungen, Prüfung und Projektpflege durch den Maintainer.

Kein großer KI-Hinweis im normalen Workflow.

---

## 16. GitHub-/README-Präsentation

Die BorstWerk-Grundprinzipien werden nicht als großes Hero-Banner auf die Repository-Startseite gesetzt.

README-Reihenfolge bevorzugt:

1. Was macht das Tool?
2. Für wen ist es gedacht?
3. Installation / Download
4. Nutzung
5. wichtige Einschränkungen
6. Entwicklung / Build
7. Lizenz / technische Hintergründe
8. weiterführende Dokumentation

Das Brand-Moodboard bleibt interne bzw. vertiefende Designreferenz und ist nicht automatisch README-Titelbild.

---

## 17. Accessibility – verbindlich

Die Designintegration darf bestehende Accessibility nicht verschlechtern.

Mindestens prüfen:

- Tastaturbedienung aller Hauptfunktionen
- sinnvolle Tab-Reihenfolge
- sichtbarer Fokus
- AutomationProperties für relevante Controls
- Status nicht nur farblich
- ausreichende Kontraste
- Text bei 200-%-Skalierung
- kein Abschneiden wichtiger Inhalte
- verständliche Fehlermeldungen
- keine kritischen Informationen nur per Tooltip

Bestehende AutomationProperties und LiveSettings werden nur geändert, wenn dies objektiv eine Verbesserung darstellt.

---

## 18. Tests und Qualität

Nach der visuellen Integration müssen alle bestehenden Tests weiterhin grün sein.

Zusätzlich sollen sinnvolle UI-/Strukturtests ergänzt werden, insbesondere dort, wo bestehende Tests bereits XAML-Strukturen absichern.

Prüfen:

- `dotnet format` / Formatprüfung
- Build ohne neue Warnungen
- Unit Tests
- Integration Tests
- vorhandene Layout-/Bindingtests
- Windows-Build
- Publish
- Installer-Build
- vorhandene Referenzvalidatoren

Die Designintegration darf keinerlei fachliche Referenzvalidierung umgehen oder lockern.

---

## 19. Manuelle visuelle Abnahme

Vor Abschluss mindestens folgende Fensterzustände visuell prüfen:

1. Start / PDF-Auswahl
2. erfolgreich erkannte PDF
3. PDF mit Warnungen
4. Rechnungsdaten mit Herkunftshinweisen
5. Validierungsfehler
6. Review-Schritt
7. Erzeugung
8. erfolgreiches Ergebnis
9. Einstellungen
10. Info-/Über-Bereich

Jeweils mindestens bei:

- 100 % Skalierung
- 150 % Skalierung
- 200 % Skalierung

Wenn möglich zusätzlich bei kleiner zulässiger Fenstergröße.

---

## 20. Definition of Done

Die BorstWerk-EInvoice-Integration ist abgeschlossen, wenn:

- [ ] sichtbarer Produktname auf BorstWerk E-Rechnung angepasst ist
- [ ] freigegebenes BorstWerk-Zeichen sauber als Asset vorliegt
- [ ] Windows-App-Icon in relevanten Größen integriert ist
- [ ] Installer dasselbe Icon verwendet
- [ ] Produktakzent `#176B87` umgesetzt ist
- [ ] harte zentrale Farbwerte in semantische Ressourcen überführt sind
- [ ] Typografie und Abstände dem Design-System entsprechen
- [ ] Statusdarstellung semantisch und barrierearm bleibt
- [ ] kein fachlicher Workflow unnötig umgebaut wurde
- [ ] kein Dashboard / keine Sidebar / kein Branding-Banner hinzugefügt wurde
- [ ] Info-/Über-Bereich dezent integriert ist
- [ ] keine Telemetrie oder Netzwerkabhängigkeit hinzugekommen ist
- [ ] alle bestehenden Tests grün sind
- [ ] CI weiterhin grün ist
- [ ] Publish und Installer funktionieren
- [ ] manuelle visuelle Prüfung bei 100/150/200 % erfolgt ist
- [ ] README sachlich aktualisiert ist, ohne Projektphilosophie als Hero-Marketing zu platzieren

---

## 21. Vorgehensweise für den Umsetzungs-Chat

Vor Änderungen:

1. aktuellen Branch und aktuellen Produktstand lesen
2. vorhandenes XAML und die bestehenden Tests analysieren
3. die vier BorstWerk-Grundlagendokumente lesen
4. die mitgegebene Logo-/App-Icon-Referenz ansehen
5. prüfen, ob parallel laufende Änderungen inzwischen Teile dieser Vorgabe bereits berühren

Dann in kleinen, nachvollziehbaren Schritten arbeiten:

### Phase A – Assets und Tokens

- Logo-/Icon-Assets vorbereiten
- semantische Ressourcen einführen
- noch keine fachliche Struktur verändern

### Phase B – bestehende UI auf Tokens umstellen

- Farben
- Typografie
- Abstände
- Statusdarstellung
- Buttons / Eingaben

### Phase C – Branding

- Produktname
- Logo
- App-/Installer-Icon
- Info-/Über-Bereich

### Phase D – Validierung

- Tests
- Build
- Installer
- visuelle Prüfung
- Accessibility

Keine große Sammeländerung, wenn sich die Phasen getrennt prüfen lassen.

---

## 22. Abschlussbericht

Der Umsetzungs-Chat soll am Ende knapp dokumentieren:

- welche Dateien geändert wurden
- welche visuellen Tokens eingeführt wurden
- welche Assets entstanden sind
- welche bestehenden Komponenten bewusst unverändert blieben
- welche Tests/Validatoren liefen
- Ergebnis von Build und CI
- bekannte verbleibende visuelle Einschränkungen
- Screenshots bzw. Beschreibung der geprüften Hauptzustände

Wenn bei der Umsetzung ein Konflikt zwischen BorstWerk-Design und fachlicher Bedienbarkeit entsteht, hat die **fachlich verständlichere und barriereärmere Lösung Vorrang**. Der Konflikt wird dokumentiert, statt das Design dogmatisch durchzudrücken.

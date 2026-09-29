# BorstWerk E-Rechnung 0.3.0 – Release Notes

**Entwurf – nicht veröffentlicht.** Der lokale Versionsstand ist nach
Reviewfreigabe und manueller Testung zur PR-Erstellung freigegeben. Integration,
endgültiger CI-Abnahmekandidat und Veröffentlichungsfreigabe stehen noch aus.

## Neu gegenüber 0.2.0

- **Vorhandene E-Rechnung technisch prüfen:** Ein eigener read-only Prüfmodus
  zeigt PDF- und Anhanginformationen, SHA-256, gelesene CII-Kerndaten, Summen
  und technische Befunde. Ein laufender Erzeugungsvorgang bleibt erhalten.
- **Verständlichere Anzeige:** Technische Kennungen bleiben für Support und
  Nachvollziehbarkeit sichtbar, stehen aber nachrangig unter dem Fachtext.
  Der Checker zeigt Datumswerte ohne Uhrzeit und Geldbeträge mit deutschen
  Nachkommastellen und der tatsächlich gelesenen Währung.
- **Verkäuferidentifikation verständlich getrennt:** Schritt 2 unterscheidet
  steuerliche Angaben und E-Rechnungs-Identifikation. Die vorhandene USt-IdNr.
  zählt auch zur Identifikation; eine Steuernummer allein genügt nicht.
  Die vom Kunden vergebene Kennung heißt Lieferanten-/Kreditorennummer.
- **Positionen in mehrseitigen PDFs:** Eine unterstützte Positionstabelle
  kann erkannt werden, wenn sie vollständig einschließlich Summengrenze auf
  genau einer Seite liegt; andere Seiten dürfen Deckblatt oder Begleittext
  enthalten. Die Erkennung bleibt konservativ und verwirft mehrdeutige Fälle.
- **Abgesicherter Installerbau:** Das MSI wird ausschließlich aus einem
  frischen Publish über den gemeinsamen Releaseweg gebaut. Direkte Builds
  des WiX-Projekts werden gegen die Nutzung alter Programmdateien gesperrt.
- **Aktualisierte Standarddokumentation und Währungsprüfung:** Das geprüfte
  Erzeugungsziel ist ZUGFeRD 2.5.2 / Factur-X 1.09.2 mit D22B. Normgültige
  Währungscodes und die kleinere angebotene Auswahlliste bleiben getrennt.

## Grenzen bleiben ausdrücklich bestehen

- Der Checker ist eine technische Bestandsaufnahme, **keine vollständige
  EN-16931- oder PDF/A-Konformitätsprüfung**. PDF/A-Angaben aus der Datei sind
  Deklarationen, keine bestandene Referenzprüfung.
- Kein Reparieren geprüfter Dateien; XRechnung/Order-X werden nicht als
  auswertbare Rechnungsformate unterstützt. Mehrdeutige Rechnungsanhänge
  werden nicht willkürlich ausgewählt.
- Keine über Seiten fortgesetzten Positionstabellen, kein OCR und keine
  automatische Positionsübernahme aus unverstandenen Tabellen.
- Die Original-PDF bleibt unverändert. Eine angebotene sichtbare PDF/A-Kopie
  benötigt ausdrückliche Zustimmung und verliert die sichtbare Textebene.
- Keine Cloud, Telemetrie, automatische Übertragung oder automatischer
  E-Mail-Versand. Java und externe Referenzvalidatoren werden nur in
  Entwicklung und Releaseprüfung verwendet, nicht auf dem Anwenderrechner.

## Bereitstellung und Upgrade – noch abzunehmen

Geplant sind ein self-contained Windows-x64-MSI, die portable ZIP-Fassung
und `SHA256SUMS.txt`, jeweils mit den bestehenden Drittanbieterhinweisen und
Lizenztexten. Der .NET-Runtimepatch bleibt 10.0.11. Signing wird nicht eingeführt.

0.3.0 hat einen eigenen festen ProductCode; der UpgradeCode bleibt erhalten.
Upgrade 0.2.0 → 0.3.0, Erhalt von Firmendaten/Einstellungen sowie Installer-
und Portable-Verhalten müssen am endgültigen Kandidaten noch praktisch
bestätigt werden. Diese Notizen behaupten keine abgeschlossene Releaseabnahme.

Nachweise und Freigabestatus stehen im
[Abnahmeprotokoll](ACCEPTANCE-0.3.0-WINDOWS.md).

# Offene Punkte

Nur echte, noch nicht erledigte Aufgaben. Abgeschlossenes gehört nicht
hierher, sondern in die Commit-Historie.

Verbindliche Arbeit für eine konkrete Version steht in der jeweiligen
Requirements-Datei, aktuell in [`REQUIREMENTS-0.2.0.md`](REQUIREMENTS-0.2.0.md).
Wiederkehrende manuelle Release- und Windows-Prüfungen stehen getrennt in
[`RELEASE-CHECKLIST.md`](RELEASE-CHECKLIST.md).

## Releaseweg

- **Releaseweg gegen veralteten Publish-Bestand absichern.** Ein direkter Build
  des WiX-/Installerprojekts aus Visual Studio kann einen bereits vorhandenen
  Bestand aus `artifacts/publish/win-x64` paketieren, statt ihn neu zu
  erzeugen. Am 25.08.2026 entstand dadurch bei einem lokalen Test ein formal
  korrektes 0.2.0-MSI mit älteren Programmbinärdateien – die Versionsangaben
  stimmten, der Inhalt nicht.

  Der offizielle Releaseweg über `build/Build-Release.ps1` war davon **nicht**
  betroffen: Er erzeugt den Publish-Bestand frisch und räumt vorher auf.

  Künftig soll der direkte Installerbau entweder einen frischen Publish
  erzwingen oder – besser – hart abbrechen und auf `Build-Installer.ps1`
  beziehungsweise `Build-Release.ps1` verweisen. Ein Bau, der stillschweigend
  Altbestand einpackt, ist schlimmer als einer, der sich weigert.

  Kein Blocker für 0.2.0, da der freigegebene und getestete Releaseweg korrekt
  arbeitet.

## Bedienung

- **Steuernummer und EN-16931-Verkäuferkennung im Rechnungsformular deutlicher
  voneinander abgrenzen.** Bei der Windows-Abnahme am 24.08.2026 fiel auf, dass
  beide Angaben im Verkäuferblock nebeneinanderstehen und fachlich leicht zu
  verwechseln sind: Die Steuernummer sieht aus wie eine Kennung, ist aber keine
  im Sinne von BR-CO-26. Denkbar wäre eine sichtbare Trennung, etwa
  „Steuerliche Angaben“ gegenüber „Verkäuferidentifikation für die
  E-Rechnung“.

  Das ist ausdrücklich **keine** fachliche Änderung: BT-29, BT-30 und BT-31
  bleiben die drei Wege, BR-CO-26 zu erfüllen, BT-32 bleibt eine separate
  steuerliche Angabe, und die Prüfung bleibt unverändert. Es geht allein
  darum, dem Anwender die Unterscheidung vor dem Befund sichtbar zu machen
  statt erst durch ihn.

## Code

- **Wiederholung im Eingabeformular auflösen.** Die Karten für Verkäufer und
  Käufer haben denselben Aufbau, unterscheiden sich aber in jedem
  Bindungspfad. Sauber wäre ein gemeinsames `PartyDraft` im Entwurf, an das
  beide Karten mit einer gemeinsamen Vorlage binden – dann verschwinden rund
  200 Zeilen XAML. Der Umbau greift in `InvoiceDraft`, in die Vorbefüllung und
  in die Regelprüfung; er gehört nicht in eine Fehlerbehebung.
- **Herkunftshinweis als eigenes Bedienelement.** Beschriftung, Feld und
  Hinweis stehen heute je Feld ausgeschrieben da. Ein kleines
  ContentControl mit einer Vorlage würde das zusammenfassen. Zurückgestellt,
  weil eine fehlerhafte ControlTemplate erst zur Laufzeit auffällt und die
  Oberfläche hier nicht ausgeführt werden kann.
- **InvoiceDraft verkleinern.** Die Klasse ist groß und enthält neben dem
  Entwurf auch die Umwandlung in das Domänenmodell. Ein erster mechanischer
  Zerlegungsversuch wurde zurückgenommen, weil Textersetzung Zeichenketten,
  FieldPath-Angaben und Namen in Objektinitialisierern veränderte. Ein späterer
  Umbau braucht einen begründeten Plan und werkzeuggestützte Refactorings –
  nicht bloß eine Reaktion auf die Zeilenzahl.

Große Klassen allein sind kein Fehler. `PdfAnalyzer`, `EInvoiceService`,
`InvoiceDraft` oder große XAML-Dateien werden nicht nur deshalb umgebaut, weil
sie viele Zeilen haben. Refactorings brauchen einen konkreten Nutzen für
Wartbarkeit, Testbarkeit oder Risikoreduktion und durchlaufen den normalen
BorstWerk-Planungsprozess.

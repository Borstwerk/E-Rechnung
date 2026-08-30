# Offene Punkte

Nur echte, noch nicht erledigte Aufgaben. Abgeschlossenes gehört nicht
hierher, sondern in die Commit-Historie.

Verbindliche Arbeit für eine konkrete Version steht in der jeweiligen
Requirements-Datei, aktuell in [`REQUIREMENTS-0.3.0.md`](REQUIREMENTS-0.3.0.md).
Wiederkehrende manuelle Release- und Windows-Prüfungen stehen getrennt in
[`RELEASE-CHECKLIST.md`](RELEASE-CHECKLIST.md).

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

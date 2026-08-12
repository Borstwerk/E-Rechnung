# Offene Punkte

Nur echte, noch nicht erledigte Aufgaben. Abgeschlossenes gehört nicht
hierher, sondern in die Commit-Historie.

Verbindliche Arbeit für eine konkrete Version steht in der jeweiligen
Requirements-Datei, aktuell in [`REQUIREMENTS-0.2.0.md`](REQUIREMENTS-0.2.0.md).
Wiederkehrende manuelle Release- und Windows-Prüfungen stehen getrennt in
[`RELEASE-CHECKLIST.md`](RELEASE-CHECKLIST.md).

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

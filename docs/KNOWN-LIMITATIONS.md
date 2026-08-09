# Bekannte Grenzen

Diese Punkte sind bewusst so und nicht etwa übersehen.

## Nicht jede PDF lässt sich verwenden

Es gibt **keine frei verwendbare .NET-Bibliothek**, die ein beliebiges PDF
nach PDF/A-3 wandelt. Die Anwendung wertet deshalb geeignete Dateien auf und
lehnt ungeeignete ab – mit einer Begründung und einem Hinweis, was zu tun
ist. Sie erzeugt nie eine Datei, die nur *vermutlich* normgerecht ist.

Häufigste Ablehnungsgründe:

- **Nicht eingebettete Schriftarten.** PDF/A verlangt, dass jede verwendete
  Schrift in der Datei steckt. Betroffen sind unter anderem alle PDFs, die
  sich auf die 14 Standardschriften verlassen. Abhilfe: beim Erzeugen der PDF
  die Schrifteinbettung einschalten.
- **Digital signierte PDFs.** Eine Signatur bezieht sich auf den unveränderten
  Inhalt. Das Einbetten der Rechnungsdaten würde sie ungültig machen. Solche
  Dateien werden deutlich abgelehnt, nicht stillschweigend verändert.
- **Verschlüsselte PDFs** und Dateien mit aktiven Inhalten.

## Bereits vorhandene Rechnungsdaten

Enthält die gewählte PDF schon strukturierte Rechnungsdaten, werden diese
**nie stillschweigend ersetzt**. Die Anwendung fragt ausdrücklich nach.

## Die eigene Regelprüfung ist kein Konformitätsnachweis

`En16931RuleValidator` meldet Fehler früh und in verständlichem Deutsch.
Was er durchlässt, gilt damit **nicht** als normgerecht bestätigt. Die
Freigabe erteilen ausschließlich die externen Referenzvalidatoren
(CEN-Schematron und veraPDF über die Mustangproject-CLI).

Ohne diese Werkzeuge prüft die Anwendung PDF/A nur strukturell. Der Bericht
weist das aus und schreibt **NICHT AUSGEFÜHRT** in die Textfassung. Ein
fehlender Validator wird nie als bestandene Prüfung dargestellt.

## E-Mail

Es wird **nichts versendet**. Die Anwendung legt einen Entwurf an und öffnet
ihn; abschicken bleibt eine bewusste Handlung.

Für das **"neue Outlook"** ist ein Verlust von Anhängen bei HTML-Nachrichten
berichtet. Deshalb wird bewusst reiner Text verwendet. Ob `X-Unsent: 1` im
jeweils aktuellen Build als Entwurf öffnet, ist von außen nicht zuverlässig
vorhersagbar. Es gibt darum immer einen Rückfallweg: `mailto:` ohne Anhang
plus Hinweis auf den Ausgabeordner.

## DPAPI und die IBAN

Die IBAN wird unter Windows mit dem Datenschutz des Betriebssystems (DPAPI)
verschlüsselt abgelegt und ist nur unter dem eigenen Benutzerkonto lesbar.
Steht DPAPI nicht zur Verfügung, wird sie **gar nicht** gespeichert, statt im
Klartext auf der Platte zu landen.

## Grenzen der Datenerkennung

Nach der Auswahl einer PDF versucht die Anwendung, das Formular aus dem bereits
vorhandenen PDF-Text vorauszufuellen. Das ist eine **Schreibhilfe**, keine
Quelle der Wahrheit.

- **Kein OCR.** Ausgewertet wird nur Text, der schon in der Datei steht. Eine
  eingescannte Rechnung besteht aus Bildern und liefert nichts. Die Anwendung
  sagt das ausdrücklich und Sie erfassen die Daten von Hand.
- **Kein Wert geht ungeprüft weiter.** Der Weg lautet immer: PDF →
  Erkennungsergebnis → Formular → Ihre Bestätigung → E-Rechnung. Das ist
  durch die Bauart sichergestellt, nicht durch eine Zusage.
- **Unsichere Werte fuellen nichts aus.** Jeder gelesene Wert trägt eine
  Vertrauensstufe. Was nicht mindestens mittlere Sicherheit hat, wird
  angezeigt, aber nicht eingetragen.
- **Rechnungspositionen werden gar nicht erkannt.** Sie müssen von Hand
  erfasst werden. Rechnungstabellen sind zwischen Vorlagen zu uneinheitlich,
  um sie mit zeilenbasierten Regeln zuverlässig zu treffen, und eine falsche
  Position würde den Rechnungsbetrag verändern.
- **Verkäufer und Käufer** werden nur zugeordnet, wenn es ein belastbares
  Signal gibt: die gespeicherte eigene Firmenvorlage oder ein Schlüsselwort
  wie "Rechnung an". Ohne beides bleiben die Felder leer. Vertauschte Parteien
  wären schlimmer als leere Felder.
- **Das Land des Käufers wird nicht erkannt** und auch nicht angenommen. Es
  bleibt leer, bis Sie es auswählen – ein stilles "DE" würde bei einem
  ausländischen Kunden eine formal gültige, inhaltlich falsche Rechnung
  ergeben.
- **Der aus der PDF gelesene Gesamtbetrag** dient dem Abgleich mit den
  erfassten Positionen. Er ist ein zweites, unabhängiges Signal – keine
  rechtliche Wahrheit. Eine Abweichung blockiert nichts, sie weist hin.
- **Alles bleibt örtlich.** Der Text wird im Arbeitsspeicher ausgewertet und
  danach verworfen. Er wird nicht gespeichert, nicht protokolliert und nicht
  übertragen. Es gibt keine Cloud-Auswertung und keinen externen Dienst.

## Keine Steuerberatung

Geprüft wird das **Format**, nicht die inhaltliche oder steuerliche
Richtigkeit. Ob ein Steuersatz stimmt, eine Befreiung zutrifft oder ein
Reverse-Charge-Fall vorliegt, entscheidet nicht die Anwendung.

Ebenso wenig prüft sie, ob die erfassten Daten der sichtbaren PDF
entsprechen. Das kann nur ein Mensch – deshalb die Pflichtbestätigung in
Schritt 3.

## Bewusst nicht enthalten

Rechnungserstellung, Nummernvergabe, Kundenverwaltung, Artikelstamm,
Buchhaltung, DATEV-Export, Mahnwesen, Zahlungsabgleich, Peppol, Portale,
Mandantenfähigkeit, Cloud, Mehrbenutzerbetrieb und OCR als maßgebliche
Quelle.

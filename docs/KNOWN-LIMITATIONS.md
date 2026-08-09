# Bekannte Grenzen

Diese Punkte sind bewusst so und nicht etwa uebersehen.

## Nicht jede PDF laesst sich verwenden

Es gibt **keine frei verwendbare .NET-Bibliothek**, die ein beliebiges PDF
nach PDF/A-3 wandelt. Die Anwendung wertet deshalb geeignete Dateien auf und
lehnt ungeeignete ab – mit einer Begruendung und einem Hinweis, was zu tun
ist. Sie erzeugt nie eine Datei, die nur *vermutlich* normgerecht ist.

Haeufigste Ablehnungsgruende:

- **Nicht eingebettete Schriftarten.** PDF/A verlangt, dass jede verwendete
  Schrift in der Datei steckt. Betroffen sind unter anderem alle PDFs, die
  sich auf die 14 Standardschriften verlassen. Abhilfe: beim Erzeugen der PDF
  die Schrifteinbettung einschalten.
- **Digital signierte PDFs.** Eine Signatur bezieht sich auf den unveraenderten
  Inhalt. Das Einbetten der Rechnungsdaten wuerde sie ungueltig machen. Solche
  Dateien werden deutlich abgelehnt, nicht stillschweigend veraendert.
- **Verschluesselte PDFs** und Dateien mit aktiven Inhalten.

## Bereits vorhandene Rechnungsdaten

Enthaelt die gewaehlte PDF schon strukturierte Rechnungsdaten, werden diese
**nie stillschweigend ersetzt**. Die Anwendung fragt ausdruecklich nach.

## Die eigene Regelpruefung ist kein Konformitaetsnachweis

`En16931RuleValidator` meldet Fehler frueh und in verstaendlichem Deutsch.
Was er durchlaesst, gilt damit **nicht** als normgerecht bestaetigt. Die
Freigabe erteilen ausschliesslich die externen Referenzvalidatoren
(CEN-Schematron und veraPDF ueber die Mustangproject-CLI).

Ohne diese Werkzeuge prueft die Anwendung PDF/A nur strukturell. Der Bericht
weist das aus und schreibt **NICHT AUSGEFUEHRT** in die Textfassung. Ein
fehlender Validator wird nie als bestandene Pruefung dargestellt.

## E-Mail

Es wird **nichts versendet**. Die Anwendung legt einen Entwurf an und oeffnet
ihn; abschicken bleibt eine bewusste Handlung.

Fuer das **"neue Outlook"** ist ein Verlust von Anhaengen bei HTML-Nachrichten
berichtet. Deshalb wird bewusst reiner Text verwendet. Ob `X-Unsent: 1` im
jeweils aktuellen Build als Entwurf oeffnet, ist von aussen nicht zuverlaessig
vorhersagbar. Es gibt darum immer einen Rueckfallweg: `mailto:` ohne Anhang
plus Hinweis auf den Ausgabeordner.

## DPAPI und die IBAN

Die IBAN wird unter Windows mit dem Datenschutz des Betriebssystems (DPAPI)
verschluesselt abgelegt und ist nur unter dem eigenen Benutzerkonto lesbar.
Steht DPAPI nicht zur Verfuegung, wird sie **gar nicht** gespeichert, statt im
Klartext auf der Platte zu landen.

## Grenzen der Datenerkennung

Nach der Auswahl einer PDF versucht die Anwendung, das Formular aus dem bereits
vorhandenen PDF-Text vorauszufuellen. Das ist eine **Schreibhilfe**, keine
Quelle der Wahrheit.

- **Kein OCR.** Ausgewertet wird nur Text, der schon in der Datei steht. Eine
  eingescannte Rechnung besteht aus Bildern und liefert nichts. Die Anwendung
  sagt das ausdruecklich und Sie erfassen die Daten von Hand.
- **Kein Wert geht ungeprueft weiter.** Der Weg lautet immer: PDF →
  Erkennungsergebnis → Formular → Ihre Bestaetigung → E-Rechnung. Das ist
  durch die Bauart sichergestellt, nicht durch eine Zusage.
- **Unsichere Werte fuellen nichts aus.** Jeder gelesene Wert traegt eine
  Vertrauensstufe. Was nicht mindestens mittlere Sicherheit hat, wird
  angezeigt, aber nicht eingetragen.
- **Rechnungspositionen werden gar nicht erkannt.** Sie muessen von Hand
  erfasst werden. Rechnungstabellen sind zwischen Vorlagen zu uneinheitlich,
  um sie mit zeilenbasierten Regeln zuverlaessig zu treffen, und eine falsche
  Position wuerde den Rechnungsbetrag veraendern.
- **Verkaeufer und Kaeufer** werden nur zugeordnet, wenn es ein belastbares
  Signal gibt: die gespeicherte eigene Firmenvorlage oder ein Schluesselwort
  wie "Rechnung an". Ohne beides bleiben die Felder leer. Vertauschte Parteien
  waeren schlimmer als leere Felder.
- **Das Land des Kaeufers wird nicht erkannt** und auch nicht angenommen. Es
  bleibt leer, bis Sie es auswaehlen – ein stilles "DE" wuerde bei einem
  auslaendischen Kunden eine formal gueltige, inhaltlich falsche Rechnung
  ergeben.
- **Der aus der PDF gelesene Gesamtbetrag** dient dem Abgleich mit den
  erfassten Positionen. Er ist ein zweites, unabhaengiges Signal – keine
  rechtliche Wahrheit. Eine Abweichung blockiert nichts, sie weist hin.
- **Alles bleibt oertlich.** Der Text wird im Arbeitsspeicher ausgewertet und
  danach verworfen. Er wird nicht gespeichert, nicht protokolliert und nicht
  uebertragen. Es gibt keine Cloud-Auswertung und keinen externen Dienst.

## Keine Steuerberatung

Geprueft wird das **Format**, nicht die inhaltliche oder steuerliche
Richtigkeit. Ob ein Steuersatz stimmt, eine Befreiung zutrifft oder ein
Reverse-Charge-Fall vorliegt, entscheidet nicht die Anwendung.

Ebenso wenig prueft sie, ob die erfassten Daten der sichtbaren PDF
entsprechen. Das kann nur ein Mensch – deshalb die Pflichtbestaetigung in
Schritt 3.

## Bewusst nicht enthalten

Rechnungserstellung, Nummernvergabe, Kundenverwaltung, Artikelstamm,
Buchhaltung, DATEV-Export, Mahnwesen, Zahlungsabgleich, Peppol, Portale,
Mandantenfaehigkeit, Cloud, Mehrbenutzerbetrieb und OCR als massgebliche
Quelle.

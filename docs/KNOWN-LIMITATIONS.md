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

# Bekannte Grenzen

Diese Punkte sind bewusst so und nicht etwa übersehen.

## Nicht jede PDF lässt sich verwenden

Es gibt **keine frei verwendbare .NET-Bibliothek**, die ein beliebiges PDF
nach PDF/A-3 wandelt. Die Anwendung wertet deshalb geeignete Dateien auf und
lehnt ungeeignete ab – mit einer Begründung und einem Hinweis, was zu tun
ist. Sie erzeugt nie eine Datei, die nur *vermutlich* normgerecht ist.

Es gibt genau zwei Wege, und der direkte hat immer Vorrang.

### Der direkte Weg

Die Seiten des Originals werden unverändert übernommen und um die fehlenden
PDF/A-3-Bestandteile ergänzt. Der Text der Rechnung bleibt dabei Text.

### Die sichtbare Kopie

Fehlt der Datei **nur** die Schrifteinbettung, gibt es einen zweiten Weg: Die
Anwendung stellt jede Seite örtlich dar und baut aus den Seitenbildern ein
neues Dokument. Die nicht eingebettete Schrift verschwindet dabei nicht durch
einen Kniff, sondern weil es sie im Ergebnis nicht mehr gibt – es steht kein
Text mehr im sichtbaren Teil, nur noch ein Bild davon.

Das kostet etwas, und die Anwendung sagt es vor der Entscheidung:

- Der Text ist danach nicht mehr markierbar und in der Anzeige nicht mehr
  durchsuchbar.
- Verknüpfungen und Formularfunktionen gehen verloren.
- Die Datei kann größer werden.
- Die **Rechnungsdaten** bleiben vollständig maschinenlesbar. Sie stecken in
  der eingebetteten XML und sind von der Darstellung unabhängig – für den
  Empfänger einer E-Rechnung ist das der Teil, der zählt.

**Nicht angeboten wird er, wenn an der Datei weitere Anhänge hängen** – ein
Lieferschein, ein Stundenzettel, ein Foto. Die sichtbare Kopie ist ein neues
Dokument; was nicht ausdrücklich hineingelegt wird, ist darin nicht mehr da.
Die Eingangsprüfung verspricht zu jedem solchen Anhang „Er bleibt erhalten“ –
auf dem direkten Weg gilt das, auf dem Rasterweg nicht. Lieber kein Angebot als
ein Verlust, den niemand bemerkt.

Dieser Weg wird **nie automatisch und nie stillschweigend** gewählt. Er wird
angeboten, und erst die ausdrückliche Zustimmung öffnet ihn. Die Zustimmung
gilt für den einen Vorgang und wird nirgends gespeichert. Das Original bleibt
unverändert; gelesen wird es, mehr nicht.

Auch die sichtbare Kopie durchläuft die vollständige Ergebnisprüfung –
erneutes Öffnen, byteweiser Vergleich der eingebetteten XML, PDF/A-Kennung,
Prüfsumme und, wo eingerichtet, die externen Referenzvalidatoren. Der
Prüfbericht hält den gegangenen Weg fest; man sieht ihn der fertigen Datei
sonst nicht an.

### Was weiterhin abgelehnt wird

- **Beschädigte PDFs.** Was auf dem Seitenbild landete, wäre ungewiss. Eine
  Rechnung, deren Inhalt man raten muss, wird nicht erzeugt.
- **Digital signierte PDFs.** Eine Signatur bezieht sich auf den unveränderten
  Inhalt. Das Einbetten der Rechnungsdaten würde sie ungültig machen. Solche
  Dateien werden deutlich abgelehnt, nicht stillschweigend verändert.
- **PDFs mit Öffnungskennwort.** Ohne Kennwort gibt es nichts zu lesen. Die
  Anwendung fragt bewusst **nicht** nach dem Kennwort und verwaltet keines.
- **PDFs mit Besitzerkennwort.** Sie lassen sich anzeigen, aber nicht ändern –
  der Rechteinhaber hat festgelegt, was mit ihnen geschehen darf. Über diese
  Festlegung setzt sich die Anwendung nicht hinweg. Solche Dateien sind
  **nicht beschädigt**, und die Meldung sagt das auch: Sie nennt die
  Berechtigungseinschränkung und rät zu einer ungeschützten Fassung.
- **PDFs mit aktiven Inhalten** (JavaScript, startende Aktionen). Dass sie
  beim Rastern verschwänden, ist kein Grund, sie zu übergehen.

### Eingescannte Rechnungen

Geprüft wird, ob eine nicht eingebettete Schrift **tatsächlich Text zeichnet**,
nicht ob sie irgendwo als Ressource eingetragen ist. Sehr viele Scanner und
Ausgabetreiber schreiben eine Schriftressource in jede Seite, auch wenn darauf
nur ein Bild liegt; eine eingescannte Rechnung wurde deshalb früher wegen einer
Schrift abgelehnt, die im Dokument nirgends erscheint. Enthält eine Datei
dagegen Bildseiten **und** echten Text mit fehlender Einbettung, bleibt es beim
Hindernis – die Lockerung gilt der Verwendung, nicht dem Dateityp.

Eine PDF, aus der sich kein Text lesen lässt, ist verarbeitbar – die
Datenerkennung findet dann aber nichts, sagt das geradeheraus, und die
Rechnungsdaten werden von Hand erfasst. **Eine Texterkennung gibt es nicht.**
Ein Werkzeug, das aus einem Bild Beträge errät und ins Formular schreibt, wäre
schlimmer als eines, das schweigt: Am Ende bestätigen Sie, dass die erfassten
Daten mit der sichtbaren Rechnung übereinstimmen.

## Bereits vorhandene Rechnungsdaten

Enthält die gewählte PDF schon strukturierte Rechnungsdaten, werden diese
**nie stillschweigend ersetzt**. Die Anwendung fragt ausdrücklich nach.

## Die eigene Regelprüfung ist kein Konformitätsnachweis

`En16931RuleValidator` meldet Fehler früh und in verständlichem Deutsch.
Was er durchlässt, gilt damit **nicht** als normgerecht bestätigt. Die
Freigabe erteilen ausschließlich die externen Referenzvalidatoren
(CEN-Schematron und veraPDF über die Mustangproject-CLI).

**Diese Werkzeuge laufen in Entwicklung und Release, nicht auf Ihrem
Rechner.** Sie brauchen eine Java-Laufzeit, und die soll niemand
nachinstallieren müssen, um eine Rechnung zu schreiben. Geprüft wird damit die
Anwendung – nicht jede einzelne Rechnung, die Sie damit erzeugen.

Die installierte Anwendung prüft PDF/A deshalb nur strukturell. Der Bericht zu
jeder erzeugten Datei sagt das: Er nennt die ausgeführten Prüfungen und
schreibt, wenn kein Referenzvalidator eingerichtet war. Eine nicht
stattgefundene Prüfung wird nie als bestandene dargestellt.

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
vorhandenen PDF-Text vorauszufüllen. Das ist eine **Schreibhilfe**, keine
Quelle der Wahrheit.

- **Kein OCR.** Ausgewertet wird nur Text, der schon in der Datei steht. Eine
  eingescannte Rechnung besteht aus Bildern und liefert nichts. Die Anwendung
  sagt das ausdrücklich und Sie erfassen die Daten von Hand.
- **Kein Wert geht ungeprüft weiter.** Der Weg lautet immer: PDF →
  Erkennungsergebnis → Formular → Ihre Bestätigung → E-Rechnung. Das ist
  durch die Bauart sichergestellt, nicht durch eine Zusage.
- **Unsichere Werte füllen nichts aus.** Jeder gelesene Wert trägt eine
  Vertrauensstufe. Was nicht mindestens mittlere Sicherheit hat, wird
  angezeigt, aber nicht eingetragen.
- **Rechnungspositionen werden nur aus klar aufgebauten Tabellen erkannt.**
  Angenommen wird eine Tabelle nur, wenn sie auf einer Seite steht, einen
  eindeutigen Kopf mit Beschreibung, Menge und Einzelpreis hat, nur die
  Steuersätze 7 % oder 19 % verwendet und ihre Summe die im Dokument gefundene
  Summe trifft. Fehlt eine dieser Bedingungen, entsteht **keine einzige**
  Position, und die Tabelle ist wie bisher von Hand zu erfassen. Eine teilweise
  übernommene Tabelle gibt es nicht: Sie sähe vollständig aus und würde den
  Rechnungsbetrag verändern.
- **Eine genannte Mengeneinheit muss unterstützt sein.** Unterstützt sind
  Stück, Stunde, Kilogramm und Meter – in einer eigenen Spalte oder direkt
  hinter der Menge (`4,00 Std.`). Nennt die Rechnung eine andere Einheit, etwa
  `Fass`, wird die **gesamte** Tabelle verworfen. Eine im Dokument stehende
  Angabe stillschweigend zu übergehen wäre schlimmer, als gar nichts zu
  erkennen.
- **Nennt die Rechnung gar keine Mengeneinheit, bleibt das Feld leer.** Die
  Positionen werden dann trotzdem übernommen; im Formular fehlt nur die
  Einheit. Schritt 1 und Schritt 2 sagen ausdrücklich, bei wie vielen
  Positionen das der Fall ist. Vor der Ausgabe muss die Einheit ergänzt
  werden – ohne sie entsteht keine Rechnung. Ein stilles „Stück“ wäre bei einer
  Stundenrechnung nicht aufgefallen und hätte eine falsche E-Rechnung ergeben.
- **Jede Spalte der Tabelle muss benannt und verstanden sein.** Eine
  unbekannte Zusatzspalte verwirft die Tabelle. Die einzige Ausnahme ist eine
  Lieferdatumsspalte; sie wird erkannt, damit die Spaltengrenzen stimmen, und
  erzeugt kein Rechnungsfeld. Spalten für Rabatt, Nachlass, Zuschlag oder
  Preisbasismenge werden nicht unterstützt und verwerfen die Tabelle.
- **Eine lange Beschreibungszeile kann die ganze Tabelle verwerfen.** Reicht
  ein Text über die aus dem Tabellenkopf abgeleitete Spaltenbreite hinaus, ist
  die Zeile nicht mehr eindeutig einer Spalte zuzuordnen. Die Erkennung lehnt
  dann die gesamte Tabelle ab, statt zu raten.
- **Bereits erfasste Positionen werden nie überschrieben.** Steht schon eine
  Zeile im Formular, bleibt die erkannte Tabelle draußen – vollständig. Die
  Meldung in Schritt 2 sagt dann ausdrücklich, wie viele erkannte Positionen
  aus diesem Grund nicht übernommen wurden.
- **Verkäufer und Käufer** werden nur zugeordnet, wenn es ein belastbares
  Signal gibt: die gespeicherte eigene Firmenvorlage oder ein Schlüsselwort
  wie "Rechnung an". Ohne beides bleiben die Felder leer. Vertauschte Parteien
  wären schlimmer als leere Felder.
- **Das Land des Käufers wird nur aus einem eindeutigen Käuferkontext
  gelesen.** Steht es im Adressblock des Rechnungsempfängers, wird es
  vorgeschlagen; lässt es sich nicht belastbar zuordnen, bleibt das Feld leer,
  bis Sie es auswählen. Das Land des Verkäufers wird dabei nicht ersatzweise
  übernommen, und ein stilles "DE" gibt es nicht – es würde bei einem
  ausländischen Kunden eine formal gültige, inhaltlich falsche Rechnung
  ergeben.
- **Der aus der PDF gelesene Gesamtbetrag** dient dem Abgleich mit den
  erfassten Positionen. Er ist ein zweites, unabhängiges Signal – keine
  rechtliche Wahrheit. Eine Abweichung blockiert nichts, sie weist hin.
- **Alles bleibt örtlich.** Der Text wird im Arbeitsspeicher ausgewertet und
  danach verworfen. Er wird nicht gespeichert, nicht protokolliert und nicht
  übertragen. Es gibt keine Cloud-Auswertung und keinen externen Dienst.

## Ohne eindeutige Verkäuferkennung entsteht keine Rechnung

EN 16931 verlangt, dass der Empfänger den Rechnungssteller maschinell
identifizieren kann (BR-CO-26). Dafür genügt **eine** dieser drei Angaben:

- **USt-IdNr. (BT-31)**,
- **Registerkennung (BT-30)** – etwa die Handelsregisternummer,
- **Lieferanten- oder Kreditorennummer (BT-29)**, die Ihr Kunde Ihnen
  zugeteilt hat.

**Die Steuernummer (BT-32) genügt dafür nicht.** Sie ist eine zulässige
zusätzliche Angabe, aber keine Ersatzkennung – die Norm zählt sie an dieser
Stelle nicht mit. Fehlen alle drei, wird die Rechnung vor der Ausgabe
angehalten, statt eine Datei zu erzeugen, die erst der Empfänger beanstandet.

Die beiden zusätzlichen Kennungen werden unterschiedlich behandelt, und zwar
mit Absicht:

- Die **Registerkennung** gehört zum Firmenstamm. Sie steht in den
  Einstellungen und wird aus der gespeicherten Firmenvorlage vorbelegt.
- Die **Lieferanten- oder Kreditorennummer** gehört nicht dazu. Sie ist die
  Nummer, unter der ein bestimmter Kunde Sie führt; beim nächsten Kunden ist
  es eine andere. Sie wird deshalb je Rechnung eingetragen und **nicht**
  dauerhaft gespeichert – global hinterlegt stünde sie sonst unbemerkt auf der
  Rechnung an jemand anderen.

**Keine der beiden wird aus der PDF erkannt.** Sie stehen auf Rechnungen an
wechselnden Stellen, oft ohne Beschriftung, und sind von Kunden-, Auftrags-
oder Bestellnummern nicht sicher zu unterscheiden. Eine geratene Kennung wäre
schlimmer als keine: Sie ginge unbemerkt in eine Rechnung, die formal gültig
aussieht und den falschen Absender ausweist.

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

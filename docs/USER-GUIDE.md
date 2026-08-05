# USER-GUIDE.md – Anleitung für Anwender

Diese Anleitung ist für Anwender ohne IT-Kenntnisse geschrieben. Sie erklärt,
was das Programm tut, was es nicht tut, und was zu tun ist, wenn etwas nicht
funktioniert.

---

## Was das Programm tut – und was nicht

`EInvoiceSender` nimmt eine PDF-Rechnung, die Sie bereits in Ihrem eigenen
Rechnungsprogramm erstellt haben, und macht daraus eine elektronische Rechnung
im Format ZUGFeRD/Factur-X. Diese neue Datei sieht wie Ihre gewohnte
PDF-Rechnung aus, enthält aber zusätzlich unsichtbare, strukturierte
Rechnungsdaten, die Buchhaltungsprogramme automatisch einlesen können.

**Das Programm erstellt keine Rechnungen.** Sie brauchen weiterhin Ihr
bisheriges Rechnungsprogramm. `EInvoiceSender` ergänzt eine fertige
PDF-Rechnung um die strukturierten Daten – mehr nicht. Es ist keine
Buchhaltung, keine Kundenverwaltung und keine Rechnungsnummernverwaltung.

---

## Die fünf Schritte

1. **PDF auswählen.** Wählen Sie Ihre fertige PDF-Rechnung aus (Dateiauswahl
   oder per Ziehen-und-Ablegen). Das Programm prüft die Datei und zeigt eine
   Vorschau.
2. **Rechnungsdaten erfassen.** Tragen Sie die strukturierten Daten ein
   (Rechnungsnummer, Beträge, Verkäufer, Käufer, Positionen) oder übernehmen
   Sie sie aus einer gespeicherten Vorlage.
3. **Kontrollansicht.** Links sehen Sie Ihre PDF-Rechnung, rechts die
   erfassten Daten. Sie müssen ausdrücklich bestätigen, dass beide
   übereinstimmen. Ohne diese Bestätigung geht es nicht weiter.
4. **Erzeugen und prüfen.** Das Programm erzeugt die elektronische Rechnung
   und prüft sie in mehreren Schritten. Der Fortschritt wird angezeigt.
5. **Ergebnis.** Die fertige Datei wird gespeichert, ein E-Mail-Entwurf wird
   vorbereitet, und Sie können den Ausgabeordner öffnen.

Ihre ursprüngliche PDF-Datei wird dabei nie verändert oder überschrieben.

---

## Meine PDF wird abgelehnt – was tun?

Das Programm prüft Ihre PDF-Datei, bevor es etwas erzeugt. Wird sie abgelehnt,
nennt die Meldung immer eine konkrete Ursache. Die häufigsten:

### Schriftarten sind nicht eingebettet

**Die häufigste Ursache.** Ihre PDF-Rechnung verweist auf Schriftarten (z. B.
Arial, Calibri), ohne sie selbst mitzuliefern. Das ist für die normale Ansicht
unauffällig, für eine normgerechte elektronische Rechnung aber nicht zulässig
– auch nicht bei weitverbreiteten Standardschriften.

**Was Sie tun können:**

- Suchen Sie in Ihrem Rechnungsprogramm beim PDF-Export nach einer Option wie
  „Schriftarten einbetten" oder „Alle Schriften einbetten" (englisch oft
  „Embed fonts") und aktivieren Sie sie.
- Noch einfacher: Exportieren Sie direkt als **PDF/A**, falls Ihr Programm das
  anbietet. Bei PDF/A ist die Schrifteinbettung automatisch vorgeschrieben und
  damit bereits erledigt.
- Danach die Rechnung erneut aus Ihrem Programm speichern und in
  `EInvoiceSender` neu auswählen.

### Die Datei ist kennwortgeschützt

Eine verschlüsselte oder mit Kennwort versehene PDF-Datei kann nicht verwendet
werden. Speichern Sie die Rechnung in Ihrem Programm ohne Kennwortschutz und
ohne Berechtigungseinschränkungen erneut.

### Die Datei ist beschädigt

Die PDF-Datei ließ sich nicht vollständig lesen, zum Beispiel weil sie beim
Speichern oder Übertragen abgeschnitten wurde. Erzeugen Sie die Rechnung in
Ihrem Programm neu und speichern Sie sie erneut ab.

### Die Datei enthält aktive Inhalte

Manche PDF-Dateien enthalten Skripte oder automatisch startende Aktionen (z. B.
aus Formularfunktionen). Solche Inhalte sind in einer elektronischen Rechnung
nicht zulässig. Exportieren Sie die Rechnung ohne Formularfunktionen und ohne
Skripte.

### Die Datei ist digital signiert

Eine bereits digital signierte PDF-Datei kann nicht verwendet werden: Durch das
Einbetten der Rechnungsdaten würde die Signatur ungültig. Verwenden Sie die
unsignierte Fassung und signieren Sie erst die fertige elektronische Rechnung,
falls das für Sie nötig ist.

### Die Datei ist zu groß

Die zulässige Dateigröße ist begrenzt (Vorgabe 20 MB). Verkleinern Sie die
Datei, zum Beispiel indem Sie beim PDF-Export eine geringere Bildauflösung
wählen.

### Es handelt sich nicht um eine echte PDF-Datei

Das Programm prüft nicht nur die Dateiendung, sondern den tatsächlichen
Inhalt. Eine Datei, die nur `.pdf` heisst, aber kein PDF ist (etwa eine
umbenannte Bilddatei), wird abgelehnt. Wählen Sie die tatsächliche
PDF-Fassung Ihrer Rechnung aus.

---

## E-Mail: Was passiert und was nicht

**`EInvoiceSender` versendet keine E-Mails.** Das Programm bereitet lediglich
einen E-Mail-Entwurf vor (Empfänger, Betreff, Text, Anhang) und öffnet ihn in
Ihrem eigenen E-Mail-Programm. Den Versand lösen ausschließlich **Sie** selbst
aus, nachdem Sie den Entwurf geprüft haben.

**Falls sich der Entwurf nicht öffnen lässt:** Klicken Sie auf „Ausgabeordner
öffnen" und hängen Sie die dort liegende Rechnungsdatei von Hand an eine neue
E-Mail an. Dieser Weg funktioniert immer, unabhängig davon, welches
E-Mail-Programm Sie verwenden.

---

## Wo liegen meine Daten

Alle Daten – Ihre Firmenvorlage, gespeicherte Bankverbindung, erzeugte
Rechnungen und Berichte – liegen ausschließlich auf Ihrem eigenen Rechner
unter:

```
%LOCALAPPDATA%\EInvoiceSender
```

Die Verarbeitung erfolgt vollständig lokal. Ohne Ihre ausdrückliche Aktion
(zum Beispiel das Versenden einer E-Mail) verlässt keine Ihrer Angaben – kein
PDF, kein Rechnungsdatum, keine E-Mail-Adresse, keine Bankverbindung, keine
Steuerangabe – Ihren Rechner. Es gibt keinen Online-Konverter und keine
Cloud-Anbindung im eigentlichen Verarbeitungsprozess.

---

## Grenzen

Das Programm prüft das **Format** der Rechnung: ob die Datei den technischen
Vorgaben für elektronische Rechnungen entspricht, ob Summen zusammenpassen und
ob Pflichtangaben vorhanden sind.

Es prüft **nicht** die inhaltliche oder steuerliche Richtigkeit Ihrer
Rechnung. Ob die Beträge, Steuersätze und Angaben tatsächlich zutreffen,
verantworten weiterhin ausschließlich Sie als Rechnungssteller. Das Programm
leistet keine Steuerberatung.

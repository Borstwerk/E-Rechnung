# Technischer Versuch: örtlicher Raster-Rückfallweg für problematische PDF

**Stand:** 10.08.2026 · **Art:** technischer Vorversuch, kein Benutzerfeature
· **Zweig:** `rebuild/simple-v2`

Dieser Bericht beantwortet eine einzige Frage: Lassen sich Eingangs-PDF, die
der direkte Weg zu Recht ablehnt – vor allem solche mit nicht eingebetteten
Schriften –, mit dem bereits vorhandenen örtlichen Baukasten über ein Rastern
der Seiten in eine gültige PDF/A-3b-ZUGFeRD-Datei überführen?

Es wurde nichts ausgeliefert, keine Ablehnungsregel entfernt und kein
Produktversprechen geändert. Der direkte Weg verhält sich unverändert.

---

## 1. Ergebnis

**Ja, der Versuch ist gelungen** – im vollen Umfang des Erfolgskriteriums.

Eine realistische Rechnung mit nicht eingebetteter Schrift wird

1. vom direkten Weg abgelehnt (Hindernis `FontsNotEmbedded`),
2. von PDFium vollständig gerendert,
3. als neue PDF/A-3b-Datei mit eingebetteter Rechnungs-XML aufgebaut,
4. von veraPDF mit `isCompliant=true` anerkannt,
5. vom CEN-Schematron ohne gescheiterte Regel bestätigt,
6. und ihre eingebettete XML kommt byte-identisch wieder heraus.

Die sichtbare Seite ist dabei vom Original nicht zu unterscheiden.

Das gilt für den geprüften Ausschnitt an Dateien, nicht für „beliebige PDF“.
Was dieser Versuch **nicht** belegt, steht in Abschnitt 8.

## 2. Ergebnis von veraPDF

Geprüft über die Mustang-CLI 2.24.0, die veraPDF mitbringt. Alle Dateien
liegen unter `artifacts/spike-raster`, die Gegenprüfung läuft mit
`build/validate-raster-spike.sh`.

```
Datei                                  Größe/KB Seiten    veraPDF   Regeln Ergebnis
messreihe-dicht-150dpi.pdf                  1342      3         ja    0/113       ok
messreihe-dicht-200dpi.pdf                  1965      3         ja    0/113       ok
messreihe-dicht-300dpi.pdf                  3050      3         ja    0/113       ok
messreihe-dicht-400dpi.pdf                  4705      3         ja    0/113       ok
messreihe-einseitig-150dpi.pdf               111      1         ja    0/113       ok
messreihe-einseitig-200dpi.pdf               159      1         ja    0/113       ok
messreihe-einseitig-300dpi.pdf               240      1         ja    0/113       ok
messreihe-einseitig-400dpi.pdf               331      1         ja    0/113       ok
messreihe-mehrseitig-150dpi.pdf              109      5         ja    0/113       ok
messreihe-mehrseitig-200dpi.pdf              166      5         ja    0/113       ok
messreihe-mehrseitig-300dpi.pdf              277      5         ja    0/113       ok
messreihe-mehrseitig-400dpi.pdf              428      5         ja    0/113       ok
rasterweg-mehrseitig.pdf                     107      3         ja    0/113       ok
rasterweg-seitenformate.pdf                  116      3         ja    0/113       ok
rasterweg-testrechnung.pdf                   240      1         ja    0/113       ok

Geprüft: 15, Abweichungen: 0
```

Im Einzelnen für die Hauptdatei: `flavour=3b`, `totalAssertions=378`,
`assertions=[]`, `isCompliant=true`.

Das Prüfskript verlangt bewusst mehr als das bisherige: Nicht nur, dass keine
Teilzusammenfassung „invalid“ meldet, sondern die ausdrückliche Aussage
`isCompliant=true`. Sonst wäre der Nachweis von der Auslegung der
Zusammenfassung abhängig.

### Der eine Punkt, an dem es zuerst scheiterte

Der erste Durchlauf war **nicht** konform, und zwar an einer einzigen von 378
Prüfaussagen:

> ISO 19005-3:2012, Abschnitt 6.2.8, Test 3 – *If an Image dictionary contains
> the Interpolate key, its value shall be false.*

PDFsharp schreibt an jedes Bild `/Interpolate true`. Das ist eine Empfehlung an
den Betrachter, beim Vergrößern zu glätten; PDF/A verbietet sie, weil das
Aussehen des Dokuments nicht vom Betrachter abhängen soll. Behoben in
`RasterizedPdfBuilder.DisableImageInterpolation`. Sichtbar ändert sich dadurch
nichts.

Dieser Fehlschlag ist zugleich der Beleg, dass das Freigabegate wirkt: Ohne die
Korrektur schlagen die drei Gate-Tests fehl, mit ihr laufen sie durch.

## 3. Ergebnis von Mustang und CEN-Schematron

Für jede erzeugte Datei: Profil `urn:cen.eu:en16931:2017`, Version 2,
**113 Regeln ausgelöst, 0 gescheitert**, `<summary status="valid"/>` im
XML-Abschnitt.

Die eingebettete XML ist dieselbe wie auf dem direkten Weg – sie entsteht
unabhängig davon, wie die sichtbaren Seiten zustande kommen. Dass die
Schematron-Prüfung durchläuft, war deshalb zu erwarten; geprüft wurde es
trotzdem, weil ein Fehler beim Einbetten die Datei sonst still beschädigt
hätte.

Zusätzlich geprüft und in Ordnung: `/AFRelationship /Alternative`, MIME-Typ
`text/xml`, Dateiname `factur-x.xml`, Eintrag im Namensbaum
`/Names /EmbeddedFiles`, `pdfaid:part 3`, `pdfaid:conformance B`,
`fx:ConformanceLevel EN 16931`.

## 4. Geprüfte Eingangs-PDF

| # | Datei | Merkmal | direkter Weg | Rasterweg |
|---|-------|---------|--------------|-----------|
| A | `TestInvoice.CreatePdf()` – die Rechnung aus dem manuellen Testlauf, zweispaltig, mit Positionstabelle, Summen, Bankverbindung und Umlauten | Helvetica, **nicht eingebettet** | abgelehnt (`APP-PDF-002`) | gültig |
| B | dasselbe – die synthetische Datei mit nicht eingebetteter Schrift **ist** hier die realistische Rechnung | s. o. | abgelehnt | gültig |
| C | `CreateMultiPagePdf(5)` – mehrseitig | nicht eingebettet | abgelehnt | gültig, 5 Seiten |
| D | `CreateMixedPageSizesPdf()` – A4 hoch, A4 quer, A4 hoch mit `/Rotate 90` | nicht eingebettet | abgelehnt | gültig, Formate erhalten |
| E | `TestPdfFactory.CreateSimplePdf()` – reine Vektorgrafik, der Normalfall | einbettbar | **angenommen**, unverändert | nicht nötig |
| F | dicht bedruckte Sammelrechnung, 150 Positionen über 3 Seiten | nicht eingebettet | abgelehnt | gültig |

Fall A/B ist der wichtige: Die Testrechnung ist mit der Standardschrift
Helvetica gesetzt, und Standardschriften werden nicht eingebettet. Der Test
`DerDirekteWegLehntDieTestrechnungWegenDerSchriftAb` hält ausdrücklich fest,
dass der direkte Weg diese Datei ablehnt – ohne ihn bewiese der Rasterweg an
ihr überhaupt nichts.

Fall E blieb unverändert: Eine geeignete Datei geht weiterhin den direkten Weg
und behält ihre echte, markierbare Textebene.

## 5. Auflösung und Dateigröße

| dpi | einseitige Rechnung | dicht bedruckt (3 S.) | je dichter Seite | schwach bedruckt (5 S.) |
|-----|--------------------:|----------------------:|-----------------:|------------------------:|
| 150 | 111 KB | 1342 KB | ~447 KB | 109 KB |
| 200 | 159 KB | 1965 KB | ~655 KB | 166 KB |
| **300** | **240 KB** | **3050 KB** | **~1017 KB** | 277 KB |
| 400 | 331 KB | 4705 KB | ~1568 KB | 428 KB |

Die Eingangsdatei der einseitigen Rechnung ist 1,8 KB groß. Der Faktor ist also
erheblich – das liegt aber am Ausgangsformat: Eine Textseite ohne eingebettete
Schrift ist extrem kompakt. Gegenüber einer üblichen Rechnung aus einem
Buchhaltungsprogramm (100–300 KB) liegt der Rasterweg bei 300 dpi in derselben
Größenordnung, solange die Seite nicht dicht bedruckt ist.

Die Spalte „dicht bedruckt“ ist die ehrliche Obergrenze: rund 1 MB je Seite bei
300 dpi. Eine zehnseitige Sammelrechnung landet damit bei etwa 10 MB – über der
üblichen Anhanggrenze vieler Postfächer und nahe der eigenen Eingabegrenze von
20 MB.

Die Bilder werden **verlustfrei** gespeichert (PNG-Kodierung, von PDFsharp als
`FlateDecode` in `DeviceRGB` übernommen, ohne Alphakanal). Das ist eine
bewusste Entscheidung gegen die kleinere Datei: Auf der Testrechnung liefert
JPEG mit Güte 85 bei 300 dpi 220 KB gegenüber 281 KB für PNG, also rund ein
Viertel weniger. Dafür setzt JPEG Artefakte an die Schriftkanten – und die
Schrift ist bei einer Rechnung alles, worauf es ankommt. Eine spätere Fassung
könnte hier wählen; für den Versuch gilt: erst die Darstellung, dann die
Größe.

(Auf einer fast leeren Seite kehrt sich das Verhältnis um – dort war PNG mit
41 KB kleiner als JPEG mit 67 KB. Maßgeblich ist die dicht bedruckte Seite.)

**300 dpi ist der Vorschlag, kein Dogma.** Eine automatische, sich anpassende
Auflösung wurde bewusst nicht gebaut.

## 6. Sichtbare Qualität

Der Sichtvergleich lief an der Testrechnung, Original gegen Rasterausgabe,
beide bei 100 dpi nebeneinander gestellt, sowie an einem Ausschnitt in
Originalpixeln über alle vier Auflösungen.

Geprüft und in Ordnung:

* **Seitenzahl** – gleich (1, 3, 5 Seiten).
* **Seitengröße und Ausrichtung** – A4 hoch bleibt hoch, A4 quer bleibt quer.
* **Drehung** – die Seite mit `/Rotate 90` kommt quer heraus, so wie sie
  angezeigt wird. PDFium meldet die *sichtbare* Größe und rechnet die Drehung
  bereits ein; die neue Seite übernimmt diese Größe und braucht selbst kein
  `/Rotate`. Wer stattdessen die MediaBox des Originals nähme, bekäme ein
  Hochformat mit quer hineingestauchtem Inhalt.
* **Ränder und Rahmen** – der Prüfrahmen der Vorlage liegt an derselben Stelle;
  nichts ist beschnitten.
* **Tabellenlinien, Spaltenausrichtung** – unverändert.
* **Umlaute und Sonderzeichen** – `Musterstraße`, `Hafenstraße`, `Währung`,
  `Fällig`, `%`, `€` alle korrekt. Das ist erwartbar, weil gerendert und nicht
  neu gesetzt wird, war aber zu prüfen.
* **Seitenverhältnis** – Bild und Seite stimmen auf 1 % überein, automatisch
  geprüft.

Zur Lesbarkeit kleiner Schrift, 10-pt-Text bei 300 dpi betrachtet:

| dpi | Beurteilung |
|-----|-------------|
| 150 | lesbar, beim Vergrößern deutlich weich; Kanten sichtbar verwaschen |
| 200 | brauchbar, leicht weich |
| 300 | vom Original kaum zu unterscheiden |
| 400 | nicht mehr unterscheidbar; der Zugewinn rechtfertigt die Größe nicht |

Pixelgleichheit wurde nicht verlangt und nicht geprüft – zwei Renderdurchläufe
unterscheiden sich schon durch Kantenglättung.

Ein Muster mit QR-Code oder Barcode liegt nicht vor; das bleibt offen
(Abschnitt 9).

## 7. Welche bisherigen Hindernisse damit lösbar würden

| Hindernis | heute | mit Rasterweg |
|-----------|-------|---------------|
| `FontsNotEmbedded` – Schriften nicht eingebettet | Ablehnung | **lösbar, nachgewiesen** |
| unpassender Farbraum, unklares Farbprofil | wird heute nicht geprüft, würde extern auffallen | **lösbar** – gerendert wird nach sRGB, der OutputIntent passt dazu |
| Transparenz, Überdrucken, Ebenen | dito | **lösbar** – im Ergebnis liegt ein deckendes Bild |
| veraltete oder kaputte PDF-Struktur, solange PDFium sie noch anzeigt | Ablehnung als beschädigt | **wahrscheinlich lösbar**, nicht systematisch geprüft |

Das ist der eigentliche Gewinn: Der häufigste Ablehnungsgrund überhaupt – die
nicht eingebettete Schrift – bekommt einen Ausweg, der ohne Java, ohne
Ghostscript, ohne Cloud und ohne neue Bibliothek auskommt.

## 8. Was unlösbar bleibt

* **Reine Scan-PDF ohne Textebene.** Der Rasterweg erzeugt zwar eine gültige
  Datei, aber die Erkennung findet im Original nichts. Alle Rechnungsdaten
  müssten von Hand erfasst werden. OCR ist ausdrücklich nicht Gegenstand
  dieses Auftrags und wurde nicht untersucht.
* **Kennwortgeschützte Dateien** (Benutzerkennwort). Weder PDFium noch PdfSharp
  öffnen sie; PDFium meldet `PdfPasswordProtectedException`. Ohne
  Kennwortverwaltung – die es nicht geben soll – bleibt das eine Ablehnung.
* **Digital signierte Dateien.** Technisch rendert PDFium sie. Geprüft wurde
  das an einer Datei mit Signaturfeld und `/SigFlags 3` – also an der
  Struktur, nicht an einer echten kryptografischen Signatur mit `/ByteRange`
  und Zertifikat. Für die Frage „lässt sich das anzeigen“ genügt das; wer
  darauf eine Funktion bauen will, sollte an einer wirklich signierten Datei
  nachprüfen. Die Signatur ginge dabei ohnehin verloren: Sie kann nicht in die abgeleitete E-Rechnung übernommen werden.
  Eine signierte PDF könnte visuell übernommen werden, die Signatur würde
  jedoch nicht in die abgeleitete E-Rechnung übernommen. Automatisch
  verarbeitet wird sie deshalb weiterhin nicht.
* **Aktive Inhalte** (JavaScript, `/OpenAction`). Sie verschwinden zwar beim
  Rastern, aber ihr Vorhandensein sagt etwas über die Herkunft der Datei aus.
  Ob die Ablehnung bleiben soll, ist eine fachliche Entscheidung, keine
  technische.
* **Beschädigte Dateien, die PDFium nicht öffnet.** Bleiben abgelehnt.

Ebenfalls nicht belegt: dass „beliebige PDF“ funktionieren. Geprüft wurden
sechs Vorlagen, alle selbst erzeugt. Ein Bestand echter Rechnungen aus
verschiedenen Buchhaltungsprogrammen wurde nicht geprüft, weil dafür keine
künstlichen Testdaten vorliegen.

## 9. Konkrete Risiken

1. **Stiller Qualitätsverlust.** Der größte Risikopunkt ist kein technischer:
   Wenn der Rasterweg unbemerkt greift, bekommt der Empfänger ein Bild statt
   eines Textdokuments, ohne dass es jemand wollte. Er darf niemals still
   laufen (Abschnitt 11).
2. **Dateigröße.** Rund 1 MB je dicht bedruckter Seite. Bei langen Rechnungen
   sprengt das Postfachgrenzen. Eine Warnung ab einer Schwelle wäre nötig.
3. **PDFium ist nativ und nicht threadsicher.** Das Rendern läuft deshalb
   serialisiert (`PdfiumLock`). Ein Absturz in nativem Code ließe sich nicht
   abfangen. Bislang nicht beobachtet – die Vorschau setzt dieselbe Komponente
   seit Beginn ein.
4. **Speicherbedarf.** Eine A4-Seite bei 300 dpi sind 2480 × 3507 Punkte, roh
   rund 25 MB. Es wird seitenweise gerendert und sofort wieder freigegeben,
   aber bei 400 dpi und großen Formaten wächst das spürbar.
5. **Barrierefreiheit.** Die Ausgabe ist für Vorlesewerkzeuge wertlos. PDF/A-3b
   verlangt keine Auszeichnungsstruktur, deshalb ist die Datei normgerecht –
   aber sie ist es auf die schwächere Art.
6. **Ein Fund am Rande, nicht behoben:** Eine PDF mit reinem *Besitzer*kennwort
   (Rechteschutz, leeres Benutzerkennwort) trägt ein `/Encrypt`-Wörterbuch,
   wird von PdfSharp aber als `IsEncrypted = false` gemeldet und von PDFium
   anstandslos gerendert. Der heutige `PdfAnalyzer` stuft sie damit **nicht**
   als verschlüsselt ein. Für die Norm ist das folgenlos – die Ausgabe wird
   ohnehin unverschlüsselt geschrieben –, aber die vom Ersteller gesetzte
   Rechteeinschränkung wird dabei stillschweigend übergangen. Das ist eine
   eigene Entscheidung und gehört nicht in diesen Versuch; hier steht es, damit
   es nicht verloren geht.

## 10. Änderungen am vorhandenen `PdfAInvoiceComposer`

Klein und rein umstellend, ohne Verhaltensänderung:

* Die gemeinsamen PDF/A- und ZUGFeRD-Bausteine sind nach
  `Pdf/PdfAInvoiceParts.cs` gewandert: Dokumentinformationen, Einbetten der
  XML samt `/AF` und Namensbaum, sRGB-OutputIntent, XMP-Paket, Anheben auf
  PDF 1.7. Eine Methode, `Finish(PdfDocument, PdfACompositionRequest)`.
* `PdfAInvoiceComposer` behält genau seine Aufgabe: prüfen, ob das Original
  aufwertbar ist, Hindernisse verständlich melden, und – wenn ja – das Original
  öffnen und `Finish` aufrufen. Von 324 auf 179 Zeilen.
* `ProducerName` steht jetzt bei den gemeinsamen Bausteinen.
* `RasterizedPdfBuilder` ruft dasselbe `Finish` auf. Beide Wege unterscheiden
  sich ausschließlich darin, woher die Seiten kommen.

Damit entsteht die vom Auftrag gewünschte Aufteilung – Aufwertung \ gemeinsame
Bausteine / Rastererzeugung – ohne Strategy-Hierarchie und ohne Fabrik.

Neu hinzugekommen:

* `src/EInvoiceSender.Core/Pdf/PdfAInvoiceParts.cs`
* `src/EInvoiceSender.Core/Pdf/RasterizedPdfBuilder.cs`
* `src/EInvoiceSender.Core/Pdf/RasterizedPdfResult.cs`
* `tests/EInvoiceSender.IntegrationTests/RasterFallbackSpikeTests.cs` (17 Tests)
* `build/validate-raster-spike.sh`
* Vorlagen in `TestPdfFactory`: `CreateMixedPageSizesPdf`, `CreateMultiPagePdf`

`EInvoiceSender.Core` verweist zusätzlich auf PDFtoImage – dieselbe
Abhängigkeit, die die Oberfläche für die Seitenvorschau schon einsetzt.
**Kein neuer Baustein und keine neue Lizenz:** PDFtoImage (MIT), PDFium
(BSD-3-Clause) und SkiaSharp (MIT) sind bereits in
`docs/THIRD-PARTY-NOTICES.md` und in den Installationshinweisen geführt und
werden bereits ausgeliefert. Kein Ghostscript, kein iText, kein Java, kein
Webdienst.

Bestehende Tests und Golden Master wurden nicht verändert.

## 11. Vorschlag für die Eingangsprüfung – nur als Modell

**Noch nicht umgesetzt, keine Oberfläche, keine endgültigen Texte.**

Drei Zustände statt heute zwei:

| Zustand | Bedeutung | Folge |
|---------|-----------|-------|
| `Direct` | Die Datei ist aufwertbar. | Wie heute. Textebene bleibt erhalten. |
| `RasterFallbackPossible` | Nicht aufwertbar, aber vollständig darstellbar. | Der Anwender wird gefragt. Ohne seine Zustimmung geschieht nichts. |
| `Rejected` | Weder aufwertbar noch darstellbar – Kennwortschutz, Beschädigung. | Wie heute, mit der bisherigen Erklärung. |

Ablauf, unverändert in der Reihenfolge:

1. Original laden
2. Text aus dem **Original** auslesen
3. Rechnungsdaten aus dem Original erkennen
4. Anwender prüft und bestätigt
5. **erst jetzt**, für die Ausgabe: gegebenenfalls Rasterweg

Die Erkennung wird also **nicht** schlechter: Sie arbeitet weiterhin auf dem
Original, bevor überhaupt gerendert wird. Aus der gerasterten Ausgabe wird
niemals erneut Text gelesen – dass das gar nicht ginge, hält der Test
`ImSichtbarenDokumentStehtKeinTextMehr` fest.

Sinngemäßer Hinweistext, noch nicht endgültig:

> Diese PDF-Datei lässt sich nicht unverändert übernehmen, weil nicht alle
> Schriftarten eingebettet sind. Die Rechnung kann stattdessen als Bild
> übernommen werden. Das Aussehen bleibt erhalten, der Text ist danach aber
> nicht mehr markierbar oder durchsuchbar, und die Datei wird größer. Die
> Rechnungsdaten selbst bleiben vollständig maschinenlesbar.

Zwei Schaltflächen, keine Vorauswahl, kein „nicht mehr fragen“ in der ersten
Fassung.

## 12. Was der Rasterweg kostet

Vollständig, damit es niemand später entdecken muss:

* Der sichtbare Text ist **nicht mehr markierbar**.
* Im sichtbaren Dokument ist **keine Textsuche** mehr möglich.
* **Verknüpfungen und Formularfelder** gehen verloren.
* **Auszeichnungsstruktur und Barrierefreiheit** gehen verloren.
* Die **Datei wird größer**, bei dicht bedruckten Seiten deutlich.
* Eine vorhandene **Signatur des Originals kann nicht erhalten** bleiben.
* Das **Original bleibt unverändert** – es wird ausschließlich gelesen,
  automatisch geprüft in `DasOriginalBleibtByteIdentisch`.

Unberührt davon: Die strukturierte Rechnungs-XML bleibt vollständig und
maschinenlesbar. Für die Weiterverarbeitung beim Empfänger ist sie das, worauf
es ankommt – das Bild ist die menschenlesbare Beilage.

## 13. Empfehlung

**Als optionalen Rückfallweg weiterverfolgen** – mit ausdrücklicher Rückfrage,
ohne Vorauswahl, und ohne den direkten Weg anzutasten.

Begründung: Der häufigste Ablehnungsgrund bekommt eine Antwort, die technisch
belegt ist und den örtlichen, permissiv lizenzierten Baukasten nicht verlässt.
Der Preis ist bekannt, benennbar und für den Anwender entscheidbar.

Vor einer Umsetzung als Benutzerfeature offen:

1. Prüfung an echten Rechnungen aus verschiedenen Buchhaltungsprogrammen.
2. Ein Muster mit QR-Code oder Barcode – bei 150 dpi ist Lesbarkeit durch einen
   Scanner nicht selbstverständlich.
3. Entscheidung über eine Warnschwelle für die Dateigröße.
4. Ausformulierung der Texte für die Rückfrage.
5. Die Ausführung unter Windows – hier lief alles unter Linux; PDFium ist
   nativ, und das Zusammenspiel ist auf der Zielplattform nachzuvollziehen.

**Nicht behauptet wird**, dass damit beliebige PDF unterstützt werden. Belegt
ist, was in Abschnitt 4 steht.

---

## Nachvollziehen

```bash
dotnet test tests/EInvoiceSender.IntegrationTests -c Release \
    --filter "FullyQualifiedName~RasterFallbackSpikeTests"
./build/validate-raster-spike.sh
```

Das erste Kommando erzeugt die Dateien unter `artifacts/spike-raster`, das
zweite legt sie veraPDF und dem CEN-Schematron vor. Der Nachweis gilt nur,
wenn das zweite Kommando `Abweichungen: 0` meldet – kein eigener Test ersetzt
ihn.

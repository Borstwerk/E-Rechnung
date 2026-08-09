# DECISIONS.md – Architekturentscheidungen (ADR)

Format je Eintrag: Datum, Entscheidung, betrachtete Alternativen, Gründe,
Auswirkungen. Neue Einträge werden angehängt, alte nicht gelöscht – wenn eine
Entscheidung revidiert wird, bekommt sie den Status *ersetzt durch ADR-xxxx*.

---

## ADR-0001 – Zielformat: ZUGFeRD 2.3 / Factur-X 1.07, Profil EN 16931, PDF/A-3b

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Erzeugt wird ausschließlich das Profil EN 16931 (COMFORT) mit
dem URN `urn:cen.eu:en16931:2017`, eingebettet als `factur-x.xml` in ein
PDF/A-3b.

**Alternativen:** (a) BASIC – zu wenig Felder, für Dienstleistungsrechnungen mit
Positionsdetails unpassend. (b) EXTENDED – deutlich größerer Umfang, für den
MVP nicht nötig, erhöht Fehlerfläche. (c) MINIMUM / BASIC WL – in Deutschland
nicht als Rechnung zulässig. (d) Reine XRechnung – kein Hybridformat, das
sichtbare PDF ginge verloren; bleibt als spätere Zusatzausgabe vorgesehen.
(e) ZUGFeRD 2.4/2.5 (CII D22B) als Erzeugungsziel.

**Gründe:** EN 16931 ist das rechtlich geforderte Mindestniveau und
gleichzeitig das am breitesten unterstützte Profil. Der Profil-URN, der
Anhangname und das XMP-Schema sind über ZUGFeRD 2.1 bis 2.5 identisch; D16B
wird von D22B-Verarbeitern gelesen. Eine Datei nach dieser Festlegung ist von
jedem Empfänger ab ZUGFeRD 2.0.1 verarbeitbar.

**Auswirkungen:** Ein Wechsel auf D22B ist später eine reine Erweiterung des
XML-Writers, kein Umbau. Die Profilkennung bleibt unverändert.

---

## ADR-0002 – Eigener CII-XML-Writer statt Fremdbibliothek

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Die EN-16931-CII-XML wird in `EInvoiceSender.Formats` selbst
erzeugt, ausschließlich mit `System.Xml`. Keine ZUGFeRD-Bibliothek im
Erzeugungspfad.

**Alternativen:** (a) `ZUGFeRD-csharp` 18.0.0 (Apache-2.0) – funktional
ausreichend, aber vom Autor ausdrücklich auf „maintenance only" gesetzt, mit
kommerziellem Nachfolger `FactoorSharp`. (b) `Securibox.FacturX` (MIT) – schreibt
`/AFRelationship = /Data` statt `/Alternative`, also nicht spezifikationstreu.
(c) `FacturXDotNet` – seit über einem Jahr im Alpha-Stand. (d) `FacturX-csharp` –
sehr jung, geringe Verbreitung.

**Gründe:** Der Umfang eines EN-16931-Writers für die unterstützten
Geschäftsfälle ist überschaubar und vollständig testbar. Eigener Code bedeutet:
exakte Kontrolle über jedes Element, direkte Abbildung des eigenen Domänenmodells
ohne Zwischenmodell, keine Abhängigkeit von einer eingefrorenen Bibliothek, und
kein Vertrauensvorschuss gegenüber fremdem Mapping. Die Korrektheit wird nicht
durch Vertrauen, sondern durch Golden-Master-Tests und die Gegenprüfung mit der
Mustang-CLI (die das offizielle CEN-Schematron ausführt) belegt.

**Auswirkungen:** Mehr eigener Code und eigene Verantwortung für Mapping-Fehler.
Kompensiert durch verpflichtende Schematron-Gegenprüfung in der CI. Nicht
unterstützte Geschäftsfälle müssen ausdrücklich dokumentiert und von der
Anwendung abgelehnt werden, statt still falsch erzeugt zu werden.

---

## ADR-0003 – PDF/A-3: Aufwertung statt Konvertierung, mit hartem Abbruch

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Die Anwendung **konvertiert kein beliebiges PDF** nach PDF/A-3.
Sie prüft das Eingangs-PDF auf Aufwertbarkeit und ergänzt bei Eignung die
fehlenden PDF/A-3b-Bestandteile (OutputIntent mit ICC-Profil, XMP-Metadaten,
eingebettete XML, `/AF`). Ist das Eingangs-PDF nicht aufwertbar, wird der Vorgang
**abgebrochen**, die Ursache verständlich erklärt und **keine Ausgabedatei**
erzeugt.

**Alternativen:** (a) Ghostscript vorschalten – AGPL-3.0, für ein verteiltes
Desktopprodukt ein Copyleft-Risiko. (b) iText7 pdfa – AGPL-3.0 bzw.
kommerziell, gleiches Problem. (c) Kommerzielle Bibliotheken (Aspose, PDFlib,
Syncfusion) – laufende Kosten, im MVP ausgeschlossen. (d) LibreOffice als
Konverter – Layouttreue nicht zuverlässig. (e) Stillschweigend eine Datei
ausgeben, die die PDF/A-Prüfung nicht besteht – ausdrücklich unzulässig.

**Gründe:** Es existiert nachweislich **keine** permissiv lizenzierte
.NET-Bibliothek, die ein beliebiges PDF verlässlich nach PDF/A-3 konvertiert
(Schriften einbetten und subsetten, Farbräume normalisieren, Transparenz
auflösen). Das ehrliche Verhalten ist daher die Vorprüfung mit klarem Abbruch.
Die Spezifikation verlangt genau das (Abschnitt 5.6).

**Auswirkungen:** Ein Teil der Eingangs-PDFs – vor allem solche ohne eingebettete
Schriften – wird abgelehnt. Die Anwendung erklärt dem Benutzer verständlich, was
er in seinem Rechnungsprogramm umstellen muss (typisch: „Schriftarten einbetten"
bzw. direkt als PDF/A exportieren). Diese Einschränkung ist in
`docs/USER-GUIDE.md` und im Releasebericht offen dokumentiert.

---

## ADR-0004 – Zweistufige Validierung: eigene Regeln verpflichtend, externe Validatoren zusätzlich

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Die EN-16931-Geschäftsregeln werden in
`EInvoiceSender.Validation` selbst implementiert und laufen **immer**. Externe
Validatoren (Mustang-CLI mit CEN-Schematron und veraPDF) werden über den Port
`IExternalDocumentValidator` eingebunden, laufen **zusätzlich**, sofern
konfiguriert, und werden im Bericht mit Name und Version ausgewiesen.

**Alternativen:** (a) Nur externe Validatoren – setzt eine mitgelieferte JRE
voraus (Installer +40 bis 70 MB nach `jlink`, sonst ~200 MB) und liefert
englische Regeltexte, die für Endanwender unbrauchbar sind. (b) Schematron
in-process ausführen – das CEN-Schematron kompiliert nach XSLT 2.0, .NET bietet
nur XSLT 1.0, und Saxon ist für .NET nicht als NuGet-Paket verfügbar.
(c) Nur eigene Regeln – kein unabhängiger Gegenbeweis.

**Gründe:** Die Anwendung braucht ohnehin eine eigene Regelprüfung, weil sie
Fehler **vor** der Erzeugung in verständlichem Deutsch melden muss (Anforderung
Abschnitt 11: nicht `BR-CO-10 failed`, sondern ein erklärender Satz). Die
Gegenprüfung mit dem offiziellen Schematron in der CI belegt, dass die eigene
Regelimplementierung mit der Norm übereinstimmt.

**Auswirkungen und offene Einschränkung:** Ohne konfigurierten externen Validator
prüft die Anwendung PDF/A nur strukturell (eigene Prüfung: OutputIntent, XMP,
Schrifteinbettung, Verschlüsselung, Anhangstruktur, Dateibeziehungen) – das ist
eine **Teilmenge** dessen, was veraPDF prüft. Der Validierungsbericht weist
diesen Zustand ausdrücklich als Warnung aus und behauptet keine vollständige
PDF/A-Konformität. In der CI läuft die veraPDF-Prüfung verbindlich.

---

## ADR-0005 – E-Mail-Entwurf als `.eml`-Datei mit `X-Unsent: 1`

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Der MVP erzeugt mit MimeKit eine `.eml`-Datei (Empfänger,
Betreff, reiner Text, Anhang) und öffnet sie über die Windows-Dateizuordnung.
Header `X-Unsent: 1`, **keine** `Message-ID`, Textkörper als `text/plain`.
Zusätzlich immer verfügbar: „Ausgabeordner öffnen" und ein `mailto:`-Fallback
ohne Anhang.

**Alternativen:** (a) Outlook-COM-Interop – funktioniert im „neuen Outlook" nicht
mehr und setzt das klassische Outlook voraus. (b) Simple MAPI – im neuen Outlook
faktisch tot. (c) Microsoft Graph – erfordert Entra-App-Registrierung und
Zustimmung, unzumutbar für Einzelunternehmer mit GMX-/Web.de-Postfach.
(d) SMTP mit gespeicherten Zugangsdaten – versendet direkt und verletzt damit die
Anforderung „Benutzerkontrolle vor dem Versand". (e) `mailto:` als Hauptweg – nach
RFC 6068 gibt es keinen Anhangsparameter, Outlook ignoriert ihn.

**Gründe:** `.eml` ist die einzige Variante, die ohne Einrichtung, ohne Cloud und
**mit Anhang** funktioniert und den Versand dem Benutzer überlässt. Reiner Text
statt HTML umgeht einen berichteten Anhangsverlust im neuen Outlook; das
Weglassen der `Message-ID` verhindert Probleme beim wiederholten Öffnen.

**Auswirkungen:** Die Zuverlässigkeit hängt vom installierten Mailclient ab. Das
Verhalten des „neuen Outlook" ist aus dieser Umgebung nicht prüfbar und muss auf
einem echten Windows-11-System verifiziert werden (offener Punkt in
`docs/STATUS.md`). Der Fallback „Ordner öffnen" ist deshalb immer sichtbar und
nicht versteckt. `IEmailDraftService` erlaubt spätere Provider ohne Eingriff in
die Rechnungslogik.

---

## ADR-0006 – Eigenes sRGB-ICC-Profil statt mitgelieferter Fremddatei

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Das für den PDF/A-OutputIntent nötige ICC-Profil wird zur
Laufzeit programmatisch als minimales, gültiges ICC-v2-Matrix/TRC-Profil mit
sRGB-Primärvalenzen erzeugt (`SRgbIccProfile`).

**Alternativen:** (a) Eine fremde `.icc`-Datei mitliefern – erfordert Klärung und
Dokumentation der Lizenz und Herkunft jeder einzelnen Profildatei.
(b) Ein Profil aus einer Linux-Distribution übernehmen – Herkunft im Windows-
Produkt schwer belegbar.

**Gründe:** Ein ICC-v2-Matrix/TRC-Profil ist ein vollständig spezifiziertes,
kompaktes Binärformat (rund 1 KB). Selbst erzeugt ist es lizenzrechtlich
eindeutig, reproduzierbar und ohne Herkunftsnachweis für Dritte.

**Auswirkungen:** Das Profil muss von veraPDF akzeptiert werden – das ist ein
verpflichtender Integrationstest. Falls die Prüfung fehlschlägt, wird auf eine
mitgelieferte Profildatei umgestellt und diese ADR ersetzt.

---

## ADR-0007 – Solution im `.slnx`-Format

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Die Solution liegt als `EInvoiceSender.slnx` vor, nicht als
klassische `.sln`.

**Alternativen:** klassische `.sln`.

**Gründe:** Das .NET-10-SDK erzeugt standardmäßig `.slnx`; das Format wird von
`dotnet build/test/publish` und aktuellen Visual-Studio-Versionen unterstützt und
ist deutlich diff-freundlicher.

**Auswirkungen:** Werkzeuge älter als Visual Studio 2022 17.13 können die Datei
nicht öffnen. Für dieses Projekt ohne Bedeutung, da der Build über die
`dotnet`-CLI läuft.

---

## ADR-0008 – Zerlegung in Domain / Application / Adapter, Testprojekte plattformneutral

**Datum:** 2026-08-04 · **Status:** aktiv

**Entscheidung:** Alle Projekte außer `EInvoiceSender.Desktop` zielen auf
`net10.0` (plattformneutral); nur die WPF-Oberfläche zielt auf `net10.0-windows`.

**Alternativen:** Alles auf `net10.0-windows`.

**Gründe:** So laufen sämtliche Fach-, Format- und Validierungstests auf jedem
Build-Agenten, auch unter Linux. Das erzwingt zugleich die Architekturregel,
dass Windows-spezifische Belange (DPAPI, Shell, Dateidialoge) hinter Ports
liegen und nicht in die Fachlogik sickern.

**Auswirkungen:** Windows-spezifische Implementierungen (DPAPI-Schutz,
Shell-Öffnen) müssen `OperatingSystem.IsWindows()` prüfen und auf anderen
Plattformen eine klar benannte Ersatzimplementierung verwenden – im Test
sichtbar, im Produkt unter Windows nie aktiv.

---

## ADR-0009 – Eigene Regelprüfung als Benutzerführung, nicht als Freigabe

**Datum:** 2026-08-05 · **Status:** aktiv

**Entscheidung:** `En16931RuleValidator` prüft die erfassten Daten lokal und
meldet Probleme in verständlichem Deutsch. Er ist ausdrücklich **kein Ersatz**
für Mustang, das CEN-Schematron oder veraPDF. Die Freigabe erteilen
ausschließlich die externen Validatoren.

**Alternativen:** (a) Nur externe Validatoren – deren Meldungen sind englische
Regeltexte wie `[BR-CO-13] failed`, für Endanwender unbrauchbar, und sie greifen
erst, wenn bereits eine Datei erzeugt wurde. (b) Eigene Prüfung als alleinige
Instanz – wäre eine Selbstbestätigung ohne unabhängigen Beleg.

**Gründe:** Die Anwendung muss Fehler **vor** der Erzeugung melden, sonst
erzeugt der Benutzer wiederholt ungültige Dateien. Gleichzeitig darf eine
bestandene Eigenprüfung nicht als Konformitätsaussage missverstanden werden.

**Auswirkungen:** Es gilt eine bewusste Asymmetrie: Was der eigene Validator
beanstandet, wird nicht erzeugt. Was er durchlässt, ist damit **nicht** als
normkonform bestätigt. Ein Basistest stellt sicher, dass keiner der acht vom
CEN-Schematron bestätigten Golden Master von der Eigenprüfung beanstandet wird –
ein zu strenger Validator wäre schädlicher als gar keiner.

---

## ADR-0010 – VATEX-Untercodes werden akzeptiert, nicht erfunden

**Datum:** 2026-08-05 · **Status:** aktiv

**Entscheidung:** `VatExemptionReasonCodes` enthält die VATEX-Basiscodes.
Untercodes eines bekannten Basiscodes (etwa `VATEX-EU-132-1A` zu
`VATEX-EU-132`) gelten als bekannt, ohne einzeln aufgeführt zu sein.

**Alternativen:** (a) Alle Untercodes einzeln aufnehmen – die offizielle
CEF-VATEX-Liste war aus dieser Umgebung nicht abrufbar; die Codes aus dem
Gedächtnis zu ergänzen hieße, sie zu erfinden. (b) Untercodes als unbekannt
melden – erzeugt eine Falschwarnung bei einer korrekten Rechnung.

**Gründe:** Ein Fehlalarm bei einem gültigen Code kostet den Anwender Zeit und
Vertrauen. Ein unbekannter Code führt ohnehin nur zu einer Warnung, nicht zu
einem Abbruch, und die verbindliche Prüfung übernimmt das CEN-Schematron.

**Auswirkungen:** Ein erfundener Code der Form `VATEX-EU-132-XYZ` würde nicht
beanstandet. Das ist hingenommen: Die Warnung ist Benutzerführung, keine
Konformitätsprüfung. Aufgefallen ist der Fall über den Golden Master
`04-steuerfrei`, der zuvor eine unnötige Warnung erzeugte.

---

## ADR-0011 – Installer mit WiX v5, Installation pro Benutzer

**Datum:** 2026-08-05 · **Status:** aktiv

**Entscheidung:** Der Windows-Installer wird als MSI mit **WiX Toolset 5.0.2**
über das MSBuild-SDK `WixToolset.Sdk` gebaut. Installiert wird **pro Benutzer**
(`Scope="perUser"`) nach `%LOCALAPPDATA%`.

**Prüfung der Lizenzlage an der Primärquelle** (2026-08-05): Die Datei
`LICENSE.TXT` in `wixtoolset/wix` auf GitHub weist WiX als **Microsoft
Reciprocal License (MS-RL)** aus – dieselbe Lizenz gilt für WiX v3. Der
Lizenztext enthält **keine Gebührenklausel**. Die in der Vorrecherche genannte
„Open Source Maintenance Fee" ist eine Sponsoring-Bitte des Herstellers
FireGiant, kein Bestandteil der Lizenz; berichtet wurde eine erzwungene
EULA-Zustimmung erst ab **v6**. Version 5.0.2 liegt vor dieser Änderung und ist
damit unbelastet.

**Alternativen:** (a) WiX v6/v7 – neuer, aber genau der Bereich mit der
berichteten EULA-Erzwingung; ohne Not nicht nötig. (b) WiX v3.14 – ebenfalls
MS-RL, aber altes Werkzeug ohne MSBuild-SDK-Anbindung. (c) Inno Setup – laut
Vorrecherche werden gewerbliche Nutzer zum Lizenzkauf aufgefordert; das ist für
ein Produkt, das gerade an Kleinunternehmen gehen soll, die schlechtere Lage.
(d) MSIX – benötigt ein vertrauenswürdiges Zertifikat, für einen unsignierten
MVP untauglich. (e) Velopack – MIT und attraktiv, bringt aber
Auto-Update-Mechanik mit, die der MVP nicht braucht.

**Gründe:** MS-RL ist eindeutig und gebührenfrei. Die Installation pro Benutzer
kommt ohne Administratorrechte aus – passend dazu, dass die Anwendung ohnehin
nur ins Benutzerprofil schreibt.

**Auswirkungen:** Der Installer lässt sich **nur unter Windows bauen**; das
`.wixproj` ist deshalb bewusst **nicht** Teil von `EInvoiceSender.slnx`, damit
der Linux-Build der Solution nicht bricht. Gebaut wird ausschließlich im
Windows-Job der CI. Benutzerdaten unter `%LOCALAPPDATA%\EInvoiceSender` bleiben
bei einer Deinstallation erhalten.

**Offen und ausdrücklich ungeprüft:** Neuinstallation, Upgrade über eine ältere
Fassung, Deinstallation und Startmenüeintrag sind in dieser Umgebung **nicht
ausführbar**. Sie müssen auf einem echten Windows-System geprüft werden, bevor
ein Release freigegeben wird.

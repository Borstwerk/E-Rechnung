# BorstWerk E-Rechnung – Release-Checkliste

Diese Datei enthält wiederkehrende manuelle Prüfungen für Releasekandidaten.
Sie ist **kein Backlog**: Ein abgehakter Punkt verschwindet nicht dauerhaft,
sondern wird bei einem späteren Release erneut geprüft, soweit er für die
jeweilige Version relevant ist.

Die verbindlichen Anforderungen einer Version stehen in der jeweiligen
`REQUIREMENTS-x.y.z.md`. Diese Checkliste liefert manuelle Nachweise für
Verhalten, das sich in CI oder Unit-Tests nicht vollständig beurteilen lässt.

## Release-Artefakte

- [ ] Release-Build aus dem vorgesehenen Releaseweg erzeugen.
- [ ] Release-Ausgabeverzeichnis enthält keine Artefakte eines älteren Builds.
- [ ] MSI ist vorhanden und startet.
- [ ] Portable ZIP ist vorhanden und startet.
- [ ] Portable ZIP enthält die vorgesehenen Drittanbieterhinweise.
- [ ] Installer enthält die vorgesehenen Drittanbieterhinweise.
- [ ] Drittanbieterhinweise entsprechen den tatsächlich ausgelieferten Komponenten.
- [ ] `SHA256SUMS.txt` wurde erst nach Fertigstellung aller Release-Artefakte erzeugt.
- [ ] `SHA256SUMS.txt` enthält ausschließlich Dateien dieses Release-Builds.
- [ ] Alle aufgeführten SHA-256-Prüfsummen lassen sich gegen die vorhandenen Dateien verifizieren.
- [ ] Versionsangaben von Anwendung, MSI und Release-Artefakten sind konsistent.
- [ ] Falls Code Signing für die Version aktiv ist: EXE/DLL und MSI sind gültig signiert, bevor Prüfsummen erzeugt wurden.

## Sichtbare Kopie / Raster-Fallback

Der Weg ist automatisiert bis hin zu veraPDF und CEN-Schematron geprüft. Hier
geht es zusätzlich darum, wie das Ergebnis auf einem echten Windows-System
aussieht.

1. `01_Kontrolle_eingebettete_Schriften.pdf` – geht den direkten Weg, ohne Angebot einer Kopie. Der Prüfbericht nennt „Direkte Übernahme der Originalseiten“.
2. `02_Nicht_eingebettete_Schrift.pdf` – „Weiter“ ist zunächst gesperrt; das Angebot erscheint als Warnung und nicht als Fehler.
3. Ohne Zustimmung lässt sich Schritt 1 nicht verlassen.
4. Nach „Mit visueller Kopie fortfahren“ steht dort „Visuelle Kopie wird verwendet“, und „Weiter“ ist frei.
5. Die Datenerkennung ist vorher auf dem **Original** gelaufen: Die Erkennungsübersicht zeigt Werte, obwohl die Kopie später keinen Text mehr enthält.
6. Die erzeugte Datei öffnen und mit dem Original vergleichen: nichts beschnitten, nichts verschoben, Schrift bei 100 % lesbar.
7. Im Ergebnis lässt sich kein Text markieren – das ist der erwartete Preis.
8. Der Prüfbericht nennt „Sichtbare PDF/A-Kopie (Raster-Fallback, 300 dpi)“.
9. `03_QR_Code_300dpi_Kandidat.pdf` – QR-Code im **Ergebnis** mit einem Mobiltelefon einlesen. Er muss denselben Inhalt liefern wie im Original.
10. `04_Mehrseitig_gemischte_Orientierung.pdf` – Seitenzahl und Seitenausrichtung bleiben erhalten, auch bei gedrehten Seiten.
11. `05_Scan_only_kein_PDF_Text.pdf` – kein Befund zur Schrifteinbettung; die Datei geht den direkten Weg. Die Erkennung meldet fehlenden Text und erfindet nichts.
12. `06_Dicht_10_Seiten.pdf` – Dauer und Dateigröße notieren.
13. `07_Besitzerkennwort_Rechte_eingeschraenkt.pdf` – wird schon in Schritt 1 abgelehnt; Meldung nennt die Berechtigungseinschränkung und bezeichnet die Datei nicht als beschädigt.
14. `08_Oeffnungspasswort_test123.pdf` – wird ohne Kennwortabfrage sauber abgelehnt; kein zusätzliches Störungsfenster mit englischem Bibliothekstext.
15. Nach jedem Durchlauf: Original-PDF unverändert; Dateigröße und Änderungsdatum, im Zweifel Prüfsumme vergleichen.

## Fünfstufiger Echtdurchlauf

- [ ] Eine echte, nicht vertrauliche Test-PDF über Dateidialog laden.
- [ ] Eine Test-PDF per Drag-and-drop laden.
- [ ] PDF-Vorschau prüfen.
- [ ] Erkennungsübersicht prüfen.
- [ ] Feldkennzeichnungen und Herkunftshinweise prüfen.
- [ ] Summenabgleich prüfen.
- [ ] Vergleichsschritt vollständig durchführen.
- [ ] ZUGFeRD-/Factur-X-PDF erzeugen.
- [ ] Ergebnis und Prüfbericht öffnen.
- [ ] E-Mail-Entwurf erzeugen.

## Vorhandene E-Rechnung technisch prüfen

- [ ] Im Hauptfenster „E-Rechnung prüfen …“ öffnen; der bestehende
  Erzeugungsvorgang bleibt sichtbar unverändert erhalten.
- [ ] Eine normale Factur-X-/ZUGFeRD-Hybridrechnung auswählen: Dateiname,
  Größe, SHA-256, PDF-Angaben, Rechnungsanhang, Profil, Kerndaten, Summen und
  Befunde erscheinen.
- [ ] Rechnungsdatum erscheint als `dd.MM.yyyy` ohne Uhrzeit oder AM/PM.
- [ ] Positionssumme, Nettosumme, Gesamtsteuer, Bruttosumme und offener
  Zahlbetrag erscheinen mit deutschem Dezimaltrennzeichen, zwei
  Nachkommastellen und der Währung der geprüften Rechnung.
- [ ] Der Hinweis nennt sichtbar, dass dies keine vollständige EN-16931- oder
  PDF/A-Konformitätsprüfung ist; nirgends wird aus „vollständig durchgeführt“
  ein „gültig“ oder „bestanden“.
- [ ] Gewöhnliche PDF ohne Rechnungs-XML prüfen: verständlicher Leerbefund,
  kein Absturz und keine Datei neben der Quelle.
- [ ] Beschädigte Rechnungs-XML prüfen: eigener XML-Befund, nicht fälschlich
  „keine Rechnungsdaten“.
- [ ] XRechnung und Order-X prüfen: als erkannt, aber nicht unterstützt
  bezeichnet; ihr Inhalt wird nicht auf Verdacht entpackt.
- [ ] PDF mit mehreren rechnungsartigen Anhängen prüfen: keine willkürliche
  Auswahl und keine angezeigten Rechnungsdetails aus dem ersten Anhang.
- [ ] Übergroßen beziehungsweise nicht begrenzt entpackbaren Anhang prüfen:
  kontrollierter Befund, Anwendung bleibt bedienbar.
- [ ] Laufende Prüfung abbrechen und danach eine andere Datei auswählen: Der
  alte Anzeigestand ist vollständig verschwunden.
- [ ] Prüffenster während eines begonnenen Erzeugungsvorgangs schließen:
  aktueller Schritt und Eingaben sind unverändert.
- [ ] Quelldatei vor und nach dem UI-Ablauf per SHA-256 und Bytevergleich
  prüfen; sie ist unverändert, daneben entstand keine neue Datei.
- [ ] Tastaturbedienung, Fokusreihenfolge, Sprachausgabenamen, Scrollbereich
  und Darstellung bei der Mindestfenstergröße prüfen.
- [ ] Im Erfassungsworkflow den blockierenden Verkäuferbefund
  `APP-SEL-004`/`BR-CO-26` auslösen: Der verständliche Fachtext bleibt die
  Hauptaussage; beide Kennungen stehen weiterhin in einer nachrangigen,
  verständlich beschrifteten technischen Detailzeile.
- [ ] Einen rein internen Befund ohne Normregel anzeigen: Die technische
  Detailzeile erfindet keinen EN-16931-Bezug.

## Neue Rechnung und Einstellungen

- [ ] Nach einem vollständigen Durchlauf „Neue Rechnung“ starten: Formular ist leer, eigene Firma steht wieder da.
- [ ] Einstellungen ändern und schließen: neue Vorgaben gelten ohne Neustart für den nächsten Vorgang.
- [ ] Ein bereits ausgefülltes Formular bleibt durch bloßes Speichern der Einstellungen unberührt.
- [ ] Ist Schritt 2 bereits offen, erscheint die vorgesehene Rückfrage; „Für diese Rechnung übernehmen“ aktualisiert ausschließlich Verkäufer- und Bankangaben.
- [ ] Ohne Firmenvorlage typische Rechnung mit eindeutigem Seller und getrenntem Käuferblock öffnen: Seller wird sichtbar als erkannt vorausgefüllt.
- [ ] „Als meine Unternehmensdaten speichern“ verwenden: Bei der ersten Vorlage wird ohne vorgelagerten zweiten Bestätigungsschritt gespeichert.
- [ ] Dasselbe Angebot mit „Nicht als eigene Daten speichern“ schließen: Erkannte Rechnungsfelder bleiben stehen, die Vorlage bleibt unverändert.
- [ ] Neue Rechnung starten: Die gespeicherten Unternehmensdaten werden als normale Firmenvorlage vorbelegt.
- [ ] Mehrdeutige Rechnung mit zwei gleich starken Firmen prüfen: Seller bleibt leer und es erscheint kein Speicherangebot.
- [ ] Rechnung mit Käufer-/Lieferblock oben oder links prüfen: Kein Wert dieses Blocks wird dem Seller zugeordnet.
- [ ] Rechnung mit mehreren gültigen IBANs prüfen: Keine Bankverbindung wird als eigene vorgeschlagen.
- [ ] Erkannte Werte außerhalb des konkreten Seller-Proposals bleiben von der Speicheraktion ausgeschlossen.
- [ ] Bei vorhandener Firmenvorlage genau ein Verkäuferfeld manuell ändern: Vor dem Aktualisieren erscheint die Inline-Bestätigung.
- [ ] Inline-Bestätigung abbrechen: Die gespeicherte Vorlage bleibt unverändert.
- [ ] Nach erfolgreichem Speichern bleiben Inhalt und Herkunftshinweise der laufenden Rechnung unverändert.
- [ ] Firmenvorlage anschließend in den regulären Einstellungen öffnen und bearbeiten.

## Buyer-Land, -USt-ID und -E-Mail

- [ ] PDF mit eindeutigem Rechnungsempfänger einlesen: Land, USt-IdNr. und
  E-Mail erscheinen in Schritt 2 mit PDF-Herkunft.
- [ ] Rechnung mit Seller- und Buyerangaben in zwei Spalten prüfen: Land,
  USt-IdNr. und E-Mail bleiben den richtigen Parteien zugeordnet.
- [ ] Vorgelagerte Lieferanschrift prüfen: Sie wird nicht zum Rechnungsempfänger.
- [ ] PDF ohne Buyerland prüfen: Das Feld bleibt leer und wird nicht als `DE` vorbelegt.
- [ ] Zwei plausible Buyerblöcke prüfen: Es wird keine erste Fundstelle geraten.
- [ ] Schritt 3 zeigt Buyerland, Buyer-USt-IdNr. und Buyer-E-Mail vollständig.
- [ ] Gültige Buyer-E-Mail erzeugt einen `.eml`-Entwurf mit dieser Adresse als
  Empfänger; ungültige E-Mail stoppt vorher mit einem Validierungsbefund.
- [ ] Reverse-Charge- und innergemeinschaftliche Rechnung ohne Buyer-USt-ID
  werden verständlich blockiert; mit gültiger ID bestehen sie die Prüfung.

## Typische Kleinunternehmer-Rechnung

- [ ] Anonymisierte Referenzrechnung ohne Firmenvorlage einlesen:
  Rechnungsnummer, Rechnungsdatum, Leistungszeitraum, Seller, Buyer und Summen
  entsprechen der sichtbaren Rechnung.
- [ ] Das nachgestellte Ortsdatum wird als mittlere Erkennungssicherheit
  gekennzeichnet; ein ausdrücklich beschriftetes Rechnungsdatum bleibt hoch.
- [ ] Leistungszeitraum ist in Schritt 2 vollständig sichtbar und wird als
  Zeitraum, nicht als Lieferdatum, übernommen.
- [ ] Sellerkopf und ankerloser Buyer bleiben trotz mehrspaltigem Layout sauber
  getrennt; Lieferanschriften werden nicht zum Buyer.
- [ ] Positionsnahe Nettoangaben werden nicht mit `Gesamt Netto` verwechselt.
- [ ] Die ungültige Referenz-IBAN bleibt leer. Eine einzeln erkannte BIC wird
  ohne gültige zugehörige IBAN nicht als eigene Bankverbindung angeboten.
- [ ] Bei zwei gleich plausiblen Referenzen, Empfänger- oder Sellerblöcken wird
  kein erster Fund geraten.

## Positionserkennung – Freigabeschranke ER-020-POS-01

Diese Punkte sind vor der Freigabe auf Windows durchzuführen. Grüne Unittests
genügen hier nicht: Zu prüfen ist, was der Anwender im Fenster sieht.

- [ ] Eine digitale Rechnung mit klar aufgebauter Positionstabelle einlesen:
  Schritt 1 meldet „Rechnungspositionen erkannt: N“ – **nur die Anzahl**, keine
  Beschreibungen, Mengen oder Preise.
- [ ] Schritt 2 zeigt alle N Positionen mit Bezeichnung, Beschreibung, Menge,
  Einheit, Einzelpreis, Steuersatz und Steuerkategorie. Die Einheit stimmt mit
  der PDF überein und steht nicht pauschal auf „Stück“.
- [ ] Die Meldung über der Tabelle nennt übernommene Felder und übernommene
  Positionen getrennt und endet mit der Aufforderung zu prüfen.
- [ ] Alle bearbeitbaren Positionsangaben lassen sich ändern; die Summen
  rechnen danach neu. Die Positionsnummerierung verhält sich wie bisher.
- [ ] Eine Rechnung **ohne** Mengeneinheit einlesen: Die Positionen werden
  übernommen, das Einheitenfeld bleibt leer, und beide Schritte nennen die
  Anzahl der betroffenen Positionen. Es steht dort ausdrücklich **nicht**
  „Stück“.
- [ ] „Weiter“ ist mit leerer Einheit nicht möglich; nach dem Ergänzen einer
  gültigen Einheit läuft der Ablauf normal weiter.
- [ ] Eine Rechnung mit einer nicht unterstützten Einheit (etwa „Fass“) ergibt
  null Positionen – keine Teilmenge.
- [ ] Eine Rechnung **ohne eigene Steuerspalte** wird nur dann erkannt, wenn
  im Dokument **genau ein** Steuersatz von 7 % oder 19 % steht und dieser die
  bestehenden Sicherheitsbedingungen erfüllt: Netto, Steuer und Brutto müssen
  alle drei zweifelsfrei gelesen sein, die Steuer muss sich aus Netto und Satz
  ergeben, und Brutto muss Netto plus Steuer sein. Fehlt eine dieser
  Bedingungen – kein Satz, mehrere Sätze, ein anderer Satz, eine unsicher
  gelesene Summe oder eine Summe, die nicht aufgeht –, ergibt die Rechnung
  null Positionen.
- [ ] Ein Aufbau mit widersprüchlichen Dokumentsummen ergibt null Positionen.
- [ ] Eine Rechnung ohne Tabellenkopf ergibt null Positionen, und Schritt 1
  sagt ausdrücklich, dass von Hand zu erfassen ist.
- [ ] Zuerst von Hand eine Position eintragen, dann die PDF einlesen: Die
  eigene Zeile bleibt unverändert stehen, es kommt keine erkannte hinzu, und
  die Meldung nennt die nicht übernommenen Positionen mit Grund.
- [ ] „Neue Rechnung“ räumt auch vorbefüllte Positionen weg.
- [ ] Die fertige E-Rechnung besteht Mustang/CEN-Schematron und veraPDF, und
  die Beschreibung steht als BT-154 in der CII-Datei.

## Summen während der Eingabe

1. Drei Positionen eintragen – 4 × 85,00, 2 × 95,00, 1 × 70,00, jeweils 19 %.
2. Nach Verlassen der letzten Zelle müssen die Summen automatisch 600,00 / 114,00 / 714,00 / 714,00 zeigen.
3. Steuersatz der letzten Position ändern und **ohne** Verlassen der Zelle „Summen neu berechnen“ klicken: Summen müssen sofort passen.
4. Dasselbe mit Menge und Einzelpreis als zuletzt bearbeiteter Zelle durchführen.
5. „Weiter“ darf eine bereits angezeigte korrekte Summe nicht verändern.

## Installer – sauberer Windows-11-Rechner

Zielsystem: Windows 11 x64 ohne .NET SDK, Java, Mustang oder veraPDF.

- [ ] MSI installieren.
- [ ] Anwendung starten.
- [ ] PDF laden.
- [ ] Rechnung erfassen.
- [ ] ZUGFeRD-PDF erzeugen.
- [ ] Speichern.
- [ ] E-Mail-Entwurf erzeugen.
- [ ] Anwendung deinstallieren.
- [ ] Es musste keine Laufzeit nachinstalliert werden.

## Installer – Benutzerinstallation und Upgrade

- [ ] MSI ohne erhöhte Rechte starten; für die normale Benutzerinstallation darf keine UAC-Rückfrage erscheinen.
- [ ] Anwendung startet.
- [ ] Installationspfad entspricht der freigegebenen Installerkonfiguration.
- [ ] Bei sauberer Erstinstallation erscheint „Desktop-Verknüpfung erstellen“ standardmäßig aktiviert.
- [ ] Erstinstallation mit aktivierter Option: Startmenüeintrag und genau eine Desktopverknüpfung sind vorhanden; Ziel ist die installierte `EInvoiceSender.exe`, das Symbol gehört zu BorstWerk E-Rechnung.
- [ ] Erstinstallation mit abgewählter Option: Startmenüeintrag ist vorhanden, Desktopverknüpfung fehlt.
- [ ] Im Dialog zurück- und wieder vorgehen, Auswahl ändern und prüfen, dass nur der zuletzt bestätigte Zustand installiert wird.
- [ ] Stille Erstinstallation ohne UI legt Startmenü- und Desktopverknüpfung standardmäßig an.
- [ ] Repair nach aktivierter Option zeigt die Auswahl nicht erneut und behält genau eine Desktopverknüpfung.
- [ ] Repair nach abgewählter Option zeigt die Auswahl nicht erneut und legt keine Desktopverknüpfung an.
- [ ] Upgrade von der letzten veröffentlichten Version auf den Releasekandidaten durchführen.
- [ ] Upgrade zeigt die Desktopoption nicht erneut und übernimmt den bisherigen Featurezustand.
- [ ] Upgrade einer Installation mit Desktopverknüpfung behält genau eine Desktopverknüpfung; ohne vorherige Desktopverknüpfung bleibt sie abwesend.
- [ ] Nach Upgrade existiert genau ein Programmeintrag.
- [ ] Keine doppelten Startmenü- oder Desktopverknüpfungen.
- [ ] Gespeicherte Firmendaten und Einstellungen bleiben erhalten.
- [ ] Daten unter `%LOCALAPPDATA%\EInvoiceSender` bleiben erhalten.
- [ ] Erneute Installation derselben Version erzeugt keine zweite Produktinstanz.
- [ ] Downgrade auf die vorherige Version wird sauber verhindert beziehungsweise verständlich behandelt.
- [ ] Deinstallation entfernt eine vom Installer angelegte Desktopverknüpfung sowie den Startmenüeintrag, nicht aber die Benutzerdaten.
- [ ] Symbol in Taskleiste, Startmenü, Explorer und „Installierte Apps“ prüfen.

Falls die freigegebene Installerarchitektur weiterhin eine Installation für alle
Benutzer unterstützt:

- [ ] `msiexec /i BorstWerk-E-Rechnung-Setup.msi MSIINSTALLPERUSER=""` jeweils mit Desktopoption an und aus prüfen; UAC ist hier erwartet und der Zielpfad muss der freigegebenen Machine-Installation entsprechen.
- [ ] Per-machine Repair und Deinstallation im Installationsbenutzerprofil prüfen; das bestehende profilbezogene HKCU-/DesktopFolder-Verhalten darf nicht regressieren.

## Lokaler Releasebau

- [ ] `.\build\Test-ReleasePackaging.ps1` ausführen.
- [ ] `.\build\Build-Release.ps1` aus einer gewöhnlichen PowerShell ohne erhöhte Rechte ausführen.
- [ ] MSI-Prüfung läuft fehlerfrei durch.
- [ ] `artifacts/release` enthält exakt MSI, portable ZIP und `SHA256SUMS.txt`.
- [ ] Ein zweiter Releasebau über vorhandenen Artefakten bleibt frei von Altdateien.
- [ ] Lokaler Build und CI erzeugen inhaltlich denselben vorgesehenen Satz von Release-Artefakten.

## Oberfläche

- [ ] Skalierung 100 %: vollständiger Ablauf bedienbar.
- [ ] Skalierung 150 %: vollständiger Ablauf bedienbar.
- [ ] Skalierung 200 %: vollständiger Ablauf bedienbar.
- [ ] Kleinste zulässige Fenstergröße 940 × 640: Kopf, Schritt, Statuszeile und Navigation überlagern sich nicht.
- [ ] Mit Tabulatortaste durch jeden Schritt gehen; Fokus ist überall sichtbar.
- [ ] Zugriffstasten Alt+W, Alt+Z, Alt+R, Alt+E und Alt+Ü prüfen.
- [ ] Alle Statusarten prüfen: Fehler, Warnung, Hinweis, Erfolg; jeweils Zeichen, Wort und Farbe sichtbar.
- [ ] Einstellungen öffnen.
- [ ] „Über“ öffnen.
- [ ] Fenstersymbol und Taskleistensymbol prüfen.
- [ ] Fassungsnummer im Info-Bereich entspricht der gebauten Version und ist nicht „unbekannt“.

## Windows-spezifische Funktionen

- [ ] `.eml` im klassischen Outlook praktisch öffnen.
- [ ] `.eml` im neuen Outlook praktisch öffnen.
- [ ] DPAPI-Schutz der IBAN in den Einstellungen prüfen.
- [ ] Einen vollständigen Testvorgang durchführen und „Über“ → „Diagnoseordner öffnen“ wählen; geöffnet wird `%LOCALAPPDATA%\EInvoiceSender\Diagnose`.
- [ ] Das Sitzungslog enthält Start, technische Verarbeitung und reguläres Ende, aber keine Ein-/Ausgabedateinamen oder Rechnungs-/Kundendaten.
- [ ] Es findet keine automatische Übertragung, Freigabe oder Versendung der Diagnoseprotokolle statt.
- [ ] Mehr als zehn abgeschlossene Sitzungslogs werden beim nächsten Start auf zehn begrenzt; aktive Logs paralleler Instanzen bleiben unberührt.

## Abschluss

- [ ] Alle für diese Version verbindlichen Requirements haben einen dokumentierten Nachweis.
- [ ] Vollständige CI ist grün.
- [ ] Externe Referenzvalidatoren sind grün.
- [ ] README, bekannte Grenzen und Release Notes entsprechen dem tatsächlich veröffentlichten Funktionsstand.
- [ ] Erst danach Release-Tag und öffentliche Artefakte erstellen beziehungsweise veröffentlichen.

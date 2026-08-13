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

## Neue Rechnung und Einstellungen

- [ ] Nach einem vollständigen Durchlauf „Neue Rechnung“ starten: Formular ist leer, eigene Firma steht wieder da.
- [ ] Einstellungen ändern und schließen: neue Vorgaben gelten ohne Neustart für den nächsten Vorgang.
- [ ] Ein bereits ausgefülltes Formular bleibt durch bloßes Speichern der Einstellungen unberührt.
- [ ] Ist Schritt 2 bereits offen, erscheint die vorgesehene Rückfrage; „Für diese Rechnung übernehmen“ aktualisiert ausschließlich Verkäufer- und Bankangaben.
- [ ] Ohne Firmenvorlage Verkäuferdaten von Hand erfassen und „Als eigene Unternehmensdaten speichern“ verwenden; es erscheint keine zweite Bestätigung.
- [ ] Neue Rechnung starten: Die gespeicherten Unternehmensdaten werden als normale Firmenvorlage vorbelegt.
- [ ] Erkannte fremde Verkäuferdaten sowie erkannte IBAN/BIC bleiben von der Speicheraktion ausgeschlossen.
- [ ] Bei vorhandener Firmenvorlage genau ein Verkäuferfeld manuell ändern: Vor dem Aktualisieren erscheint die Inline-Bestätigung.
- [ ] Inline-Bestätigung abbrechen: Die gespeicherte Vorlage bleibt unverändert.
- [ ] Nach erfolgreichem Speichern bleiben Inhalt und Herkunftshinweise der laufenden Rechnung unverändert.
- [ ] Firmenvorlage anschließend in den regulären Einstellungen öffnen und bearbeiten.

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

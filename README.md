# BorstWerk E-Rechnung

BorstWerk E-Rechnung ist eine Windows-Desktopanwendung, die aus einer **bereits vorhandenen PDF-Rechnung** eine **ZUGFeRD-/Factur-X-Rechnung** im Profil EN 16931 erzeugt.

Die Anwendung ist bewusst **kein Rechnungsprogramm**. Sie schreibt keine Rechnungen, vergibt keine Rechnungsnummern, führt keine Buchhaltung und versendet keine E-Mails selbst. Sie nimmt die Rechnung, die bereits vorhanden ist, ergänzt die benötigten strukturierten Rechnungsdaten und erzeugt daraus eine elektronische Rechnung.

Alles läuft **lokal auf dem eigenen Rechner**. Rechnungen, Adressen, Bankverbindungen und E-Mail-Daten werden nicht an externe Dienste übertragen.

Die Anwendung gehört zur Werkzeugfamilie **BorstWerk** und ist eigenständig installier- und nutzbar.

---

# Für Anwender

## Was brauche ich?

- Windows 10 oder Windows 11, 64 Bit
- eine vorhandene PDF-Rechnung

Mehr nicht.

Die ausgelieferte Anwendung bringt die benötigte .NET-Laufzeit mit. **Java wird für die installierte Anwendung nicht benötigt** und vom Installer auch nicht eingerichtet.

## Installation

Für die normale Nutzung ist das MSI-Installationspaket vorgesehen.

Die Standardinstallation erfolgt **nur für den aktuellen Benutzer** und benötigt keine Administratorrechte. Die Anwendung wird dabei unter dem persönlichen Windows-Programmverzeichnis installiert.

Der Installer:

- legt einen Eintrag im Startmenü an,
- kann optional eine Desktopverknüpfung erstellen,
- unterstützt spätere Updates,
- verhindert versehentliche Downgrades,
- lässt persönliche Einstellungen bei der Deinstallation unangetastet.

Alternativ kann eine portable ZIP-Fassung verwendet werden.

## So funktioniert es

Die Anwendung führt in fünf Schritten durch den Vorgang:

1. **PDF auswählen**  
   Die vorhandene Rechnung wird geprüft und als Vorschau angezeigt. Die Originaldatei wird nur gelesen und niemals verändert.

2. **Rechnungsdaten prüfen und ergänzen**  
   Erkannte Angaben werden in das Formular übernommen. Fehlende oder unsichere Angaben können ergänzt beziehungsweise korrigiert werden.

3. **PDF und strukturierte Daten vergleichen**  
   Vor der Erzeugung muss ausdrücklich bestätigt werden, dass die erfassten Rechnungsdaten mit der sichtbaren PDF übereinstimmen.

4. **E-Rechnung erzeugen**  
   Die strukturierte XML wird erstellt, geprüft und in die PDF/A-3-Datei eingebettet.

5. **Ergebnis speichern und E-Mail vorbereiten**  
   Die fertige Datei und ein Prüfbericht werden gespeichert. Auf Wunsch erstellt die Anwendung einen `.eml`-Entwurf für das vorhandene Mailprogramm. Die Nachricht wird nicht automatisch versendet.

## Welche Daten kann die Anwendung aus der PDF lesen?

Bei digital erzeugten PDFs versucht die Anwendung, bereits vorhandenen PDF-Text lokal auszulesen und das Formular damit vorauszufüllen.

Derzeit werden unter anderem erkannt:

- Rechnungsnummer
- Rechnungsdatum
- Leistungsdatum
- Fälligkeitsdatum
- Währung
- Käuferangaben aus dem Adressblock
- Verkäuferangaben mithilfe der gespeicherten Firmenvorlage
- IBAN und BIC
- Nettobetrag
- Umsatzsteuer
- Bruttobetrag
- Zahlbetrag
- Steuersätze

Jeder erkannte Wert bleibt überprüfbar und kann geändert werden. Unsichere Werte werden entsprechend gekennzeichnet oder nicht automatisch übernommen.

**Rechnungspositionen werden derzeit noch nicht zuverlässig aus Tabellen übernommen und müssen gegebenenfalls von Hand erfasst werden.**

Es findet aktuell keine OCR-Texterkennung für reine Scan-PDFs statt.

## Eigene Firmendaten speichern

Wiederkehrende Angaben können einmalig in den Einstellungen hinterlegt werden, zum Beispiel:

- Firmenname und Anschrift
- USt-IdNr. oder Steuernummer
- E-Mail-Adresse
- Kontoinhaber, IBAN und BIC
- Standardwährung
- Zahlungsbedingungen
- Standard-E-Mail-Text
- Ausgabeverzeichnis

Sensible gespeicherte Daten wie die IBAN werden unter Windows geschützt abgelegt.

## Datenschutz

Die Verarbeitung erfolgt lokal.

Die Anwendung:

- lädt keine Rechnung zu einem Webdienst hoch,
- verwendet keine Cloud-KI für Rechnungsinhalte,
- überträgt keine Bankverbindungen oder Empfängeradressen,
- verändert niemals die Original-PDF,
- versendet keine E-Mail ohne Zutun des Benutzers.

## Prüfbericht

Zu jeder erzeugten E-Rechnung kann ein Prüfbericht erstellt werden. Darin stehen unter anderem:

- Rechnungsnummer und Beteiligte
- erzeugte Datei
- Dateigröße
- SHA-256-Prüfsumme
- festgestellte Fehler und Warnungen
- tatsächlich verwendete Prüfwerkzeuge

Die installierte Anwendung führt ihre eingebauten Prüfungen aus. Externe Referenzvalidatoren gehören zur Entwicklungs- und Releaseprüfung und werden nicht mit dem normalen Installer ausgeliefert. Der Prüfbericht macht ausdrücklich kenntlich, wenn keine externe Referenzprüfung stattgefunden hat.

## Bekannte Grenzen

- **Nicht jede PDF lässt sich direkt verwenden.** Geeignete PDFs werden nach PDF/A-3 aufgewertet, und dieser Weg hat immer Vorrang. Fehlt einer Datei nur die Schrifteinbettung, bietet die Anwendung stattdessen eine **sichtbare Kopie** an: Sie stellt die Seiten örtlich dar und baut daraus ein neues Dokument. Das kostet den durchsuchbaren Text, lässt die Rechnungsdaten in der eingebetteten XML aber vollständig maschinenlesbar – und geschieht nur nach ausdrücklicher Zustimmung. Beschädigte, kennwortgeschützte, rechtebeschränkte, digital signierte PDFs und solche mit aktiven Inhalten werden weiterhin abgelehnt.
- **Rechnungspositionen müssen derzeit häufig noch von Hand erfasst werden.**
- **Keine Steuerberatung.** Die Anwendung kann technische und formale Prüfungen durchführen, aber nicht beurteilen, ob eine Rechnung steuerlich oder inhaltlich richtig ist.

Ausführlicher: [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md)

## Bildschirmfotos

<!-- Bildschirmfotos der fünf Schritte ergänzen, sobald die Oberfläche für die erste Veröffentlichung eingefroren ist. -->

| Schritt | Bild |
|---|---|
| 1 – PDF auswählen | _(folgt)_ |
| 2 – Rechnungsdaten | _(folgt)_ |
| 3 – Kontrollansicht | _(folgt)_ |
| 4 – Erzeugen | _(folgt)_ |
| 5 – Ergebnis | _(folgt)_ |

---

# Für Entwickler

## Ziel des Projekts

Das Projekt soll ein kleines, nachvollziehbares Windows-Werkzeug bleiben und ausdrücklich **kein ERP-, Buchhaltungs- oder CRM-System** werden.

Die Lösung besteht im Wesentlichen aus:

```text
EInvoiceSender.sln

src/
├── EInvoiceSender.App
└── EInvoiceSender.Core

tests/
├── EInvoiceSender.Core.Tests
└── EInvoiceSender.IntegrationTests

installer/
└── EInvoiceSender.Setup
```

`EInvoiceSender.App` enthält WPF-Oberfläche und Windows-spezifische Bedienlogik.  
`EInvoiceSender.Core` enthält Rechnungsmodelle, Berechnung, Validierung, CII-/ZUGFeRD-Erzeugung, PDF-Verarbeitung, Speicherung und E-Mail-Entwurf.

Die Architektur soll bewusst einfach bleiben. Zusätzliche Projekte, Interfaces oder Frameworks sollten nur eingeführt werden, wenn sie einen konkreten Nutzen für Wartbarkeit, Testbarkeit oder Plattformtrennung haben.

## Entwicklungsumgebung

Empfohlen:

- .NET SDK 10
- Visual Studio 2026
- Arbeitslast **.NET-Desktopentwicklung**
- Windows x64

Die normale Anwendung benötigt kein Java.

## In Visual Studio starten

1. Repository klonen
2. `EInvoiceSender.sln` öffnen
3. `EInvoiceSender.App` als Startprojekt verwenden
4. **F5**

Die Tests erscheinen im Visual-Studio-Test-Explorer.

## Bauen und testen

Die alltägliche Entwicklung kann vollständig aus Visual Studio erfolgen.

Zusätzlich stehen PowerShell-Skripte zur Verfügung:

```powershell
.\build\Build.ps1
.\build\Test.ps1
.\build\Publish.ps1
.\build\Build-Installer.ps1
```

Entsprechend direkt mit dem .NET-SDK:

```powershell
dotnet build EInvoiceSender.sln -c Release
dotnet test EInvoiceSender.sln -c Release
dotnet format EInvoiceSender.sln --verify-no-changes
```

## Self-contained Publish

`Publish.ps1` erzeugt eine eigenständige `win-x64`-Fassung. Die .NET-Laufzeit wird mit veröffentlicht; auf dem Zielrechner muss daher kein separates .NET Runtime-Paket installiert werden.

## Installer bauen

Unter Windows mit PowerShell 7:

```powershell
.\build\Build-Installer.ps1
```

Das Skript veröffentlicht die Anwendung und erzeugt anschließend:

- MSI-Installationspaket
- portable ZIP-Fassung
- SHA-256-Prüfsummen

Die Standardinstallation des MSI ist eine Installation für den aktuellen Benutzer ohne UAC-Rückfrage. Das gleiche Paket kann bei Bedarf auch für alle Benutzer installiert werden.

WiX wird nur zum **Bauen** des Installers benötigt, nicht auf dem Zielrechner.

## Externe Referenzprüfung

Die Anwendung besitzt eigene Prüfungen. Für Entwicklung, Regressionstests und Releasefreigaben werden zusätzlich unabhängige Referenzwerkzeuge verwendet:

- Mustangproject
- CEN-Schematron für EN 16931
- veraPDF für PDF/A

Für diese zusätzliche Referenzprüfung wird Java 17 oder neuer benötigt.

**Das betrifft ausschließlich Entwicklung und Releaseprüfung. Die installierte Anwendung benötigt kein Java.**

Die Gegenprüfung kann über die vorhandenen Build-/Testskripte ausgeführt werden. Details stehen in [`docs/TESTING.md`](docs/TESTING.md).

## Grundsätze für Änderungen

Das Projekt orientiert sich an einer einfachen, wartbaren Struktur. Besonders wichtig sind:

- KISS und DRY
- kleine, klar benannte Verantwortlichkeiten
- Single Responsibility und Separation of Concerns
- verständliche Integrationsmethoden auf einem einheitlichen Abstraktionsniveau
- Root-Cause-Fixes statt Symptombehandlung
- Regressionstests für gefundene Fehler
- echte deutsche Umlaute in deutschsprachigen Texten
- keine unnötigen neuen Abhängigkeiten

Fachlich kritische Änderungen an CII/XML, PDF/A, Einbettung, Summen oder Steuerlogik müssen zusätzlich gegen die vorhandenen Golden Master und externen Referenzvalidatoren geprüft werden.

## Oberfläche und Symbole

Farben, Schriftgrößen, Abstände und Bedienelemente stehen unter
`src/EInvoiceSender.App/UI/Themes/`. In den Ansichten steht kein Farbwert –
wer eine Farbe ändern will, ändert sie dort.

Das BorstWerk-Zeichen und die Windows-Symboldatei entstehen aus einer
gemeinsamen Geometrie:

```powershell
dotnet run --project build/icon
```

Das schreibt `src/EInvoiceSender.App/Assets/BorstWerkEInvoice.ico` und die
Vorschau unter `docs/images/`. Die Pfaddaten stehen in
`build/icon/BorstWerkMark.cs` und noch einmal in
`UI/Themes/BorstWerkLogo.xaml`; ein Test vergleicht beide, damit die
Vektorzeichnung in der Oberfläche und das Symbol nicht auseinanderlaufen.

Das Werkzeug ist nicht Teil der Projektmappe und läuft nicht im Bau mit –
das Ergebnis ist eingecheckt.

## Weitere Unterlagen

| Datei | Inhalt |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Aufbau der Projektmappe und Ablauf |
| [`docs/BUILD.md`](docs/BUILD.md) | Bauen mit Visual Studio und PowerShell |
| [`docs/E-INVOICE-STANDARD.md`](docs/E-INVOICE-STANDARD.md) | Norm, Profil und verwendete Fassungen |
| [`docs/TESTING.md`](docs/TESTING.md) | Testebenen und Referenzvalidatoren |
| [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md) | Bekannte Grenzen |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | Offene Punkte |
| [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md) | Fremdkomponenten und Lizenzen |

`docs/legacy/` enthält Unterlagen aus der Entstehungszeit. Sie bleiben als Historie erhalten, sind für die aktuelle Entwicklung aber nicht mehr maßgeblich.

# EInvoiceSender

Eine Windows-Desktopanwendung, die aus einer **vorhandenen PDF-Rechnung** und
von Hand erfassten, ausdrücklich bestätigten Rechnungsdaten eine
**ZUGFeRD-/Factur-X-Rechnung** (Profil EN 16931, PDF/A-3) erzeugt und einen
E-Mail-Entwurf vorbereitet.

Die Anwendung ist **kein Rechnungsprogramm**: Sie schreibt keine Rechnungen,
vergibt keine Rechnungsnummern, führt keine Buchhaltung und versendet nichts
von selbst. Sie nimmt die Rechnung, die Sie ohnehin schon haben, und macht
daraus eine elektronische Rechnung.

Nach der Auswahl der PDF versucht die Anwendung, das Formular aus dem bereits
vorhandenen PDF-Text **vorauszufüllen**. Gelesen werden derzeit:

- Rechnungsnummer
- Rechnungs-, Leistungs- und Fälligkeitsdatum
- Währung
- Käuferangaben aus dem Adressblock, Verkäuferangaben über die gespeicherte
  Firmenvorlage
- IBAN und BIC
- Netto, Umsatzsteuer, Brutto, Zahlbetrag und die Steuersätze

**Rechnungspositionen werden noch nicht aus Tabellen übernommen** und müssen
von Hand erfasst werden.

Jeder Vorschlag ist gekennzeichnet und lässt sich überschreiben; unsichere
Werte werden gar nicht erst eingetragen. Es findet keine Texterkennung an
Bildern statt, und die Bestätigung durch Sie bleibt in jedem Fall
erforderlich.

Alles läuft örtlich auf Ihrem Rechner. Es werden keine Rechnungsdaten,
PDF-Dateien, E-Mail-Adressen oder Bankverbindungen an fremde Rechner
übertragen.

## Bildschirmfotos

<!-- Platzhalter: Bildschirmfotos der fünf Schritte ergänzen. -->

| Schritt | Bild |
|---|---|
| 1 – PDF auswählen | _(noch nicht erfasst)_ |
| 2 – Rechnungsdaten | _(noch nicht erfasst)_ |
| 3 – Kontrollansicht | _(noch nicht erfasst)_ |
| 4 – Erzeugen | _(noch nicht erfasst)_ |
| 5 – Ergebnis | _(noch nicht erfasst)_ |

## Voraussetzungen

### Zum Benutzen der Anwendung

- Windows 10 oder 11, 64 Bit

Mehr nicht. Die ausgelieferte Fassung bringt die .NET-Laufzeit mit. **Eine
Java-Laufzeit wird nicht benötigt**, und das Installationspaket installiert
auch keine.

### Zum Entwickeln

- .NET SDK 10
- Visual Studio 2026 (oder 2022 ab 17.14) mit der Arbeitslast
  **.NET-Desktopentwicklung**

## Referenzprüfung in Entwicklung und Release

Für die zusätzliche Referenzprüfung während Entwicklung und Release wird
Java 17 oder neuer benötigt. **Die installierte Anwendung benötigt kein Java.**

Geprüft wird damit gegen Mustangproject, das offizielle CEN-Schematron und
veraPDF – das ist der Nachweis, dass diese Anwendung normgerechte Dateien
erzeugt. Diese Werkzeuge gehören in die Werkstatt, nicht auf den Rechner des
Anwenders: Sie werden mit `build/fetch-validators.sh` beschafft, laufen in der
Pipeline und über `build/Validate-Reference.ps1`, und sie sind kein Bestandteil
des Installationspakets.

Der Bericht zu einer erzeugten Rechnung sagt jeweils, was geprüft wurde. Ohne
eingerichteten Referenzvalidator steht dort ausdrücklich, dass keiner
eingerichtet war – die Anwendung behauptet nie, eine externe Prüfung habe
stattgefunden. Näheres in `docs/TESTING.md`.

## In Visual Studio starten

1. Repository klonen
2. `EInvoiceSender.sln` öffnen
3. `EInvoiceSender.App` ist als Startprojekt eingestellt
4. **F5**

Der Test-Explorer findet alle Tests ohne weitere Einrichtung.

## Bauen, testen, veröffentlichen

```powershell
.\build\Build.ps1                       # Wiederherstellen und Release-Build
.\build\Test.ps1                        # alle Tests
.\build\Test.ps1 -RequireExternalValidators   # wie in der Pipeline
.\build\Publish.ps1                     # eigenständige win-x64-Fassung
.\build\Build-Installer.ps1             # MSI, tragbares ZIP und Prüfsummen
.\build\Validate-Reference.ps1          # Gegenprüfung mit Schematron und veraPDF
```

Ohne Skripte geht es genauso:

```powershell
dotnet build EInvoiceSender.sln -c Release
dotnet test  EInvoiceSender.sln -c Release
dotnet format EInvoiceSender.sln --verify-no-changes
```

## Installer

`Build-Installer.ps1` erzeugt ein MSI. Vorgegeben ist die Installation
**nur für Sie** – ohne Administratorrechte, nach
`%LOCALAPPDATA%\Programs\EInvoiceSender Projekt\EInvoiceSender`. Dasselbe
Paket kann auch für alle Benutzer installiert werden; das verlangt dann die
übliche Rückfrage von Windows:

```powershell
msiexec /i EInvoiceSender-Setup.msi MSIINSTALLPERUSER=""
```

Der Installer legt einen Startmenüeintrag an, auf Wunsch eine
Desktopverknüpfung, unterstützt Upgrades, wehrt Downgrades mit einer
verständlichen Meldung ab und lässt Ihre Daten bei der Deinstallation
unangetastet – sie liegen unter `%LOCALAPPDATA%\EInvoiceSender` und damit
außerhalb des Installationsordners. WiX erzeugt MSI-Dateien nur unter
Windows.

## Einschränkungen in Kürze

- **Nicht jede PDF lässt sich verwenden.** Es gibt keine frei verwendbare
  .NET-Bibliothek, die beliebige PDFs nach PDF/A-3 wandelt. Die Anwendung
  wertet geeignete Dateien auf und lehnt ungeeignete mit einer Begründung ab –
  vor allem PDFs ohne eingebettete Schriften und digital signierte PDFs.
- **Die eigene Regelprüfung ist kein Konformitätsnachweis.** Die Freigabe
  erteilen die externen Referenzvalidatoren.
- **Keine Steuerberatung.** Geprüft wird das Format, nicht die inhaltliche
  oder steuerliche Richtigkeit.

Ausführlich: [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md)

## Weitere Unterlagen

| Datei | Inhalt |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Aufbau der Projektmappe und der Ablauf |
| [`docs/BUILD.md`](docs/BUILD.md) | Bauen mit Visual Studio und PowerShell |
| [`docs/E-INVOICE-STANDARD.md`](docs/E-INVOICE-STANDARD.md) | Norm, Profil und verwendete Fassungen |
| [`docs/TESTING.md`](docs/TESTING.md) | Testebenen und Referenzvalidatoren |
| [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md) | Bekannte Grenzen |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | Offene Punkte |
| [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md) | Fremdkomponenten und Lizenzen |

`docs/legacy/` enthält die ausführlichen Unterlagen aus der Entstehungszeit.
Sie sind aufgehoben, aber nicht mehr maßgeblich.

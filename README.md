# EInvoiceSender

Eine Windows-Desktopanwendung, die aus einer **vorhandenen PDF-Rechnung** und
von Hand erfassten, ausdruecklich bestaetigten Rechnungsdaten eine
**ZUGFeRD-/Factur-X-Rechnung** (Profil EN 16931, PDF/A-3) erzeugt und einen
E-Mail-Entwurf vorbereitet.

Die Anwendung ist **kein Rechnungsprogramm**: Sie schreibt keine Rechnungen,
vergibt keine Rechnungsnummern, fuehrt keine Buchhaltung und versendet nichts
von selbst. Sie nimmt die Rechnung, die Sie ohnehin schon haben, und macht
daraus eine elektronische Rechnung.

Alles laeuft oertlich auf Ihrem Rechner. Es werden keine Rechnungsdaten,
PDF-Dateien, E-Mail-Adressen oder Bankverbindungen an fremde Rechner
uebertragen.

## Bildschirmfotos

<!-- Platzhalter: Bildschirmfotos der fuenf Schritte ergaenzen. -->

| Schritt | Bild |
|---|---|
| 1 – PDF auswaehlen | _(noch nicht erfasst)_ |
| 2 – Rechnungsdaten | _(noch nicht erfasst)_ |
| 3 – Kontrollansicht | _(noch nicht erfasst)_ |
| 4 – Erzeugen | _(noch nicht erfasst)_ |
| 5 – Ergebnis | _(noch nicht erfasst)_ |

## Voraussetzungen

- Windows 10 oder 11, 64 Bit
- .NET SDK 10 (nur zum Entwickeln; die ausgelieferte Fassung bringt die
  Laufzeit mit)
- Visual Studio 2022 oder neuer mit der Arbeitslast **.NET-Desktopentwicklung**
- **Optional:** eine Java-Laufzeit (17 oder neuer) fuer die externen
  Referenzvalidatoren. Ohne sie laeuft die Anwendung normal weiter und weist
  das Ergebnis ausdruecklich als **nur intern geprueft** aus.

## In Visual Studio starten

1. Repository klonen
2. `EInvoiceSender.sln` oeffnen
3. `EInvoiceSender.App` ist als Startprojekt eingestellt
4. **F5**

Der Test-Explorer findet alle Tests ohne weitere Einrichtung.

## Bauen, testen, veroeffentlichen

```powershell
.\build\Build.ps1                       # Wiederherstellen und Release-Build
.\build\Test.ps1                        # alle Tests
.\build\Test.ps1 -RequireExternalValidators   # wie in der Pipeline
.\build\Publish.ps1                     # eigenstaendige win-x64-Fassung
.\build\Build-Installer.ps1             # MSI, tragbares ZIP und Pruefsummen
.\build\Validate-Reference.ps1          # Gegenpruefung mit Schematron und veraPDF
```

Ohne Skripte geht es genauso:

```powershell
dotnet build EInvoiceSender.sln -c Release
dotnet test  EInvoiceSender.sln -c Release
dotnet format EInvoiceSender.sln --verify-no-changes
```

## Installer

`Build-Installer.ps1` erzeugt ein MSI, das **pro Benutzer** und ohne
Administratorrechte installiert. Es legt einen Startmenueeintrag an, auf Wunsch
eine Desktopverknuepfung, unterstuetzt Upgrades, wehrt Downgrades mit einer
verstaendlichen Meldung ab und laesst Ihre Daten bei der Deinstallation
unangetastet. WiX erzeugt MSI-Dateien nur unter Windows.

## Einschraenkungen in Kuerze

- **Nicht jede PDF laesst sich verwenden.** Es gibt keine frei verwendbare
  .NET-Bibliothek, die beliebige PDFs nach PDF/A-3 wandelt. Die Anwendung
  wertet geeignete Dateien auf und lehnt ungeeignete mit einer Begruendung ab –
  vor allem PDFs ohne eingebettete Schriften und digital signierte PDFs.
- **Die eigene Regelpruefung ist kein Konformitaetsnachweis.** Die Freigabe
  erteilen die externen Referenzvalidatoren.
- **Keine Steuerberatung.** Geprueft wird das Format, nicht die inhaltliche
  oder steuerliche Richtigkeit.

Ausfuehrlich: [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md)

## Weitere Unterlagen

| Datei | Inhalt |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Aufbau der Projektmappe und der Ablauf |
| [`docs/BUILD.md`](docs/BUILD.md) | Bauen mit Visual Studio und PowerShell |
| [`docs/E-INVOICE-STANDARD.md`](docs/E-INVOICE-STANDARD.md) | Norm, Profil und verwendete Fassungen |
| [`docs/TESTING.md`](docs/TESTING.md) | Testebenen und Referenzvalidatoren |
| [`docs/KNOWN-LIMITATIONS.md`](docs/KNOWN-LIMITATIONS.md) | Bekannte Grenzen |
| [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md) | Fremdkomponenten und Lizenzen |

`docs/legacy/` enthaelt die ausfuehrlichen Unterlagen aus der Entstehungszeit.
Sie sind aufgehoben, aber nicht mehr massgeblich.

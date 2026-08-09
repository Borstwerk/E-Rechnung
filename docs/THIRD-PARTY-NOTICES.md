# Hinweise zu Fremdkomponenten

Alle unten genannten Pakete sind permissiv lizenziert und erlauben die
Weitergabe in einer geschlossenen Anwendung. Die Lizenztexte werden mit dem
Installer ausgeliefert (`installer/EInvoiceSender.Setup/Lizenzhinweise.rtf`).

Die ausfuehrliche Bewertung – Lizenz, Projektstatus, Einschraenkungen und
Auswahlgrund je Paket – steht in
[`docs/legacy/DEPENDENCIES.md`](legacy/DEPENDENCIES.md).

## In die Anwendung eingebundene Pakete

| Paket | Fassung | Lizenz | Wofuer |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT | `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT | Composition Root der Anwendung |
| Microsoft.Extensions.Logging(.Abstractions) | 10.0.10 | MIT | Protokollierung |
| PdfSharp | 6.2.4 | MIT | PDF lesen, aufwerten, Anhaenge einbetten |
| PDFtoImage | 5.3.0 | MIT | Vorschaubild der ersten Seite (PDFium) |
| MimeKit | 4.17.0 | MIT | `.eml`-Entwurf nach RFC 5322 |
| System.Security.Cryptography.ProtectedData | 10.0.10 | MIT | DPAPI-Schutz der IBAN unter Windows |

PDFium (ueber PDFtoImage) steht unter der BSD-3-Clause-Lizenz von Google.
SkiaSharp (ebenfalls ueber PDFtoImage) steht unter MIT.

## Nur beim Entwickeln und Testen

| Paket | Fassung | Lizenz |
|---|---|---|
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT |
| xunit.v3 | 3.2.2 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 |

## Externe Werkzeuge – getrennte Prozesse, nicht eingebunden

Diese Werkzeuge werden **nicht** mit ausgeliefert. Sie werden, sofern
vorhanden, als eigener Prozess aufgerufen und laufen vollstaendig oertlich; es
werden keine Rechnungsdaten uebertragen.

| Werkzeug | Fassung | Lizenz | Rolle |
|---|---|---|---|
| Mustangproject CLI | 2.24.0 | Apache-2.0 | fuehrt CEN-Schematron und veraPDF aus |
| veraPDF | 1.30.2 | GPLv3 **oder** MPLv2 | PDF/A-Pruefung, Flavour 3b |
| CEN-Schematron (EN 16931) | – | – | offizielles Regelwerk |

Da veraPDF und Mustang als **eigenstaendige Prozesse** aufgerufen und nicht
gelinkt werden, greift die GPL-Wirkung nicht auf diese Anwendung durch.
Mitgeliefert werden sie ohnehin nicht.

## Installer

| Werkzeug | Fassung | Lizenz |
|---|---|---|
| WiX Toolset | 5.0.2 | MS-RL (Microsoft Reciprocal License) |

Die Lizenz wurde an der Primaerquelle (`LICENSE.TXT` des Projekts) geprueft.
Es gibt keine Gebuehrenklausel fuer die Nutzung.

## Das ICC-Profil

Das sRGB-Ausgabeprofil in `Core/Pdf/SRgbIccProfile.cs` wird **programmatisch
erzeugt** und stammt nicht aus einer fremden Datei. Damit gibt es keine
Lizenzfrage und keine Binaerdatei unbekannter Herkunft im Repository. Der
Inhalt ist per SHA-256 festgenagelt und wird von einem Test geprueft.

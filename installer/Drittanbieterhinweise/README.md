# BorstWerk E-Rechnung – Hinweise zu Drittkomponenten

BorstWerk E-Rechnung enthält die folgenden Fremdkomponenten. Diese Übersicht
nennt bewusst keine Fassungsnummern: Die exakten Fassungen und ihre technische
Zuordnung werden beim Bau automatisiert gegen den tatsächlichen
Auslieferungsbestand geprüft.

| Komponente | Lizenz beziehungsweise Hinweis | Verwendung |
|---|---|---|
| CommunityToolkit.Mvvm | MIT | Oberfläche und ViewModels |
| Microsoft.Extensions.DependencyInjection und `.Abstractions` | MIT | Zusammenstellung der Anwendung |
| Microsoft.Extensions.Logging und `.Abstractions` | MIT | lokale Protokollierung |
| Microsoft.Extensions.Options und Microsoft.Extensions.Primitives | MIT | transitive Laufzeitbestandteile |
| PDFsharp | MIT | PDF-Verarbeitung und Einbettung |
| PdfPig | Apache-2.0 | lokale Texterkennung in PDF-Dateien |
| PDFtoImage | MIT | Vorschau und Rasterweg |
| bblanchon.PDFium.Win32 und PDFium | Paket- und Drittkomponentenlizenzen siehe `Lizenzen` | native PDF-Darstellung |
| SkiaSharp und SkiaSharp.NativeAssets.Win32 | MIT und eigene Third-Party-Notices | Bildverarbeitung für PDFtoImage |
| MimeKit | MIT | Erzeugung von E-Mail-Entwürfen |
| BouncyCastle.Cryptography | MIT | transitiver Kryptografiebestandteil von MimeKit |
| System.Security.Cryptography.ProtectedData | MIT | Windows-Datenschutz für die gespeicherte IBAN |
| Microsoft.NETCore.App und Microsoft.WindowsDesktop.App | Paketlizenzen, .NET Library License und Third-Party-Notices | self-contained .NET-Laufzeit |

Die vollständigen, an den verwendeten Paketfassungen beziehungsweise am
unveränderlichen PDFium-Binärrelease geprüften Lizenz- und Notice-Texte liegen
im Unterordner `Drittanbieterhinweise\Lizenzen` der Installation. Die technische
Zuordnung dieser Dateien wird im Projekt gepflegt und automatisiert geprüft.

## Selbst erzeugte Bestandteile

Das im Ausgabedokument eingebettete sRGB-ICC-Farbprofil wird von BorstWerk
E-Rechnung programmatisch erzeugt. Es stammt aus keiner fremden Datei.

## Nicht mitgelieferte, optionale Prüfwerkzeuge

Mustangproject CLI, veraPDF, CEN-Schematron und die dafür erforderliche
Java-Laufzeit sind nicht Bestandteil der Installation. Sie können getrennt
installiert und in den Einstellungen für eine zusätzliche örtliche Prüfung
ausgewählt werden.

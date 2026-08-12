# Hinweise zu Fremdkomponenten

Diese Datei ist die **fachlich-technische Hauptquelle** für die Zuordnung der
Fremdkomponenten von BorstWerk E-Rechnung. Sie trennt bewusst fünf Ebenen:

1. Die produktiven Projektdateien bestimmen die direkten Paketverweise.
2. `EInvoiceSender.deps.json` und der Publish-Inhalt belegen den tatsächlichen
   Auslieferungsbestand einschließlich transitiver und nativer Bestandteile.
3. Diese Datei dokumentiert Fassung, Rolle, Lizenz und geprüfte Primärquelle.
4. `installer/Drittanbieterhinweise/README.md` und
   `installer/EInvoiceSender.Setup/Lizenzhinweise.rtf` sind kürzere
   Anwenderdarstellungen.
5. Die vollständigen mitgelieferten Lizenz- und Notice-Texte liegen unter
   `installer/Drittanbieterhinweise/Lizenzen`.

Paketnamen und Fassungen in dieser Datei werden automatisiert gegen die
produktiven Projekte und den gebauten Publish geprüft. Eine Lizenzzuordnung
wird trotzdem nicht aus `deps.json` geraten: Dafür gilt jeweils die unten
genannte Primärquelle.

## Ausgelieferte NuGet-Laufzeitpakete

Die Spalte `deps.json` kennzeichnet Paketbibliotheken, die im aktuellen
self-contained `win-x64`-Publish als eigene Paketzeile erscheinen. Die Marker
um die Tabelle werden von der Artefaktprüfung gelesen und dürfen nicht entfernt
werden.

<!-- runtime-packages:start -->
| Paket-ID | Art | Fassung | Lizenz/Zuordnung | deps.json |
|---|---|---:|---|---|
| `CommunityToolkit.Mvvm` | direkt | 8.4.2 | MIT | ja |
| `Microsoft.Extensions.DependencyInjection` | direkt | 10.0.10 | MIT | ja |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | transitiv | 10.0.10 | MIT | ja |
| `Microsoft.Extensions.Logging` | direkt | 10.0.10 | MIT | ja |
| `Microsoft.Extensions.Logging.Abstractions` | direkt und transitiv | 10.0.10 | MIT | ja |
| `Microsoft.Extensions.Options` | transitiv | 10.0.10 | MIT | ja |
| `Microsoft.Extensions.Primitives` | transitiv | 10.0.10 | MIT | ja |
| `PdfSharp` | direkt | 6.2.4 | MIT | ja |
| `PdfPig` | direkt | 0.1.15 | Apache-2.0 | ja |
| `PDFtoImage` | direkt | 5.3.0 | MIT | ja |
| `MimeKit` | direkt | 4.17.0 | MIT | ja |
| `BouncyCastle.Cryptography` | transitiv über MimeKit | 2.6.2 | MIT | ja |
| `bblanchon.PDFium.Win32` | transitiv über PDFtoImage | 152.0.7961 | NuGet-Metadaten: Apache-2.0; Release-LICENSE: MIT | ja |
| `SkiaSharp` | transitiv über PDFtoImage | 4.150.1 | MIT | ja |
| `SkiaSharp.NativeAssets.Win32` | transitiv über PDFtoImage | 4.150.1 | MIT und eigene Third-Party-Notices | ja |
| `System.Security.Cryptography.ProtectedData` | direkt; im Publish vom Runtimepack bereitgestellt | 10.0.10 | MIT | nein |
<!-- runtime-packages:end -->

Die Assembly von `System.Security.Cryptography.ProtectedData` wird
ausgeliefert. Die self-contained Veröffentlichung ordnet sie aber dem
Runtimepack zu und führt deshalb keine eigene Paketzeile dafür in
`EInvoiceSender.deps.json`.

### Native PDF-Bestandteile

PDFtoImage bringt `pdfium.dll` aus `bblanchon.PDFium.Win32` sowie
`libSkiaSharp.dll` aus `SkiaSharp.NativeAssets.Win32` in den Publish.

Das für Version 152.0.7961 verwendete unveränderliche PDFium-Release
`152.0.7961.0` enthält neben seinem eigenen `LICENSE` einen Ordner mit
15 Lizenztexten für die tatsächlich einkompilierten Bestandteile, darunter
Abseil, FreeType, ICU, Little CMS, libjpeg-turbo, OpenJPEG, libpng, libtiff,
LLVM libc, simdutf und zlib. Diese Dateien werden unverändert mit ausgeliefert.
Die `pdfium.dll` aus diesem Release, dem NuGet-Paket und dem geprüften Publish
hat denselben SHA-256-Wert
`d3d9f4b7c9dabe3363f30779c5c3c715c47332749fa590e4b4a2b8b6780cb1c4`.

Die widersprüchlichen Bezeichnungen beim Distributionspaket werden nicht
glattgezogen: Das NuGet-Manifest bezeichnet `bblanchon.PDFium.Win32` als
Apache-2.0, während das zum exakt verwendeten Binärrelease gehörende
Haupt-`LICENSE` MIT nennt und für die enthaltene PDFium-Bibliothek sowie ihre
Drittkomponenten auf den mitgelieferten Lizenzordner verweist. Deshalb werden
sowohl Apache-2.0 als auch das vollständige Lizenzpaket des Releases beigelegt.

### Warum PdfPig neben PDFsharp

PDFsharp schreibt und verändert PDF-Dateien, stellt aber keine Schnittstelle
für lesbaren Seitentext bereit. PdfPig übernimmt ausschließlich die
Textextraktion für die lokale Datenerkennung. Die Vorabversion 0.1.15 bleibt
damit an einer kleinen, austauschbaren Stelle gekapselt.

## Self-contained .NET-Laufzeit

<!-- runtime-packs:start -->
| Runtimepack-ID | Fassung | im Publish | Primärtexte |
|---|---:|---|---|
| `runtimepack.Microsoft.NETCore.App.Runtime.win-x64` | 10.0.11 | ja | Paket-LICENSE und THIRD-PARTY-NOTICES |
| `runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64` | 10.0.11 | ja | Paket-LICENSE |
<!-- runtime-packs:end -->

Der aktuelle Windows-Publish ist self-contained und enthält beide Runtimepacks.
Die exakten Pakete deklarieren MIT und liefern eigene Lizenzdateien; das
NETCore-Runtimepack liefert zusätzlich `THIRD-PARTY-NOTICES.TXT`. Microsofts
übergeordnetes Lizenzmodell ordnet Windows-Produktdistributionen und
Runtimepacks außerdem der .NET Library License zu. Um keine dieser beiden
Primärangaben wegzuinterpretieren, werden sowohl die paketgenauen Texte als
auch die .NET Library License mit ausgeliefert.

Die gleichen .NET-Lizenz- und Notice-Texte decken die ausgelieferten
`Microsoft.Extensions.*`-Pakete und
`System.Security.Cryptography.ProtectedData` ab. Die NuGet-Pakete deklarieren
MIT; ihre paketbeiliegende `THIRD-PARTY-NOTICES.TXT` ist bytegleich mit der
beigelegten Runtime-Notice.

## Geprüfte Lizenz- und Notice-Primärquellen

| Komponente | geprüfte Primärquelle | mitgelieferte Datei |
|---|---|---|
| CommunityToolkit.Mvvm 8.4.2 | `License.md` und `ThirdPartyNotices.txt` aus dem NuGet-Paket | `CommunityToolkit.Mvvm-8.4.2-*` |
| Microsoft.Extensions.* 10.0.10 | NuGet-Lizenzmetadaten und paketbeiliegende `THIRD-PARTY-NOTICES.TXT` | .NET-Runtime-Lizenz/Notice |
| .NETCore Runtime win-x64 10.0.11 | `LICENSE.TXT` und `THIRD-PARTY-NOTICES.TXT` aus dem Runtimepack 10.0.11 (Repository-Commit `e2f47b0110ed922f21a1522da67279133ce28f32`) | `Microsoft.NETCore.App.Runtime.win-x64-10.0.11-*` |
| WindowsDesktop Runtime win-x64 10.0.11 | `LICENSE` aus dem Runtimepack 10.0.11 (Repository-Commit `e2f47b0110ed922f21a1522da67279133ce28f32`) | `Microsoft.WindowsDesktop.App.Runtime.win-x64-10.0.11-LICENSE.txt` |
| Windows-.NET-Distribution | Microsoft .NET Library License aus der installierten .NET-Distribution und der offiziellen Lizenzseite | `Microsoft-.NET-Library-License.txt` |
| BouncyCastle.Cryptography 2.6.2 | `LICENSE.md` aus dem NuGet-Paket | `BouncyCastle.Cryptography-2.6.2-LICENSE.md` |
| MimeKit 4.17.0 | `LICENSE` am Tag `4.17.0` | `MimeKit-4.17.0-LICENSE.txt` |
| PDFsharp 6.2.4 | `LICENSE` am Tag `v6.2.4` | `PDFsharp-6.2.4-LICENSE.txt` |
| PdfPig 0.1.15 | SPDX-Angabe Apache-2.0 im NuGet-Paket; kein separates NOTICE im Paket | `Apache-2.0.txt` |
| PDFtoImage 5.3.0 | `LICENSE` am Tag `v5.3.0` | `PDFtoImage-5.3.0-LICENSE.txt` |
| bblanchon/PDFium 152.0.7961 | NuGet-Manifest sowie `LICENSE` und kompletter `licenses/`-Ordner des unveränderlichen Releases 152.0.7961.0 | `PDFium-152.0.7961/**` und `Apache-2.0.txt` |
| SkiaSharp 4.150.1 | `LICENSE.txt` und `THIRD-PARTY-NOTICES.txt` aus `SkiaSharp.NativeAssets.Win32` | `SkiaSharp-4.150.1-*` |

## Nur beim Entwickeln und Testen

Diese Pakete werden nicht mit BorstWerk E-Rechnung ausgeliefert.

| Paket | Fassung | Lizenz |
|---|---:|---|
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT |
| xunit.v3 | 3.2.2 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 |

Das manuell ausgeführte Icon-Werkzeug unter `build/icon` verwendet SkiaSharp
und SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.0. Es gehört nicht zum
Anwendungsbuild und wird nicht ausgeliefert.

## Externe Werkzeuge – getrennte Prozesse, nicht eingebunden

Diese Werkzeuge werden nicht mit der Anwendung ausgeliefert. Sie werden, sofern
vorhanden, als eigene Prozesse zur Gegenprüfung aufgerufen.

| Werkzeug | Fassung | Lizenz | Rolle |
|---|---:|---|---|
| Mustangproject CLI | 2.24.0 | Apache-2.0 | führt CEN-Schematron und veraPDF aus |
| veraPDF | 1.30.2 | GPLv3 oder MPLv2 | PDF/A-Prüfung, Flavour 3b |
| CEN-Schematron (EN 16931) | über Mustang | jeweilige Primärbedingungen | offizielles Regelwerk |

## Installer-Buildwerkzeug

WiX Toolset 5.0.2 und `WixToolset.UI.wixext` stehen unter der Microsoft
Reciprocal License. Sie erzeugen das MSI, werden aber nicht als Bestandteil der
Anwendung installiert.

## Das ICC-Profil

Das sRGB-Ausgabeprofil in `Core/Pdf/SRgbIccProfile.cs` wird programmatisch
erzeugt und stammt nicht aus einer fremden Datei. Sein Inhalt ist per SHA-256
festgelegt und wird von einem Test geprüft.

# Hinweise zu Drittkomponenten

Diese Datei wird mit der Anwendung ausgeliefert. Sie listet alle
Fremdbibliotheken auf, die im ausgelieferten Programm enthalten sind, mit ihrer
Lizenz. Die vollständige Bewertung steht in `docs/DEPENDENCIES.md`.

| Komponente | Version | Lizenz | Herausgeber |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT | Microsoft / .NET Foundation |
| Microsoft.Extensions.Hosting u. a. | 10.0.10 | MIT | Microsoft |
| Serilog.Extensions.Hosting | 10.0.0 | Apache-2.0 | Serilog Contributors |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | Serilog Contributors |
| Serilog.Formatting.Compact | 3.0.0 | Apache-2.0 | Serilog Contributors |
| PDFsharp | 6.2.4 | MIT | empira Software GmbH |
| PDFtoImage | 5.3.0 | MIT | sungaila |
| PDFium (in PDFtoImage enthalten) | – | BSD-3-Clause | The Chromium Authors |
| SkiaSharp (über PDFtoImage) | – | MIT | Microsoft / Xamarin |
| MimeKit | 4.17.0 | MIT | Jeffrey Stedfast |
| System.Security.Cryptography.ProtectedData | 10.0.10 | MIT | Microsoft |
| .NET-Laufzeit (self-contained enthalten) | 10.0 | MIT | Microsoft |

## Selbst erzeugte Bestandteile

Das im Ausgabedokument eingebettete **sRGB-ICC-Farbprofil** wird von dieser
Anwendung programmatisch erzeugt (siehe `docs/STANDARDS.md`, Abschnitt 7). Es
stammt aus keiner fremden Quelle und unterliegt keiner Fremdlizenz.

## Nicht mitgelieferte, optionale Werkzeuge

Die zusätzliche Prüfung der erzeugten Datei mit den offiziellen
Referenzwerkzeugen ist optional und **nicht Bestandteil der Installation**:

| Werkzeug | Lizenz | Hinweis |
|---|---|---|
| Mustangproject CLI | Apache-2.0 | benötigt eine Java-Laufzeit ab Version 11 |
| veraPDF | MPL-2.0 / GPLv3 (dual) | in Mustangproject enthalten |

Wer sie einsetzen möchte, installiert sie getrennt und trägt den Pfad in den
Einstellungen ein. Ohne sie prüft die Anwendung nur mit ihren eingebauten
Mitteln und weist das im Prüfbericht ausdrücklich aus.

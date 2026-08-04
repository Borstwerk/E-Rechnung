# DEPENDENCIES.md – Geprüfte Abhängigkeits- und Lizenzmatrix

Stand: 2026-08-04. Jede Abhängigkeit im Produktivpfad ist hier bewertet. Neue
Abhängigkeiten nur nach den Regeln in `AGENTS.md`, Abschnitt 6.

Versionen sind in `Directory.Packages.props` zentral gepinnt (Central Package
Management). Zur Laufzeit wird **nichts** nachgeladen.

---

## 1. Übernommene NuGet-Pakete

### CommunityToolkit.Mvvm 8.4.2

| | |
|---|---|
| Lizenz | MIT |
| Herausgeber | Microsoft / .NET Foundation |
| Projektstatus | aktiv gepflegt |
| Rolle | MVVM-Grundlagen (`ObservableObject`, `RelayCommand`, Quellgeneratoren) |
| Einschränkungen | keine für diesen Einsatz |
| Wartungsrisiko | sehr niedrig |
| Auswahlgrund | in der Aufgabenstellung vorgegeben; De-facto-Standard für MVVM in WPF |

### Microsoft.Extensions.Hosting / .Logging.Abstractions / .Options / .DependencyInjection.Abstractions 10.0.10

| | |
|---|---|
| Lizenz | MIT |
| Herausgeber | Microsoft |
| Projektstatus | Teil von .NET 10 LTS |
| Rolle | Dependency Injection, Konfiguration, Lebenszyklus, Protokollierungsabstraktion |
| Einschränkungen | keine |
| Wartungsrisiko | sehr niedrig (LTS-Support bis 2028) |
| Auswahlgrund | in der Aufgabenstellung vorgegeben |

### Serilog.Extensions.Hosting 10.0.0, Serilog.Sinks.File 7.0.0, Serilog.Formatting.Compact 3.0.0

| | |
|---|---|
| Lizenz | Apache-2.0 |
| Projektstatus | aktiv gepflegt, sehr verbreitet |
| Rolle | strukturierte lokale Protokollierung als JSON-Zeilen, tagesweise rollierend |
| Einschränkungen | schreibt nur lokal – ausdrücklich gewünscht |
| Wartungsrisiko | niedrig |
| Auswahlgrund | erfüllt „strukturierte lokale Protokollierung" hinter `Microsoft.Extensions.Logging`; austauschbar, da die Anwendung nur `ILogger<T>` kennt |

### PdfSharp 6.2.4

| | |
|---|---|
| Lizenz | MIT |
| Herausgeber | empira Software GmbH |
| Letzte Veröffentlichung | 2026-01-06 |
| Projektstatus | aktiv (7.0.0-preview verfügbar) |
| Unterstützte Formate | PDF lesen/schreiben, niedrige Objektebene zugänglich |
| Rolle | PDF-Analyse, Anhänge einbetten, XMP und OutputIntent setzen, atomare Ausgabe |
| **Bekannte Einschränkungen** | **Kein PDF/A-Support als Feature.** Der Maintainer stellt ausdrücklich klar, dass PDF/A-3 nicht unterstützt wird. Alles PDF/A-Spezifische (XMP, OutputIntent, `/AF`, Konformitätsprüfung) ist Eigenimplementierung dieses Projekts. Kann keine Schriften nachträglich einbetten und keine Farbräume normalisieren. |
| Wartungsrisiko | mittel – kleines Team, aber langjährig stabil |
| Auswahlgrund | einzige permissiv lizenzierte .NET-Bibliothek mit ausreichend tiefem Zugriff auf die PDF-Objektstruktur. Die Einschränkung ist durch ADR-0003 (Abbruch statt Konvertierung) bewusst eingeplant. |

### PDFtoImage 5.3.0

| | |
|---|---|
| Lizenz | MIT (bündelt PDFium, BSD-3-Clause) |
| Letzte Veröffentlichung | 2026-07-28 |
| Projektstatus | aktiv gepflegt |
| Rolle | Seitenrendering für die PDF-Vorschau in der WPF-Oberfläche |
| **Bekannte Einschränkungen** | **nicht threadsicher** – das Rendering läuft ausschließlich serialisiert hinter `PdfPreviewRenderer`. Zieht SkiaSharp als native Abhängigkeit nach (vergrößert das Publish-Ergebnis). |
| Wartungsrisiko | niedrig |
| Auswahlgrund | nur für die Anzeige, nicht im Erzeugungspfad. Alternativen (`Docnet.Core`, `PdfiumViewer`) sind seit 2023 bzw. 2017 unbetreut. |

### MimeKit 4.17.0

| | |
|---|---|
| Lizenz | MIT |
| Letzte Veröffentlichung | 2026-05 |
| Projektstatus | sehr aktiv, Referenzimplementierung für MIME in .NET |
| Rolle | Erzeugung der `.eml`-Entwurfsdatei mit Anhang und korrekter Umlautkodierung |
| Einschränkungen | keine für diesen Einsatz; die Versandfunktion (MailKit) wird bewusst **nicht** eingebunden |
| Wartungsrisiko | sehr niedrig |
| Auswahlgrund | `System.Net.Mail.MailMessage` bietet keine unterstützte Speicherfunktion; der Umweg über `SpecifiedPickupDirectory` erlaubt weder Dateinamen- noch Headerkontrolle (`X-Unsent`) |

### System.Security.Cryptography.ProtectedData 10.0.10

| | |
|---|---|
| Lizenz | MIT |
| Rolle | DPAPI-Schutz sensibler lokaler Einstellungen (Benutzergeltungsbereich) |
| Einschränkungen | **nur unter Windows funktionsfähig.** Aufrufe sind mit `OperatingSystem.IsWindows()` abgesichert; auf anderen Plattformen wird nichts gespeichert und der Zustand offen gemeldet. |
| Wartungsrisiko | sehr niedrig |
| Auswahlgrund | in der Aufgabenstellung als Schutzmechanismus vorgesehen |

---

## 2. Testabhängigkeiten

| Paket | Version | Lizenz | Bemerkung |
|---|---|---|---|
| xunit.v3 | 3.2.2 | Apache-2.0 | Testframework |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | VSTest-Adapter |
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT | Testinfrastruktur |
| Microsoft.Extensions.Logging | 10.0.10 | MIT | nur in Integrationstests |

---

## 3. Optionale externe Werkzeuge (getrennte Prozesse, nicht eingebunden)

Diese Werkzeuge laufen als eigenständige Prozesse hinter einem Adapter. Sie sind
**nicht** in die Anwendung hineingelinkt, ihre Lizenzen wirken damit nicht auf
den eigenen Code.

### Mustangproject CLI 2.24.0

| | |
|---|---|
| Lizenz | Apache-2.0 |
| Bezug | Maven Central, `org/mustangproject/Mustang-CLI/2.24.0` (Version dort verifiziert) |
| Rolle | Gegenprüfung: CEN-Schematron für EN 16931 und PDF/A-Validierung (veraPDF 1.30.2 ist enthalten) |
| Voraussetzung | Java-Laufzeit 11 oder neuer |
| Einschränkungen | erwartet für die PDF-Prüfung PDF/A-Eingaben; konvertiert **nicht** nach PDF/A |
| Wartungsrisiko | niedrig, sehr aktives Projekt |
| Auswahlgrund | einziges Apache-lizenziertes Werkzeug, das Schematron **und** PDF/A-Validierung offline in einem Aufruf abdeckt |
| Status im Produkt | in der CI verpflichtend, in der Anwendung optional (siehe ADR-0004) |

### veraPDF 1.30.2

| | |
|---|---|
| Lizenz | MPL-2.0 oder GPLv3 (Dual) – genutzt wird der MPL-2.0-Zweig |
| Rolle | Referenzvalidator für PDF/A |
| Bezug | über Mustang-CLI enthalten; alternativ Maven Central `org.verapdf` |
| Einschränkungen | Java erforderlich |
| Auswahlgrund | Referenzimplementierung, arbeitet vollständig offline |

### KoSIT-Validator 1.6.2

| | |
|---|---|
| Lizenz | Apache-2.0 |
| Rolle | ausschließlich für die spätere XRechnung-Ausgabe relevant |
| Status | **nicht Teil des MVP**, in der Abhängigkeitsmatrix nur vorgemerkt |

---

## 4. Ausdrücklich abgelehnte Abhängigkeiten

| Kandidat | Ablehnungsgrund |
|---|---|
| **iText7 / itext7.pdfa** | AGPL-3.0 oder kommerziell. Copyleft-Risiko für ein verteiltes Desktopprodukt. |
| **Ghostscript** | AGPL-3.0 oder kommerziell. Gleiches Risiko; wäre technisch der einfachste Weg zu PDF/A. |
| **QuestPDF** | Lizenz mit Umsatzschwelle; erzeugt zudem nur neue PDFs, kann bestehende nicht umwandeln. |
| **FactoorSharp.FacturX** | proprietär/kommerziell. |
| **ZUGFeRD-csharp 18.0.0** | Apache-2.0 und technisch brauchbar, aber vom Autor auf „maintenance only" gesetzt; siehe ADR-0002. |
| **Securibox.FacturX** | MIT, aber setzt `/AFRelationship = /Data` statt `/Alternative` – nicht spezifikationstreu. |
| **FacturXDotNet** | seit über einem Jahr Alpha, keine Veröffentlichung seit 2025-04. |
| **Docnet.Core / PdfiumViewer** | unbetreut (2023 bzw. 2017). |
| **Aspose / Syncfusion / PDFlib / TX Text Control** | kommerziell, laufende Kosten. |
| **Jede Cloud-/Online-API zur Konvertierung oder Validierung** | verstößt gegen die Datenschutzvorgabe: keine Übertragung von Rechnungsdaten an externe Server. |

---

## 5. Nicht verifizierte Angaben

Für Transparenz offen ausgewiesen:

- Veröffentlichungsdaten von ZUGFeRD 2.4 und 2.5 stammen aus Sekundärquellen;
  `ferd-net.de` und `fnfe-mpe.org` waren aus dieser Umgebung nicht abrufbar
  (HTTP 403).
- Die Aussage, dass die Mustang-CLI-JAR selbstenthaltend („shaded") ist, ist
  über die Dateigröße plausibel, aber nicht geprüft. Wird beim ersten
  tatsächlichen Aufruf in der CI verifiziert.
- Die Java-Mindestversion für Mustang 2.24.x ist mit „11 oder neuer" angegeben,
  aus der Projektkonfiguration abgeleitet, nicht aus der Dokumentation bestätigt.
- Das Verhalten des „neuen Outlook" beim Öffnen von `.eml`-Dateien mit Anhang ist
  aus dieser Umgebung nicht prüfbar (siehe ADR-0005 und `docs/STATUS.md`).

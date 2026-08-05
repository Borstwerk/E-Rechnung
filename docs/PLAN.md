# PLAN.md – Meilensteine und Arbeitspakete

Status je Paket: `offen` · `in Arbeit` · `fertig` · `zurückgestellt`

Ein Meilenstein gilt nur als fertig, wenn Build **und** alle verpflichtenden
Tests grün sind (`AGENTS.md`, Abschnitt 8).

---

## M0 – Recherche und Architektur · **fertig**

| # | Paket | Status |
|---|---|---|
| 0.1 | Umgebung prüfen, .NET-10-SDK bereitstellen | fertig |
| 0.2 | Standards recherchieren und selbst verifizieren | fertig |
| 0.3 | Bibliotheksvergleich und Lizenzmatrix | fertig |
| 0.4 | PDF/A-, XML-, Validator-, Mail-, Installerstrategie | fertig |
| 0.5 | Architektur festlegen, ADRs schreiben | fertig |
| 0.6 | Solution-Grundgerüst, Build grün | fertig |

**Akzeptanz:** `docs/STANDARDS.md`, `docs/DEPENDENCIES.md`, `docs/DECISIONS.md`,
`docs/ARCHITECTURE.md` vorhanden; `dotnet build` grün.
**Validierung:** `dotnet build EInvoiceSender.slnx -c Release`

---

## M1 – Solution und Domain

| # | Paket | Abhängig von | Status |
|---|---|---|---|
| 1.1 | Werttypen: `Money`, `Iban`, `CountryCode`, `CurrencyCode`, `UnitCode`, `VatCategory` | – | offen |
| 1.2 | Rechnungsmodell: Dokument, Verkäufer, Käufer, Positionen, Zahlung | 1.1 | offen |
| 1.3 | Berechnungskern: Positionssummen, Steueraufschlüsselung, Gesamtsummen, Rundung | 1.1, 1.2 | offen |
| 1.4 | IBAN-Prüfziffer (ISO 7064 Mod-97-10) | 1.1 | offen |
| 1.5 | Dateinamenbereinigung `SafeFileName` | – | offen |
| 1.6 | Unit-Tests zu 1.1–1.5 | 1.1–1.5 | offen |
| 1.7 | CI-Pipeline (Restore, Build, Format, Analyse, Tests) | 1.6 | offen |

**Akzeptanz:** Alle EN-16931-Rechenregeln aus `docs/STANDARDS.md` §3 sind
implementiert und je mit mindestens einem positiven und einem negativen Test
belegt. Mehrere Steuersätze, Rabatte, Zuschläge, Rundung und Anzahlung sind
abgedeckt.
**Validierung:** `dotnet test tests/EInvoiceSender.Domain.Tests -c Release`

---

## M2 – PDF-Eingang und Analyse · **fertig bis auf die Vorschau**

| # | Paket | Status |
|---|---|---|
| 2.1 | `IPdfAnalyzer` + PdfSharp-Adapter: Signatur, Version, Verschlüsselung, Seiten | offen |
| 2.2 | Erkennung eingebetteter Rechnungs-XML (ZUGFeRD/Factur-X) inkl. Profilauslesung | offen |
| 2.3 | Prüfung der PDF/A-Aufwertbarkeit (Schrifteinbettung, Verschlüsselung, Aktionen) | offen |
| 2.4 | Größen- und Typprüfung, verständliche deutsche Fehlermeldungen | offen |
| 2.5 | Tests inkl. beschädigter PDF, verschlüsselter PDF, bereits eingebetteter XML | offen |

**Akzeptanz:** Eine bereits hybride Rechnung wird erkannt und gemeldet; eine
beschädigte Datei führt zu einer verständlichen Meldung statt zu einer Ausnahme.
**Validierung:** `dotnet test tests/EInvoiceSender.IntegrationTests -c Release`

---

## M3 – Datenerfassung (WPF)

| # | Paket | Status |
|---|---|---|
| 3.1 | Shell mit fünf Schritten, Navigation, Tastaturbedienung | offen |
| 3.2 | ViewModels: Dokument, Verkäufer, Käufer, Positionen, Zahlung | offen |
| 3.3 | Live-Summenberechnung aus dem Domänenkern | offen |
| 3.4 | Vorlagen (eigene Firmendaten, Bank, Zahlungsziel, Mailtexte) mit DPAPI-Schutz | offen |
| 3.5 | Kontrollansicht mit Pflichtbestätigung | offen |
| 3.6 | Drag-and-drop, Dateiauswahl, PDF-Vorschau | offen |
| 3.7 | ViewModel-Tests | offen |

**Akzeptanz:** Ohne die ausdrückliche Bestätigung der inhaltlichen
Übereinstimmung ist die Erzeugung technisch gesperrt.

---

## M4 – XML-Erzeugung · **fertig**

| # | Paket | Status |
|---|---|---|
| 4.1 | `CiiConstants`, sichere XML-Basis (`SecureXml`) | offen |
| 4.2 | `CiiInvoiceWriter` für Profil EN 16931 | offen |
| 4.3 | `CiiInvoiceReader` (Rücklesen und Profilerkennung) | offen |
| 4.4 | Golden-Master-Tests für die zwölf Pflichtfälle | offen |
| 4.5 | Gegenprüfung mit Mustang-CLI in der CI | offen |

**Akzeptanz:** Alle Golden Master sind stabil, und die Mustang-Schematron-Prüfung
meldet für die gültigen Fälle keinen Fehler.
**Validierung:** `dotnet test tests/EInvoiceSender.Formats.Tests -c Release`,
`./build/validate-golden-masters.sh`

---

## M5 – PDF/A-3 und Einbettung · **fertig**

| # | Paket | Status |
|---|---|---|
| 5.1 | `SRgbIccProfile` (ICC v2 programmatisch erzeugt) | offen |
| 5.2 | XMP-Erzeugung inkl. PDF/A-Extension-Schema und `fx`-Feldern | offen |
| 5.3 | `PdfAInvoiceComposer`: OutputIntent, Anhang, `/AF`, Dokumentinfo | offen |
| 5.4 | `PdfAStructureValidator` (eigene Strukturprüfung) | offen |
| 5.5 | Rücklesen: erzeugte Datei öffnen, XML extrahieren, erneut validieren | offen |
| 5.6 | veraPDF-Gegenprüfung in der CI | offen |

**Akzeptanz:** Die erzeugte Datei besteht die veraPDF-Prüfung mit
`--flavour 3b`; die extrahierte XML ist byte-identisch mit der erzeugten.

---

## M6 – Gesamtablauf und Berichte · **fertig**

| # | Paket | Status |
|---|---|---|
| 6.1 | `CreateEInvoiceUseCase` mit Schrittfortschritt und Abbruch | offen |
| 6.2 | Validierungsbericht: maschinenlesbar (JSON) und menschenlesbar (Text) | offen |
| 6.3 | SHA-256, Zeitstempel, Standard-, Profil- und Validatorversionen | offen |
| 6.4 | Atomare Ausgabe, Überschreibschutz, Dateinamensvorschlag | offen |
| 6.5 | Übersetzung technischer Regelverstöße in deutsche Sätze | offen |
| 6.6 | Ende-zu-Ende-Integrationstest | offen |

---

## M7 – E-Mail-Entwurf

| # | Paket | Status |
|---|---|---|
| 7.1 | `IEmailDraftService` + `EmlDraftService` (MimeKit) | offen |
| 7.2 | `mailto:`-Fallback ohne Anhang | offen |
| 7.3 | Entwurfsvorschau in der Oberfläche, Versand nur durch den Benutzer | offen |
| 7.4 | „Ausgabeordner öffnen" | offen |
| 7.5 | Tests: Header, Kodierung, Anhang, Maskierung in Logs | offen |

---

## M8 – Installer und Release

| # | Paket | Status |
|---|---|---|
| 8.1 | Self-contained `win-x64`-Veröffentlichung | offen |
| 8.2 | Installer (Entscheidung WiX/Inno bei Beginn des Meilensteins, siehe unten) | offen |
| 8.3 | Startmenü, optionale Desktopverknüpfung, saubere Deinstallation | offen |
| 8.4 | Drittanbieter-Lizenzhinweise mitliefern | offen |
| 8.5 | Portable ZIP-Ausgabe | offen |
| 8.6 | Prüfsummen aller Artefakte, Vorbereitung für Codesignierung | offen |

**Offene Entscheidung zu 8.2:** WiX ab v6 verlangt von Organisationen mit mehr
als 10.000 USD Jahresumsatz eine Sponsoring-Gebühr (Open Source Maintenance Fee);
Inno Setup fordert von gewerblichen Nutzern den Kauf einer Lizenz. Beide Angaben
stammen aus Sekundärquellen und werden zu Beginn von M8 an der Primärquelle
geprüft. Vorläufige Vorgabe: **WiX v3.14 oder v5** (ohne Gebührenpflicht), sonst
Inno Setup. Ergebnis wird als ADR-0009 festgehalten.

---

## M9 – Endabnahme

| # | Paket | Status |
|---|---|---|
| 9.1 | Installation auf sauberem Windows-System | offen |
| 9.2 | Vollständiger Smoke-Test nach Definition of Done | offen |
| 9.3 | Alle Tests grün, Sicherheitsreview ohne kritische Befunde | offen |
| 9.4 | Bekannte Einschränkungen dokumentiert, Releasebericht | offen |

**Hinweis:** 9.1 und der UI-Teil von 9.2 erfordern ein echtes Windows-System und
können in dieser Umgebung nicht abgeschlossen werden. Der Zustand wird im
Releasebericht offen ausgewiesen statt behauptet.

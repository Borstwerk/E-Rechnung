# STANDARDS.md – Verwendete Normen, Versionen und exakte Werte

Stand: 2026-08-04. Alle hier festgelegten Werte sind **gepinnt**. Änderungen nur
durch den Hauptagenten und nur mit Eintrag in `docs/DECISIONS.md`.

Jede Angabe trägt eine Vertrauensangabe:

- **[V]** = im Quelltext einer Primär- oder Referenzimplementierung bzw. in einem
  offiziellen Repository selbst nachgelesen.
- **[S]** = aus Sekundärquellen (Fachartikel, Suchtreffer). Die offiziellen
  Seiten `ferd-net.de`, `fnfe-mpe.org` und `pdflib.com` haben auf Abrufe aus
  dieser Umgebung mit HTTP 403 geantwortet, eine Primärprüfung war dort nicht
  möglich.

---

## 1. Zielformat

| Eigenschaft | Festlegung |
|---|---|
| Format | ZUGFeRD 2.3.x / Factur-X 1.07.x (Hybridrechnung) |
| Profil | **EN 16931 (COMFORT)** |
| Syntax | UN/CEFACT Cross Industry Invoice (CII), D16B |
| Trägerformat | PDF/A-3, Konformitätsstufe **B** |
| Anhangname | `factur-x.xml` |
| Zeichensatz | UTF-8 |

**Begründung der Versionswahl:** Der Profil-URN, der Dateiname des Anhangs und
das XMP-Extension-Schema sind über ZUGFeRD 2.1 bis 2.5 hinweg unverändert. D16B
wird von D22B-Verarbeitern (ZUGFeRD 2.4+) akzeptiert. Die Neuerungen in 2.4/2.5
betreffen im Wesentlichen das Profil EXTENDED und französische CTC-Anforderungen
und sind für diesen Anwendungsfall ohne Bedeutung. Eine erzeugte Datei ist damit
für jeden Empfänger ab ZUGFeRD 2.0.1 lesbar.

### Versionslage (Kontext)

| Version | Veröffentlichung | Bemerkung |
|---|---|---|
| ZUGFeRD 2.3.3 / Factur-X 1.07.3 | 2025-05-07 **[S]** | Erzeugungsziel dieser Anwendung |
| ZUGFeRD 2.4 / Factur-X 1.08 | 2025-12-04, anzuwenden ab 2026-01-15 **[S]** | Basis CII D22B, abwärtskompatibel |
| ZUGFeRD 2.5 / Factur-X 1.09 | 2026-06-10 (Teil 1) **[S]** | Zweite Teilveröffentlichung angekündigt |

Ältere Fassungen ab ZUGFeRD 2.0.1 bleiben umsatzsteuerlich gültig **[S]**.

---

## 2. Exakte Zeichenketten

Diese Werte sind im Code als Konstanten hinterlegt
(`EInvoiceSender.Formats/CiiConstants.cs`) und dürfen nirgends dupliziert werden.

### 2.1 Profilkennung

`rsm:ExchangedDocumentContext / ram:GuidelineSpecifiedDocumentContextParameter / ram:ID`

```
urn:cen.eu:en16931:2017
```

**[V]** – bestätigt aus zwei unabhängigen Referenzimplementierungen:
`stephanstapel/ZUGFeRD-csharp` (`ZUGFeRD/Profile.cs`, Zeilen 109 und 156) und
`ZUGFeRD/mustangproject` (`Profiles.java`).

Weitere Profil-URNs (nur zur **Erkennung** eingehender Dateien, nicht zur
Erzeugung) **[V]**:

| Profil | URN |
|---|---|
| MINIMUM | `urn:factur-x.eu:1p0:minimum` |
| BASIC WL | `urn:factur-x.eu:1p0:basicwl` |
| BASIC | `urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic` |
| EN 16931 | `urn:cen.eu:en16931:2017` |
| EXTENDED | `urn:cen.eu:en16931:2017#conformant#urn:factur-x.eu:1p0:extended` |
| XRECHNUNG 3.0 | `urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0` |
| ZUGFeRD-2.0-Altform BASIC | `urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2p0:basic` |
| ZUGFeRD-2.0-Altform EXTENDED | `urn:cen.eu:en16931:2017#conformant#urn:zugferd.de:2p0:extended` |

### 2.2 XML-Namensräume (CII) **[V]**

Root-Element: `rsm:CrossIndustryInvoice`

| Präfix | Namensraum |
|---|---|
| `rsm` | `urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100` |
| `ram` | `urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100` |
| `udt` | `urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100` |
| `qdt` | `urn:un:unece:uncefact:data:standard:QualifiedDataType:100` |

### 2.3 PDF-Einbettung

| Eigenschaft | Wert |
|---|---|
| Dateiname des Anhangs | `factur-x.xml` |
| MIME-Typ (`/Subtype`) | `text/xml` |
| `/AFRelationship` | `/Alternative` |
| Katalog-Eintrag | `/AF` (Array mit dem Filespec) |
| Namensbaum | `/Names /EmbeddedFiles` |
| `/Desc` | `Factur-X/ZUGFeRD Rechnung` |

`/Alternative` ist der für ZUGFeRD/Factur-X vorgesehene Wert, wenn die
strukturierten Daten eine alternative Darstellung derselben Rechnung sind – das
ist hier immer der Fall, da das PDF vom Benutzer stammt **[S]**.

### 2.4 XMP-Erweiterungsschema **[V]**

Namensraum-URI:

```
urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#
```

Präfix: `fx`. Erforderliche Felder für dieses Profil:

```xml
<fx:DocumentType>INVOICE</fx:DocumentType>
<fx:DocumentFileName>factur-x.xml</fx:DocumentFileName>
<fx:Version>1.0</fx:Version>
<fx:ConformanceLevel>EN 16931</fx:ConformanceLevel>
```

Das Erweiterungsschema muss zusätzlich im PDF/A-Extension-Schema-Container
(`http://www.aiim.org/pdfa/ns/extension/`) deklariert werden, sonst ist das
Dokument nicht PDF/A-konform.

Bestätigt an drei unabhängigen Implementierungen: TCPDF (`example_068.php`),
MuPDF (`source/pdf/pdf-zugferd.c`) und WeasyPrint.

Zusätzlich erforderlich (PDF/A-3b):

```xml
<pdfaid:part>3</pdfaid:part>
<pdfaid:conformance>B</pdfaid:conformance>
```

`fx:Version` = `1.0` entspricht der durchgängigen Implementierungspraxis; ein
Beleg aus der FeRD-Spezifikation selbst konnte nicht abgerufen werden **[S]**.

---

## 3. EN-16931-Rechenregeln

Verbindlich implementiert in `EInvoiceSender.Domain/Calculation` und geprüft in
`EInvoiceSender.Validation`. Quelle **[V]**:
`ConnectingEurope/eInvoicing-EN16931`, `cii/schematron/abstract/EN16931-CII-model.sch`.

| Regel | Bedeutung |
|---|---|
| BR-CO-10 | BT-106 = Σ BT-131 (Summe der Positionsnettobeträge) |
| BR-CO-13 | BT-109 = Σ BT-131 − BT-107 (Nachlässe) + BT-108 (Zuschläge) |
| BR-CO-14 | BT-110 = Σ BT-117 (Gesamtsteuer = Summe je Steuersatz) |
| BR-CO-15 | BT-112 = BT-109 + BT-110 (Brutto) |
| BR-CO-16 | BT-115 = BT-112 − BT-113 (bereits gezahlt) + BT-114 (Rundung) |
| BR-CO-17 | BT-117 = round(BT-116 × BT-119 / 100, 2) |
| BR-CO-18 | Mindestens eine Steueraufschlüsselung (BG-23) |
| BR-CO-25 | Bei BT-115 > 0: BT-9 (Fälligkeit) **oder** BT-20 (Zahlungsbedingungen) |
| BR-45 | Jede BG-23 benötigt BT-116 |
| BR-S-08 | Je Steuersatz: BT-116 = Σ BT-131 + Σ BT-99 − Σ BT-92 |
| BR-S-09 | BT-117 = BT-116 × BT-119 |
| BR-DEC-09…17 | Höchstens 2 Nachkommastellen für BT-106/107/108/109/110/111/112/113/114 |

**Rundung:** Die CII-Implementierung des Schematron rundet als
`round(x * 10 * 10) div 100`, also **kaufmännisch auf zwei Nachkommastellen**.
Die Anwendung verwendet `Math.Round(value, 2, MidpointRounding.AwayFromZero)`
und rechnet durchgehend mit `decimal`.

### Pflichtangaben Profil EN 16931 **[V]**

BT-24 (Profilkennung), BT-1 (Rechnungsnummer), BT-2 (Rechnungsdatum),
BT-3 (Typcode), BT-5 (Währung), BT-27 (Verkäufername), BT-44 (Käufername),
BG-5 (Verkäuferanschrift), BT-40 (Verkäuferland), mindestens eine BG-25
(Rechnungsposition), BT-106/109/112/115 (Summen), mindestens eine BG-23
(Steueraufschlüsselung), BT-9 **oder** BT-20.

BT-10 (Buyer reference / Leitweg-ID) ist in EN 16931 **optional**. Verpflichtend
wird sie erst durch die deutsche CIUS XRechnung (BR-DE-15) im B2G-Bereich. Die
Anwendung erfasst das Feld, erzwingt es aber im Profil EN 16931 nicht.

---

## 4. Rechtlicher Rahmen (Deutschland) **[S]**

- § 14 UStG i. d. F. des Wachstumschancengesetzes.
- Seit 2025-01-01: Empfangspflicht für inländische Unternehmer.
- Ab 2027-01-01: Ausstellungspflicht bei Vorjahresumsatz über 800.000 EUR.
- Ab 2028-01-01: Ausstellungspflicht ohne Umsatzgrenze.
- Zulässig sind EN-16931-konforme Formate. ZUGFeRD ab 2.0.1 in den Profilen
  BASIC, EN 16931, EXTENDED und XRECHNUNG.
- **Nicht** als Rechnung zulässig: Profile MINIMUM und BASIC WL.
- Bei Hybridformaten ist der strukturierte XML-Teil führend (BMF-Schreiben
  vom 2025-10-15).

Diese Anwendung erzeugt ausschließlich das Profil EN 16931 und erfüllt damit die
Formatanforderung. **Sie leistet keine Steuerberatung** – die inhaltliche
Richtigkeit der Rechnung verantwortet der Benutzer.

---

## 5. Codelisten

| Zweck | Liste | Umsetzung |
|---|---|---|
| Rechnungsart (BT-3) | UNTDID 1001 | Teilmenge: 380, 381, 384, 389 |
| Währung (BT-5) | ISO 4217 | Prüfung gegen eingebettete Liste |
| Land (BT-40/BT-55) | ISO 3166-1 alpha-2 | Prüfung gegen eingebettete Liste |
| Einheit (BT-130) | UN/ECE Rec. 20 + 21 | Teilmenge, siehe `UnitCodes.cs` |
| Steuerkategorie (BT-118/BT-151) | UNTDID 5305 | S, Z, E, AE, K, G, O |
| Zahlungsart (BT-81) | UNTDID 4461 | 30 (Überweisung), 58 (SEPA), 42, 48, 49, 57, 68, 97, 1 |

Die Listen sind als statische Daten im Projekt `EInvoiceSender.Validation`
hinterlegt. Sie werden **nicht** zur Laufzeit aus dem Netz geladen.

---

## 6. Externe Prüfwerkzeuge (optional, gekapselt)

| Werkzeug | Version | Lizenz | Rolle |
|---|---|---|---|
| Mustangproject CLI | 2.24.0 **[V]** (Maven Central) | Apache-2.0 | Schematron- und PDF/A-Gegenprüfung |
| veraPDF | 1.30.2 (in Mustang enthalten) **[S]** | MPL-2.0 / GPLv3 | PDF/A-Referenzvalidator |
| KoSIT-Validator | 1.6.2, Konfiguration 2026-01-31 **[S]** | Apache-2.0 | Nur relevant für XRechnung (Ausbaustufe) |

Diese Werkzeuge sind **nicht** Teil des Kernprozesses. Die Anwendung führt ihre
eigenen Prüfungen immer durch; externe Validatoren laufen zusätzlich, wenn sie
konfiguriert sind, und werden im Bericht namentlich mit Version ausgewiesen.
Details und die Grenzen dieser Konstruktion: `docs/DECISIONS.md`, ADR-0004.

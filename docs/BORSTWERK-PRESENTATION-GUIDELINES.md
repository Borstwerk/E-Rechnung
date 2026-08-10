# BorstWerk – Präsentations- und Sichtbarkeitsregeln

**Stand:** 10.08.2026  
**Zweck:** BorstWerk soll seine Prinzipien durch die Produkte zeigen, nicht sie als Marketingbotschaften ausstellen.

---

## 1. Grundsatz

> **Prinzipien werden umgesetzt, nicht plakatiert.**

Local First, Datenschutz, Barrierefreiheit, Wartbarkeit, Open Source und Qualitätsprüfungen sind Entwicklungs- und Produktregeln. Sie gehören nicht automatisch prominent auf Startseiten, in Kopfzeilen oder in GitHub-Banner.

Der Benutzer soll diese Eigenschaften vor allem daran erkennen, dass die Software entsprechend funktioniert.

---

## 2. Sichtbarkeit in Anwendungen

### Prominent sichtbar

Nur Informationen, die dem aktuellen Arbeitsablauf dienen:

- BorstWerk-Zeichen dezent als Herkunftskennzeichnung
- konkreter Produktname, z. B. `E-Rechnung`
- aktueller Arbeitsschritt oder Seitentitel
- notwendige Hinweise, Fehler und Statusmeldungen
- relevante Aktionen

### Nicht prominent sichtbar

Nicht als Startbanner, Hero-Block, Splashscreen oder dauerhafte Seitenleiste darstellen:

- „Local First“
- „Open Source“
- „keine Telemetrie“
- „mit KI entwickelt“
- Technologie-Stack
- Projektmission
- Designprinzipien
- Aussagen über die gesellschaftliche oder wirtschaftliche Bedeutung der Zielgruppe

Diese Informationen sind wahr und dokumentierbar, aber für die tägliche Bedienung in der Regel irrelevant.

### Geeigneter Ort: „Über“ / „Info“

Dort dürfen knapp und sachlich stehen:

- Version
- Lizenz
- privater Projektcharakter
- Open-Source-Hinweis
- kurze Datenschutzinformation, sofern sinnvoll
- neutraler KI-Hinweis
- Link bzw. Verweis auf Quellcode und Dokumentation

Auch dort gilt: kein Manifest und keine Selbstdarstellung.

---

## 3. Öffentliche GitHub-Darstellung

Das README eines einzelnen Werkzeugs beantwortet zuerst praktische Fragen.

Empfohlene Reihenfolge:

1. **Was macht das Programm?**
2. **Für wen ist es gedacht?**
3. **Download / Installation / Build**
4. **Kurzer Arbeitsablauf**
5. **Screenshots, wenn hilfreich**
6. **Bekannte Grenzen**
7. **Datenschutz und lokale Datenverarbeitung, wenn für die Nutzung relevant**
8. **Lizenz**
9. **Technische Details**
10. **Weiterführende Projektgrundsätze**

### Nicht als README-Kopf verwenden

- großes Marken-Moodboard
- Mission-Statement-Banner
- lange Prinzipienliste
- KI-Werbung
- Entwicklerporträt
- Spenden-/Sponsor-Hinweis
- politische oder wirtschaftspolitische Claims

Ein kleines BorstWerk-Zeichen oder ein dezentes Produktlogo ist zulässig, aber nicht erforderlich.

---

## 4. Screenshots und Designreferenzen

Moodboards, Komponentenübersichten und Design-System-Tafeln sind **interne Entwicklungsreferenzen**.

Sie dürfen im Repository unter `docs/` liegen, müssen aber nicht auf der GitHub-Startseite präsentiert werden.

Öffentlich gezeigte Screenshots sollen echte Nutzungssituationen zeigen:

- wenige, aussagekräftige Screenshots
- kein künstliches Marketing-Dashboard
- keine dekorativen Geräte-Mockups ohne Informationswert
- keine Kundenzitate oder erfundenen Erfolgsgeschichten

---

## 5. Sprache

BorstWerk beschreibt Funktionen sachlich.

Bevorzugt:

- „Kostenlose Werkzeuge für kleine Unternehmen.“
- „Erstellt eine E-Rechnung aus einer vorhandenen PDF-Rechnung.“
- „Die Verarbeitung erfolgt lokal auf dem Rechner.“

Vermeiden:

- „Wir revolutionieren …“
- „für das Rückgrat der Wirtschaft“
- „gegen den Bürokratiewahnsinn“
- „Enterprise-Qualität für alle“
- „AI-powered“ als Qualitätsbehauptung

---

## 6. Produktidentität statt Personenidentität

BorstWerk darf erkennbar sein. Die Person dahinter muss es nicht sein.

Der Name des Maintainers wird nur dort genannt, wo dies sinnvoll oder erforderlich ist, beispielsweise in Lizenz- oder Copyright-Hinweisen.

Nicht vorgesehen sind:

- persönliche About-Seiten
- Entwicklerbiografie im Tool
- Gründerstory
- Profilfoto
- Social-Media-Verlinkungen als Teil der Produktoberfläche

---

## 7. Entscheidungsregel

Vor jedem sichtbaren Projekttext oder Brandingelement wird gefragt:

> **Hilft das dem Benutzer gerade bei seiner Aufgabe?**

- **Ja:** sichtbar machen.
- **Nein, aber rechtlich oder technisch relevant:** dezent in Info/Dokumentation.
- **Nein:** weglassen.

BorstWerk soll eher durch gute Arbeit auffallen als durch die Behauptung, gute Arbeit zu leisten.

# STATUS.md

Letzte Aktualisierung: 2026-08-04

## Aktueller Meilenstein

**M1 – Solution und Domain** (in Arbeit)

## Erledigt

### M0 – Recherche und Architektur ✅

- Entwicklungsumgebung geprüft: Linux-Container, .NET SDK **10.0.110** aus dem
  Ubuntu-Archiv installiert, Java 21 vorhanden. Die offiziellen
  .NET-Downloadhosts sind über den Egress-Proxy gesperrt; NuGet, Maven Central,
  GitHub und das Ubuntu-Archiv sind erreichbar.
- Nachgewiesen: Die WPF-Anwendung lässt sich mit `EnableWindowsTargeting=true`
  auch unter Linux **übersetzen** (Gesamtbuild grün), aber nicht ausführen.
- Drei parallele Recherchen ausgewertet und die tragenden Aussagen selbst
  nachgeprüft (Profil-URN gegen zwei Referenzimplementierungen, Mustang-Version
  gegen Maven Central, Paketversionen gegen die NuGet-API).
- Standards festgelegt und dokumentiert: `docs/STANDARDS.md`.
- Abhängigkeits- und Lizenzmatrix erstellt: `docs/DEPENDENCIES.md`.
- Acht Architekturentscheidungen festgehalten: `docs/DECISIONS.md`.
- Solution-Grundgerüst mit 7 Quell- und 6 Testprojekten angelegt, Build grün.

## In Arbeit

- M1: Domänenmodell, Summen- und Steuerberechnung, Werttypen, Unit-Tests,
  CI-Pipeline.

## Nächster Schritt

Domänenmodell und Berechnungskern implementieren, danach die Regelprüfung.

## Bekannte Probleme und Einschränkungen

| Thema | Stand |
|---|---|
| **PDF/A-3-Konvertierung** | Beliebige PDFs können nicht nach PDF/A-3 konvertiert werden (keine permissiv lizenzierte .NET-Bibliothek kann das). Die Anwendung wertet geeignete PDFs auf und bricht sonst ab – ADR-0003. |
| **PDF/A-Validierung ohne Java** | Ohne externen Validator prüft die Anwendung PDF/A nur strukturell (Teilmenge). Der Bericht weist das aus – ADR-0004. |
| **„Neues Outlook"** | Verhalten beim Öffnen von `.eml` mit Anhang ist aus dieser Umgebung nicht prüfbar. Muss auf echtem Windows 11 verifiziert werden – ADR-0005. |
| **UI-Laufzeitprüfung** | Die WPF-Oberfläche kann hier nur kompiliert, nicht ausgeführt werden. Smoke-Tests der UI gehören auf einen Windows-Agenten. |
| **Installer-Build** | WiX und Inno Setup erzeugen MSI/EXE nur unter Windows. Der Installer wird deshalb ausschließlich im Windows-CI-Job gebaut. |
| **Offizielle Spezifikationsseiten** | `ferd-net.de`, `fnfe-mpe.org`, `pdflib.com` antworten aus dieser Umgebung mit HTTP 403. Betroffene Angaben sind in `docs/STANDARDS.md` als **[S]** markiert. |

## Blockierende Entscheidungen

Keine. Alle offenen Punkte haben eine dokumentierte konservative Vorgabe.

## Zuletzt erfolgreich ausgeführte Prüfungen

| Befehl | Ergebnis | Zeitpunkt |
|---|---|---|
| `dotnet build EInvoiceSender.slnx` | 0 Fehler, 0 Warnungen | 2026-08-04 |

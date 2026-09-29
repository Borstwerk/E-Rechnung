# Windows- und Release-Abnahme 0.3.0

**Status: Lokaler Prüfling manuell freigegeben – endgültiger CI-Kandidat und Veröffentlichung offen**

Bezug: ER-030-VER-01 und ER-030-REL-02. Der Auftraggeber hat nach seinem
Upgrade und der manuellen Testung des unten identifizierten lokalen MSI
die Freigabe zur PR-Erstellung erteilt. Daraus folgt keine Freigabe zum
Merge, Tag oder zur Veröffentlichung.

Die bereits abgenommenen Funktionsslices ersetzen nicht die Abnahme des
konkreten auszuliefernden MSI-/ZIP-Satzes. Automatisierte Paketprüfung und
manuelle Windows-Abnahme werden hier getrennt dokumentiert. Die
[Release-Checkliste](RELEASE-CHECKLIST.md) bleibt maßgeblich für die weiteren
relevanten Prüfungen.

## Vorbereitungsstand

| Angabe | Wert |
|---|---|
| Ausgangs-main | `b67f835a6a27e7a3b97c82e67bacf4a5169584bc` |
| Vorbereitungsbranch | `codex/0.3.0-release-preparation` |
| Produktversion | `0.3.0` |
| Fester ProductCode | `{62DB778C-7E3A-4241-AFF3-6DB002BD29F4}` |
| Unveränderter UpgradeCode | `{7F3C1D92-4B6A-4D21-9D0E-2A5C8B1E7F40}` |
| Publish-Runtimepatch | `10.0.11` |
| Baseline vor Versionsumstellung | 1.213 Core- und 99 Integrationstests grün, keine Skips; externe Validatoren verpflichtend |
| Finaler Release-Commit | offen – erst nach Integration und Kandidatenfestlegung |
| Finaler CI-Run / CodeQL | offen – erst nach Review und Integration |
| CI-Artifact | offen – manueller CI-Lauf mit `publish_artifacts` erst nach Integration |

## Automatisierte Vorprüfungen

Ausgeführt am 29.09.2026. Ein lokal gebauter Vorprüfling aus einem
uncommitteten Arbeitsstand ist nicht der endgültige CI-Abnahmekandidat.

| Prüfung | Ergebnis |
|---|---|
| Versions-/Identitätstests zuerst rot | 2 erwartete Fehler: zentrale Version noch 0.2.0 und fehlende dritte ProductCode-Zuordnung |
| Fokussierte Versions-/Installer-/Lizenz-/Paketierungstests | 51 grün |
| Brechprobe: Rückfall auf 0.2.0 | gezielter Versionstest rot; Mutation zurückgenommen, Fokustests wieder grün |
| Brechprobe: 0.2.0-ProductCode für 0.3.0 wiederverwenden | Identitätstest wegen nicht eindeutiger Codes rot; Mutation zurückgenommen |
| Unbekannte Version 9.9.9 | `ValidateProductIdentity` bricht verständlich ab, ohne WiX-Bau |
| Fehlender ProductCode für 0.3.0 | `ValidateProductIdentity` bricht verständlich ab, ohne WiX-Bau |
| Vollständige Tests nach Vorbereitung | 1.214 Core und 99 Integration grün, keine Skips; externe Validatoren verpflichtend |
| Release-Build / Format / Diff | grün; Solution-Build mit 0 Warnungen und 0 Fehlern |
| Frischer Publish und MSI-/Lizenz-/Runtimeprüfung | beide lokalen Releasebauten grün; 15 Runtimepakete, beide Runtimepacks 10.0.11, Hinweisdatei und 30 Lizenz-/Notice-Dateien im MSI |
| Paketierungs- und SHA-Negativfälle | alle 13 bestehenden Paketierungstests grün, darunter fehlende/zusätzliche Dateien und manipuliertes MSI/ZIP |
| Installer-Buildwächter | alle 4 echten Buildversuche bestanden |
| Zweiter Releasebau über vorhandenem Bestand | grün; absichtliche `ALTDATEI-PROBE.txt` entfernt, final exakt drei Dateien, Produktidentität stabil |
| App-/MSI-Versionsabweichung und falscher UpgradeCode | 4 gezielte Artefaktproben korrekt abgewiesen: MSI-Version 0.2.0, alter ProductCode, falscher UpgradeCode, alte App 0.2.0.0; nur Testkopien verändert |
| Unveränderte Golden Master | unverändert; separate Gegenprüfung von 15 Dateien, 0 Abweichungen, Mustang 2.24.0 / CEN-Schematron / veraPDF |

Ein Zwischenlauf der Coretests fand den zunächst noch fehlenden Verweis auf
dieses Abnahmeprotokoll. Nach dessen Ergänzung war der vollständige Testlauf
grün; keine Prüfregel wurde dafür verändert.

### Lokaler Vorprüfling – ausdrücklich nicht zur Veröffentlichung freigegeben

Der zweite Bau liegt unter `artifacts/release`. EXE und verwaltete Assembly
tragen Datei-/Assemblyversion `0.3.0.0`; die InformationalVersion ist
`0.3.0+b67f835a6a27e7a3b97c82e67bacf4a5169584bc`. Der Hashanteil bezeichnet
den Ausgangs-HEAD, nicht einen Commit des noch uncommitteten Versionsdiffs.

Das MSI meldet ProductVersion `0.3.0` sowie die oben festgelegten Produkt- und
Upgradecodes. Seine PackageCode ist
`{EE16ACCC-C242-41BD-8591-2550BAC5E687}`. Beim ersten Bau war sie
`{C2460E10-DE9E-4230-82ED-0B0C2F551791}`; dieser zulässige Wechsel betrifft
nicht den in beiden Bauten gleichen ProductCode.

| Datei | Größe in Bytes | SHA-256 |
|---|---:|---|
| `BorstWerk-E-Rechnung-Setup.msi` | 88131939 | `0c63a2017728f522a8603772148aa61c6162dc05c61e7eacf2a4ae81ba436caa` |
| `BorstWerk-E-Rechnung-portable-win-x64.zip` | 107474719 | `0c7583a3ef71e945ecd66be96810f45663faeffc069a45cf394a6f359769dc23` |
| `SHA256SUMS.txt` | 207 | nennt ausschließlich MSI und ZIP in dieser Reihenfolge |

Die SHA-Prüfung wurde nach dem zweiten Bau erneut erfolgreich ausgeführt.
Der ZIP-/Publish-Vergleich war bytegenau grün: 319 Einträge, Anwendung und
Hinweisdatei direkt an der Wurzel sowie alle 30 Lizenz-/Notice-Dateien im
vorgesehenen Unterordner. Beide `deps.json`-Runtimepacks lauten:

```text
runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.11
runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.11
```

Die lokalen Buildtranskripte und isolierten Negativproben liegen unter dem
ignorierten `artifacts/release-preparation-030`. Kein Releaseartefakt, keine
Testkopie und kein lokaler Hilfsprüfer wird eingecheckt. Die produktiven
Prüfskripte sind unverändert.

### Traceability

| Kriterium | Nachweis / offener Rest |
|---|---|
| VER-A1 | zentraler Versions-/Override-Test und tatsächliche App-/MSI-Version 0.3.0 |
| VER-A2 | feste drei Zuordnungen, GUID-/Eindeutigkeitsprüfung, Brechprobe und gleicher ProductCode bei beiden Releasebauten |
| VER-A3 | echte `ValidateProductIdentity`-Abbrüche für unbekannte Version und fehlenden Code |
| VER-A4 | unveränderte WiX-Regeln, Scope-/Featuretests und MSI-Upgrade-/Aktionstabellen; lokales Upgrade vom Auftraggeber freigegeben, endgültiger CI-Kandidat noch offen |
| VER-A5 | kein Diff an Anwendung, Runtimekonfiguration, Dependencies, Datenpfaden oder Lizenzquellen; tatsächliche Runtime-/Lizenz-Artefaktprüfung grün |
| VER-A6 | vollständige Tests/Validatoren, Build, Format, Diff sowie MSI-/Publishprüfung grün, Golden Master unverändert |
| REL-A1/A2/A3 | unveränderter Paketierungsweg; zwei Bauten, exakter Dateisatz, ZIP-/Lizenz-/SHA-Vergleich und 13 Paketierungstests |
| REL-A4 | lokaler Vorprüfling erfasst; endgültiger Release-Commit und CI-Kandidat noch offen |
| REL-A5 | lokales Upgrade und manuelle Tests vom Auftraggeber ohne Befund bestätigt; Einzelkontexte nicht gesondert erfasst, endgültiger CI-Kandidat noch offen |
| REL-A6 | CI/CodeQL des neuen Versionsstands und bewusster Artifact-Upload nach Review/Integration noch offen |
| REL-A7 | Release-Notes-Entwurf mit Grenzen; kein Signing, Tag, GitHub Release oder Veröffentlichung |

## Endgültigen Prüfling erfassen

Vor der manuellen Abnahme nach Review und Integration ausfüllen:

| Angabe | Wert |
|---|---|
| Eingefrorener Release-Commit | offen |
| Erfolgreicher CI-Run mit Artifact-Upload | offen |
| Erfolgreicher CodeQL-Run | offen |
| MSI SHA-256 | offen |
| Portable ZIP SHA-256 | offen |
| Heruntergeladene SHA-Datei erneut verifiziert | offen |
| Windows-Version / Build, Datum, Tester | offen |
| Installationskontext der vorhandenen 0.2.0 | offen |

Erwarteter finaler Bestand, exakt drei Dateien aus demselben Build:

```text
BorstWerk-E-Rechnung-Setup.msi
BorstWerk-E-Rechnung-portable-win-x64.zip
SHA256SUMS.txt
```

Die SHA-Datei nennt ausschließlich MSI und ZIP in dieser Reihenfolge. Keine
Mischung lokaler und CI-Artefakte. Byteidentische lokale und CI-Bauten sind
nicht gefordert. Der abgenommene Satz darf vor Veröffentlichung nicht
ausgetauscht werden; Änderungen erfordern neue betroffene Nachweise.

## Manuelle Windows-Abnahme – lokale Rückmeldung und endgültiger CI-Kandidat

Der Auftraggeber bestätigte am 29.09.2026 die Verwendung des oben
identifizierten lokalen MSI (`0c63a2017728f522a8603772148aa61c6162dc05c61e7eacf2a4ae81ba436caa`),
das durchgeführte Upgrade und manuelle Tests ohne Befund. Anschließend wurde
die Reviewfreigabe zur PR-Erstellung erteilt. Der MSI-Hash wurde erneut
gegen den lokalen Bestand geprüft und ist unverändert.

Ausgangsversion, Installationskontext, Windows-Build und einzelne
Prüfhandlungen wurden nicht separat protokolliert. Aus der Rückmeldung
werden daher keine erfundenen Einzelresultate für beide Installationskontexte
oder ein sauberes Zielsystem abgeleitet. Die folgende Matrix bleibt für den
endgültigen CI-Abnahmekandidaten offen; sie widerruft nicht die erteilte
Freigabe des lokalen Prüflings.

Erst nach Diff-Review und mit dem oben identifizierten Kandidaten durchführen.
Vorhandene Benutzerdaten vor Upgrade-/Deinstallationstests sichern; keine
produktive Installation ohne bewusste Zustimmung verändern.

| Fall | Sollresultat | Ergebnis |
|---|---|---|
| Portable ZIP auf sauberem Windows x64 | Start ohne .NET-/Java-Nachinstallation; Über zeigt 0.3.0 | offen |
| Erstinstallation per-user, Desktopoption an/aus | regulärer Start; Startmenü immer, Desktop nur bei Wahl, keine Duplikate | offen |
| Erstinstallation per-machine, Desktopoption an/aus | regulärer Machine-Pfad; vorhandenes profilbezogenes Shortcutverhalten unverändert | offen |
| Upgrade 0.2.0 → 0.3.0 per-user | genau ein Produkt; vorhandener Desktop-Featurezustand, Firma und Einstellungen erhalten | offen |
| Upgrade 0.2.0 → 0.3.0 per-machine | im gleichen Kontext, kein unbeabsichtigter Wechsel; erhaltene Einstellungen und Featurezustände | offen |
| Repair mit/ohne Desktoplink | keine erneute Auswahl, keine Duplikate oder unerwünschten Links | offen |
| Erneuter Start desselben MSI | Wartungsmodus, keine zweite Produktinstanz | offen |
| Downgrade auf 0.2.0 | kontrolliert verhindert, 0.3.0 bleibt intakt | offen |
| Deinstallation | installierte Verknüpfungen entfernt; Benutzerdaten unter `%LOCALAPPDATA%\EInvoiceSender` erhalten | offen |
| Erzeugungsworkflow vollständig | PDF laden, Herkunftshinweise, Vergleich, E-Rechnung und optionaler EML-Entwurf korrekt; Original unverändert | offen |
| Read-only Checker | Kerndaten/Befunde, deutsches Datum/Beträge, keine falsche Konformitätsaussage; Quelle und Wizardzustand unverändert | offen |
| Verkäufer-UX | steuerliche Angaben und Identifikation getrennt; nur Steuernummer bleibt blockiert | offen |
| POS-01-Smoke | Tabelle Seite 1 + Hinweis Seite 2 sowie Deckblatt + Tabelle erkannt; Fortsetzungen/doppelte Köpfe bleiben leer | offen |
| Mindestfenster / Skalierung / Tastatur | keine Überlagerungen oder abgeschnittenen Hilfetexte, sinnvoller Fokus | offen |
| Windows-spezifische Releasecheckliste | relevante EML-/DPAPI-/Diagnosefälle tatsächlich geprüft oder offen begründet | offen |

Ein geänderter 0.3.0-Build mit demselben ProductCode ist kein Major Upgrade
einer früheren 0.3.0-Testinstallation. Eine gegebenenfalls nötige Bereinigung
der Testinstallation erfolgt nur bewusst nach Review; Benutzerdaten bleiben
erhalten. Kein neuer ProductCode pro Kandidat und keine Aktivierung von
Same-Version-Upgrades.

## Freigaben

- Diff-Review / Freigabe zur PR-Erstellung: vom Auftraggeber erteilt am 29.09.2026.
- Lokales Upgrade und manuelle Tests: vom Auftraggeber ohne Befund bestätigt am 29.09.2026.
- Integration / Main-CI / CodeQL des Versionsstands: offen.
- Windows-Abnahme des endgültigen Kandidaten: offen.
- Gesonderte Veröffentlichungsfreigabe: offen.
- Tag `v0.3.0` und GitHub Release: nicht erstellt.

Signing wird in diesem Slice nicht eingeführt. Ein erfolgreicher technischer
Build darf keinen dieser noch offenen Freigabeschritte als erledigt darstellen.

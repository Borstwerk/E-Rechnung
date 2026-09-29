# Windows- und Release-Abnahme 0.3.0

**Status: Abgeschlossen – Version 0.3.0 aus dem lokal abgenommenen Artefaktsatz veröffentlicht**

Bezug: ER-030-VER-01 und ER-030-REL-02. Der Auftraggeber hat das unten
identifizierte lokale MSI aktualisiert, die manuelle Windows-Testung ohne
Befund abgeschlossen und anschließend die Reviewfreigabe zur PR-Erstellung
erteilt. Der Stand wurde in `main` integriert; Main-CI und CodeQL sind grün.
Version 0.3.0 wurde am 29.09.2026 veröffentlicht.

Der Auftraggeber bestätigt ausdrücklich die bewusste Verwendung des lokal
gebauten und abgenommenen MSI-/ZIP-Satzes als endgültige Auslieferung.
Der ursprünglich für ER-030-REL-02 vorgesehene separate CI-Artefaktsatz
wird für diesen Release nicht verwendet. Diese Freigabeentscheidung ersetzt
keine tatsächlich nicht ausgeführte Prüfung: Automatisierte Nachweise,
manuelle Gesamtabnahme und nicht separat erfasste Einzelprüfungen bleiben
unterscheidbar. Die [Release-Checkliste](RELEASE-CHECKLIST.md) bleibt die
Grundlage für künftige Abnahmen.

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
| Geprüfter Implementierungscommit | `d4c618142198d66bedf35a41c07f701316272e26` |
| Integration / Release-Tag | `b62c60472485a37fd574cbf628d5c8d60b5a3c5f`, Tag `v0.3.0` |
| Main-CI | [Run 36541480112](https://github.com/Borstwerk/E-Rechnung/actions/runs/36541480112), erfolgreich |
| CodeQL | [Run 36541480502](https://github.com/Borstwerk/E-Rechnung/actions/runs/36541480502), C# und Actions erfolgreich |
| Herkunft der veröffentlichten Artefakte | zweiter lokaler Releasebau; bewusst freigegeben, kein CI-Artefaktsatz |

## Automatisierte Vorprüfungen

Ausgeführt am 29.09.2026. Die Prüfungen beziehen sich auf den lokal gebauten
Arbeitsstand. Der zweite lokale Releasebau wurde später unverändert als
Auslieferungssatz freigegeben; er wird nicht nachträglich als CI-Bau bezeichnet.

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

### Lokal gebauter, abgenommener und veröffentlichter Artefaktsatz

Der zweite Bau liegt unter `artifacts/release`. EXE und verwaltete Assembly
tragen Datei-/Assemblyversion `0.3.0.0`; die InformationalVersion ist
`0.3.0+b67f835a6a27e7a3b97c82e67bacf4a5169584bc`. Der Hashanteil bezeichnet
den Ausgangs-HEAD beim Bau aus dem damals uncommitteten Versionsdiff, nicht
den späteren Implementierungscommit oder den Release-Tag. Diese Herkunft
bleibt ausdrücklich dokumentiert; die veröffentlichten Pakete wurden nicht
nach dem Merge erneut gebaut oder ausgetauscht.

Das MSI meldet ProductVersion `0.3.0` sowie die oben festgelegten Produkt- und
Upgradecodes. Seine PackageCode ist
`{EE16ACCC-C242-41BD-8591-2550BAC5E687}`. Beim ersten Bau war sie
`{C2460E10-DE9E-4230-82ED-0B0C2F551791}`; dieser zulässige Wechsel betrifft
nicht den in beiden Bauten gleichen ProductCode.

| Datei | Größe in Bytes | SHA-256 |
|---|---:|---|
| `BorstWerk-E-Rechnung-Setup.msi` | 88131939 | `0c63a2017728f522a8603772148aa61c6162dc05c61e7eacf2a4ae81ba436caa` |
| `BorstWerk-E-Rechnung-portable-win-x64.zip` | 107474719 | `0c7583a3ef71e945ecd66be96810f45663faeffc069a45cf394a6f359769dc23` |
| `SHA256SUMS.txt` | 207 | `2f245c4711347d554463e6c87b5422b736bbe3d63da5b72355008bfd147da1ae` |

Die SHA-Datei nennt ausschließlich MSI und ZIP in dieser Reihenfolge.
Die SHA-Prüfung wurde nach dem zweiten Bau und bei dieser Dokumentations-
nachpflege erneut erfolgreich ausgeführt. Größen und SHA-256-Werte aller
drei lokalen Dateien stimmen mit den von GitHub gemeldeten Asset-Digests
des [veröffentlichten Releases](https://github.com/Borstwerk/E-Rechnung/releases/tag/v0.3.0)
überein. Ein erneuter Download der vollständigen MSI-/ZIP-Dateien wird damit
nicht behauptet.
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

| Kriterium | Nachweis / Freigabeentscheidung |
|---|---|
| VER-A1 | zentraler Versions-/Override-Test und tatsächliche App-/MSI-Version 0.3.0 |
| VER-A2 | feste drei Zuordnungen, GUID-/Eindeutigkeitsprüfung, Brechprobe und gleicher ProductCode bei beiden Releasebauten |
| VER-A3 | echte `ValidateProductIdentity`-Abbrüche für unbekannte Version und fehlenden Code |
| VER-A4 | unveränderte WiX-Regeln, Scope-/Featuretests und MSI-Upgrade-/Aktionstabellen; lokales Upgrade und veröffentlichter Satz vom Auftraggeber freigegeben |
| VER-A5 | kein Diff an Anwendung, Runtimekonfiguration, Dependencies, Datenpfaden oder Lizenzquellen; tatsächliche Runtime-/Lizenz-Artefaktprüfung grün |
| VER-A6 | vollständige Tests/Validatoren, Build, Format, Diff sowie MSI-/Publishprüfung grün, Golden Master unverändert |
| REL-A1/A2/A3 | unveränderter Paketierungsweg; zwei Bauten, exakter Dateisatz, ZIP-/Lizenz-/SHA-Vergleich und 13 Paketierungstests |
| REL-A4 | lokale Bauherkunft, Implementierungscommit, Release-Tag, Main-CI und veröffentlichte Asset-Digests erfasst; bewusste Freigabe des unveränderten lokalen Satzes |
| REL-A5 | lokales Upgrade und manuelle Tests vom Auftraggeber ohne Befund bestätigt; Gesamtabnahme abgeschlossen, nicht separat protokollierte Einzelkontexte bleiben als solche kenntlich |
| REL-A6 | Main-CI und CodeQL erfolgreich; bewusste Abweichung vom geplanten CI-Artifact-Download: lokaler Satz veröffentlicht und gegen GitHub-Asset-Digests abgeglichen |
| REL-A7 | veröffentlichte Release Notes nennen Umfang und Grenzen; Veröffentlichung durch den Auftraggeber ausgeführt und lokaler Satz ausdrücklich bestätigt; kein Signing |

## Endgültiger veröffentlichter Prüfling

Festgelegter Auslieferungssatz am 29.09.2026:

| Angabe | Wert |
|---|---|
| Release-Tag / Commit | `v0.3.0` / `b62c60472485a37fd574cbf628d5c8d60b5a3c5f` |
| Paketherkunft | zweiter lokaler Bau aus dem geprüften Arbeitsstand; InformationalVersion wie oben, kein nachträglicher CI-Bau |
| Erfolgreicher Main-CI-Run | [36541480112](https://github.com/Borstwerk/E-Rechnung/actions/runs/36541480112); Linux und Windows einschließlich Releasepaketbau erfolgreich |
| CI-Artifact-Upload | im Main-Push-Lauf regulär übersprungen; für die Auslieferung durch ausdrückliche Entscheidung für den lokalen Satz ersetzt |
| Erfolgreicher CodeQL-Run | [36541480502](https://github.com/Borstwerk/E-Rechnung/actions/runs/36541480502); C# und Actions erfolgreich |
| MSI SHA-256 | `0c63a2017728f522a8603772148aa61c6162dc05c61e7eacf2a4ae81ba436caa` |
| Portable ZIP SHA-256 | `0c7583a3ef71e945ecd66be96810f45663faeffc069a45cf394a6f359769dc23` |
| SHA-Datei verifiziert | lokale SHA-Datei gegen MSI/ZIP erfolgreich; alle drei lokalen Hashes stimmen mit veröffentlichten GitHub-Asset-Digests überein |
| Windows-Version / Build, Datum, Tester | manuelle Rückmeldung des Auftraggebers am 29.09.2026; Windows-Version und Build nicht separat protokolliert |
| Installationskontext der vorhandenen Installation | nicht separat protokolliert; keine nachträgliche Behauptung beider Installationskontexte |
| Veröffentlichung | [BorstWerk E-Rechnung 0.3.0](https://github.com/Borstwerk/E-Rechnung/releases/tag/v0.3.0), 29.09.2026, 08:35:05 UTC |

Veröffentlichter Bestand, exakt drei hochgeladene Dateien aus demselben lokalen Build:

```text
BorstWerk-E-Rechnung-Setup.msi
BorstWerk-E-Rechnung-portable-win-x64.zip
SHA256SUMS.txt
```

Es wurden keine lokalen und CI-Artefakte gemischt. Byteidentische lokale und
CI-Bauten sind nicht gefordert. Der abgenommene lokale Satz wurde unverändert
veröffentlicht; Änderungen an den Paketen erfordern neue betroffene Nachweise.

## Manuelle Windows-Abnahme – abgeschlossene Gesamtabnahme des lokalen Satzes

Der Auftraggeber bestätigte am 29.09.2026 die Verwendung des oben
identifizierten lokalen MSI (`0c63a2017728f522a8603772148aa61c6162dc05c61e7eacf2a4ae81ba436caa`),
das durchgeführte Upgrade und manuelle Tests ohne Befund. Anschließend wurde
die Reviewfreigabe zur PR-Erstellung erteilt. Nach der Veröffentlichung
bestätigte der Auftraggeber ausdrücklich die Verwendung des lokalen Satzes
und den Abschluss des Abnahmeprotokolls. Der MSI-Hash ist unverändert.

Ausgangsversion, Installationskontext, Windows-Build und einzelne
Prüfhandlungen wurden nicht separat protokolliert. Aus der Rückmeldung
werden daher keine erfundenen Einzelresultate für beide Installationskontexte
oder ein sauberes Zielsystem abgeleitet. Nicht separat belegte Fälle sind
mit der bewussten Gesamtfreigabe ohne weitere Einzelprotokolle abgeschlossen,
nicht nachträglich als technisch bestanden markiert.

Die folgende Matrix hält die Nachweisgrenzen fest. Für künftige Abnahmen
weiterhin Benutzerdaten vor Upgrade-/Deinstallationstests sichern und keine
produktive Installation ohne bewusste Zustimmung verändern.

| Fall | Sollresultat | Ergebnis |
|---|---|---|
| Portable ZIP auf sauberem Windows x64 | Start ohne .NET-/Java-Nachinstallation; Über zeigt 0.3.0 | kein separater Einzelbefund; im Rahmen der Gesamtfreigabe ohne zusätzliches Protokoll abgeschlossen |
| Erstinstallation per-user, Desktopoption an/aus | regulärer Start; Startmenü immer, Desktop nur bei Wahl, keine Duplikate | kein separater Einzelbefund; Gesamtfreigabe ohne Kontextnachweis |
| Erstinstallation per-machine, Desktopoption an/aus | regulärer Machine-Pfad; vorhandenes profilbezogenes Shortcutverhalten unverändert | kein separater Einzelbefund; Gesamtfreigabe ohne Kontextnachweis |
| Upgrade 0.2.0 → 0.3.0 per-user | genau ein Produkt; vorhandener Desktop-Featurezustand, Firma und Einstellungen erhalten | Upgrade ohne Befund bestätigt; Ausgangsversion und Kontext nicht separat erfasst |
| Upgrade 0.2.0 → 0.3.0 per-machine | im gleichen Kontext, kein unbeabsichtigter Wechsel; erhaltene Einstellungen und Featurezustände | Upgrade ohne Befund bestätigt; kein zusätzlicher Nachweis eines zweiten Kontexts |
| Repair mit/ohne Desktoplink | keine erneute Auswahl, keine Duplikate oder unerwünschten Links | kein separater Einzelbefund; Gesamtfreigabe ohne zusätzliches Protokoll |
| Erneuter Start desselben MSI | Wartungsmodus, keine zweite Produktinstanz | kein separater Einzelbefund; Gesamtfreigabe ohne zusätzliches Protokoll |
| Downgrade auf 0.2.0 | kontrolliert verhindert, 0.3.0 bleibt intakt | kein separater Einzelbefund; Gesamtfreigabe ohne zusätzliches Protokoll |
| Deinstallation | installierte Verknüpfungen entfernt; Benutzerdaten unter `%LOCALAPPDATA%\EInvoiceSender` erhalten | kein separater Einzelbefund; Gesamtfreigabe ohne zusätzliches Protokoll |
| Erzeugungsworkflow vollständig | PDF laden, Herkunftshinweise, Vergleich, E-Rechnung und optionaler EML-Entwurf korrekt; Original unverändert | manuelle Gesamttestung ohne Befund bestätigt; kein zusätzliches Einzelprotokoll |
| Read-only Checker | Kerndaten/Befunde, deutsches Datum/Beträge, keine falsche Konformitätsaussage; Quelle und Wizardzustand unverändert | Funktionsslice unter Windows abgenommen; manuelle Gesamttestung des lokalen Satzes ohne Befund bestätigt |
| Verkäufer-UX | steuerliche Angaben und Identifikation getrennt; nur Steuernummer bleibt blockiert | Funktionsslice unter Windows abgenommen; manuelle Gesamttestung des lokalen Satzes ohne Befund bestätigt |
| POS-01-Smoke | Tabelle Seite 1 + Hinweis Seite 2 sowie Deckblatt + Tabelle erkannt; Fortsetzungen/doppelte Köpfe bleiben leer | synthetische Windows-Testfälle durch Screenshots und Freigabe belegt; Gesamttestung bestätigt |
| Mindestfenster / Skalierung / Tastatur | keine Überlagerungen oder abgeschnittenen Hilfetexte, sinnvoller Fokus | Funktionsslice unter Windows abgenommen; kein zusätzliches Einzelprotokoll für den lokalen Satz |
| Windows-spezifische Releasecheckliste | relevante EML-/DPAPI-/Diagnosefälle tatsächlich geprüft oder Nachweisgrenzen begründet | manuelle Gesamttestung bestätigt; keine separaten Einzelbefunde, mit Gesamtfreigabe abgeschlossen |

Ein geänderter 0.3.0-Build mit demselben ProductCode ist kein Major Upgrade
einer früheren 0.3.0-Testinstallation. Eine gegebenenfalls nötige Bereinigung
der Testinstallation erfolgt nur bewusst nach Review; Benutzerdaten bleiben
erhalten. Kein neuer ProductCode pro Kandidat und keine Aktivierung von
Same-Version-Upgrades.

## Freigaben

- Diff-Review / Freigabe zur PR-Erstellung: vom Auftraggeber erteilt am 29.09.2026.
- Lokales Upgrade und manuelle Tests: vom Auftraggeber ohne Befund bestätigt am 29.09.2026.
- Integration: [PR #17](https://github.com/Borstwerk/E-Rechnung/pull/17) am 29.09.2026 in `main` gemergt.
- Main-CI / CodeQL des Versionsstands: erfolgreich; Runs und einzelne Jobs oben verknüpft.
- Windows-Abnahme: Gesamtabnahme des unveränderten lokalen Satzes vom Auftraggeber bestätigt; oben dokumentierte Einzelprotokollgrenzen bleiben bestehen.
- Veröffentlichungsentscheidung: vom Auftraggeber ausgeführt; bewusste Verwendung des lokalen Satzes und Abschluss dieses Protokolls am 29.09.2026 ausdrücklich bestätigt.
- Abweichung vom CI-Artefaktplan: lokaler Satz statt separatem CI-Download bewusst freigegeben; keine noch ausstehende CI-Kandidatenabnahme für diesen Release.
- Tag `v0.3.0` und GitHub Release: veröffentlicht am 29.09.2026; exakt der oben identifizierte lokale Dateisatz.

Signing wurde nicht eingeführt. Der Abschluss beruht auf den technischen
Nachweisen und der ausdrücklichen Entscheidung des Auftraggebers, nicht auf
einer nachträglichen Behauptung fehlender Einzeltests oder eines CI-Downloads.

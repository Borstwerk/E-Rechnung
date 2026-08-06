# Releasebericht

Stand: 2026-08-05 · Branch `claude/einvoice-sender-app-axwp4h`

Dieser Bericht sagt, was nachweislich funktioniert, wie es belegt ist, und was
**nicht** geprüft wurde. Der zweite Teil ist der wichtigere.

---

## 1. Kurzfassung

Der fachliche und technische Kern ist fertig und **von den offiziellen
Referenzwerkzeugen bestätigt**. Die Oberfläche, der E-Mail-Entwurf und der
Installer sind implementiert beziehungsweise definiert, aber auf einem echten
Windows-System **nicht erprobt**.

Ein Release ist damit **noch nicht freigegeben**. Die Definition of Done aus der
Aufgabenstellung (Abschnitt 17) verlangt einen vollständigen Durchlauf auf einem
frischen Windows-System; dieser Schritt steht aus und lässt sich in der
vorliegenden Umgebung nicht ersetzen.

---

## 2. Was nachweislich funktioniert

### 2.1 Normkonformität – durch externe Werkzeuge belegt

Für jeden der acht Golden Master läuft bei jedem Testlauf die vollständige
Kette gegen die echten Referenzwerkzeuge:

| Prüfschritt | Werkzeug | Ergebnis |
|---|---|---|
| CII-XML erzeugen | eigener Writer | 8 von 8 |
| EN-16931-Geschäftsregeln | CEN-Schematron über Mustang 2.24.0 | `status="valid"` |
| Einbettung in PDF/A-3b | eigener Composer | 8 von 8 |
| PDF/A-3b-Konformität | **veraPDF 1.30.2, Flavour 3b** | **389 Prüfpunkte, `isCompliant=true`** |
| Factur-X-Prüfung der fertigen PDF | Mustang | bestanden |
| Erneutes Öffnen und Extraktion | eigener Analyzer | byte-identisch |
| Extrahierte XML erneut gegen Schematron | Mustang | `status="valid"` |
| Anhangname, MIME-Typ, `/AFRelationship` | eigene Prüfung | `factur-x.xml`, `text/xml`, `/Alternative` |
| Profilkennung | eigene Prüfung | `urn:cen.eu:en16931:2017` |
| XMP-Metadaten | eigene Prüfung | `pdfaid:part 3`, `conformance B`, `fx`-Felder, Erweiterungsschema |

Drei absichtlich verfälschte Dateien werden vom Schematron **beanstandet** –
ohne diesen Gegenbeweis wäre die Prüfkette wertlos.

### 2.2 Testumfang

| Projekt | Tests |
|---|---|
| Domain.Tests | 187 |
| Validation.Tests | 122 |
| IntegrationTests | 48 |
| Formats.Tests | 40 |
| Presentation.Tests | 32 |
| Mail.Tests | 10 |
| **Gesamt** | **439, alle grün** |

Befehle: `dotnet build EInvoiceSender.slnx -c Release`,
`REQUIRE_EXTERNAL_VALIDATORS=1 dotnet test EInvoiceSender.slnx -c Release`,
`./build/validate-golden-masters.sh`.

### 2.3 Erfüllte Anforderungen

- Die Originaldatei wird niemals verändert – durch Tests abgesichert.
- Ohne die Bestätigung der inhaltlichen Übereinstimmung entsteht keine Datei;
  die Sperre sitzt im Anwendungsfall, nicht in der Oberfläche.
- Eine bereits eingebettete Rechnung wird nie stillschweigend ersetzt.
- Nicht aufwertbare PDFs werden abgelehnt – mit einer Meldung, die die konkrete
  Einstellung nennt, die der Anwender ändern soll.
- Keine halb fertige Ausgabedatei: Beanstandung, Zeitüberschreitung oder
  Abbruch hinterlassen nichts.
- Temporäre Dateien verschwinden auch bei Fehler und Abbruch.
- Ausgabe atomar, kein stillschweigendes Überschreiben, Schutz gegen Path
  Traversal.
- Alle Verarbeitung lokal; keine Übertragung an externe Dienste.
- Keine AGPL- oder kommerzielle Abhängigkeit im Produktivpfad.

---

## 3. Was NICHT geprüft ist

Diese Punkte sind **nicht** „vermutlich in Ordnung", sondern schlicht ungeprüft.

| Bereich | Zustand | Warum |
|---|---|---|
| **WPF-Oberfläche zur Laufzeit** | teilweise geprüft, siehe Hinweis | Die Anwendung **kompiliert** unter Linux, lässt sich dort aber nicht starten. Die Ablauflogik ist über `EInvoiceSender.Presentation` mit 23 Tests abgedeckt; das Zusammenspiel mit echten Fenstern, Dateidialogen, Drag-and-drop und der PDF-Vorschau ist nie gelaufen. **Beim ersten Start durch einen Menschen ist hier prompt ein Fehler aufgetreten** – siehe unten. |
| **Installer** | nie gebaut, nie ausgeführt | WiX erzeugt MSI-Dateien nur unter Windows. Neuinstallation, Startmenüeintrag, Upgrade über eine ältere Fassung und Deinstallation sind ungeprüft. |
| **`.eml` im „neuen Outlook"** | ungeprüft | Aus dieser Umgebung nicht testbar. Für HTML-Nachrichten ist ein Anhangsverlust berichtet; deshalb wird bewusst reiner Text verwendet. Ob `X-Unsent: 1` im aktuellen Build als Entwurf öffnet, muss ein Mensch prüfen. |
| **DPAPI-Schutz der IBAN** | ungeprüft | Läuft nur unter Windows. Auf anderen Plattformen wird die IBAN bewusst **nicht** gespeichert. |
| **Self-contained `win-x64`-Veröffentlichung** | ungeprüft | `PublishReadyToRun` unterstützt kein Cross-OS-Publishing. Der Windows-CI-Job ist eingerichtet, aber nie gelaufen. |
| **PDF-Vorschau (PDFtoImage/PDFium)** | ungeprüft | Native Abhängigkeit, nur unter Windows im Zielbetrieb. |

#### Was der erste echte Start zutage gefördert hat

Beim ersten Start der Anwendung auf einem Windows-Rechner schlug die
PDF-Auswahl sofort fehl:

> `System.InvalidOperationException`: Der aufrufende Thread kann nicht auf
> dieses Objekt zugreifen, da sich das Objekt im Besitz eines anderen Threads
> befindet.

Ursache: Das `ShellViewModel` hat seine `await`-Aufrufe mit
`ConfigureAwait(false)` abgeschlossen. Damit läuft die Fortsetzung nach dem
`await` auf einem Threadpool-Thread. Die anschließende Zuweisung
`IsBusy = false` meldet über `NotifyCanExecuteChanged` an die angebundene
Schaltfläche – aus dem falschen Thread, und WPF bricht ab.

Der Fall ist lehrreich, weil **keiner der damals 426 Tests ihn finden konnte**: Ein
Testlauf hat keinen Synchronisierungskontext, und ohne Kontext verhalten sich
`ConfigureAwait(true)` und `ConfigureAwait(false)` identisch. Der Fehler war für
die vorhandene Teststrategie unsichtbar, nicht bloß unentdeckt.

Behoben durch `ConfigureAwait(true)` an allen fünf `await`-Stellen des
ViewModels. Gegen einen Rückfall steht `UiThreadAffinityTests`: Der Test stellt
einen echten Ein-Thread-Kontext bereit – den kleinsten Nachbau des
WPF-Dispatchers – und prüft für alle vier abwartenden Befehle, dass jede
Änderungsmeldung vom Oberflächen-Thread kommt. Vor der Behebung schlagen alle
vier fehl, danach laufen sie durch.

Der zweite Start förderte prompt zwei weitere Fehler zutage, beide ebenfalls
nur im laufenden Programm sichtbar und beide von WPF stillschweigend begangen:

- **Die Befundliste zeigte den Klassennamen** statt der Meldung, weil für
  `FindingViewModel` keine `DataTemplate` existierte. WPF fällt in diesem Fall
  wortlos auf `ToString()` zurück.
- **„Weiter" blieb dauerhaft gesperrt**, obwohl die Statuszeile die PDF als
  verarbeitbar meldete. `CanGoForward` liest `IsBusy`, doch `IsBusy`
  benachrichtigte nur „Erzeugen" und „Abbrechen"; die einzige Neubewertung von
  „Weiter" fiel in den Moment, in dem `IsBusy` noch `true` war.

Beide sind behoben und durch neue Tests abgesichert
(`FindingTemplateBindingTests`, `CommandEnablementTests`). Bemerkenswert am
zweiten Fall ist der Testentwurf: Ein Test, der schlicht `CanExecute` aufruft,
kann ihn nicht finden, weil dieser Aufruf die Bedingung jedes Mal neu auswertet
und darum immer die richtige Antwort gibt – auch wenn der Knopf auf dem
Bildschirm falsch aussieht. Erst ein Nachbau des Knopfverhaltens deckt die
Abweichung auf. Der erste Anlauf dieses Tests bestand gegen den fehlerhaften
Stand und musste verworfen werden.

Nebenbei fiel ein sporadisch fehlschlagender Integrationstest auf:
`Progress<T>` stellt Meldungen über den Threadpool zu, sodass ein Rückruf
`Cancel` auf einer bereits entsorgten `CancellationTokenSource` aufrufen
konnte. Das Ergebnis war eine unbeobachtete `ObjectDisposedException`, die
xunit als „Catastrophic failure" meldet. Tests verwenden jetzt eine sofort
zustellende Fortschrittsmeldung.

**Die Lehre für die restliche Liste oben:** Was in dieser Umgebung nicht
gestartet werden kann, ist ungeprüft – und ungeprüft heißt hier nachweislich
nicht „vermutlich in Ordnung". Drei Fehler in zwei Starts, keiner davon von 426
Tests auffindbar.

### Fachliche Grenzen (bewusst, dokumentiert)

- **Keine PDF/A-Konvertierung.** Es gibt keine permissiv lizenzierte
  .NET-Bibliothek, die ein beliebiges PDF nach PDF/A-3 konvertiert. Die
  Anwendung wertet geeignete PDFs auf und lehnt ungeeignete ab (ADR-0003). PDFs
  ohne eingebettete Schriften – darunter alle mit den 14 Standardschriften –
  werden abgelehnt.
- **Die eigene Regelprüfung ist kein Konformitätsnachweis.** Was sie
  durchlässt, ist damit nicht als normgerecht bestätigt; die Freigabe erteilen
  die externen Validatoren (ADR-0004, ADR-0009).
- **Ohne externen Validator** prüft die Anwendung PDF/A nur strukturell. Der
  Bericht weist das aus und schreibt „NICHT AUSGEFUEHRT" in die Textfassung.
- **Keine Steuerberatung.** Geprüft wird das Format, nicht die inhaltliche oder
  steuerliche Richtigkeit.
- Die Angaben zu ZUGFeRD 2.4/2.5 stammen aus Sekundärquellen; `ferd-net.de` und
  `fnfe-mpe.org` antworten aus dieser Umgebung mit HTTP 403. Betroffene Werte
  sind in `docs/STANDARDS.md` mit **[S]** markiert. Die tatsächlich erzeugten
  Zeichenketten (Profil-URN, Namensräume, XMP-Schema) wurden dagegen aus
  Referenzimplementierungen verifiziert und zusätzlich durch die
  veraPDF-/Schematron-Prüfung bestätigt.

---

## 4. Nächste Schritte bis zur Freigabe

Alle verbleibenden Punkte brauchen ein echtes Windows-System:

1. Windows-CI-Lauf auslösen und Ergebnis prüfen (Build, Tests, Veröffentlichung,
   Installer-Build).
2. Installer auf einem frischen Windows-System: installieren, starten,
   Startmenüeintrag prüfen.
3. Vollständiger Durchlauf nach Definition of Done, Schritte 4 bis 16:
   PDF wählen, Daten erfassen, bestätigen, erzeugen, speichern, E-Mail-Entwurf
   öffnen, Ausgabeordner öffnen, Anwendung neu starten und Einstellungen laden.
4. `.eml`-Verhalten mit klassischem **und** neuem Outlook prüfen.
5. Upgrade über eine ältere Testfassung und saubere Deinstallation prüfen;
   dabei kontrollieren, dass Benutzerdaten erhalten bleiben.
6. Ergebnisse hier eintragen. Erst danach ist ein Release freigegeben.

**Nicht freigeben**, solange der Windows-Build oder ein Validator fehlschlägt.

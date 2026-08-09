# SECURITY.md – Sicherheitsmaßnahmen

Stand: 2026-08-05. Dieses Dokument beschreibt, was im Code tatsächlich
umgesetzt ist, mit Verweis auf die jeweilige Klasse. Kurzfassung der Regeln:
`AGENTS.md`, Abschnitt 5 (S1–S8). Diese Datei ist die dort angekündigte
vollständige Fassung.

---

## Bedrohungsmodell

Externe PDF- und XML-Dateien sind grundsätzlich **nicht vertrauenswürdig**
(**S1**). Die Anwendung öffnet PDF-Dateien, die der Benutzer aus einem
beliebigen fremden Programm mitbringt, und liest daraus möglicherweise bereits
eingebettete Rechnungs-XML. Beide Eingaben können absichtlich präpariert sein.
Daraus folgt: Jeder Lesepfad muss mit einer feindseligen Datei rechnen, nie mit
einer wohlgeformten.

Nicht Teil des Bedrohungsmodells: Mehrbenutzerbetrieb, Netzwerkangriffe,
Rechteausweitung auf Betriebssystemebene. Die Anwendung ist ein lokales
Einzelbenutzer-Desktopprogramm (siehe `docs/SPECIFICATION.md`, Abschnitt 13).

---

## XML-Verarbeitung

Einziger zulässiger Lesepfad für XML im gesamten Projekt ist `SecureXml`
(`src/EInvoiceSender.Application/Xml/SecureXml.cs`, **S2**). Sowohl die
Formatschicht (Rechnungs-XML) als auch die Validierungsschicht (Berichte
externer Werkzeuge) verwenden ausschließlich diese Klasse.

`SecureXml.CreateReaderSettings()` setzt:

| Einstellung | Wert | Zweck |
|---|---|---|
| `DtdProcessing` | `Prohibit` | verhindert XXE und Entity-Expansion („Billion Laughs") vollständig, da DTDs gar nicht verarbeitet werden |
| `XmlResolver` | `null` | kein Nachladen externer Ressourcen, weder Datei noch Netz |
| `MaxCharactersFromEntities` | `0` | keine Entity-Expansion, selbst wenn DTDs verarbeitet würden |
| `MaxCharactersInDocument` | 8 MB (`SecureXml.MaxXmlSizeInBytes`) | Größenbegrenzung |

Zusätzlich prüft `SecureXml.CreateReader(byte[])` die Puffergröße **vor** dem
Erzeugen des Parsers, damit ein übergroßes Dokument gar nicht erst in den
Parser gelangt. `SecureXml.MaxDepth` (100) begrenzt die zulässige
Verschachtelungstiefe.

Dass XXE- und Billion-Laughs-Angriffe tatsächlich abgewehrt werden, ist nicht
nur behauptet, sondern durch dedizierte Sicherheitstests belegt (siehe
`docs/TESTING.md`).

---

## Dateisystem

**Dateinamen.** `SafeFileName`
(`src/EInvoiceSender.Domain/Files/SafeFileName.cs`) bereinigt jeden aus
fachlichen Angaben (Rechnungsnummer, Empfängername) abgeleiteten Dateinamen:
Steuerzeichen, Pfadtrenner (`/`, `\`) und die unter Windows verbotenen Zeichen
werden entfernt, reservierte Gerätenamen (`CON`, `PRN`, `NUL`, `COM1`–`COM9`,
`LPT1`–`LPT9`) erhalten einen Unterstrich, führende/abschließende Punkte und
Leerzeichen werden entfernt. Eine manipulierte Rechnungsnummer wie
`../../etc/passwd` kann so keinen Pfadwechsel auslösen.

**Path-Traversal-Schutz.** `FileStorage.ResolveTargetPath`
(`src/EInvoiceSender.Infrastructure/Storage/FileStorage.cs`, **S3**) prüft nach
dem Zusammensetzen von Zielverzeichnis und bereinigtem Dateinamen zusätzlich,
dass der resultierende, mit `Path.GetFullPath` normalisierte Pfad tatsächlich
unterhalb des Zielverzeichnisses liegt (`candidate.StartsWith(targetDirectory + Separator)`).
Der Code kommentiert das selbst als „Gürtel und Hosenträger" – die Bereinigung
allein soll bereits ausreichen, die Pfadprüfung ist eine zweite, unabhängige
Absicherung.

**Atomares Schreiben.** `FileStorage.WriteAsync` schreibt zunächst in eine
temporäre Datei (`.{Name}.{GUID}.tmp`) im selben Zielverzeichnis und benennt
sie erst danach mit `File.Move` um (**S4**). Da die temporäre Datei im selben
Verzeichnis liegt, ist das abschließende Verschieben ein Umbenennen innerhalb
desselben Dateisystems und damit atomar. Schlägt der Schreibvorgang fehl, wird
die temporäre Datei gelöscht (`DeleteQuietly`); es bleibt entweder die alte
Datei oder gar keine zurück, niemals eine halb geschriebene.

**Kein stilles Überschreiben.** Vorgabe ist `OverwriteBehavior.CreateNumberedCopy`
– existiert die Zieldatei bereits, wird eine nummerierte Kopie angelegt
(`Rechnung (2).pdf`). Ein tatsächliches Überschreiben verlangt die ausdrückliche
Angabe `OverwriteBehavior.Overwrite` durch den Aufrufer.

**Original bleibt unverändert.** Die Eingangs-PDF wird in der gesamten Pipeline
ausschließlich gelesen (`docs/SPECIFICATION.md`, Abschnitt 4 und 8;
`docs/ARCHITECTURE.md`, Abschnitt 3). `PdfPreflightService`
(`src/EInvoiceSender.Infrastructure/PdfA/PdfPreflightService.cs`) öffnet die
Datei nur zur Analyse.

**Temporäre Dateien.** `TemporaryWorkspace`
(`src/EInvoiceSender.Infrastructure/Storage/FileStorage.cs`) legt für jeden
Vorgang ein eigenes Arbeitsverzeichnis unterhalb von `Path.GetTempPath()` an
(**S8**). `Dispose()` löscht dieses Verzeichnis rekursiv – auch dann, wenn ein
Fehler oder ein Benutzerabbruch den Vorgang beendet hat, weil der aufrufende
Code diesen Aufräumpfad im `finally` einer `using`-Anweisung sicherstellt
(`docs/ARCHITECTURE.md`, Abschnitt 6). Fehler beim Löschen selbst
(`IOException`, `UnauthorizedAccessException`) werden bewusst geschluckt, „damit
beim Aufräumen nichts mehr schiefgehen kann".

---

## Prozessausführung

`ProcessRunner` (`src/EInvoiceSender.Infrastructure/Process/ProcessRunner.cs`,
**S5**) ist der einzige Ort, an dem externe Prüfwerkzeuge (Mustang-CLI)
gestartet werden:

- Argumente ausschließlich über `ProcessStartInfo.ArgumentList`, nie als
  zusammengesetzte Kommandozeile – Dateinamen mit Leerzeichen oder
  Anführungszeichen können nichts auslösen.
- `UseShellExecute = false` – keine Shell, also keine Shell-Sonderzeichen.
- Jeder Aufruf verlangt ein Zeitlimit (`ArgumentOutOfRangeException`, falls
  `timeout <= TimeSpan.Zero`). Läuft die Zeit ab, wird der komplette
  Prozessbaum beendet (`process.Kill(entireProcessTree: true)` in
  `KillQuietly`).
- stdout und stderr werden vollständig und **nebenläufig** gelesen
  (`OutputDataReceived`/`ErrorDataReceived`, `BeginOutputReadLine`/
  `BeginErrorReadLine`). Würde nur eine der beiden Leitungen gelesen, könnte
  der Kindprozess blockieren, sobald die andere ihren Puffer füllt.
- Ein Abbruch durch den Benutzer (`CancellationToken`) beendet den Prozess über
  denselben Pfad wie ein Zeitlimit.

Architekturregel **A5** (`AGENTS.md`) verbietet zusätzlich jeden
`Process.Start`-Aufruf aus ViewModels; externe Prozesse laufen ausschließlich
hinter einem Port in `Infrastructure`/`Validation`.

---

## Geheimnisse und Protokollierung

- **Keine Kennwörter.** `JsonSettingsStore`
  (`src/EInvoiceSender.Infrastructure/Settings/JsonSettingsStore.cs`) speichert
  grundsätzlich keine Kennwörter – „die Anwendung braucht keine".
- **IBAN per DPAPI.** Die IBAN ist die einzige wirklich schutzwürdige Angabe in
  der Firmenvorlage. Unter Windows wird sie mit
  `System.Security.Cryptography.ProtectedData` im Geltungsbereich des
  angemeldeten Benutzers (`DataProtectionScope.CurrentUser`) verschlüsselt
  abgelegt (Präfix `dpapi:`). Ohne DPAPI-Unterstützung (Linux/macOS, im Produkt
  nicht vorgesehen) wird die IBAN **nicht** gespeichert statt still im Klartext
  – `SupportsProtectedStorage` meldet diesen Zustand, die Oberfläche weist
  darauf hin (**S7**).
- **IBAN-Maskierung.** `Iban.Mask`/`Iban.ToMaskedString`
  (`src/EInvoiceSender.Domain/Values/Iban.cs`) zeigen nur die ersten vier und
  die letzten zwei Zeichen, der Rest wird durch `*` ersetzt; bei sechs oder
  weniger Zeichen wird vollständig maskiert. Diese Maskierung greift auch beim
  Fehlertext einer ungültigen IBAN (`Iban.Parse`).
- **Keine Zugangsdaten im Repository**, auch nicht in Testdaten (**S7**);
  Testdaten sind künstlich erzeugt, keine echten Personendaten
  (`AGENTS.md`, Definition of Done, Punkt 6).
- Logs sollen E-Mail-Adressen, IBAN, Steuernummern und Dateiinhalte maskieren
  (**S6**, `LogMasking`).

---

## Shell-Aufrufe

`WindowsShellService`
(`src/EInvoiceSender.Desktop/Services/WindowsShellService.cs`) ist der einzige
Ort im Programm, an dem `UseShellExecute = true` steht. Für `OpenUriAsync` sind
ausschließlich die Schemata `mailto`, `http`, `https` und `file` zugelassen –
jedes andere Schema wird abgelehnt und protokolliert (`LogSchemeRejected`).
Übergeben werden laut Klassendokumentation ausschließlich Pfade, die die
Anwendung selbst erzeugt hat, beziehungsweise vom Benutzer ausgewählte Dateien
– nie eine Zeichenkette aus einer Rechnung oder aus fremdem XML.

---

## Abhängigkeiten

- **Central Package Management.** Alle Versionen sind in
  `Directory.Packages.props` zentral gepinnt, zur Laufzeit wird nichts
  nachgeladen (`docs/DEPENDENCIES.md`).
- **Gepinnte Versionen** für alle Produktiv- und Testabhängigkeiten, mit
  Lizenz, Rolle und Wartungsrisiko bewertet in `docs/DEPENDENCIES.md`.
- **Prüfsumme beim Validator-Download.** `build/fetch-validators.sh` lädt die
  Mustang-CLI von Maven Central und vergleicht die SHA-256-Prüfsumme
  (`verify_sha256`) gegen den im Skript gepinnten Wert; bei Abweichung wird die
  Datei gelöscht und das Skript bricht ab. Zusätzlich wird ein
  Funktionsnachweis geführt (`java -jar ... --help`), damit ein nicht
  ausführbares Werkzeug beim Beschaffen auffällt, nicht erst im Test.
- Ausgeschlossen sind AGPL-Bibliotheken im Anwendungsprozess, kommerziell
  lizenzierte Bibliotheken und jede Bibliothek oder API, die Rechnungsdaten an
  einen externen Dienst überträgt (`AGENTS.md`, Abschnitt 6).

---

## Bekannte Restrisiken

Ehrlich benannt, nicht beschönigt:

- **Keine Codesignierung im MVP.** `docs/PLAN.md` (M8.6) sieht lediglich die
  „Vorbereitung für Codesignierung" vor; ein signierter Installer existiert
  nicht.
- **Kein Sicherheitsaudit durch Dritte.** Die hier beschriebenen Maßnahmen
  sind durch Code und Tests belegt, aber nicht von einer unabhängigen Stelle
  geprüft worden. `docs/PLAN.md` (M9.3) sieht ein Sicherheitsreview „ohne
  kritische Befunde" als Teil der Endabnahme vor; dieser Meilenstein ist laut
  `docs/STATUS.md` noch offen.
- **veraPDF/Mustang nur optional installiert.** Die externen Validatoren
  laufen als getrennter Prozess und müssen über `build/fetch-validators.sh`
  eigens beschafft werden. Ohne sie prüft die Anwendung PDF/A nur strukturell,
  also nur eine Teilmenge dessen, was veraPDF prüft
  (`docs/DECISIONS.md`, ADR-0004). Der Validierungsbericht weist diesen
  Zustand ausdrücklich als Warnung aus.
- **UI-Laufzeitprüfung ausstehend.** Ob die beschriebenen Schutzmaßnahmen auch
  im Zusammenspiel mit echten Windows-Dialogen und Dateiauswahl greifen, ist
  laut `docs/STATUS.md` noch nicht auf einem echten Windows-System geprüft
  (M9).
- **Installer ungeprüft.** Neuinstallation, Upgrade und Deinstallation des
  WiX-Installers sind laut `docs/STATUS.md` und `docs/DECISIONS.md`
  (ADR-0011) in dieser Umgebung nicht ausführbar und daher ungeprüft.

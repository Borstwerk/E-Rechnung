namespace EInvoiceSender.Core.Zugferd;

/// <summary>
/// Wie eine fremde XML beim Lesen eingeordnet wurde.
///
/// **Warum es diesen Aufzählungstyp gibt.** <see cref="CiiInvoiceReader.ReadEcho"/>
/// liefert in drei sehr verschiedenen Fällen dasselbe <see langword="null"/>:
/// wenn die Bytes gar keine wohlgeformte XML sind, wenn sie eine XML sind, aber
/// kein CII-Dokument, und wenn das Wurzelelement zwar stimmt, die
/// Dokumentangaben aber fehlen. Für die Erzeugung genügte das – dort ist jedes
/// dieser Ergebnisse gleichbedeutend mit „die Gegenprüfung schlägt fehl“.
///
/// Für die Prüfung einer fremden Rechnung ist es das Gegenteil von genügend:
/// Der Anwender bekommt sonst denselben Satz zu lesen, gleich ob seine Datei
/// beschädigt ist oder ob sie schlicht ein anderes Format enthält. Das sind
/// verschiedene Probleme mit verschiedenen nächsten Schritten.
/// </summary>
public enum CiiStructureStatus
{
    /// <summary>
    /// Die XML ist wohlgeformt und trägt die erwartete CII-Struktur. Das ist
    /// eine Aussage über die <b>Struktur</b>, ausdrücklich keine über die
    /// Normkonformität des Inhalts.
    /// </summary>
    Cii,

    /// <summary>Der Anhang enthält keine Bytes.</summary>
    Empty,

    /// <summary>
    /// Der Anhang überschreitet die Größengrenze aus
    /// <see cref="Security.SecureXml.MaxXmlSizeInBytes"/> und wird deshalb
    /// gar nicht erst geparst.
    /// </summary>
    TooLarge,

    /// <summary>
    /// Die Bytes sind keine wohlgeformte XML – oder sie verwenden Mittel, die
    /// der abgesicherte Leser ablehnt, etwa eine DTD.
    /// </summary>
    NotWellFormed,

    /// <summary>
    /// Wohlgeformte XML, aber keine Cross-Industry-Invoice, die diese
    /// Anwendung auswerten kann. Etwa UBL oder ein CII-Dokument ohne
    /// verwertbare Dokumentangaben.
    /// </summary>
    NotCii,
}

/// <summary>
/// Die Kerndaten einer fremden CII-Rechnung, so wie sie in der Datei stehen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Das ist ausdrücklich keine <see cref="Models.Invoice"/>.</b> Das
/// Domänenmodell bildet ab, was BorstWerk erzeugen kann, und setzt dabei
/// Gültigkeit voraus: Pflichtangaben sind nicht optional, Codes sind
/// geprüfte Werttypen. Eine fremde Rechnung darf beides verletzen – genau
/// deshalb wird sie ja geprüft. Sie in das Domänenmodell zu zwingen hieße,
/// entweder beim Einlesen zu scheitern oder stillschweigend zu reparieren;
/// beides verlöre den Befund, um den es geht.
/// </para>
/// <para>
/// Deshalb ist hier alles optional und alles roher Text beziehungsweise eine
/// rohe Zahl. Fehlt eine Angabe, steht <see langword="null"/> – und das ist
/// eine Feststellung, keine Bewertung.
/// </para>
/// </remarks>
/// <param name="ProfileId">Profilkennung aus dem Dokumentkontext.</param>
/// <param name="InvoiceNumber">Rechnungsnummer (BT-1).</param>
/// <param name="IssueDate">Rechnungsdatum (BT-2).</param>
/// <param name="TypeCode">Rechnungsart (BT-3), roh als Text.</param>
/// <param name="Currency">Währung (BT-5), roh als Text.</param>
/// <param name="SellerIdentifier">Verkäuferkennung (BT-29).</param>
/// <param name="LineTotal">Summe der Positionen (BT-106).</param>
/// <param name="TaxBasisTotal">Nettosumme (BT-109).</param>
/// <param name="TaxTotal">Gesamtsteuer (BT-110).</param>
/// <param name="GrandTotal">Bruttosumme (BT-112).</param>
/// <param name="DuePayableAmount">Offener Zahlbetrag (BT-115).</param>
/// <param name="LineCount">Anzahl der Positionen.</param>
public sealed record CiiInvoiceSummary(
    string? ProfileId,
    string? InvoiceNumber,
    DateOnly? IssueDate,
    string? TypeCode,
    string? Currency,
    string? SellerIdentifier,
    decimal? LineTotal,
    decimal? TaxBasisTotal,
    decimal? TaxTotal,
    decimal? GrandTotal,
    decimal? DuePayableAmount,
    int LineCount);

/// <summary>
/// Ergebnis des Einlesens einer fremden Rechnungs-XML.
/// </summary>
/// <param name="Status">Wie die Datei eingeordnet wurde.</param>
/// <param name="Summary">
/// Die gelesenen Kerndaten. Nur bei <see cref="CiiStructureStatus.Cii"/>
/// gesetzt – in allen anderen Fällen gibt es nichts zu berichten, und ein
/// halb gefülltes Ergebnis wäre irreführend.
/// </param>
public sealed record CiiInspection(CiiStructureStatus Status, CiiInvoiceSummary? Summary)
{
    /// <summary>Ein Ergebnis ohne Daten für den angegebenen Grund.</summary>
    public static CiiInspection Failed(CiiStructureStatus status) => new(status, null);

    /// <summary>Ein erfolgreich gelesenes CII-Dokument.</summary>
    public static CiiInspection Read(CiiInvoiceSummary summary) => new(CiiStructureStatus.Cii, summary);
}

/// <summary>
/// Liest eine <b>fremde</b> Rechnungs-XML und sagt dabei, woran es lag, wenn es
/// nicht ging.
///
/// Bewusst getrennt von <see cref="Services.IInvoiceXmlReader"/>: Jener Anschluss
/// gehört zur Erzeugung und beantwortet „steht in der Datei, die wir gerade
/// geschrieben haben, das Erwartete?“. Hier lautet die Frage „was ist das
/// überhaupt für eine Datei?“ – und die Antwort „nichts Brauchbares“ muss
/// begründet sein.
///
/// Gelesen wird ausschließlich über
/// <see cref="Security.SecureXml"/>: keine DTD, keine externen Entitäten,
/// Größen- und Tiefengrenzen bleiben in Kraft.
/// </summary>
public interface ICiiInvoiceInspector
{
    /// <summary>
    /// Liest die Bytes eines Rechnungsanhangs. Wirft nicht – ein unbrauchbarer
    /// Anhang ist der Normalfall dieser Anwendung und kein Programmfehler.
    /// </summary>
    CiiInspection Inspect(byte[] xml);
}

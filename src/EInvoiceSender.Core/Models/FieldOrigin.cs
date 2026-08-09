namespace EInvoiceSender.Core.Models;

/// <summary>
/// Woher der Inhalt eines Formularfeldes stammt.
///
/// Die Oberflaeche zeigt das an jedem Feld an. Der Anwender soll auf einen
/// Blick sehen, was er selbst getippt hat und was die Anwendung vorgeschlagen
/// hat – und bei Letzterem, wie verlaesslich der Vorschlag ist.
/// </summary>
public enum FieldOrigin
{
    /// <summary>Vom Anwender eingegeben oder geaendert.</summary>
    Manual,

    /// <summary>Aus der gespeicherten Firmenvorlage uebernommen.</summary>
    Template,

    /// <summary>Aus dem PDF-Text gelesen, Zuordnung eindeutig.</summary>
    DetectedReliably,

    /// <summary>
    /// Aus dem PDF-Text gelesen, Zuordnung aber nicht eindeutig. Diese Felder
    /// muessen besonders geprueft werden.
    /// </summary>
    DetectedUncertain,
}

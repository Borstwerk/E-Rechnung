namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Ein aus dem PDF-Text gelesener Wert samt Begründung.
///
/// Die Begründung ist kein Beiwerk: Der Anwender soll nachvollziehen können,
/// **warum** die Anwendung diesen Wert vorschlägt, und ihn dadurch schneller
/// bestätigen oder verwerfen können.
/// </summary>
/// <param name="Value">Der gelesene Wert.</param>
/// <param name="Confidence">Wie sicher die Zuordnung ist.</param>
/// <param name="SourceText">Die Textzeile, aus der er stammt.</param>
/// <param name="Reason">Warum er so zugeordnet wurde.</param>
public sealed record DetectedValue<T>(
    T Value,
    DetectionConfidence Confidence,
    string? SourceText = null,
    string? Reason = null)
{
    /// <summary>
    /// Darf dieser Wert das Formular vorausfuellen? Unsichere Werte werden
    /// angezeigt, aber nie stillschweigend eingetragen.
    /// </summary>
    public bool IsUsable => Confidence >= DetectionConfidence.Medium;
}

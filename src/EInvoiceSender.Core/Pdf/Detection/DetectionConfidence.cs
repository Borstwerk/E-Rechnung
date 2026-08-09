namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Wie sicher ist ein aus der PDF gelesener Wert?
///
/// Die Stufe entscheidet darüber, was mit dem Wert geschieht: Nur
/// <see cref="High"/> und <see cref="Medium"/> fuellen das Formular vor,
/// und beide werden dort sichtbar gekennzeichnet. <see cref="Low"/> wird
/// angezeigt, aber nie übernommen.
/// </summary>
public enum DetectionConfidence
{
    /// <summary>
    /// Der Wert passt zwar zum Muster, aber der Zusammenhang ist unklar.
    /// Wird nicht ins Formular übernommen.
    /// </summary>
    Low,

    /// <summary>
    /// Der Wert steht in einem plausiblen Zusammenhang, ist aber nicht
    /// eindeutig. Wird übernommen und zur Prüfung gekennzeichnet.
    /// </summary>
    Medium,

    /// <summary>
    /// Der Wert steht unmittelbar hinter einem eindeutigen Schlüsselwort
    /// oder wurde zusätzlich rechnerisch bestätigt (etwa eine IBAN mit
    /// gültiger Prüfsumme).
    /// </summary>
    High,
}

namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>
/// Wie sicher ist ein aus der PDF gelesener Wert?
///
/// Die Stufe entscheidet darueber, was mit dem Wert geschieht: Nur
/// <see cref="High"/> und <see cref="Medium"/> fuellen das Formular vor,
/// und beide werden dort sichtbar gekennzeichnet. <see cref="Low"/> wird
/// angezeigt, aber nie uebernommen.
/// </summary>
public enum DetectionConfidence
{
    /// <summary>
    /// Der Wert passt zwar zum Muster, aber der Zusammenhang ist unklar.
    /// Wird nicht ins Formular uebernommen.
    /// </summary>
    Low,

    /// <summary>
    /// Der Wert steht in einem plausiblen Zusammenhang, ist aber nicht
    /// eindeutig. Wird uebernommen und zur Pruefung gekennzeichnet.
    /// </summary>
    Medium,

    /// <summary>
    /// Der Wert steht unmittelbar hinter einem eindeutigen Schluesselwort
    /// oder wurde zusaetzlich rechnerisch bestaetigt (etwa eine IBAN mit
    /// gueltiger Pruefsumme).
    /// </summary>
    High,
}

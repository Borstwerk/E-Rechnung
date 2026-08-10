namespace EInvoiceSender.Core.Pdf;

/// <summary>
/// Das Ergebnis des Rasterwegs: die fertige Datei und das, was über ihre
/// Entstehung nachprüfbar sein muss.
/// </summary>
/// <param name="PdfBytes">Die fertige PDF/A-3b-Datei mit eingebetteter Rechnungs-XML.</param>
/// <param name="Dpi">Die verwendete Auflösung.</param>
/// <param name="Pages">Je Originalseite eine Angabe – Grundlage der Nachprüfung.</param>
public sealed record RasterizedPdfResult(
    byte[] PdfBytes,
    int Dpi,
    IReadOnlyList<RasterizedPageInfo> Pages)
{
    /// <summary>Die Seitenzahl. Sie muss der des Originals entsprechen.</summary>
    public int PageCount => Pages.Count;
}

/// <summary>
/// Eine einzelne übernommene Seite.
///
/// Die Größe in Punkt ist die **sichtbare** Größe der Originalseite: PDFium
/// rechnet einen Eintrag <c>/Rotate</c> bereits ein und meldet eine um 90 Grad
/// gedrehte Hochformatseite als Querformat. Genau diese Größe bekommt die neue
/// Seite, und genau so sieht sie dann auch aus.
/// </summary>
/// <param name="WidthInPoints">Sichtbare Breite der Originalseite in Punkt.</param>
/// <param name="HeightInPoints">Sichtbare Höhe der Originalseite in Punkt.</param>
/// <param name="PixelWidth">Breite des gerenderten Bildes.</param>
/// <param name="PixelHeight">Höhe des gerenderten Bildes.</param>
public sealed record RasterizedPageInfo(
    double WidthInPoints,
    double HeightInPoints,
    int PixelWidth,
    int PixelHeight)
{
    /// <summary>Hoch- oder Querformat – abgeleitet, nicht geraten.</summary>
    public bool IsLandscape => WidthInPoints > HeightInPoints;
}

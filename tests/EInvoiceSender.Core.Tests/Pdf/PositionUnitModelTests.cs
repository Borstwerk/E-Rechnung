using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.Pdf;

/// <summary>
/// Slice C1 – das interne Modell muss „keine Einheit angegeben“ ausdrücken
/// können.
///
/// **Warum das eine eigene Zusicherung ist:** Bis Phase B kannte die Erkennung
/// nur zwei Zustände – Einheit verstanden oder Tabelle verworfen. Reale
/// Rechnungen kennen einen dritten: Die Rechnung nennt schlicht keine
/// Mengeneinheit. Das ist keine unsichere Erkennung, sondern eine sichere
/// Aussage über eine Lücke im Dokument.
///
/// Diese drei Fälle dürfen nie zusammenfallen:
///
/// * Einheit vorhanden und verstanden → Code,
/// * Einheit im Dokument nicht vorhanden → <see langword="null"/>,
/// * Einheit vorhanden, aber nicht verstanden → Tabelle verwerfen.
///
/// Ein Platzhalter wie <c>"XXX"</c> oder ein stiller <c>"C62"</c> würde den
/// zweiten und den dritten Fall miteinander verwechseln, und zwar in genau der
/// Richtung, die eine falsche Rechnung erzeugt.
/// </summary>
public sealed class PositionUnitModelTests
{
    [Fact]
    public void EinePositionOhneAngegebeneEinheitIstDarstellbar()
    {
        DetectedInvoiceLine line = Line(unitCode: null);

        Assert.Null(line.UnitCode);
        Assert.Equal(3m, line.Quantity);
    }

    [Fact]
    public void EineVerstandeneEinheitBleibtErhalten()
        => Assert.Equal("HUR", Line(unitCode: "HUR").UnitCode);

    /// <summary>
    /// Die fehlende Einheit hindert den internen Transport nicht. Eine
    /// erkannte Tabelle bleibt ein Fund, auch wenn die Rechnung keine
    /// Mengeneinheit nennt.
    /// </summary>
    [Fact]
    public void EinePositionOhneEinheitErreichtDasErkennungsergebnis()
    {
        var detection = new InvoiceDetectionResult
        {
            HasUsableText = true,
            Lines = [Line(unitCode: null)],
        };

        Assert.Null(Assert.Single(detection.Lines).UnitCode);
        Assert.True(detection.HasAnything);
    }

    private static DetectedInvoiceLine Line(string? unitCode) => new(
        1, "Beratung", null, 3m, unitCode, 100.00m, 300.00m,
        VatCategory.StandardRate, 19m, []);
}

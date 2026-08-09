namespace EInvoiceSender.Core.Pdf.Detection;

/// <summary>Eine erkannte Rechnungsposition.</summary>
public sealed record DetectedLine(
    int Number,
    string Description,
    decimal? Quantity,
    string? Unit,
    decimal? NetUnitPrice,
    decimal? VatRate,
    decimal? LineTotal);

/// <summary>Erkannte Angaben zu einer Partei.</summary>
public sealed record DetectedParty
{
    public DetectedValue<string>? Name { get; init; }
    public DetectedValue<string>? Street { get; init; }
    public DetectedValue<string>? PostalCode { get; init; }
    public DetectedValue<string>? City { get; init; }
    public DetectedValue<string>? Country { get; init; }
    public DetectedValue<string>? VatId { get; init; }
    public DetectedValue<string>? TaxNumber { get; init; }
    public DetectedValue<string>? Email { get; init; }

    /// <summary>Wurde ueberhaupt etwas erkannt?</summary>
    public bool HasAnything =>
        Name is not null || Street is not null || PostalCode is not null || City is not null
        || VatId is not null || TaxNumber is not null || Email is not null;
}

/// <summary>Erkannte Summen aus der PDF.</summary>
public sealed record DetectedTotals
{
    public DetectedValue<decimal>? Net { get; init; }
    public DetectedValue<decimal>? Tax { get; init; }
    public DetectedValue<decimal>? Gross { get; init; }
    public DetectedValue<decimal>? Payable { get; init; }

    /// <summary>Die im Dokument gefundenen Steuersaetze.</summary>
    public IReadOnlyList<DetectedValue<decimal>> VatRates { get; init; } = [];
}

/// <summary>
/// Alles, was aus einer PDF gelesen werden konnte.
///
/// Dieses Ergebnis ist **kein** Rechnungsmodell und laesst sich auch nicht in
/// eines verwandeln. Es fuellt ausschliesslich das Eingabeformular vor. Der
/// Weg lautet immer:
///
/// <c>PDF → InvoiceDetectionResult → InvoiceDraft → Pruefung durch den
/// Menschen → Invoice</c>
///
/// Damit ist bereits durch die Bauart ausgeschlossen, dass ein gelesener Wert
/// ungeprueft in der E-Rechnung landet.
/// </summary>
public sealed record InvoiceDetectionResult
{
    /// <summary>Enthielt die PDF ueberhaupt auswertbaren Text?</summary>
    public bool HasUsableText { get; init; }

    /// <summary>Anzahl ausgewerteter Seiten.</summary>
    public int PageCount { get; init; }

    // --- Dokument ----------------------------------------------------------
    public DetectedValue<string>? InvoiceNumber { get; init; }
    public DetectedValue<DateOnly>? IssueDate { get; init; }
    public DetectedValue<DateOnly>? DeliveryDate { get; init; }
    public DetectedValue<DateOnly>? BillingPeriodStart { get; init; }
    public DetectedValue<DateOnly>? BillingPeriodEnd { get; init; }
    public DetectedValue<DateOnly>? DueDate { get; init; }
    public DetectedValue<string>? PaymentTerms { get; init; }
    public DetectedValue<string>? Currency { get; init; }

    // --- Parteien ----------------------------------------------------------
    public DetectedParty Seller { get; init; } = new();
    public DetectedParty Buyer { get; init; } = new();

    // --- Bankverbindung ----------------------------------------------------
    public DetectedValue<string>? Iban { get; init; }
    public DetectedValue<string>? Bic { get; init; }

    // --- Summen ------------------------------------------------------------
    public DetectedTotals Totals { get; init; } = new();

    /// <summary>
    /// Erkannte Positionen. Bewusst konservativ: Bei unklarer Tabellenstruktur
    /// bleibt diese Liste lieber leer, als eine falsche Position als sicher
    /// auszugeben.
    /// </summary>
    public IReadOnlyList<DetectedLine> Lines { get; init; } = [];

    /// <summary>Wie sicher die Positionserkennung insgesamt ist.</summary>
    public DetectionConfidence LinesConfidence { get; init; } = DetectionConfidence.Low;

    /// <summary>Ein Ergebnis fuer eine Datei ohne auswertbaren Text.</summary>
    public static InvoiceDetectionResult WithoutText { get; } = new() { HasUsableText = false };

    /// <summary>Wurde ueberhaupt etwas Brauchbares gefunden?</summary>
    public bool HasAnything =>
        InvoiceNumber is not null || IssueDate is not null || Totals.Gross is not null
        || Seller.HasAnything || Buyer.HasAnything || Lines.Count > 0;
}

namespace EInvoiceSender.Core.Pdf.Detection;

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

    /// <summary>Wurde überhaupt etwas erkannt?</summary>
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

    /// <summary>Die im Dokument gefundenen Steuersätze.</summary>
    public IReadOnlyList<DetectedValue<decimal>> VatRates { get; init; } = [];
}

/// <summary>
/// Alles, was aus einer PDF gelesen werden konnte.
///
/// Dieses Ergebnis ist **kein** Rechnungsmodell und lässt sich auch nicht in
/// eines verwandeln. Es fuellt ausschließlich das Eingabeformular vor. Der
/// Weg lautet immer:
///
/// <c>PDF → InvoiceDetectionResult → InvoiceDraft → Prüfung durch den
/// Menschen → Invoice</c>
///
/// Damit ist bereits durch die Bauart ausgeschlossen, dass ein gelesener Wert
/// ungeprüft in der E-Rechnung landet.
/// </summary>
public sealed record InvoiceDetectionResult
{
    /// <summary>Enthielt die PDF überhaupt auswertbaren Text?</summary>
    public bool HasUsableText { get; init; }

    /// <summary>Anzahl ausgewerteter Seiten.</summary>
    public int PageCount { get; init; }

    // --- Dokument ----------------------------------------------------------
    public DetectedValue<string>? InvoiceNumber { get; init; }
    public DetectedValue<DateOnly>? IssueDate { get; init; }
    public DetectedValue<DateOnly>? DeliveryDate { get; init; }
    public DetectedValue<DateOnly>? DueDate { get; init; }
    public DetectedValue<string>? Currency { get; init; }

    // --- Parteien ----------------------------------------------------------
    public DetectedParty Seller { get; init; } = new();
    public DetectedParty Buyer { get; init; } = new();

    // --- Bankverbindung ----------------------------------------------------
    public DetectedValue<string>? Iban { get; init; }
    public DetectedValue<string>? Bic { get; init; }

    // --- Summen ------------------------------------------------------------
    public DetectedTotals Totals { get; init; } = new();

    /// <summary>Ein Ergebnis für eine Datei ohne auswertbaren Text.</summary>
    public static InvoiceDetectionResult WithoutText { get; } = new() { HasUsableText = false };

    /// <summary>Wurde überhaupt etwas Brauchbares gefunden?</summary>
    public bool HasAnything =>
        InvoiceNumber is not null || IssueDate is not null || Totals.Gross is not null
        || Seller.HasAnything || Buyer.HasAnything;
}

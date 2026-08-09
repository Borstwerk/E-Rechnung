namespace EInvoiceSender.Core.Tests.Support;

/// <summary>
/// Die Rechnung aus dem manuellen Testlauf, als PDF nachgebaut.
///
/// Sie liegt hier und nicht in einem einzelnen Test, weil mehrere Tests
/// dieselbe Vorlage brauchen: die Spaltentrennung des Extraktors, die
/// Erkennung selbst und der Weg bis in das Eingabeformular. Ändert sich die
/// Vorlage, ändert sie sich für alle drei zugleich.
///
/// Aufbau wie im Original: links der Empfängerblock, rechts daneben die
/// Rechnungsdaten auf denselben Grundlinien, darunter Positionen, Summen und
/// die Bankverbindung. Alle Angaben sind erfunden.
/// </summary>
public static class TestInvoice
{
    /// <summary>Die erwarteten Kerndaten – die Sollwerte des Testlaufs.</summary>
    public const string InvoiceNumber = "RE-2026-0815";

    public const string BuyerName = "Nordlicht Handel GmbH";
    public const string BuyerPostalCode = "20095";
    public const string BuyerCity = "Hamburg";
    public const string Currency = "EUR";
    public const string Iban = "DE89370400440532013000";
    public const string Bic = "COBADEFFXXX";

    public static DateOnly IssueDate { get; } = new(2026, 8, 9);

    public static DateOnly DeliveryDate { get; } = new(2026, 8, 8);

    public static DateOnly DueDate { get; } = new(2026, 8, 23);

    public const decimal Net = 600.00m;
    public const decimal Tax = 114.00m;
    public const decimal Gross = 714.00m;
    public const decimal VatRate = 19m;

    /// <summary>Erzeugt die PDF.</summary>
    public static byte[] CreatePdf() => TextPdfBuilder.CreateTwoColumn(
        left:
        [
            "Muster IT GmbH",
            "Musterstraße 10",
            "18055 Rostock",
            "USt-IdNr.: DE123456789",
            string.Empty,
            "Rechnung an",
            BuyerName,
            "Hafenstraße 22",
            $"{BuyerPostalCode} {BuyerCity}",
            "Deutschland",
            "USt-IdNr.: DE987654321",
        ],
        right:
        [
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            $"Rechnungsnummer: {InvoiceNumber}",
            "Rechnungsdatum: 09.08.2026",
            "Leistungsdatum: 08.08.2026",
            "Fällig am: 23.08.2026",
            $"Währung: {Currency}",
        ],
        below:
        [
            "Pos Bezeichnung Menge Einheit Einzelpreis Betrag",
            "1 IT-Beratung 4 Std 100,00 400,00",
            "2 Projektleitung 2 Std 100,00 200,00",
            "Nettobetrag 600,00 EUR",
            "Umsatzsteuer 19 % 114,00 EUR",
            "Gesamtbetrag 714,00 EUR",
            "Zahlbetrag 714,00 EUR",
            "IBAN DE89 3704 0044 0532 0130 00",
            $"BIC {Bic}",
        ]);
}

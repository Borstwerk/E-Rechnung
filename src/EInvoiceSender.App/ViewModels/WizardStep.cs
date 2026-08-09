namespace EInvoiceSender.App.ViewModels;

/// <summary>Die fünf Schritte des Ablaufs.</summary>
public enum WizardStep
{
    /// <summary>Schritt 1: PDF auswählen.</summary>
    SelectPdf = 1,

    /// <summary>Schritt 2: Rechnungsdaten erfassen.</summary>
    EnterData = 2,

    /// <summary>Schritt 3: Kontrollansicht und Bestätigung.</summary>
    Review = 3,

    /// <summary>Schritt 4: Erzeugen und prüfen.</summary>
    Generate = 4,

    /// <summary>Schritt 5: Speichern und versenden.</summary>
    Finish = 5,
}

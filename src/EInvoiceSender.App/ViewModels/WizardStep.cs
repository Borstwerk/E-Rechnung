namespace EInvoiceSender.App.ViewModels;

/// <summary>Die fuenf Schritte des Ablaufs.</summary>
public enum WizardStep
{
    /// <summary>Schritt 1: PDF auswaehlen.</summary>
    SelectPdf = 1,

    /// <summary>Schritt 2: Rechnungsdaten erfassen.</summary>
    EnterData = 2,

    /// <summary>Schritt 3: Kontrollansicht und Bestaetigung.</summary>
    Review = 3,

    /// <summary>Schritt 4: Erzeugen und pruefen.</summary>
    Generate = 4,

    /// <summary>Schritt 5: Speichern und versenden.</summary>
    Finish = 5,
}

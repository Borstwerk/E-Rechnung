using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using EInvoiceSender.Presentation.Editing;
using EInvoiceSender.Presentation.ViewModels;
using EInvoiceSender.Validation.Rules;
using Xunit;

namespace EInvoiceSender.Presentation.Tests;

/// <summary>
/// Prueft, dass die Schaltflaechen im richtigen Moment freigegeben werden.
///
/// **Warum dieser Test existiert:** Eine WPF-Schaltflaeche fragt einen
/// <c>RelayCommand</c> **nicht** laufend nach seiner Freigabe. Sie fragt genau
/// einmal beim Binden und danach nur noch, wenn der Befehl
/// <c>CanExecuteChanged</c> meldet. Liest eine Freigabepruefung also eine
/// Eigenschaft, die den Befehl nicht benachrichtigt, bleibt die Schaltflaeche
/// im zuletzt bewerteten Zustand haengen – meist dauerhaft gesperrt.
///
/// Genau das war beim ersten Durchlauf der Fall: "Weiter" blieb gesperrt,
/// obwohl die Statuszeile die PDF als verarbeitbar meldete. <c>CanGoForward</c>
/// liest <c>IsBusy</c>, aber <c>IsBusy</c> benachrichtigte nur "Erzeugen" und
/// "Abbrechen". Die einzige Neubewertung von "Weiter" geschah beim Setzen von
/// <c>PreflightReport</c> – und da war <c>IsBusy</c> noch <c>true</c>.
///
/// **Der entscheidende Kniff:** Ein Test, der einfach <c>CanExecute(null)</c>
/// aufruft, findet diesen Fehler **nie**. Dieser Aufruf wertet die Bedingung
/// jedes Mal frisch aus und liefert deshalb immer die richtige Antwort – auch
/// dann, wenn die Schaltflaeche auf dem Bildschirm seit Minuten falsch aussieht.
/// Geprueft werden muss das *Ereignis*, nicht der Wert. Deshalb bildet
/// <see cref="ButtonSpy"/> das Verhalten eines echten Knopfes nach.
/// </summary>
public sealed class CommandEnablementTests
{
    /// <summary>
    /// Der allgemeine Schutz gegen diese Fehlerklasse: Nach jedem realistischen
    /// Ablauf muss das, was der Anwender sieht, mit der Wahrheit
    /// uebereinstimmen. Der Test kennt die Verdrahtung nicht – er vergleicht
    /// nur Anzeige und Wirklichkeit und findet damit auch kuenftige vergessene
    /// Benachrichtigungen.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ablaeufe))]
    public async Task NachJedemAblaufZeigenDieSchaltflaechenDieWahrheit(
        string bezeichnung, Func<ShellViewModel, Task> ablauf)
    {
        using ShellViewModel viewModel = BuildViewModel();

        // Die Knoepfe werden gebunden, bevor irgendetwas passiert – genau wie
        // beim Anzeigen des Fensters.
        ButtonSpy[] buttons = [.. CommandsOf(viewModel).Select(c => new ButtonSpy(c.Name, c.Command))];

        await ablauf(viewModel);

        foreach (ButtonSpy button in buttons)
        {
            Assert.True(
                button.IsEnabled == button.TrueState,
                $"Nach '{bezeichnung}' zeigt die Schaltflaeche {button.Name} den Zustand "
                + $"'{(button.IsEnabled ? "bedienbar" : "gesperrt")}', richtig waere aber "
                + $"'{(button.TrueState ? "bedienbar" : "gesperrt")}'. Die Freigabepruefung "
                + "liest eine Eigenschaft, die den Befehl nicht benachrichtigt. Abhilfe: "
                + $"[NotifyCanExecuteChangedFor(nameof({button.Name}))] an dieser Eigenschaft "
                + "ergaenzen.");
        }
    }

    public static TheoryData<string, Func<ShellViewModel, Task>> Ablaeufe()
        => new()
        {
            {
                "PDF pruefen",
                vm => vm.SelectPdfAsync("/tmp/beispiel.pdf")
            },
            {
                "PDF pruefen und einen Schritt weiter",
                async vm =>
                {
                    await vm.SelectPdfAsync("/tmp/beispiel.pdf");
                    vm.GoForward();
                }
            },
            {
                "Vorlage laden",
                vm => vm.LoadTemplateAsync(CancellationToken.None)
            },
            {
                "bis zur Kontrollansicht und bestaetigen",
                async vm =>
                {
                    await vm.SelectPdfAsync("/tmp/beispiel.pdf");
                    vm.GoForward();
                    FillValidDraft(vm.Draft);
                    vm.GoForward();
                    vm.ContentMatchConfirmed = true;
                }
            },
            {
                "erzeugen",
                async vm =>
                {
                    await vm.SelectPdfAsync("/tmp/beispiel.pdf");
                    FillValidDraft(vm.Draft);
                    vm.ContentMatchConfirmed = true;
                    await vm.GenerateAsync();
                }
            },
        };

    /// <summary>
    /// Der konkrete Fall aus dem Fehlerbericht, eng gefasst: Nach einer
    /// verarbeitbaren PDF muss "Weiter" auf dem Bildschirm bedienbar werden.
    /// </summary>
    [Fact]
    public async Task NachEinerVerarbeitbarenPdfWirdWeiterBedienbar()
    {
        using ShellViewModel viewModel = BuildViewModel();
        var weiter = new ButtonSpy(nameof(viewModel.GoForwardCommand), viewModel.GoForwardCommand);

        Assert.False(weiter.IsEnabled, "Ohne PDF ist 'Weiter' zu Recht gesperrt.");

        await viewModel.SelectPdfAsync("/tmp/beispiel.pdf");

        Assert.True(viewModel.HasPdf, "Die Testvorgabe meldet die PDF als verarbeitbar.");
        Assert.False(viewModel.IsBusy, "Nach der Pruefung darf nichts mehr laufen.");
        Assert.True(
            weiter.IsEnabled,
            "Die Schaltflaeche 'Weiter' ist gesperrt geblieben, obwohl die PDF verarbeitbar "
            + "ist. Der Anwender kommt damit nicht aus Schritt 1 heraus.");
    }

    private static (string Name, IRelayCommand Command)[] CommandsOf(ShellViewModel viewModel)
        => [.. typeof(ShellViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => typeof(IRelayCommand).IsAssignableFrom(p.PropertyType))
            .Select(p => (p.Name, (IRelayCommand)p.GetValue(viewModel)!))];

    private static ShellViewModel BuildViewModel()
        => new(
            new AsyncStubPreflight(),
            new En16931RuleValidator(),
            new AsyncStubUseCase(),
            new AsyncStubEmailDraftService(),
            new AsyncStubSettingsStore());

    private static void FillValidDraft(InvoiceDraft draft)
    {
        draft.InvoiceNumber = "RE-2026-0001";
        draft.IssueDate = new DateOnly(2026, 3, 15);
        draft.DueDate = new DateOnly(2026, 3, 29);
        draft.SellerName = "Musterbetrieb Beispiel GmbH";
        draft.SellerStreet = "Beispielweg 1";
        draft.SellerPostalCode = "10115";
        draft.SellerCity = "Berlin";
        draft.SellerVatId = "DE123456789";
        draft.SellerEmail = "rechnung@example.invalid";
        draft.BuyerName = "Beispielkunde AG";
        draft.BuyerStreet = "Kundenstrasse 7";
        draft.BuyerPostalCode = "20095";
        draft.BuyerCity = "Hamburg";
        draft.BuyerEmail = "einkauf@example.invalid";
        draft.BankAccountHolder = "Musterbetrieb Beispiel GmbH";
        draft.BankIban = "DE89370400440532013000";
        draft.PaymentTerms = "Zahlbar innerhalb von 14 Tagen.";

        InvoiceLineDraft line = draft.AddLine();
        line.Name = "Beratungsleistung";
        line.Quantity = "10";
        line.Unit = "HUR";
        line.NetUnitPrice = "100,00";
        line.VatRate = "19";
    }
}

/// <summary>
/// Bildet nach, wie eine WPF-Schaltflaeche ihren Zustand fuehrt: Sie fragt beim
/// Binden **einmal** und danach nur noch, wenn der Befehl
/// <c>CanExecuteChanged</c> meldet.
///
/// <see cref="IsEnabled"/> ist damit das, was der Anwender sieht;
/// <see cref="TrueState"/> ist das, was richtig waere. Gehen die beiden
/// auseinander, fehlt eine Benachrichtigung.
/// </summary>
internal sealed class ButtonSpy
{
    private readonly IRelayCommand _command;

    public ButtonSpy(string name, IRelayCommand command)
    {
        Name = name;
        _command = command;

        // Bindungszeitpunkt: einmal fragen.
        IsEnabled = command.CanExecute(null);

        command.CanExecuteChanged += (_, _) => IsEnabled = _command.CanExecute(null);
    }

    /// <summary>Der Name der Befehlseigenschaft, fuer die Fehlermeldung.</summary>
    public string Name { get; }

    /// <summary>Was der Anwender auf dem Bildschirm sieht.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Was tatsaechlich gelten muesste.</summary>
    public bool TrueState => _command.CanExecute(null);
}

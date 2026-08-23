using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Hält die Meldung zur Vorbefüllung dort, wo sie prüfbar ist.
///
/// Der Satz selbst ist im Kern gemessen (<c>PrefillNoticeTests</c>). Dieser
/// Test sichert nur die eine Sache, die dort nicht sichtbar wäre: dass die
/// Oberfläche ihn auch tatsächlich von dort holt. Ein zweiter, im
/// ViewModel zusammengesetzter Satz wäre ungeprüft und würde still
/// auseinanderlaufen – genau so ist die alte Fassung entstanden.
/// </summary>
public sealed class PrefillMessageTests
{
    [Fact]
    public void DieOberflächeSetztDenSatzNichtSelbstZusammen()
    {
        string quelle = Source("InvoiceDataViewModel.cs");

        Assert.Contains("PrefillNotice.Describe(", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("vorausgefüllt.", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("Bitte prüfen Sie besonders", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die alte Zusage „Jeder Wert lässt sich überschreiben“ stimmte nicht:
    /// Die Summen werden gerechnet, nicht getippt. Sie darf nicht zurückkehren.
    /// </summary>
    [Fact]
    public void DieAlteZusageIstVerschwunden()
        => Assert.DoesNotContain(
            "überschreiben", Source("InvoiceDataViewModel.cs"), StringComparison.Ordinal);

    private static string Source(string file)
        => File.ReadAllText(ProjectFiles.With(".cs").Single(p => Path.GetFileName(p) == file));
}

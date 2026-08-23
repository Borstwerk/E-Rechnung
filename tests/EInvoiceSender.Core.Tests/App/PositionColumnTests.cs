using System.Xml.Linq;
using EInvoiceSender.Core.Tests.Support;
using Xunit;

namespace EInvoiceSender.Core.Tests.App;

/// <summary>
/// Slice 6 – die Positionstabelle zeigt die Beschreibung.
///
/// **Warum das nötig ist:** Die Erkennung liest Fortsetzungszeilen einer
/// Position als Beschreibung (BT-154), und der Schreiber trägt sie in die
/// CII-Datei ein. Ohne Spalte stünde damit ein Wert in der E-Rechnung, den
/// der Anwender im Formular nie zu sehen bekommt und deshalb auch nicht
/// bestätigen kann. Genau das darf diese Anwendung nicht: Jeder Wert, der in
/// die Rechnung geht, muss vorher sichtbar und änderbar gewesen sein.
///
/// **Warum nur eine Spalte:** Kein Positionseditor, kein eigener Dialog, kein
/// Umbau der Tabelle. Die Beschreibung ist ein Textfeld wie die Bezeichnung
/// auch.
/// </summary>
public sealed class PositionColumnTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void DieBeschreibungHatEineEigeneSpalte()
    {
        XElement spalte = Column("Beschreibung");

        Assert.Equal(
            "{Binding Description}",
            spalte.Attribute("Binding")?.Value);
    }

    /// <summary>
    /// Anzeigen genügt nicht. Ein Wert, der in die E-Rechnung geht, muss
    /// änderbar sein – sonst ist die Bestätigung durch den Menschen nur
    /// Ansicht.
    /// </summary>
    [Fact]
    public void DieBeschreibungIstBearbeitbar()
        => Assert.NotEqual(
            "True",
            Column("Beschreibung").Attribute("IsReadOnly")?.Value ?? "False");

    /// <summary>
    /// Die übrigen Spalten bleiben, wie sie sind. Diese Änderung ergänzt eine
    /// Spalte; sie baut die Tabelle nicht um.
    /// </summary>
    [Fact]
    public void DieBestehendenSpaltenBleibenUnberührt()
    {
        string[] überschriften =
        [
            .. Grid().Descendants()
                .Where(e => e.Name.LocalName.StartsWith("DataGrid", StringComparison.Ordinal)
                            && e.Attribute("Header") is not null)
                .Select(e => e.Attribute("Header")!.Value),
        ];

        Assert.Equal(
            [
                "Nr.", "Bezeichnung", "Beschreibung", "Menge", "Einheit",
                "Einzelpreis", "Steuersatz %", "Steuerkategorie",
            ],
            überschriften);
    }

    private static XElement Column(string header)
        => Assert.Single(
            Grid().Descendants(),
            e => e.Attribute("Header")?.Value == header);

    private static XElement Grid()
        => Assert.Single(
            View().Descendants(Presentation + "DataGrid"),
            g => g.Attribute(XNamespace.Get(
                "http://schemas.microsoft.com/winfx/2006/xaml") + "Name")?.Value == "Positionen");

    private static XDocument View()
        => XDocument.Load(ProjectFiles
            .With(".xaml")
            .Single(p => Path.GetFileName(p) == "InvoiceDataView.xaml"));
}

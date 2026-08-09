using System.Reflection;
using EInvoiceSender.Core.Models;
using EInvoiceSender.Core.Pdf.Detection;
using Xunit;

namespace EInvoiceSender.Core.Tests.Models;

/// <summary>
/// Prüft, dass „Neue Rechnung“ wirklich eine neue Rechnung beginnt.
///
/// **Der Fehler:** <c>StartOver</c> setzte vier der fünf Schritte zurück – das
/// Eingabeformular nicht. Nach „Neue Rechnung“ standen Rechnungsnummer,
/// Käufer, Datumsangaben, Zahlungsbedingungen und Positionen der vorigen
/// Rechnung noch da. Wer das übersah, verschickte die zweite Rechnung mit der
/// Nummer der ersten – und mit dem Empfänger der ersten.
///
/// Diese Tests prüfen den Entwurf selbst. Dass die Oberfläche ihn beim Beginn
/// einer neuen Rechnung auch zurücksetzt, prüft <c>NewInvoiceResetTests</c>.
/// </summary>
public sealed class InvoiceDraftResetTests
{
    [Fact]
    public void NachDemZurücksetzenIstDasFormularWieNeu()
    {
        InvoiceDraft benutzt = FilledDraft();

        benutzt.Reset();

        string[] abweichend =
        [
            .. from property in WritableProperties()
               let frisch = property.GetValue(new InvoiceDraft())
               let jetzt = property.GetValue(benutzt)
               where !Equals(frisch, jetzt)
               select $"{property.Name}: erwartet {frisch ?? "null"}, war {jetzt ?? "null"}",
        ];

        Assert.True(
            abweichend.Length == 0,
            "Diese Felder tragen nach dem Zurücksetzen noch einen Wert aus der vorigen "
            + $"Rechnung:\n{string.Join("\n", abweichend)}\n\nErgänzen Sie sie in "
            + "InvoiceDraft.RestoreDefaults.");
    }

    /// <summary>
    /// Ohne diese Prüfung wäre der Test oben wertlos: Füllt er in Wahrheit
    /// nichts, vergleicht er zwei frische Entwürfe miteinander.
    /// </summary>
    [Fact]
    public void DerTestFülltVorherTatsächlichJedesFeld()
    {
        InvoiceDraft benutzt = FilledDraft();
        var frisch = new InvoiceDraft();

        string[] unverändert =
        [
            .. from property in WritableProperties()
               where Equals(property.GetValue(frisch), property.GetValue(benutzt))
               select property.Name,
        ];

        Assert.True(
            unverändert.Length == 0,
            $"Diese Felder hat der Test gar nicht erst verändert: {string.Join(", ", unverändert)}.");
    }

    [Fact]
    public void DiePositionenSindAnschließendLeer()
    {
        InvoiceDraft draft = FilledDraft();
        draft.AddLine();
        draft.AddLine();

        draft.Reset();

        Assert.Empty(draft.Lines);
        Assert.Empty(draft.ExemptionReasons);
        Assert.Empty(draft.AllowancesAndCharges);
    }

    /// <summary>
    /// Die Herkunftskennzeichnung gehört zur alten Rechnung. Bliebe sie
    /// stehen, stünde am leeren Formular „aus PDF erkannt“.
    /// </summary>
    [Fact]
    public void DieHerkunftDerAltenRechnungIstVergessen()
    {
        var draft = new InvoiceDraft();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("RE-2026-0815", DetectionConfidence.High),
        });

        draft.BuyerName = "Von Hand erfasst";

        Assert.NotEmpty(draft.Origins);

        draft.Reset();

        Assert.Empty(draft.Origins);
        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.InvoiceNumber)));
        Assert.Equal(FieldOrigin.Default, draft.OriginOf(nameof(draft.BuyerName)));
    }

    /// <summary>
    /// Nach dem Zurücksetzen darf kein Feld als Benutzereingabe gelten – sonst
    /// könnte die Firmenvorlage es nicht mehr befüllen.
    /// </summary>
    [Fact]
    public void EinZurückgesetztesFeldNimmtWiederWerteAn()
    {
        InvoiceDraft draft = FilledDraft();

        draft.Reset();

        DraftPrefiller.Apply(draft, new InvoiceDetectionResult
        {
            HasUsableText = true,
            InvoiceNumber = new DetectedValue<string>("RE-2026-0900", DetectionConfidence.High),
        });

        Assert.Equal("RE-2026-0900", draft.InvoiceNumber);
    }

    /// <summary>Meldet die Änderung, damit die Oberfläche sich neu zeichnet.</summary>
    [Fact]
    public void DasZurücksetzenWirdGemeldet()
    {
        InvoiceDraft draft = FilledDraft();
        var gemeldet = new List<string>();

        draft.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName ?? string.Empty);

        draft.Reset();

        Assert.Contains(nameof(draft.InvoiceNumber), gemeldet);
        Assert.Contains(nameof(draft.BuyerName), gemeldet);
        Assert.Contains(nameof(draft.Origins), gemeldet);
    }

    /// <summary>
    /// Ein Entwurf, in dem jedes Feld einen anderen Wert trägt als im
    /// Anfangszustand. Die Werte werden über die Eigenschaftsliste gesetzt,
    /// nicht einzeln aufgeschrieben: Ein neu hinzugekommenes Feld ist damit von
    /// selbst dabei.
    /// </summary>
    private static InvoiceDraft FilledDraft()
    {
        var draft = new InvoiceDraft();

        foreach (PropertyInfo property in WritableProperties())
        {
            property.SetValue(draft, DifferentValue(property, property.GetValue(draft)));
        }

        return draft;
    }

    private static object? DifferentValue(PropertyInfo property, object? current)
    {
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
        {
            return "Wert aus der vorigen Rechnung";
        }

        if (type == typeof(DateOnly))
        {
            return new DateOnly(2001, 2, 3);
        }

        if (type == typeof(bool))
        {
            return !(bool)(current ?? false);
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().First(v => !Equals(v, current));
        }

        throw new NotSupportedException(
            $"{property.Name} hat den Typ {type.Name}. Ergänzen Sie hier einen abweichenden "
            + "Wert, sonst prüft der Test dieses Feld nicht.");
    }

    private static IEnumerable<PropertyInfo> WritableProperties()
        => typeof(InvoiceDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);
}

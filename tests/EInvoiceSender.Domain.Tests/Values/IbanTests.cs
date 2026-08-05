using EInvoiceSender.Domain.Values;
using Xunit;

namespace EInvoiceSender.Domain.Tests.Values;

/// <summary>
/// Tests fuer <see cref="Iban"/>: Pruefzifferberechnung (ISO 7064 Mod 97-10),
/// Normalisierung, Maskierung und Anzeigeformat.
/// </summary>
public sealed class IbanTests
{
    // Offiziell publizierte Beispiel-IBANs (u. a. aus der IBAN-Registry-Dokumentation).
    // Keine echten Bankdaten.
    public static TheoryData<string, string> GueltigeIbans => new()
    {
        { "DE89370400440532013000", "DE" }, // Deutschland
        { "GB82WEST12345698765432", "GB" }, // Vereinigtes Koenigreich – Buchstaben im Kontoteil
        { "FR1420041010050500013M02606", "FR" }, // Frankreich – Buchstabe im Kontoteil
        { "AT611904300234573201", "AT" }, // Oesterreich
        { "CH9300762011623852957", "CH" }, // Schweiz
        { "NL91ABNA0417164300", "NL" }, // Niederlande
    };

    [Theory]
    [MemberData(nameof(GueltigeIbans))]
    public void TryParse_AkzeptiertGueltigeIbansAusMehrerenLaendern(string wert, string erwartetesLand)
    {
        bool erfolg = Iban.TryParse(wert, out Iban iban);

        Assert.True(erfolg);
        Assert.Equal(wert, iban.Value);
        Assert.Equal(erwartetesLand, iban.CountryPrefix);
    }

    [Fact]
    public void TryParse_AkzeptiertIbanMitLeerzeichenAlsVierergruppen()
    {
        bool erfolg = Iban.TryParse("DE89 3704 0044 0532 0130 00", out Iban iban);

        Assert.True(erfolg);
        Assert.Equal("DE89370400440532013000", iban.Value);
    }

    [Fact]
    public void TryParse_AkzeptiertIbanMitBindestrichen()
    {
        bool erfolg = Iban.TryParse("DE89-3704-0044-0532-0130-00", out Iban iban);

        Assert.True(erfolg);
        Assert.Equal("DE89370400440532013000", iban.Value);
    }

    [Fact]
    public void TryParse_AkzeptiertIbanInKleinbuchstaben()
    {
        bool erfolg = Iban.TryParse("de89370400440532013000", out Iban iban);

        Assert.True(erfolg);
        Assert.Equal("DE89370400440532013000", iban.Value);
    }

    [Fact]
    public void TryParse_FormatvariantenLiefernDasselbeNormalisierteErgebnis()
    {
        Assert.True(Iban.TryParse("DE89370400440532013000", out Iban kanonisch));
        Assert.True(Iban.TryParse("de89 3704 0044 0532 0130 00", out Iban mitLeerzeichenUndKlein));
        Assert.True(Iban.TryParse("DE89-3704-0044-0532-0130-00", out Iban mitBindestrichen));

        Assert.Equal(kanonisch.Value, mitLeerzeichenUndKlein.Value);
        Assert.Equal(kanonisch.Value, mitBindestrichen.Value);
    }

    [Fact]
    public void TryParse_PruefzifferBerechnungBeruecksichtigtBuchstabenImKontoteil_GbBeispiel()
    {
        // GB82WEST... enthaelt mit "WEST" Buchstaben im Kontoteil, die als 10..35
        // in die Mod-97-Rechnung eingehen muessen, damit die Pruefziffer stimmt.
        bool erfolg = Iban.TryParse("GB82WEST12345698765432", out Iban iban);

        Assert.True(erfolg);
    }

    [Fact]
    public void TryParse_PruefzifferBerechnungBeruecksichtigtBuchstabenImKontoteil_FrBeispiel()
    {
        // FR...M02606 enthaelt den Buchstaben 'M' im Kontoteil.
        bool erfolg = Iban.TryParse("FR1420041010050500013M02606", out Iban iban);

        Assert.True(erfolg);
    }

    [Fact]
    public void TryParse_LehntNullAb()
    {
        bool erfolg = Iban.TryParse(null, out Iban iban);

        Assert.False(erfolg);
        Assert.Equal(default, iban);
    }

    [Fact]
    public void TryParse_LehntLeerenStringAb()
    {
        bool erfolg = Iban.TryParse(string.Empty, out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntNurWhitespaceAb()
    {
        bool erfolg = Iban.TryParse("   ", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntZuKurzeIbanAb()
    {
        // Kuerzer als 15 Zeichen (Mindestlaenge, siehe Norwegen).
        bool erfolg = Iban.TryParse("DE8937040044", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntZuLangeIbanAb()
    {
        // Laenger als 34 Zeichen (Hoechstlaenge nach ISO 13616).
        bool erfolg = Iban.TryParse("DE893704004405320130001234567890123", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntFalschePruefzifferAb()
    {
        // Letzte Ziffer einer gueltigen IBAN veraendert (0 -> 1).
        bool erfolg = Iban.TryParse("DE89370400440532013001", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntSonderzeichenAb()
    {
        bool erfolg = Iban.TryParse("DE89370400440532013$00", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntLandNichtZweiBuchstabenAb()
    {
        // Ziffer an Stelle des Laendercodes.
        bool erfolg = Iban.TryParse("D189370400440532013000", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void TryParse_LehntPruefzifferstellenOhneZiffernAb()
    {
        // Buchstaben statt Ziffern an den Pruefzifferstellen (Position 3-4).
        bool erfolg = Iban.TryParse("DEAB370400440532013000", out Iban iban);

        Assert.False(erfolg);
    }

    [Fact]
    public void Parse_LiefertIbanBeiGueltigerEingabe()
    {
        Iban iban = Iban.Parse("DE89370400440532013000");

        Assert.Equal("DE89370400440532013000", iban.Value);
    }

    [Fact]
    public void Parse_WirftFormatExceptionBeiUngueltigerEingabe()
    {
        Assert.Throws<FormatException>(() => Iban.Parse("keine-iban"));
    }

    [Fact]
    public void Parse_ExceptionMessageEnthaeltNichtDieVollstaendigeIban()
    {
        // Sicherheitsanforderung: die Fehlermeldung darf die IBAN nicht im Klartext
        // enthalten, damit sie in Logs/Meldungen nicht landet.
        const string ungueltigeIban = "DE89370400440532013001";

        FormatException exception = Assert.Throws<FormatException>(() => Iban.Parse(ungueltigeIban));

        Assert.DoesNotContain(ungueltigeIban, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mask_ZeigtErsteVierUndLetzteZweiZeichen()
    {
        string? maskiert = Iban.Mask("DE89370400440532013000");

        Assert.Equal("DE89****************00", maskiert);
        Assert.Equal("DE89370400440532013000".Length, maskiert!.Length);
    }

    [Fact]
    public void Mask_LiefertNurSterneBeiKurzemWert()
    {
        // Laenge <= 6 nach Normalisierung: keine Zeichen bleiben sichtbar.
        string? maskiert = Iban.Mask("DE8937");

        Assert.Equal("******", maskiert);
    }

    [Fact]
    public void Mask_LiefertNullBeiNull()
    {
        string? maskiert = Iban.Mask(null);

        Assert.Null(maskiert);
    }

    [Fact]
    public void Mask_LiefertLeerenStringBeiLeererEingabe()
    {
        string? maskiert = Iban.Mask(string.Empty);

        Assert.Equal(string.Empty, maskiert);
    }

    [Fact]
    public void ToDisplayString_GruppiertInVierergruppenMitLeerzeichen()
    {
        Assert.True(Iban.TryParse("DE89370400440532013000", out Iban iban));

        string anzeige = iban.ToDisplayString();

        Assert.Equal("DE89 3704 0044 0532 0130 00", anzeige);
    }

    [Fact]
    public void ToDisplayString_LetzteGruppeDarfKuerzerSein()
    {
        Assert.True(Iban.TryParse("NL91ABNA0417164300", out Iban iban));

        string anzeige = iban.ToDisplayString();

        Assert.Equal("NL91 ABNA 0417 1643 00", anzeige);
        Assert.EndsWith("00", anzeige, StringComparison.Ordinal);
    }

    [Fact]
    public void CountryPrefix_LiefertDieErstenZweiZeichen()
    {
        Assert.True(Iban.TryParse("CH9300762011623852957", out Iban iban));

        Assert.Equal("CH", iban.CountryPrefix);
    }

    [Fact]
    public void ToMaskedString_MaskiertDenEigenenWert()
    {
        Assert.True(Iban.TryParse("AT611904300234573201", out Iban iban));

        Assert.Equal(Iban.Mask(iban.Value), iban.ToMaskedString());
    }
}

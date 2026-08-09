using EInvoiceSender.Core.Storage;
using Xunit;

namespace EInvoiceSender.Core.Tests.Files;

/// <summary>
/// Tests für <see cref="SafeFileName"/>. Ein Teil der Fälle sind
/// Sicherheitstests gegen Path Traversal (siehe docs/SECURITY.md, S3).
/// </summary>
public sealed class SafeFileNameTests
{
    [Theory]
    [InlineData("A<B>C", "A_B_C")]
    [InlineData("A:B\"C", "A_B_C")]
    [InlineData("A/B\\C", "A_B_C")]
    [InlineData("A|B?C*D", "A_B_C_D")]
    public void Sanitize_ErsetztVerboteneWindowsZeichenDurchUnterstrich(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SafeFileName.Sanitize(eingabe));
    }

    [Fact]
    public void Sanitize_EntferntSteuerzeichenUndZeilenumbrüche()
    {
        // Tabulator, Zeilenvorschub und Wagenrücklauf sind Steuerzeichen.
        string ergebnis = SafeFileName.Sanitize("A\t\n\rB");

        Assert.Equal("A_B", ergebnis);
    }

    [Fact]
    public void Sanitize_MehrereUngültigeZeichenHintereinanderWerdenZuEinemUnterstrich()
    {
        string ergebnis = SafeFileName.Sanitize("A<<<>>>B");

        Assert.Equal("A_B", ergebnis);
        Assert.DoesNotContain("__", ergebnis, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_UmlauteBleibenErhalten()
    {
        string ergebnis = SafeFileName.Sanitize("ÄÖÜäöüß");

        Assert.Equal("ÄÖÜäöüß", ergebnis);
    }

    [Fact]
    public void Sanitize_UmlauteInZusammengesetztemNamenBleibenErhalten()
    {
        string ergebnis = SafeFileName.Sanitize("Müller Straße");

        Assert.Equal("Müller_Straße", ergebnis);
    }

    // --- Sicherheitstests: Path Traversal darf niemals zu einem Pfadwechsel führen. ---

    [Fact]
    public void Sicherheitstest_Sanitize_EntferntUnixPathTraversalVollständig()
    {
        string ergebnis = SafeFileName.Sanitize("../../etc/passwd");

        Assert.DoesNotContain('/', ergebnis);
        Assert.DoesNotContain('\\', ergebnis);
        Assert.DoesNotContain(':', ergebnis);
        Assert.False(ergebnis.StartsWith('.'));
    }

    [Fact]
    public void Sicherheitstest_Sanitize_EntferntWindowsPathTraversalVollständig()
    {
        string ergebnis = SafeFileName.Sanitize("..\\..\\windows\\system32");

        Assert.DoesNotContain('/', ergebnis);
        Assert.DoesNotContain('\\', ergebnis);
        Assert.DoesNotContain(':', ergebnis);
        Assert.False(ergebnis.StartsWith('.'));
    }

    [Fact]
    public void Sicherheitstest_Sanitize_EntferntLaufwerksAngabeUndBackslashes()
    {
        string ergebnis = SafeFileName.Sanitize("C:\\Windows\\evil");

        Assert.DoesNotContain('/', ergebnis);
        Assert.DoesNotContain('\\', ergebnis);
        Assert.DoesNotContain(':', ergebnis);
        Assert.False(ergebnis.StartsWith('.'));
    }

    [Fact]
    public void Sanitize_LiefertFallbackBeiNull()
    {
        Assert.Equal(SafeFileName.Fallback, SafeFileName.Sanitize(null));
    }

    [Fact]
    public void Sanitize_LiefertFallbackBeiLeeremString()
    {
        Assert.Equal(SafeFileName.Fallback, SafeFileName.Sanitize(string.Empty));
    }

    [Fact]
    public void Sanitize_LiefertFallbackBeiNurWhitespace()
    {
        Assert.Equal(SafeFileName.Fallback, SafeFileName.Sanitize("   "));
    }

    [Fact]
    public void Sanitize_LiefertFallbackBeiNurSonderzeichen()
    {
        Assert.Equal(SafeFileName.Fallback, SafeFileName.Sanitize("???"));
    }

    [Fact]
    public void Sanitize_KürztÜberlangenNamenAufMaxLength()
    {
        string lang = new string('a', 70);

        string ergebnis = SafeFileName.Sanitize(lang, maxLength: 60);

        Assert.Equal(60, ergebnis.Length);
        Assert.Equal(new string('a', 60), ergebnis);
    }

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("con", "con_")]
    [InlineData("PRN", "PRN_")]
    [InlineData("com1", "com1_")]
    [InlineData("NUL.txt", "NUL.txt_")]
    public void Sanitize_HängtUnterstrichAnReservierteWindowsNamenAn(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SafeFileName.Sanitize(eingabe));
    }

    [Fact]
    public void Sanitize_VerändertNormalenNamenNicht_DerNurÄhnlichWieReservierterNameIst()
    {
        // "Contract" beginnt wie "CON", ist aber kein reservierter Gerätename.
        Assert.Equal("Contract", SafeFileName.Sanitize("Contract"));
    }

    [Fact]
    public void BuildOutputFileName_ErzeugtErwartetenNamen()
    {
        string ergebnis = SafeFileName.BuildOutputFileName("RE-2026-001", "Müller GmbH");

        Assert.Equal("RE-2026-001_Müller_GmbH_ZUGFeRD.pdf", ergebnis);
    }

    [Fact]
    public void Sicherheitstest_BuildOutputFileName_NeutralisiertGefährlicheRechnungsnummer()
    {
        string ergebnis = SafeFileName.BuildOutputFileName("../../etc/passwd", "Müller GmbH");

        Assert.DoesNotContain('/', ergebnis);
        Assert.DoesNotContain('\\', ergebnis);
        Assert.DoesNotContain(':', ergebnis);
        Assert.EndsWith("_ZUGFeRD.pdf", ergebnis, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendCounter_HängtZählerInKlammernAn()
    {
        string ergebnis = SafeFileName.AppendCounter("Rechnung.pdf", 2);

        Assert.Equal("Rechnung (2).pdf", ergebnis);
    }

    [Fact]
    public void AppendCounter_BerücksichtigtNurDieLetzteEndungBeiPunktenImNamen()
    {
        string ergebnis = SafeFileName.AppendCounter("Archiv.2026.pdf", 3);

        Assert.Equal("Archiv.2026 (3).pdf", ergebnis);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void AppendCounter_WirftBeiZählerKleinerZwei(int zähler)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SafeFileName.AppendCounter("Rechnung.pdf", zähler));
    }

    [Fact]
    public void AppendCounter_WirftBeiLeeremDateinamen()
    {
        Assert.Throws<ArgumentException>(() => SafeFileName.AppendCounter(string.Empty, 2));
    }
}

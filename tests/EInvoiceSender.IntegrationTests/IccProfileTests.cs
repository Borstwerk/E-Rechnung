using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EInvoiceSender.Infrastructure.PdfA;
using Xunit;

namespace EInvoiceSender.IntegrationTests;

/// <summary>
/// Prueft das programmatisch erzeugte sRGB-ICC-Profil (ADR-0006).
///
/// Das Profil ist Teil jeder Ausgabedatei. Aendert es sich unbeabsichtigt,
/// aendert sich die Pruefsumme jeder erzeugten Rechnung – und im schlimmsten
/// Fall faellt die PDF/A-Pruefung durch. Deshalb ist der Aufbau hier fest
/// verankert und die Pruefsumme gepinnt.
///
/// Die vollstaendige Beschreibung steht in docs/STANDARDS.md, Abschnitt 7.
/// </summary>
public sealed class IccProfileTests
{
    /// <summary>
    /// Erwartete SHA-256-Pruefsumme des erzeugten Profils.
    ///
    /// Dieser Wert darf nur bewusst geaendert werden. Schlaegt der Test fehl,
    /// hat sich die Profilerzeugung geaendert; dann muessen die
    /// Ende-zu-Ende-Tests mit veraPDF erneut laufen und der Wert samt
    /// docs/STANDARDS.md nachgezogen werden.
    /// </summary>
    private const string ExpectedSha256 =
        "4eddebbfa044ee963d28f6ac89d52db3f6cdee7106adb30d9424a6e60783b8e8";

    /// <summary>Erwartete Groesse in Bytes.</summary>
    private const int ExpectedLength = 536;

    [Fact]
    public void ProfilIstDeterministischUndEntsprichtDerGepinntenPruefsumme()
    {
        byte[] first = SRgbIccProfile.GetBytes();
        byte[] second = SRgbIccProfile.GetBytes();

        Assert.Equal(first, second);
        Assert.Equal(ExpectedLength, first.Length);

        string hash = Convert.ToHexString(SHA256.HashData(first)).ToLowerInvariant();

        Assert.Equal(ExpectedSha256, hash);
    }

    [Fact]
    public void AufrufeLiefernJeweilsEineEigeneKopie()
    {
        // Die Bytes gehen in einen PDF-Stream. Wuerde immer dasselbe Feld
        // zurueckgegeben, koennte ein Aufrufer das zwischengespeicherte Profil
        // fuer alle spaeteren Dateien veraendern.
        byte[] first = SRgbIccProfile.GetBytes();
        first[0] = 0xFF;

        Assert.NotEqual(0xFF, SRgbIccProfile.GetBytes()[0]);
    }

    [Fact]
    public void KopfEntsprichtDerIccSpezifikation()
    {
        byte[] profile = SRgbIccProfile.GetBytes();

        // Groessenangabe im Kopf muss zur tatsaechlichen Groesse passen.
        Assert.Equal(profile.Length, BinaryPrimitives.ReadInt32BigEndian(profile));

        // Version 2.4.0
        Assert.Equal(0x02400000u, BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(8)));

        Assert.Equal("mntr", Encoding.ASCII.GetString(profile, 12, 4));
        Assert.Equal("RGB ", Encoding.ASCII.GetString(profile, 16, 4));
        Assert.Equal("XYZ ", Encoding.ASCII.GetString(profile, 20, 4));
        Assert.Equal("acsp", Encoding.ASCII.GetString(profile, 36, 4));

        // Wiedergabeabsicht 0 = wahrnehmungsorientiert
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(64)));

        // Beleuchtung des Verbindungsraums: D50, als s15Fixed16
        Assert.Equal(ToFixed(0.9642), BinaryPrimitives.ReadInt32BigEndian(profile.AsSpan(68)));
        Assert.Equal(ToFixed(1.0000), BinaryPrimitives.ReadInt32BigEndian(profile.AsSpan(72)));
        Assert.Equal(ToFixed(0.8249), BinaryPrimitives.ReadInt32BigEndian(profile.AsSpan(76)));
    }

    [Fact]
    public void AlleFuerEinMatrixProfilVorgeschriebenenTagsSindVorhanden()
    {
        byte[] profile = SRgbIccProfile.GetBytes();

        int tagCount = (int)BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(128));
        Assert.Equal(9, tagCount);

        var signatures = new List<string>(tagCount);

        for (int i = 0; i < tagCount; i++)
        {
            int position = 132 + (i * 12);
            signatures.Add(Encoding.ASCII.GetString(profile, position, 4));

            int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(position + 4));
            int size = (int)BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(position + 8));

            // Jeder Tag muss vollstaendig innerhalb des Profils liegen ...
            Assert.InRange(offset, 132 + (tagCount * 12), profile.Length);
            Assert.InRange(offset + size, offset, profile.Length);

            // ... und an einer durch vier teilbaren Adresse beginnen.
            Assert.Equal(0, offset % 4);
        }

        foreach (string required in new[]
                 { "desc", "cprt", "wtpt", "rXYZ", "gXYZ", "bXYZ", "rTRC", "gTRC", "bTRC" })
        {
            Assert.Contains(required, signatures);
        }
    }

    [Fact]
    public void FarbwertTagsTragenDieSRgbPrimaervalenzen()
    {
        byte[] profile = SRgbIccProfile.GetBytes();

        AssertColorant(profile, "rXYZ", 0.4360, 0.2225, 0.0139);
        AssertColorant(profile, "gXYZ", 0.3851, 0.7169, 0.0971);
        AssertColorant(profile, "bXYZ", 0.1431, 0.0606, 0.7141);
        AssertColorant(profile, "wtpt", 0.9642, 1.0000, 0.8249);
    }

    private static void AssertColorant(byte[] profile, string signature, double x, double y, double z)
    {
        int offset = FindTag(profile, signature);

        Assert.Equal("XYZ ", Encoding.ASCII.GetString(profile, offset, 4));
        Assert.Equal(ToFixed(x), BinaryPrimitives.ReadInt32BigEndian(profile.AsSpan(offset + 8)));
        Assert.Equal(ToFixed(y), BinaryPrimitives.ReadInt32BigEndian(profile.AsSpan(offset + 12)));
        Assert.Equal(ToFixed(z), BinaryPrimitives.ReadInt32BigEndian(profile.AsSpan(offset + 16)));
    }

    private static int FindTag(byte[] profile, string signature)
    {
        int tagCount = (int)BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(128));

        for (int i = 0; i < tagCount; i++)
        {
            int position = 132 + (i * 12);

            if (Encoding.ASCII.GetString(profile, position, 4) == signature)
            {
                return (int)BinaryPrimitives.ReadUInt32BigEndian(profile.AsSpan(position + 4));
            }
        }

        throw new InvalidOperationException($"Tag '{signature}' fehlt im Profil.");
    }

    private static int ToFixed(double value)
        => (int)Math.Round(value * 65536.0, MidpointRounding.AwayFromZero);
}

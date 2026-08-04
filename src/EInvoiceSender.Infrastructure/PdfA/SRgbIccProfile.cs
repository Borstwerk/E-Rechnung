using System.Buffers.Binary;
using System.Text;

namespace EInvoiceSender.Infrastructure.PdfA;

/// <summary>
/// Erzeugt ein gueltiges, minimales ICC-v2-Farbprofil fuer sRGB.
///
/// Ein PDF/A-Dokument braucht einen OutputIntent mit eingebettetem
/// Ausgabeprofil. Statt eine fremde <c>.icc</c>-Datei mitzuliefern, deren
/// Herkunft und Lizenz gesondert dokumentiert werden muesste, wird das Profil
/// hier programmatisch erzeugt (ADR-0006). Das Ergebnis ist rund ein Kilobyte
/// gross, reproduzierbar und lizenzrechtlich eindeutig.
///
/// Aufbau nach ICC.1:2001-04 (ICC-Version 2.4), Geraeteklasse "Monitor",
/// Matrix/TRC-Modell:
/// Kopf (128 Byte), Tag-Tabelle, danach die Tag-Daten. Enthalten sind die fuer
/// ein RGB-Matrix-Profil vorgeschriebenen Tags: desc, wtpt, cprt, rXYZ, gXYZ,
/// bXYZ, rTRC, gTRC, bTRC.
///
/// Die Primaervalenzen sind die bekannten, auf den Weisspunkt D50 adaptierten
/// Werte des sRGB-Farbraums nach IEC 61966-2.1.
/// </summary>
public static class SRgbIccProfile
{
    /// <summary>Beschreibung, die im Profil und im OutputIntent steht.</summary>
    public const string ProfileDescription = "sRGB IEC61966-2.1";

    /// <summary>Kennung des Ausgabebedingungs-Eintrags im PDF.</summary>
    public const string OutputConditionIdentifier = "sRGB IEC61966-2.1";

    /// <summary>Anzahl der Farbkanaele des Profils.</summary>
    public const int ComponentCount = 3;

    private const string Copyright = "Public domain colorant values, generated profile";

    // Auf D50 adaptierte sRGB-Primaervalenzen (IEC 61966-2.1).
    private static readonly (double X, double Y, double Z) RedColorant = (0.4360, 0.2225, 0.0139);
    private static readonly (double X, double Y, double Z) GreenColorant = (0.3851, 0.7169, 0.0971);
    private static readonly (double X, double Y, double Z) BlueColorant = (0.1431, 0.0606, 0.7141);

    // Weisspunkt D50, wie er im ICC-Profilverbindungsraum vorgeschrieben ist.
    private static readonly (double X, double Y, double Z) D50WhitePoint = (0.9642, 1.0000, 0.8249);

    /// <summary>
    /// Gammawert der Tonwertkurve als u8Fixed8. 0x0233 entspricht 2,19921875 –
    /// die uebliche Naeherung fuer die sRGB-Uebertragungsfunktion in einem
    /// einfachen Kurventag.
    /// </summary>
    private const ushort GammaU8Fixed8 = 0x0233;

    private static readonly Lock CacheLock = new();
    private static byte[]? _cached;

    /// <summary>
    /// Liefert das Profil als Bytefolge. Das Ergebnis ist bei jedem Aufruf
    /// identisch, damit zwei Laeufe dieselbe Ausgabedatei erzeugen.
    /// </summary>
    public static byte[] GetBytes()
    {
        lock (CacheLock)
        {
            _cached ??= Build();
            return (byte[])_cached.Clone();
        }
    }

    private static byte[] Build()
    {
        // Tag-Reihenfolge ist frei, wird hier aber festgeschrieben, damit die
        // erzeugte Datei zwischen zwei Laeufen byte-identisch bleibt.
        var tags = new List<(string Signature, byte[] Data)>
        {
            ("desc", BuildTextDescription(ProfileDescription)),
            ("cprt", BuildText(Copyright)),
            ("wtpt", BuildXyz(D50WhitePoint)),
            ("rXYZ", BuildXyz(RedColorant)),
            ("gXYZ", BuildXyz(GreenColorant)),
            ("bXYZ", BuildXyz(BlueColorant)),
            ("rTRC", BuildGammaCurve()),
            ("gTRC", BuildGammaCurve()),
            ("bTRC", BuildGammaCurve()),
        };

        const int headerSize = 128;
        int tagTableSize = 4 + (tags.Count * 12);
        int dataStart = headerSize + tagTableSize;

        // Jedes Tag beginnt an einer durch vier teilbaren Adresse.
        var offsets = new int[tags.Count];
        int cursor = dataStart;
        for (int i = 0; i < tags.Count; i++)
        {
            cursor = Align4(cursor);
            offsets[i] = cursor;
            cursor += tags[i].Data.Length;
        }

        int totalSize = Align4(cursor);
        byte[] profile = new byte[totalSize];

        WriteHeader(profile, totalSize);

        // Tag-Tabelle
        int position = headerSize;
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(position), (uint)tags.Count);
        position += 4;

        for (int i = 0; i < tags.Count; i++)
        {
            WriteSignature(profile.AsSpan(position), tags[i].Signature);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(position + 4), (uint)offsets[i]);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(position + 8), (uint)tags[i].Data.Length);
            position += 12;
        }

        // Tag-Daten
        for (int i = 0; i < tags.Count; i++)
        {
            tags[i].Data.CopyTo(profile.AsSpan(offsets[i]));
        }

        return profile;
    }

    private static void WriteHeader(byte[] profile, int totalSize)
    {
        Span<byte> header = profile.AsSpan(0, 128);

        // 0: Gesamtgroesse
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)totalSize);

        // 4: bevorzugtes CMM – bewusst leer, kein bestimmtes verlangt
        // 8: Profilversion 2.4.0
        BinaryPrimitives.WriteUInt32BigEndian(header[8..], 0x02400000);

        // 12: Geraeteklasse "Monitor"
        WriteSignature(header[12..], "mntr");

        // 16: Datenfarbraum RGB (mit abschliessendem Leerzeichen)
        WriteSignature(header[16..], "RGB ");

        // 20: Profilverbindungsraum XYZ
        WriteSignature(header[20..], "XYZ ");

        // 24: Erzeugungszeitpunkt. Fest gewaehlt, damit das Profil und damit
        // auch die Pruefsumme der Ausgabedatei reproduzierbar bleiben.
        WriteDateTime(header[24..], new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // 36: Dateikennung
        WriteSignature(header[36..], "acsp");

        // 40: Primaerplattform – keine bestimmte
        // 44: Profilkennzeichen – nicht eingebettet, unabhaengig verwendbar
        // 48: Geraetehersteller, 52: Geraetemodell, 56: Geraeteeigenschaften

        // 64: Wiedergabeabsicht 0 = wahrnehmungsorientiert
        BinaryPrimitives.WriteUInt32BigEndian(header[64..], 0);

        // 68: Beleuchtung des Verbindungsraums, zwingend D50
        WriteXyzNumber(header[68..], D50WhitePoint);

        // 80: Erzeuger, 84: Profil-Pruefsumme (zulaessigerweise null),
        // 100: reserviert – alles bleibt null.
    }

    /// <summary>Textbeschreibung nach ICC v2 (<c>textDescriptionType</c>).</summary>
    private static byte[] BuildTextDescription(string text)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(text);
        int asciiLength = ascii.Length + 1; // einschliesslich Nullterminierung

        // Aufbau: Signatur(4) + reserviert(4) + ASCII-Laenge(4) + ASCII-Text
        //         + Unicode-Sprachcode(4) + Unicode-Laenge(4)
        //         + ScriptCode-Code(2) + ScriptCode-Laenge(1) + ScriptCode-Text(67)
        byte[] data = new byte[4 + 4 + 4 + asciiLength + 4 + 4 + 2 + 1 + 67];

        WriteSignature(data, "desc");
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)asciiLength);
        ascii.CopyTo(data.AsSpan(12));

        return data;
    }

    /// <summary>Einfacher Text nach ICC v2 (<c>textType</c>).</summary>
    private static byte[] BuildText(string text)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(text);
        byte[] data = new byte[8 + ascii.Length + 1];

        WriteSignature(data, "text");
        ascii.CopyTo(data.AsSpan(8));

        return data;
    }

    /// <summary>Farbwert nach ICC v2 (<c>XYZType</c>).</summary>
    private static byte[] BuildXyz((double X, double Y, double Z) value)
    {
        byte[] data = new byte[8 + 12];

        WriteSignature(data, "XYZ ");
        WriteXyzNumber(data.AsSpan(8), value);

        return data;
    }

    /// <summary>
    /// Tonwertkurve nach ICC v2 (<c>curveType</c>) mit genau einem Eintrag.
    /// Ein einzelner Wert bedeutet laut Norm: reine Gammafunktion, der Wert ist
    /// als u8Fixed8 zu lesen.
    /// </summary>
    private static byte[] BuildGammaCurve()
    {
        byte[] data = new byte[8 + 4 + 2];

        WriteSignature(data, "curv");
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12), GammaU8Fixed8);

        return data;
    }

    private static void WriteSignature(Span<byte> target, string signature)
    {
        if (signature.Length != 4)
        {
            throw new ArgumentException("Eine ICC-Signatur besteht aus genau vier Zeichen.", nameof(signature));
        }

        for (int i = 0; i < 4; i++)
        {
            target[i] = (byte)signature[i];
        }
    }

    /// <summary>Schreibt drei s15Fixed16-Werte (X, Y, Z).</summary>
    private static void WriteXyzNumber(Span<byte> target, (double X, double Y, double Z) value)
    {
        BinaryPrimitives.WriteInt32BigEndian(target, ToS15Fixed16(value.X));
        BinaryPrimitives.WriteInt32BigEndian(target[4..], ToS15Fixed16(value.Y));
        BinaryPrimitives.WriteInt32BigEndian(target[8..], ToS15Fixed16(value.Z));
    }

    /// <summary>Wandelt eine Fliesskommazahl in die ICC-Festkommadarstellung s15Fixed16.</summary>
    private static int ToS15Fixed16(double value)
        => (int)Math.Round(value * 65536.0, MidpointRounding.AwayFromZero);

    /// <summary>Schreibt einen Zeitstempel im ICC-Format (sechs 16-Bit-Werte).</summary>
    private static void WriteDateTime(Span<byte> target, DateTime timestamp)
    {
        BinaryPrimitives.WriteUInt16BigEndian(target, (ushort)timestamp.Year);
        BinaryPrimitives.WriteUInt16BigEndian(target[2..], (ushort)timestamp.Month);
        BinaryPrimitives.WriteUInt16BigEndian(target[4..], (ushort)timestamp.Day);
        BinaryPrimitives.WriteUInt16BigEndian(target[6..], (ushort)timestamp.Hour);
        BinaryPrimitives.WriteUInt16BigEndian(target[8..], (ushort)timestamp.Minute);
        BinaryPrimitives.WriteUInt16BigEndian(target[10..], (ushort)timestamp.Second);
    }

    private static int Align4(int value) => (value + 3) & ~3;
}

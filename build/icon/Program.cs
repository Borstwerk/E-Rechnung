using BorstWerk.IconTool;
using SkiaSharp;

// Erzeugt die Symboldateien für BorstWerk E-Rechnung.
//
// Aufruf aus dem Repository-Wurzelverzeichnis:
//   dotnet run --project build/icon
//
// Ergebnis:
//   src/EInvoiceSender.App/Assets/BorstWerkEInvoice.ico   Anwendung, Taskleiste, Installer
//   docs/images/borstwerk-mark.png                        Vorschau für die Dokumentation

string root = FindRepositoryRoot();
string assets = Path.Combine(root, "src", "EInvoiceSender.App", "Assets");
string docsImages = Path.Combine(root, "docs", "images");

Directory.CreateDirectory(assets);
Directory.CreateDirectory(docsImages);

// Die Größen, die Windows für Anwendung, Taskleiste und Startmenü abruft.
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

string icoPath = Path.Combine(assets, "BorstWerkEInvoice.ico");
WriteIcon(icoPath, sizes);
Console.WriteLine($"geschrieben: {Relative(icoPath)} ({sizes.Length} Größen)");

// Vorschau auf hellem Grund für die Dokumentation.
string previewPath = Path.Combine(docsImages, "borstwerk-mark.png");
using (SKBitmap preview = RenderMark(256, onDark: false))
using (SKData png = preview.Encode(SKEncodedImageFormat.Png, 100))
{
    File.WriteAllBytes(previewPath, png.ToArray());
}

Console.WriteLine($"geschrieben: {Relative(previewPath)}");

// Eine Übersicht der kleinen Größen nebeneinander – damit sich vor dem
// Einchecken beurteilen lässt, ob das Zeichen klein noch lesbar ist.
// Bilder für die visuelle Abnahme des Markenlogos.
if (args.Contains("--abnahme"))
{
    string ziel = Path.Combine(Path.GetTempPath(), "abnahme");
    Directory.CreateDirectory(ziel);

    Speichern(Path.Combine(ziel, "01-logo-hell.png"), AufGrund(512, "#FFFFFF", onDark: false));
    Speichern(Path.Combine(ziel, "02-logo-dunkel.png"), AufGrund(512, BorstWerkMark.BrandDark, onDark: true));

    foreach (int größe in new[] { 256, 64, 32, 16 })
    {
        using SKBitmap icon = RenderAppIcon(größe);
        Speichern(Path.Combine(ziel, $"03-icon-{größe:D3}.png"), Kopie(icon));
    }

    Console.WriteLine($"geschrieben: {ziel}");
}

if (args.Contains("--preview"))
{
    string sheet = Path.Combine(Path.GetTempPath(), "borstwerk-icon-preview.png");
    WriteSizeSheet(sheet, [16, 20, 24, 32, 48, 64, 128]);
    Console.WriteLine($"geschrieben: {sheet}");
}

return 0;

/// <summary>
/// Zeichnet das App-Symbol in der gewünschten Kantenlänge.
///
/// Die drei Zahlen darin sind am Markenblatt abgemessen, nicht gewählt:
/// Eckradius, Füllgrad und der leichte Versatz nach unten. In der Vorlage
/// misst die Kachel 178 Punkte, das Zeichen 112 × 131, und sein Mittelpunkt
/// liegt 7,5 Punkte unter der Kachelmitte.
/// </summary>
static SKBitmap RenderAppIcon(int size)
{
    var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));

    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    using (var ground = new SKPaint { Color = Parse(BorstWerkMark.BrandDark), IsAntialias = true })
    {
        float radius = size * (float)BorstWerkMark.TileCornerRadius;
        canvas.DrawRoundRect(new SKRect(0, 0, size, size), radius, radius, ground);
    }

    canvas.Save();
    canvas.Translate(0, size * (float)BorstWerkMark.TileMarkOffsetY);
    DrawMark(canvas, onDark: true, extent: size, fill: (float)BorstWerkMark.TileMarkFill);
    canvas.Restore();

    return bitmap;
}

/// <summary>Zeichnet nur das Zeichen, ohne Grund – für die Dokumentation.</summary>
static SKBitmap RenderMark(int size, bool onDark)
{
    var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));

    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    DrawMark(canvas, onDark, extent: size, fill: 0.92f);

    return bitmap;
}

/// <summary>
/// Zeichnet das Zeichen: zwei Flächen, Körper und Ring. Der Maulschlüssel
/// steckt bereits in der Kontur des Körpers – er ist eine offene Kerbe und
/// kein Loch, also nichts, was hier noch auszurechnen wäre.
///
/// Eingepasst wird anhand der tatsächlichen Ausmaße: Die Zeichnung wird
/// gemessen, mittig gesetzt und auf den verfügbaren Platz skaliert. Von Hand
/// abgezählte Randabstände gehen jedes Mal schief, wenn sich an der Geometrie
/// etwas ändert.
/// </summary>
static void DrawMark(SKCanvas canvas, bool onDark, float extent, float fill)
{
    using var body = new SKPaint
    {
        Color = Parse(onDark ? BorstWerkMark.OnDark : BorstWerkMark.BrandDark),
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    using var accent = new SKPaint
    {
        Color = Parse(BorstWerkMark.BrandAccent),
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
    };

    using SKPath körper = SKPath.ParseSvgPathData(BorstWerkMark.BodyPath);
    using SKPath ring = SKPath.ParseSvgPathData(BorstWerkMark.RingPath);

    SKRect bounds = körper.Bounds;
    bounds.Union(ring.Bounds);

    float scale = fill * extent / Math.Max(bounds.Width, bounds.Height);

    canvas.Save();
    canvas.Translate(extent / 2, extent / 2);
    canvas.Scale(scale);
    canvas.Translate(-bounds.MidX, -bounds.MidY);

    // Erst der Ring, dann der Körper: Sie stoßen aneinander, statt sich zu
    // überlappen. Der Körper zuletzt, damit ein etwaiger Rundungssaum an der
    // gemeinsamen Kante unter ihm verschwindet und nicht als heller Spalt
    // stehen bleibt.
    canvas.DrawPath(ring, accent);
    canvas.DrawPath(körper, body);

    canvas.Restore();
}

/// <summary>
/// Schreibt eine ICO-Datei.
///
/// Aufbau: ein Kopf mit sechs Byte, danach je Bild ein Verzeichniseintrag mit
/// sechzehn Byte, danach die Bilddaten. Die Bilder werden als PNG abgelegt –
/// das ist seit Windows Vista zulässig und erspart die alte BMP-Struktur mit
/// getrennter Maske. Kantenlänge 256 wird als 0 eingetragen, weil das Feld
/// nur ein Byte breit ist.
/// </summary>
static void WriteIcon(string path, int[] sizes)
{
    byte[][] images = [.. sizes.Select(size =>
    {
        using SKBitmap bitmap = RenderAppIcon(size);
        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        return png.ToArray();
    })];

    using var file = File.Create(path);
    using var writer = new BinaryWriter(file);

    writer.Write((ushort)0);              // reserviert
    writer.Write((ushort)1);              // Typ 1 = Symbol
    writer.Write((ushort)images.Length);

    int offset = 6 + (16 * images.Length);

    for (int i = 0; i < images.Length; i++)
    {
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));   // Breite
        writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));   // Höhe
        writer.Write((byte)0);            // Farbtabelle: keine
        writer.Write((byte)0);            // reserviert
        writer.Write((ushort)1);          // Farbebenen
        writer.Write((ushort)32);         // Bit je Bildpunkt
        writer.Write(images[i].Length);
        writer.Write(offset);

        offset += images[i].Length;
    }

    foreach (byte[] image in images)
    {
        writer.Write(image);
    }
}

/// <summary>Stellt die kleinen Größen nebeneinander, zur Beurteilung von Hand.</summary>
static void WriteSizeSheet(string path, int[] sizes)
{
    const int cell = 140;
    var info = new SKImageInfo(cell * sizes.Length, cell, SKColorType.Rgba8888, SKAlphaType.Premul);

    using var sheet = new SKBitmap(info);
    using (var canvas = new SKCanvas(sheet))
    {
        canvas.Clear(new SKColor(0xF1, 0xF5, 0xF9));

        for (int i = 0; i < sizes.Length; i++)
        {
            using SKBitmap icon = RenderAppIcon(sizes[i]);
            float x = (i * cell) + ((cell - sizes[i]) / 2f);
            float y = (cell - sizes[i]) / 2f;

            canvas.DrawBitmap(icon, x, y, paint: null);
        }
    }

    using SKData png = sheet.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(path, png.ToArray());
}

/// <summary>Zeichnet das Zeichen mittig auf eine einfarbige Fläche.</summary>
static SKBitmap AufGrund(int größe, string grund, bool onDark)
{
    var bitmap = new SKBitmap(new SKImageInfo(größe, größe, SKColorType.Rgba8888, SKAlphaType.Premul));

    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(Parse(grund));
    DrawMark(canvas, onDark, extent: größe, fill: 0.74f);

    return bitmap;
}

static SKBitmap Kopie(SKBitmap quelle)
{
    var kopie = new SKBitmap(quelle.Info);
    quelle.CopyTo(kopie);

    return kopie;
}

static void Speichern(string pfad, SKBitmap bild)
{
    using (bild)
    using (SKData png = bild.Encode(SKEncodedImageFormat.Png, 100))
    {
        File.WriteAllBytes(pfad, png.ToArray());
    }
}

static SKColor Parse(string hex) => SKColor.Parse(hex);

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null
           && !File.Exists(Path.Combine(directory.FullName, "EInvoiceSender.sln")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName
           ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
}

static string Relative(string path)
    => Path.GetRelativePath(FindRepositoryRoot(), path);

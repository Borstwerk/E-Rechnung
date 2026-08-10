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
if (args.Contains("--preview"))
{
    string sheet = Path.Combine(Path.GetTempPath(), "borstwerk-icon-preview.png");
    WriteSizeSheet(sheet, [16, 20, 24, 32, 48, 64, 128]);
    Console.WriteLine($"geschrieben: {sheet}");
}

return 0;

/// <summary>Zeichnet das App-Symbol in der gewünschten Kantenlänge.</summary>
static SKBitmap RenderAppIcon(int size)
{
    var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));

    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    // Abgerundetes Quadrat in Graphit als Grund. Der Radius wächst mit der
    // Größe mit; bei 16 Pixeln bleibt er klein genug, dass die Ecken nicht
    // ausfransen.
    using (var ground = new SKPaint { Color = Parse(BorstWerkMark.BrandDark), IsAntialias = true })
    {
        canvas.DrawRoundRect(new SKRect(0, 0, size, size), size * 0.22f, size * 0.22f, ground);
    }

    // Das Zeichen füllt gut zwei Drittel der Fläche – genug Luft, damit es in
    // der Taskleiste nicht an den Rand stößt.
    DrawMark(canvas, onDark: true, extent: size, fill: 0.66f);

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
/// Die drei Teile des Zeichens: der Stamm als Fläche, die beiden Schalen als
/// Linien mit flachen Enden.
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

    using SKPath stem = SKPath.ParseSvgPathData(BorstWerkMark.StemPath);
    using SKPath upper = EvenOdd(BorstWerkMark.UpperBowlPath);
    using SKPath lower = EvenOdd(BorstWerkMark.LowerBowlPath);

    SKRect bounds = stem.Bounds;
    bounds.Union(upper.Bounds);
    bounds.Union(lower.Bounds);

    float scale = fill * extent / Math.Max(bounds.Width, bounds.Height);

    canvas.Save();
    canvas.Translate(extent / 2, extent / 2);
    canvas.Scale(scale);
    canvas.Translate(-bounds.MidX, -bounds.MidY);

    canvas.DrawPath(stem, body);
    canvas.DrawPath(upper, body);
    canvas.DrawPath(lower, accent);

    canvas.Restore();
}

/// <summary>
/// Liest einen Pfad aus zwei Teilpfaden und stellt ihn auf EvenOdd um, damit
/// die Innenform offen bleibt. Ohne diese Regel füllt Skia die Schale voll.
/// </summary>
static SKPath EvenOdd(string pathData)
{
    SKPath path = SKPath.ParseSvgPathData(pathData);
    path.FillType = SKPathFillType.EvenOdd;

    return path;
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

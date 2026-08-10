namespace BorstWerk.IconTool;

/// <summary>
/// Die Geometrie des BorstWerk-Zeichens – an einer einzigen Stelle.
///
/// **Warum hier und nicht im XAML:** Das Zeichen wird zweimal gebraucht, als
/// Windows-Symboldatei und als Vektorzeichnung in der Oberfläche. Zwei von
/// Hand gepflegte Fassungen desselben Zeichens driften auseinander, und das
/// fällt niemandem auf, weil beide für sich betrachtet richtig aussehen.
/// Deshalb stehen die Pfade hier, das XAML trägt dieselben Zeichenketten, und
/// ein Test vergleicht beide (<c>BorstWerkMarkTests</c>).
///
/// **Aufbau des Zeichens.** Ein geometrisches B aus drei Flächen:
///
/// 1. der senkrechte Stamm,
/// 2. die obere Schale in der Grundfarbe,
/// 3. die untere Schale in der Akzentfarbe.
///
/// Jede Schale besteht aus zwei Teilpfaden – der äußeren Kontur und der
/// Innenform – und wird mit der Regel <em>EvenOdd</em> gefüllt. Die Innenform
/// bleibt dadurch offen. Genau sie ist die Werkzeuganspielung: Die untere
/// Schale liest sich als Ring, als Maulschlüsselkopf oder Unterlegscheibe,
/// ohne dass dem Zeichen ein Werkzeug angehängt werden müsste.
///
/// **Zwei Entwürfe davor, und warum sie nicht taugten.**
///
/// Der erste setzte die untere Schale als freistehenden Ring neben den Stamm.
/// Das Ergebnis las sich als „P C“, nicht als B – eine Schale muss am Stamm
/// sitzen, sonst ist sie ein eigener Buchstabe.
///
/// Der zweite zeichnete die Schalen als Linien statt als Flächen. Bei einem
/// stark elliptischen Bogen läuft die Strichbreite an den Enden auseinander;
/// die Schalen liefen spitz zu und wirkten wie Blätter. Eine Schale mit
/// getrennt beschriebener Außen- und Innenkontur hat überall die Stärke, die
/// sie haben soll.
///
/// **Warum elliptisch und nicht kreisrund.** Ein Halbkreis bindet die Breite
/// der Schale an ihre Höhe. Das Zeichen geriete doppelt so hoch wie breit; ein
/// B ist etwa anderthalbmal so hoch wie breit.
///
/// Alle Angaben beziehen sich auf ein Feld von 64 × 64 Einheiten. Wo das
/// Zeichen darin sitzt, entscheidet nicht diese Datei – gezeichnet wird es
/// anhand seiner tatsächlichen Ausmaße mittig eingepasst.
/// </summary>
public static class BorstWerkMark
{
    /// <summary>Kantenlänge des Bezugsfeldes.</summary>
    public const double ViewBox = 64;

    /// <summary>Der senkrechte Stamm des B.</summary>
    public const string StemPath = "M 13,10 L 20,10 L 20,54 L 13,54 Z";

    /// <summary>
    /// Die obere Schale: äußere Kontur und Innenform, gefüllt nach EvenOdd.
    /// Sie sitzt an der rechten Kante des Stamms.
    /// </summary>
    public const string UpperBowlPath =
        "M 20,10 A 19,10.5 0 0 1 20,31 L 20,10 Z M 20,16 A 13,4.5 0 0 1 20,25 L 20,16 Z";

    /// <summary>
    /// Die untere Schale in der Akzentfarbe, etwas größer als die obere.
    /// Sie beginnt an der Taille, wo die obere Schale endet.
    /// </summary>
    public const string LowerBowlPath =
        "M 20,31 A 21,11.5 0 0 1 20,54 L 20,31 Z M 20,37 A 15,5.5 0 0 1 20,48 L 20,37 Z";

    // --- Farben der Dachmarke, siehe 02-BORSTWERK-VISUAL-IDENTITY ---

    /// <summary>Graphit – der dunkle Grund der Dachmarke.</summary>
    public const string BrandDark = "#0F172A";

    /// <summary>Warmer Ocker – der Akzent der Dachmarke.</summary>
    public const string BrandAccent = "#D49A00";

    /// <summary>Produktfarbe E-Rechnung.</summary>
    public const string ProductAccent = "#176B87";

    /// <summary>Weiß für die Umkehrung auf dunklem Grund.</summary>
    public const string OnDark = "#FFFFFF";
}

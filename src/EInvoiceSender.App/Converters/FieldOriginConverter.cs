using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EInvoiceSender.Core.Models;

namespace EInvoiceSender.App.Converters;

/// <summary>
/// Liefert den Hinweistext zur Herkunft eines Formularfeldes.
///
/// Gebunden wird an das Herkunftsverzeichnis des Entwurfs, der Feldname kommt
/// als <c>ConverterParameter</c>:
///
/// <code>
/// {Binding Draft.Origins, Converter={StaticResource FieldOrigin},
///          ConverterParameter=InvoiceNumber}
/// </code>
///
/// So bleibt die XAML lesbar, ohne für jedes der rund zwei Dutzend Felder
/// eine eigene Eigenschaft im ViewModel anzulegen.
///
/// Der Hinweis enthält Zeichen **und** Wort. Farbe allein würde für
/// farbfehlsichtige Anwender nichts aussagen.
/// </summary>
public sealed class FieldOriginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyDictionary<string, FieldOrigin> origins
            || parameter is not string field
            || !origins.TryGetValue(field, out FieldOrigin origin))
        {
            return string.Empty;
        }

        return origin switch
        {
            FieldOrigin.DetectedReliably => "✓ aus PDF erkannt",
            FieldOrigin.DetectedUncertain => "? aus PDF erkannt – bitte prüfen",
            FieldOrigin.Template => "✓ aus gespeicherter Vorlage",
            FieldOrigin.TemplateDefault => "✓ aus gespeicherter Vorlage",
            _ => string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Die Herkunft wird nur angezeigt, nie zurückgeschrieben.");
}

/// <summary>
/// Blendet den Herkunftshinweis aus, solange ein Feld von Hand erfasst wurde.
/// Ohne ihn stünde unter jedem leeren Feld eine leere Zeile.
/// </summary>
public sealed class FieldOriginVisibilityConverter : IValueConverter
{
    private static readonly FieldOriginConverter Label = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (string)Label.Convert(value, typeof(string), parameter, culture) is { Length: > 0 }
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Die Herkunft wird nur angezeigt, nie zurückgeschrieben.");
}

using System.Globalization;
using System.Windows.Data;

namespace EInvoiceSender.App.Converters;

/// <summary>
/// Vermittelt zwischen <see cref="DateOnly"/> im Fachmodell und
/// <see cref="DateTime"/> im WPF-DatePicker.
///
/// Fachlich ist ein Rechnungsdatum ein Tag ohne Uhrzeit. Dass das
/// Bedienelement etwas anderes erwartet, ist ein Belang der Oberfläche und
/// steht deshalb hier - früher trug der Entwurf im Fachkern drei
/// Zusatzeigenschaften nur für diesen Zweck.
/// </summary>
public sealed class DateOnlyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime moment ? DateOnly.FromDateTime(moment) : null;
}

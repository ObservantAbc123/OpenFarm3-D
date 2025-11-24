using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace native_desktop_app.Converters;

/// <summary>
///     Returns true when the bound value (e.g., "printing") equals the ConverterParameter
///     (case-insensitive). Used to toggle class membership in XAML, e.g.:
///     Classes.is-printing="{Binding JobStatus, Converter={StaticResource StatusIs}, ConverterParameter=printing}"
/// </summary>
public sealed class JobStatusEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value as string;
        var expected = parameter as string;
        return string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
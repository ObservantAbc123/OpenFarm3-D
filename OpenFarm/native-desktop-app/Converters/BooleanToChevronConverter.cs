using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace native_desktop_app.Converters
{
    /// <summary>
    /// Converts a boolean (IsExpanded) into a chevron arrow character.
    /// True → ▼ (expanded)
    /// False → ▶ (collapsed)
    /// </summary>
    public sealed class BooleanToChevronConverter : IValueConverter
    {
        // Convenient static instance so you can reference it as
        // {x:Static conv:BooleanToChevronConverter.Instance}
        public static readonly BooleanToChevronConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "▼" : "▶";
            return "▶";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Not needed for one-way binding
            throw new NotSupportedException();
        }
    }
}
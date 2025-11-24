using System;
using Avalonia.Data.Converters;

namespace native_desktop_app.Converters
{
    public class ShowPaidButtonConverter : IValueConverter
    {
        // Returns true unless status == "rejected"
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var status = (value as string)?.Trim() ?? string.Empty;
            return !status.Equals("rejected", StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
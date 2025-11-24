using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using DatabaseAccess.Models;

namespace native_desktop_app.ViewModels
{
    public sealed class DayFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Emailautoreplyrule.DayOfWeekFlags flags &&
                parameter is string dayName &&
                Enum.TryParse<Emailautoreplyrule.DayOfWeekFlags>(dayName, out var dayFlag))
            {
                return flags.HasFlag(dayFlag);
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return AvaloniaProperty.UnsetValue;
        }
    }
}
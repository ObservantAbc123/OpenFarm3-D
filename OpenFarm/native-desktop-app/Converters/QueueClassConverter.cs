using System;
using System.Globalization;
using Avalonia.Data.Converters;
using DatabaseAccess.Models;

namespace native_desktop_app.Converters;

// True when JobStatus == "operatorApproved" AND Paid == true
public sealed class QueueClassConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PrintJob j)
            return string.Equals(j.JobStatus, "operatorApproved", StringComparison.OrdinalIgnoreCase) && j.Paid;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
// namespace native_desktop_app.Converters
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

public sealed class DisplayStatusConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = values.Count > 0 ? values[0]?.ToString() ?? "" : "";
        var paid   = values.Count > 1 && values[1] is bool b && b;

        if (status.Equals("operatorApproved", StringComparison.OrdinalIgnoreCase))
            return paid ? "Queue" : "Awaiting Payment";

        // Optional: nicer labels for other statuses
        return status.ToLowerInvariant() switch
        {
            "completed" => "Finished",
            "printing"  => "Printing",
            "cancelled" => "Cancelled",
            "rejected"  => "Rejected",
            _           => status
        };
    }
}
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace EquipmentBorrowing.Desktop.Converters;

public class BoolToAvailabilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAvailable)
        {
            return isAvailable ? "Available" : "Not Available";
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
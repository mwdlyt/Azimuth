using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Azimuth.Converters;

/// <summary>
/// Converts a boolean to Visibility. True = Visible, False = Collapsed.
/// Pass "Invert" as parameter to reverse.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

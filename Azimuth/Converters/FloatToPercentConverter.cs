using System.Globalization;
using System.Windows.Data;

namespace Azimuth.Converters;

/// <summary>
/// Converts a float 0..1 to a percentage value 0..100 for slider binding.
/// </summary>
public class FloatToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float f) return (double)(f * 100);
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return (float)(d / 100.0);
        return 0f;
    }
}

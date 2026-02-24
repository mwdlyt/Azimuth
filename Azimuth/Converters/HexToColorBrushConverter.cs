using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Azimuth.Converters;

/// <summary>
/// Converts a hex color string (e.g. "#7C5CFC") to a SolidColorBrush.
/// </summary>
public class HexToColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

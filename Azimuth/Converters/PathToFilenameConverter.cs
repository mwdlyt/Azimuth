using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Azimuth.Converters;

/// <summary>
/// Extracts the file name from a full path string.
/// </summary>
public class PathToFilenameConverter : IValueConverter
{
    /// <summary>Singleton instance for use with x:Static.</summary>
    public static readonly PathToFilenameConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
            return Path.GetFileName(path);
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

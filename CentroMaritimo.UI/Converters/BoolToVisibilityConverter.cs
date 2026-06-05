using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CentroMaritimo.UI.Converters;

/// <summary>True → Visible, False → Collapsed. Parameter "invert" invierte.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

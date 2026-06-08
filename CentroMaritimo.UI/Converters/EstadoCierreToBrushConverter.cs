using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PuertoBB.Core.Models.Resultados;

namespace CentroMaritimo.UI.Converters;

/// <summary>Convierte EstadoCierreAgencia a brush de fondo para el chip de la grilla.</summary>
public class EstadoCierreToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value switch
        {
            EstadoCierreAgencia.Pendiente => "#FFF3E0",
            EstadoCierreAgencia.Emitido   => "#E3F2FD",
            EstadoCierreAgencia.Completo  => "#E8F5E9",
            _                              => "#FFFFFF"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte EstadoCierreAgencia al brush de texto del pill.</summary>
public class EstadoCierreToFgBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value switch
        {
            EstadoCierreAgencia.Pendiente => "#E65100",
            EstadoCierreAgencia.Emitido   => "#1565C0",
            EstadoCierreAgencia.Completo  => "#2E7D32",
            _                              => "#212121"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte EstadoCierreAgencia a su etiqueta visible.</summary>
public class EstadoCierreToTextoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            EstadoCierreAgencia.Pendiente => "Pendiente",
            EstadoCierreAgencia.Emitido   => "Emitido (sin mail)",
            EstadoCierreAgencia.Completo  => "Completo",
            _                              => string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

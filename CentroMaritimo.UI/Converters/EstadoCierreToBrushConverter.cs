using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PuertoBB.Core.Models.Resultados;

namespace CentroMaritimo.UI.Converters;

/// <summary>Convierte EstadoCierreCliente a brush de fondo para el chip de la grilla.</summary>
public class EstadoCierreToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value switch
        {
            EstadoCierreCliente.Pendiente => "#FFF3E0",
            EstadoCierreCliente.Emitido   => "#E3F2FD",
            EstadoCierreCliente.Completo  => "#E8F5E9",
            _                              => "#FFFFFF"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte EstadoCierreCliente al brush de texto del pill.</summary>
public class EstadoCierreToFgBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value switch
        {
            EstadoCierreCliente.Pendiente => "#E65100",
            EstadoCierreCliente.Emitido   => "#1565C0",
            EstadoCierreCliente.Completo  => "#2E7D32",
            _                              => "#212121"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte EstadoCierreCliente a su etiqueta visible.</summary>
public class EstadoCierreToTextoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            EstadoCierreCliente.Pendiente => "Pendiente",
            EstadoCierreCliente.Emitido   => "Emitido (sin mail)",
            EstadoCierreCliente.Completo  => "Completo",
            _                              => string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

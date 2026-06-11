using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CamaraPortuaria.UI.Converters;

/// <summary>Convierte la etiqueta de estado (incluido "Vencido" calculado) al brush de fondo.</summary>
public class EstadoReciboToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = EstadoReciboColorHelper.Background(value?.ToString());
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte la etiqueta de estado al brush de texto del pill.</summary>
public class EstadoReciboToFgBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = EstadoReciboColorHelper.Foreground(value?.ToString());
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal static class EstadoReciboColorHelper
{
    internal static string Background(string? estado) => estado switch
    {
        "Emitido"     => "#E3F2FD",
        "Enviado"     => "#E0F7FA",
        "Pagado"      => "#E8F5E9",
        "Vencido"     => "#FFEBEE",
        "Anulado"     => "#F5F5F5",
        "Pendiente"   => "#FFF3E0",
        "Moroso"      => "#FBE9E7",
        "No emitido"  => "#ECEFF1",
        _             => "#FFFFFF"
    };

    internal static string Foreground(string? estado) => estado switch
    {
        "Emitido"     => "#1565C0",
        "Enviado"     => "#006064",
        "Pagado"      => "#2E7D32",
        "Vencido"     => "#C62828",
        "Anulado"     => "#616161",
        "Pendiente"   => "#E65100",
        "Moroso"      => "#BF360C",
        "No emitido"  => "#607D8B",
        _             => "#212121"
    };
}

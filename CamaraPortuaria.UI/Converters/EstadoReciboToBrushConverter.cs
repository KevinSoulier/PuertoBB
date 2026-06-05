using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CamaraPortuaria.UI.Converters;

/// <summary>Convierte la etiqueta de estado (incluido "Vencido" calculado) al brush de fondo.</summary>
public class EstadoReciboToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var estado = value?.ToString();
        var hex = estado switch
        {
            "Emitido" => "#E3F2FD",
            "Enviado" => "#FFF9C4",
            "Pagado"  => "#E8F5E9",
            "Vencido" => "#FFEBEE",
            "Anulado" => "#F5F5F5",
            _          => "#FFFFFF"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

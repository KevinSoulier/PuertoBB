using System.Windows.Controls;

namespace CentroMaritimo.UI.Controls;

/// <summary>
/// Overlay de espera reutilizable: fondo atenuado + tarjeta con spinner, título, entidad actual,
/// barra de progreso, contador y botón Cancelar. Hereda el DataContext del Page (el ViewModel),
/// del que toma <c>IsBusy</c> y las propiedades <c>Busy*</c> de <c>PageViewModel</c>.
/// </summary>
public partial class BusyOverlay : UserControl
{
    public BusyOverlay() => InitializeComponent();
}

using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels.Items;

public class VoucherItem
{
    public int Id { get; }
    public int Numero { get; }
    public string Cliente { get; }
    public string Barco { get; }
    public string Fecha { get; }
    public string Importe { get; }
    public bool Consolidado { get; }

    public VoucherItem(Voucher v)
    {
        Id = v.Id;
        Numero = v.Numero;
        Cliente = v.Cliente?.Nombre ?? $"#{v.ClienteId}";
        Barco = v.Barco?.Nombre ?? $"#{v.BarcoId}";
        Fecha = Formato.Fecha(v.Fecha);
        Importe = Formato.Moneda(v.Importe);
        Consolidado = v.ReciboId is not null;
    }
}

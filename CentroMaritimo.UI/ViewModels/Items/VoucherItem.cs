using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels.Items;

public class VoucherItem
{
    public int Id { get; }
    public int Numero { get; }
    public string Agencia { get; }
    public string Barco { get; }
    public string Fecha { get; }
    public string Importe { get; }
    public bool Consolidado { get; }
    public string EstadoTexto { get; }

    public VoucherItem(Voucher v)
    {
        Id = v.Id;
        Numero = v.Numero;
        Agencia = v.Agencia?.Nombre ?? $"#{v.AgenciaId}";
        Barco = v.Barco?.Nombre ?? $"#{v.BarcoId}";
        Fecha = Formato.Fecha(v.Fecha);
        Importe = Formato.Moneda(v.Importe);
        Consolidado = v.ReciboId is not null;
        EstadoTexto = v.ReciboId is not null ? "Emitido" : "Pendiente";
    }
}

using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
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
        EstadoTexto = EstadoDesde(v);
    }

    // El estado del voucher = avance del recibo que lo consolidó. Sin recibo queda "Pendiente"
    // (editable/eliminable); una vez consolidado sigue visible como referencia y refleja el recibo.
    private static string EstadoDesde(Voucher v)
    {
        if (v.ReciboId is null) return "Pendiente";
        return v.Recibo?.Estado switch
        {
            ReciboEstado.Enviado => "Enviado",
            ReciboEstado.Pagado  => "Pagado",
            // Defensivo: al anular un consolidado se desvinculan los vouchers (ReciboId vuelve a null),
            // así que un voucher consolidado no debería apuntar a un recibo Anulado.
            ReciboEstado.Anulado => "Pendiente",
            _                    => "Emitido" // Emitido o Pendiente-sin-CAE: ya consolidado
        };
    }
}

using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels.Items;

/// <summary>
/// Fila de la tabla de emisión masiva: una agencia del grupo con el estado de su recibo de cuota en el período.
/// Si no hay recibo aún, el estado es "No emitido" y solo se puede emitir.
/// </summary>
public class EmisionMasivaItem
{
    public int EntidadId { get; }
    public string Agencia { get; }
    public int? ReciboId { get; }
    public bool TieneRecibo { get; }
    public string Comprobante { get; }
    public string Importe { get; }
    public string Estado { get; }
    public string EstadoEnvio { get; }
    public string? Error { get; }
    public bool CaeOk { get; }
    public bool MailEnviado { get; }
    public string? FechaEnvioMailFormateada { get; }

    /// <summary>El paso de emisión/CAE está pendiente: no hay recibo todavía, o existe pero sin CAE (y no está pagado/anulado).</summary>
    public bool EsEmitible { get; }
    /// <summary>Hay CAE y el mail está pendiente, falló, o se puede reenviar uno ya enviado.</summary>
    public bool EsEnviable { get; }
    /// <summary>Hay un comprobante con CAE para previsualizar el PDF.</summary>
    public bool EsPrevisualizable => CaeOk;

    public EmisionMasivaItem(int entidadId, string agencia, Recibo? r)
    {
        EntidadId = entidadId;
        Agencia = agencia;

        if (r is null)
        {
            Comprobante = "—";
            Importe = "—";
            Estado = "No emitido";
            EstadoEnvio = "—";
            EsEmitible = true;
            return;
        }

        var hoy = DateTime.Today;
        ReciboId = r.Id;
        TieneRecibo = true;
        CaeOk = !string.IsNullOrEmpty(r.CAE);
        Comprobante = CaeOk ? $"{r.PuntoDeVenta:0000}-{r.NumeroComprobante:00000000}" : "—";
        Importe = Formato.Moneda(r.Importe);
        Estado = EstadoReciboHelper.EtiquetaEstado(r.Estado, r.FechaVencimientoPago, hoy);
        MailEnviado = r.Estado == ReciboEstado.Enviado;
        FechaEnvioMailFormateada = r.FechaEnvioMail.HasValue ? Formato.Fecha(r.FechaEnvioMail.Value) : null;
        Error = r.UltimoErrorCae ?? r.UltimoErrorMail;
        EsEmitible = string.IsNullOrEmpty(r.CAE) && r.Estado is not (ReciboEstado.Pagado or ReciboEstado.Anulado);
        EsEnviable = CaeOk && r.Estado is ReciboEstado.Emitido or ReciboEstado.Enviado;
        EstadoEnvio = r.Estado switch
        {
            ReciboEstado.Pendiente => "CAE pendiente",
            ReciboEstado.Enviado   => "Enviado",
            ReciboEstado.Emitido   => r.UltimoErrorMail is not null ? "Mail falló" : "Sin enviar",
            _                      => "—"
        };
    }
}

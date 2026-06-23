using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>
/// Fila de la tabla de emisión masiva: una empresa del grupo con el estado de su recibo en el período.
/// Si no hay recibo aún, el estado es "No emitido" y solo se puede emitir.
/// </summary>
public class EmisionMasivaItem
{
    public int ClienteId { get; }
    public string Cliente { get; }
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
    /// <summary>Recibo creado pero sin CAE (Pendiente): se puede eliminar para rehacerlo.</summary>
    public bool EsEliminable { get; }

    public EmisionMasivaItem(int entidadId, string empresa, Recibo? r, decimal importeEsperado)
    {
        ClienteId = entidadId;
        Cliente = empresa;

        if (r is null)
        {
            Comprobante = "—";
            Importe = Formato.Moneda(importeEsperado);   // monto esperado del grupo, para validar antes de emitir
            Estado = "No emitido";
            EstadoEnvio = "—";
            EsEmitible = true;
            return;
        }

        var hoy = DateTime.Today;
        var acc = AccionesRecibo.De(r);
        ReciboId = r.Id;
        TieneRecibo = true;
        CaeOk = !string.IsNullOrEmpty(r.CAE);
        Comprobante = CaeOk ? $"{r.PuntoDeVenta:0000}-{r.NumeroComprobante:00000000}" : "—";
        Importe = Formato.Moneda(r.Importe);
        Estado = EstadoReciboHelper.EtiquetaEstado(r, hoy);
        EstadoEnvio = EstadoReciboHelper.EtiquetaEnvio(r);
        MailEnviado = r.FechaEnvioMail is not null;
        FechaEnvioMailFormateada = r.FechaEnvioMail.HasValue ? Formato.Fecha(r.FechaEnvioMail.Value) : null;
        Error = r.UltimoErrorCae ?? r.UltimoErrorMail;
        EsEmitible = acc.EsReintentable;   // sin CAE (Pendiente)
        EsEnviable = acc.EsEnviable;       // con CAE (Emitido)
        EsEliminable = !CaeOk;             // Pendiente: borrable para rehacer
    }
}

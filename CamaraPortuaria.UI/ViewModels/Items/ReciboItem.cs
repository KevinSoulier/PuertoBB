using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>Proyección de un Recibo para la grilla (estado calculado, atraso, formato).</summary>
public class ReciboItem
{
    public int Id { get; }
    public int EmpresaId { get; }
    public string Empresa { get; }
    public string Periodo { get; }
    public string Importe { get; }
    public string Comprobante { get; }
    public string Cae { get; }
    public string FechaEmision { get; }
    public string FechaVencimiento { get; }
    public EstadoFiscal EstadoFiscal { get; }
    /// <summary>Columna "Estado" (fiscal + cobro): Pendiente / Emitido / Vencido / Pagado / Incobrable / Anulado.</summary>
    public string Estado { get; }
    /// <summary>Columna "Envío" (solo mail): Enviado / Sin enviar / Mail falló / —.</summary>
    public string EstadoEnvio { get; }
    public int DiasAtraso { get; }
    public bool EsIncobrable { get; }
    public string? MotivoIncobrable { get; }

    /// <summary>Detalle del último error (CAE o mail) para el tooltip; null si no hubo.</summary>
    public string? Error { get; }
    public bool CaeOk { get; }
    public bool MailEnviado { get; }
    public string? FechaEnvioMailFormateada { get; }
    public bool TieneNotaCredito { get; }
    /// <summary>Comprobante de la nota de crédito formateado ("0001-00000095"); null si no hay NC.</summary>
    public string? NotaCreditoComprobante { get; }

    /// <summary>Reenviable: el recibo (con CAE) o su nota de crédito si está Anulado.</summary>
    public bool EsReenviable { get; }
    public bool EsPagable { get; }
    public bool EsAnulable { get; }
    /// <summary>El paso de emisión/CAE está pendiente (sin CAE). El mail fallido lo cubre <see cref="EsReenviable"/>.</summary>
    public bool EsReintentable { get; }
    public bool EsMarcableIncobrable { get; }
    public bool EsQuitableIncobrable { get; }
    /// <summary>Recibo Pendiente (sin CAE): se puede editar el contenido o eliminarlo para rehacerlo.</summary>
    public bool EsEditable => EstadoFiscal == EstadoFiscal.Pendiente;

    public ReciboItem(Recibo r)
    {
        var hoy = DateTime.Today;
        var acc = AccionesRecibo.De(r);
        Id = r.Id;
        EmpresaId = r.EmpresaId;
        // Nombre desde el snapshot fiscal (inmutable); fallback a la navegación para recibos legacy.
        Empresa = r.ReceptorNombre is { Length: > 0 } nombre ? nombre : r.Empresa?.Nombre ?? $"#{r.EmpresaId}";
        Periodo = Formato.Periodo(r.PeriodoAnio, r.PeriodoMes);
        Importe = Formato.Moneda(r.Importe);
        CaeOk = !string.IsNullOrEmpty(r.CAE);
        Comprobante = CaeOk ? Formato.Comprobante(r.PuntoDeVenta, r.NumeroComprobante) : "—";
        Cae = CaeOk ? r.CAE : "—";
        TieneNotaCredito = r.NotaDeCredito is not null;
        NotaCreditoComprobante = r.NotaDeCredito is { } nc ? Formato.Comprobante(nc.PuntoDeVenta, nc.NumeroComprobante) : null;
        FechaEmision = Formato.Fecha(r.FechaEmision);
        FechaVencimiento = Formato.Fecha(r.FechaVencimientoPago);
        EstadoFiscal = r.EstadoFiscal;
        Estado = EstadoReciboHelper.EtiquetaEstado(r, hoy);
        EstadoEnvio = EstadoReciboHelper.EtiquetaEnvio(r);
        DiasAtraso = EstadoReciboHelper.DiasAtraso(r, hoy);
        EsIncobrable = EstadoReciboHelper.Cobro(r) == EstadoCobro.Incobrable;
        MotivoIncobrable = r.MotivoIncobrable;
        MailEnviado = r.FechaEnvioMail is not null;
        FechaEnvioMailFormateada = r.FechaEnvioMail.HasValue ? Formato.Fecha(r.FechaEnvioMail.Value) : null;
        Error = r.UltimoErrorCae ?? r.UltimoErrorMail;
        EsReenviable = acc.EsEnviable || (r.EstadoFiscal == EstadoFiscal.Anulado && TieneNotaCredito);
        EsPagable = acc.EsPagable;
        EsAnulable = acc.EsAnulable;
        EsReintentable = acc.EsReintentable;
        EsMarcableIncobrable = acc.EsMarcableIncobrable;
        EsQuitableIncobrable = acc.EsQuitableIncobrable;
    }
}

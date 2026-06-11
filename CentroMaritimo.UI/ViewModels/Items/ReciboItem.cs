using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels.Items;

/// <summary>Proyección de un Recibo del Centro Marítimo para la grilla.</summary>
public class ReciboItem
{
    public int Id { get; }
    public int AgenciaId { get; }
    public string Agencia { get; }
    public bool EsMoroso { get; }
    public string Periodo { get; }
    public string Importe { get; }
    public string Comprobante { get; }
    public string FechaEmision { get; }
    public string FechaVencimiento { get; }
    public bool Consolidado { get; }
    public ReciboEstado EstadoPersistido { get; }
    public string Estado { get; }
    public int DiasAtraso { get; }

    /// <summary>Estado del envío/CAE para la grilla: "Enviado" / "Sin enviar" / "Mail falló" / "CAE pendiente".</summary>
    public string EstadoEnvio { get; }
    /// <summary>Detalle del último error (CAE o mail) para el tooltip; null si no hubo.</summary>
    public string? Error { get; }
    public bool CaeOk { get; }
    public bool MailEnviado { get; }
    public string? FechaEnvioMailFormateada { get; }

    public bool EsReenviable => EstadoPersistido is ReciboEstado.Emitido or ReciboEstado.Enviado;
    public bool EsPagable    => EstadoPersistido is ReciboEstado.Emitido or ReciboEstado.Enviado;
    public bool EsAnulable   => EstadoPersistido != ReciboEstado.Anulado;
    /// <summary>El paso de emisión/CAE está pendiente (sin CAE). El mail fallido lo cubre <see cref="EsReenviable"/>.</summary>
    public bool EsReintentable { get; }

    public ReciboItem(Recibo r)
    {
        var hoy = DateTime.Today;
        Id = r.Id;
        AgenciaId = r.AgenciaId;
        // Nombre desde el snapshot fiscal (inmutable); fallback a la navegación para recibos legacy.
        Agencia = r.ReceptorNombre is { Length: > 0 } nombre ? nombre : r.Agencia?.Nombre ?? $"#{r.AgenciaId}";
        EsMoroso = r.Agencia?.EsMoroso ?? false;
        Periodo = Formato.Periodo(r.PeriodoAnio, r.PeriodoMes);
        Importe = Formato.Moneda(r.Importe);
        CaeOk = !string.IsNullOrEmpty(r.CAE);
        Comprobante = CaeOk ? $"{r.PuntoDeVenta:0000}-{r.NumeroComprobante:00000000}" : "—";
        FechaEmision = Formato.Fecha(r.FechaEmision);
        FechaVencimiento = Formato.Fecha(r.FechaVencimientoPago);
        Consolidado = r.EsConsolidadoVouchers;
        EstadoPersistido = r.Estado;
        Estado = r.Agencia?.EsMoroso == true
            ? "Moroso"
            : EstadoReciboHelper.EtiquetaEstado(r.Estado, r.FechaVencimientoPago, hoy);
        DiasAtraso = EstadoReciboHelper.DiasAtraso(r.Estado, r.FechaVencimientoPago, hoy);
        MailEnviado = r.Estado == ReciboEstado.Enviado;
        FechaEnvioMailFormateada = r.FechaEnvioMail.HasValue ? Formato.Fecha(r.FechaEnvioMail.Value) : null;
        Error = r.UltimoErrorCae ?? r.UltimoErrorMail;
        EsReintentable = r.Estado == ReciboEstado.Pendiente;
        EstadoEnvio = r.Estado switch
        {
            ReciboEstado.Pendiente => "CAE pendiente",
            ReciboEstado.Enviado   => "Enviado",
            ReciboEstado.Emitido   => r.UltimoErrorMail is not null ? "Mail falló" : "Sin enviar",
            _                      => "—"
        };
    }
}

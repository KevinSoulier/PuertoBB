using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;

namespace CentroMaritimo.UI.ViewModels.Items;

/// <summary>Proyección de un Recibo del Centro Marítimo para la grilla.</summary>
public class ReciboItem
{
    public int Id { get; }
    public string Agencia { get; }
    public string Periodo { get; }
    public string Importe { get; }
    public string Comprobante { get; }
    public string FechaEmision { get; }
    public string FechaVencimiento { get; }
    public bool Consolidado { get; }
    public ReciboEstado EstadoPersistido { get; }
    public string Estado { get; }
    public int DiasAtraso { get; }
    public bool EsReenviable => EstadoPersistido == ReciboEstado.Emitido;
    public bool EsPagable => EstadoPersistido is ReciboEstado.Emitido or ReciboEstado.Enviado;

    public ReciboItem(Recibo r)
    {
        var hoy = DateTime.Today;
        Id = r.Id;
        Agencia = r.Agencia?.Nombre ?? $"#{r.AgenciaId}";
        Periodo = Formato.Periodo(r.PeriodoAnio, r.PeriodoMes);
        Importe = Formato.Moneda(r.Importe);
        Comprobante = $"{r.PuntoDeVenta:0000}-{r.NumeroComprobante:00000000}";
        FechaEmision = Formato.Fecha(r.FechaEmision);
        FechaVencimiento = Formato.Fecha(r.FechaVencimientoPago);
        Consolidado = r.EsConsolidadoVouchers;
        EstadoPersistido = r.Estado;
        Estado = EstadoReciboHelper.EtiquetaEstado(r.Estado, r.FechaVencimientoPago, hoy);
        DiasAtraso = EstadoReciboHelper.DiasAtraso(r.Estado, r.FechaVencimientoPago, hoy);
    }
}

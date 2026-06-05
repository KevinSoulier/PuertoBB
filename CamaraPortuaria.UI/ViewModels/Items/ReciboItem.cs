using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Enums;
using PuertoBB.Services.Common;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>Proyección de un Recibo para la grilla (estado calculado, atraso, formato).</summary>
public class ReciboItem
{
    public int Id { get; }
    public string Empresa { get; }
    public string Periodo { get; }
    public string Importe { get; }
    public string Comprobante { get; }
    public string FechaEmision { get; }
    public string FechaVencimiento { get; }
    public ReciboEstado EstadoPersistido { get; }
    public string Estado { get; }
    public int DiasAtraso { get; }
    public bool EsReenviable => EstadoPersistido == ReciboEstado.Emitido;
    public bool EsPagable => EstadoPersistido is ReciboEstado.Emitido or ReciboEstado.Enviado;

    public ReciboItem(Recibo r)
    {
        var hoy = DateTime.Today;
        Id = r.Id;
        Empresa = r.Empresa?.Nombre ?? $"#{r.EmpresaId}";
        Periodo = Formato.Periodo(r.PeriodoAnio, r.PeriodoMes);
        Importe = Formato.Moneda(r.Importe);
        Comprobante = $"{r.PuntoDeVenta:0000}-{r.NumeroComprobante:00000000}";
        FechaEmision = Formato.Fecha(r.FechaEmision);
        FechaVencimiento = Formato.Fecha(r.FechaVencimientoPago);
        EstadoPersistido = r.Estado;
        Estado = EstadoReciboHelper.EtiquetaEstado(r.Estado, r.FechaVencimientoPago, hoy);
        DiasAtraso = EstadoReciboHelper.DiasAtraso(r.Estado, r.FechaVencimientoPago, hoy);
    }
}

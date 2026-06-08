using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

/// <summary>Acceso al singleton Configuracion (Id = 1) del Centro Marítimo y a sus puntos de venta AFIP.</summary>
public interface IConfiguracionRepository
{
    Task<Configuracion> GetAsync(CancellationToken ct = default);
    Task SaveAsync(Configuracion configuracion, CancellationToken ct = default);

    Task<IReadOnlyList<PuntoDeVenta>> GetPuntosDeVentaAsync(CancellationToken ct = default);
    Task<PuntoDeVenta> GuardarPuntoDeVentaAsync(PuntoDeVenta puntoDeVenta, CancellationToken ct = default);
    Task EliminarPuntoDeVentaAsync(int id, CancellationToken ct = default);
    /// <summary>Marca el punto de venta indicado como activo y desactiva el resto.</summary>
    Task MarcarPuntoDeVentaActivoAsync(int id, CancellationToken ct = default);
}

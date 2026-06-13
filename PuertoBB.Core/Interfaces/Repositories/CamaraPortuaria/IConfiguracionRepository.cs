using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;

/// <summary>Acceso al singleton Configuracion (Id = 1) y a sus puntos de venta AFIP.</summary>
public interface IConfiguracionRepository
{
    Task<Configuracion> GetAsync(CancellationToken ct = default);
    /// <summary>Lectura fresca (sin tracking) para servicios que solo leen (AFIP/Mail): siempre refleja
    /// el estado actual de la base, incluso desde un <c>DbContext</c> de larga vida.</summary>
    Task<Configuracion> GetSinTrackingAsync(CancellationToken ct = default);
    Task SaveAsync(Configuracion configuracion, CancellationToken ct = default);

    Task<IReadOnlyList<PuntoDeVenta>> GetPuntosDeVentaAsync(CancellationToken ct = default);
    Task<PuntoDeVenta> GuardarPuntoDeVentaAsync(PuntoDeVenta puntoDeVenta, CancellationToken ct = default);
    Task EliminarPuntoDeVentaAsync(int id, CancellationToken ct = default);
    /// <summary>Marca el punto de venta indicado como activo y desactiva el resto.</summary>
    Task MarcarPuntoDeVentaActivoAsync(int id, CancellationToken ct = default);
}

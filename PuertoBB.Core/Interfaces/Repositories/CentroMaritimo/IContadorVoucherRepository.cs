using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;

/// <summary>Acceso al singleton ContadorVoucher (Id = 1).</summary>
public interface IContadorVoucherRepository
{
    Task<ContadorVoucher> GetAsync(CancellationToken ct = default);
    Task SaveAsync(ContadorVoucher contador, CancellationToken ct = default);

    /// <summary>Incrementa atómicamente y devuelve el siguiente número de voucher.</summary>
    Task<int> ObtenerSiguienteNumeroAsync(CancellationToken ct = default);
}

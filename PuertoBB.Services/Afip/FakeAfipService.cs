using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Services.Afip;

/// <summary>
/// Implementación falsa del servicio AFIP para desarrollo/testing sin red ni certificado.
/// Genera un CAE determinístico y simula la numeración secuencial por (puntoVenta, tipo).
/// </summary>
public class FakeAfipService : IAfipService
{
    private readonly ILogger<FakeAfipService> _logger;
    private readonly ConcurrentDictionary<(int pv, int tipo), long> _ultimoNumero = new();

    public FakeAfipService(ILogger<FakeAfipService> logger) => _logger = logger;

    public Task<ServiceResult<CaeResult>> ObtenerCAEAsync(ComprobanteAfipRequest request, CancellationToken ct = default)
    {
        var numero = _ultimoNumero.AddOrUpdate((request.PuntoDeVenta, request.CodigoAfip), 1, (_, v) => v + 1);

        // CAE simulado de 14 dígitos determinístico, con formato realista.
        var cae = $"{DateTime.Now:yyyyMMdd}{numero % 1000000:000000}";
        var resultado = new CaeResult
        {
            NumeroComprobante = numero,
            Cae = cae,
            FechaVencimientoCae = request.FechaEmision.AddDays(10)
        };

        _logger.LogInformation("[FAKE] CAE simulado: PV={PuntoVenta} Tipo={Tipo} Nro={Numero} CAE={Cae}",
            request.PuntoDeVenta, request.CodigoAfip, numero, cae);

        return Task.FromResult(ServiceResult<CaeResult>.Ok(resultado));
    }

    public Task<ServiceResult<bool>> VerificarServicioAsync(CancellationToken ct = default)
        => Task.FromResult(ServiceResult<bool>.Ok(true));
}

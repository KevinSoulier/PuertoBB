using global::Afip;
using global::Afip.Wsfe;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Common;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Afip;

namespace PuertoBB.Services.Afip;

/// <summary>
/// Adaptador entre el dominio PuertoBB (<see cref="IAfipService"/>) y la librería neutra Afip.Net
/// (<see cref="IWsfeService"/>). Traduce los modelos del dominio ↔ los modelos de la librería y
/// toma la configuración vigente de cada app vía <see cref="IAfipConfigProvider"/>.
/// </summary>
public class AfipService : IAfipService
{
    private readonly IWsfeService _wsfe;
    private readonly IAfipConfigProvider _configProvider;
    private readonly ILogger<AfipService> _logger;

    public AfipService(IWsfeService wsfe, IAfipConfigProvider configProvider, ILogger<AfipService> logger)
    {
        _wsfe = wsfe;
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<CaeResult>> ObtenerCAEAsync(ComprobanteAfipRequest request, CancellationToken ct = default)
    {
        try
        {
            var config = await _configProvider.GetAsync(ct);
            if (!CertificadoDisponible(config))
                return ServiceResult<CaeResult>.Fail(
                    "No hay certificado AFIP configurado. Cargue el certificado en Configuración o use el modo de prueba.");

            if (!long.TryParse(request.CuitReceptor, out _))
                return ServiceResult<CaeResult>.Fail("El CUIT/identificación del receptor no es válido.");
            if (request.ComprobanteAsociado is { } asociado && !long.TryParse(asociado.CuitEmisor, out _))
                return ServiceResult<CaeResult>.Fail("El CUIT del emisor del comprobante asociado no es válido.");

            var resp = await _wsfe.SolicitarCaeAsync(ToOptions(config), ToAfipRequest(request), ct);
            if (!resp.Aprobado || string.IsNullOrWhiteSpace(resp.Cae))
            {
                _logger.LogError("AFIP rechazó el comprobante PV={PuntoVenta} Tipo={Tipo}: {Obs}",
                    request.PuntoDeVenta, request.CodigoAfip, resp.Observaciones);
                return ServiceResult<CaeResult>.Fail($"AFIP rechazó el comprobante: {AfipErrores.Describir(resp.Observaciones)}");
            }

            if (resp.FechaVencimientoCae is null)
                _logger.LogWarning("AFIP no devolvió FechaVencimientoCae para PV={PuntoVenta} Tipo={Tipo} Nro={Numero}", request.PuntoDeVenta, request.CodigoAfip, resp.Numero);

            return ServiceResult<CaeResult>.Ok(new CaeResult
            {
                NumeroComprobante = resp.Numero,
                Cae = resp.Cae,
                FechaVencimientoCae = resp.FechaVencimientoCae ?? default
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al solicitar CAE PV={PuntoVenta} Tipo={Tipo}", request.PuntoDeVenta, request.CodigoAfip);
            return ServiceResult<CaeResult>.Fail($"Error de comunicación con AFIP: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> VerificarServicioAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await _configProvider.GetAsync(ct);
            var ok = await _wsfe.VerificarServicioAsync(ToOptions(config), ct);
            return ok ? ServiceResult<bool>.Ok(true) : ServiceResult<bool>.Fail("WSFE no respondió OK.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Healthcheck AFIP falló");
            return ServiceResult<bool>.Fail($"No se pudo contactar AFIP: {ex.Message}");
        }
    }

    public async Task<ServiceResult<DiagnosticoAfip>> ProbarConexionAsync(int puntoVenta, int codigoComprobante, CancellationToken ct = default)
    {
        var config = await _configProvider.GetAsync(ct);
        var options = ToOptions(config);
        var detalles = new List<string>();

        bool servicioOk = false;
        try
        {
            servicioOk = await _wsfe.VerificarServicioAsync(options, ct);
            detalles.Add(servicioOk ? "Servicio WSFE: OK." : "Servicio WSFE: no respondió OK.");
        }
        catch (Exception ex)
        {
            detalles.Add($"Servicio WSFE: error ({ex.Message}).");
        }

        if (!CertificadoDisponible(config))
        {
            detalles.Add("No hay certificado configurado: no se puede probar la autenticación.");
            return ServiceResult<DiagnosticoAfip>.Ok(new DiagnosticoAfip
            {
                ServicioOk = servicioOk,
                AutenticacionOk = false,
                Detalle = string.Join(" · ", detalles)
            });
        }

        bool autOk = false;
        long? ultimo = null;
        try
        {
            ultimo = await _wsfe.UltimoComprobanteAsync(options, puntoVenta, codigoComprobante, ct);
            autOk = true;
            detalles.Add($"Autenticación: OK. Último comprobante (PV {puntoVenta}, tipo {codigoComprobante}): {ultimo}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diagnóstico AFIP: autenticación falló PV={PuntoVenta} Tipo={Tipo}", puntoVenta, codigoComprobante);
            detalles.Add($"Autenticación: error ({ex.Message}). Revise el certificado, la contraseña y que el servicio 'wsfe' esté habilitado para el CUIT.");
        }

        return ServiceResult<DiagnosticoAfip>.Ok(new DiagnosticoAfip
        {
            ServicioOk = servicioOk,
            AutenticacionOk = autOk,
            UltimoComprobante = ultimo,
            Detalle = string.Join(" · ", detalles)
        });
    }

    private static bool CertificadoDisponible(AfipConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CertificadoRuta) || !File.Exists(config.CertificadoRuta))
            return false;
        if (config.CertificadoKeyRuta is not null)
            return File.Exists(config.CertificadoKeyRuta);
        return true;
    }

    private static AfipOptions ToOptions(AfipConfig c) => new()
    {
        Cuit = c.CuitEmisor,
        CertificadoRuta = c.CertificadoRuta,
        CertificadoPassword = c.CertificadoPassword,
        CertificadoKeyRuta = c.CertificadoKeyRuta,
        UsarHomologacion = c.UsarHomologacion
    };

    private static AfipComprobanteRequest ToAfipRequest(ComprobanteAfipRequest r) => new()
    {
        CodigoComprobante = r.CodigoAfip,
        PuntoDeVenta = r.PuntoDeVenta,
        DocNroReceptor = long.TryParse(r.CuitReceptor, out var docReceptor) ? docReceptor : 0,
        ImporteTotal = r.ImporteTotal,
        FechaComprobante = r.FechaEmision,
        ServicioDesde = r.PeriodoServicioDesde,
        ServicioHasta = r.PeriodoServicioHasta,
        VencimientoPago = r.FechaVencimientoPago,
        ComprobanteAsociado = r.ComprobanteAsociado is { } a
            ? new AfipComprobanteAsociado { Tipo = a.Tipo, PuntoDeVenta = a.PuntoDeVenta, Numero = a.Numero, Cuit = long.TryParse(a.CuitEmisor, out var cuitEmisor) ? cuitEmisor : 0 }
            : null
    };
}

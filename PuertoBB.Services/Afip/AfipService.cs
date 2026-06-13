using global::Afip;
using global::Afip.Wsfe;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Afip;
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
    /// <summary>Ventana (en días corridos) que AFIP acepta para CbteFch en Concepto Servicios: ±10.</summary>
    private const int VentanaFechaDias = 10;

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
            if (request.CondicionIvaReceptorId <= 0)
                return ServiceResult<CaeResult>.Fail(
                    "Falta la condición frente al IVA del receptor (obligatoria por RG 5616). Asígnela en el ABM correspondiente.");
            if (request.ComprobanteAsociado is { } asociado && !long.TryParse(asociado.CuitEmisor, out _))
                return ServiceResult<CaeResult>.Fail("El CUIT del emisor del comprobante asociado no es válido.");
            if (request.ImporteTotal <= 0)
                return ServiceResult<CaeResult>.Fail("El importe total debe ser mayor a cero.");
            // Concepto Servicios: AFIP acepta CbteFch hasta ±10 días corridos respecto de hoy.
            if (Math.Abs((request.FechaEmision.Date - DateTime.Today).Days) > VentanaFechaDias)
                return ServiceResult<CaeResult>.Fail(
                    $"La fecha de emisión debe estar dentro de los {VentanaFechaDias} días corridos respecto de hoy (límite de AFIP).");

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
        catch (global::Afip.Wsfe.AfipRespuestaPerdidaException ex)
        {
            // El comprobante PODRÍA haberse autorizado en AFIP: NO reintentar a ciegas (duplicaría).
            _logger.LogError(ex, "Respuesta de CAE perdida PV={PuntoVenta} Tipo={Tipo}: posible emisión sin confirmar", request.PuntoDeVenta, request.CodigoAfip);
            return ServiceResult<CaeResult>.Fail(
                "Posible emisión en AFIP: la conexión se interrumpió y no se pudo confirmar el comprobante. " +
                "Verifique el último comprobante del punto de venta antes de reintentar (un reintento podría duplicarlo).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al solicitar CAE PV={PuntoVenta} Tipo={Tipo}", request.PuntoDeVenta, request.CodigoAfip);
            return ServiceResult<CaeResult>.Fail($"Error de comunicación con AFIP: {ex.Message}");
        }
    }

    public async Task<ServiceResult<CaeResult?>> RecuperarComprobanteAsync(ComprobanteAfipRequest request, CancellationToken ct = default)
    {
        try
        {
            var config = await _configProvider.GetAsync(ct);
            if (!CertificadoDisponible(config))
                return ServiceResult<CaeResult?>.Ok(null);   // sin certificado no hay nada que consultar

            var recuperado = await _wsfe.RecuperarSiYaEmitidoAsync(ToOptions(config), ToAfipRequest(request), ct);
            if (recuperado is null || string.IsNullOrWhiteSpace(recuperado.Cae))
                return ServiceResult<CaeResult?>.Ok(null);

            _logger.LogWarning("Comprobante recuperado de AFIP PV={PuntoVenta} Tipo={Tipo} Nro={Numero} CAE={Cae}",
                request.PuntoDeVenta, request.CodigoAfip, recuperado.Numero, recuperado.Cae);

            return ServiceResult<CaeResult?>.Ok(new CaeResult
            {
                NumeroComprobante = recuperado.Numero,
                Cae = recuperado.Cae!,
                FechaVencimientoCae = recuperado.FechaVencimientoCae ?? default
            });
        }
        catch (Exception ex)
        {
            // Best-effort: si la recuperación falla, NO bloquea la emisión normal posterior.
            _logger.LogWarning(ex, "Recuperación de comprobante falló PV={PuntoVenta} Tipo={Tipo}", request.PuntoDeVenta, request.CodigoAfip);
            return ServiceResult<CaeResult?>.Ok(null);
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

        // Verificaciones FEParamGet* (solo con autenticación OK). Cada una en su try/catch:
        // un fallo acá degrada el detalle pero NUNCA el resultado de la autenticación.
        bool? pvOk = null, tipoOk = null;
        IReadOnlyList<string>? condiciones = null;
        if (autOk)
        {
            try
            {
                var pvs = await _wsfe.ObtenerPuntosVentaAsync(options, ct);
                if (pvs.Count == 0)
                {
                    detalles.Add("Puntos de venta: AFIP no informó la lista (habitual en homologación).");
                }
                else if (pvs.FirstOrDefault(p => p.Numero == puntoVenta) is not { } pv)
                {
                    pvOk = false;
                    detalles.Add($"Punto de venta {puntoVenta}: NO figura entre los habilitados para Web Services ({string.Join(", ", pvs.Select(p => p.Numero))}).");
                }
                else if (pv.Bloqueado || pv.FechaBaja is not null)
                {
                    pvOk = false;
                    detalles.Add($"Punto de venta {puntoVenta}: figura BLOQUEADO o dado de baja en AFIP.");
                }
                else if (!string.Equals(pv.EmisionTipo, "CAE", StringComparison.OrdinalIgnoreCase))
                {
                    pvOk = false;
                    detalles.Add($"Punto de venta {puntoVenta}: es de tipo {pv.EmisionTipo}, no sirve para solicitar CAE.");
                }
                else
                {
                    pvOk = true;
                    detalles.Add($"Punto de venta {puntoVenta}: habilitado (CAE).");
                }
            }
            catch (Exception ex) { detalles.Add($"Puntos de venta: no se pudo verificar ({ex.Message})."); }

            try
            {
                var tipos = await _wsfe.ObtenerTiposComprobanteAsync(options, ct);
                if (tipos.Count == 0)
                {
                    detalles.Add("Tipos de comprobante: AFIP no informó la tabla.");
                }
                else if (tipos.FirstOrDefault(t => t.Id == codigoComprobante) is not { } tipo)
                {
                    tipoOk = false;
                    detalles.Add($"Tipo {codigoComprobante}: NO existe en la tabla de AFIP.");
                }
                else if (tipo.VigenteHasta is { } hasta && hasta < DateTime.Today)
                {
                    tipoOk = false;
                    detalles.Add($"Tipo {codigoComprobante} ({tipo.Descripcion}): fuera de vigencia desde {hasta:dd/MM/yyyy}.");
                }
                else
                {
                    tipoOk = true;
                    detalles.Add($"Tipo {codigoComprobante} ({tipo.Descripcion}): vigente.");
                }
            }
            catch (Exception ex) { detalles.Add($"Tipos de comprobante: no se pudo verificar ({ex.Message})."); }

            try
            {
                var clase = CatalogoComprobantesAfip.PorCodigo(codigoComprobante)?.Clase.ToString();
                var conds = await _wsfe.ObtenerCondicionesIvaReceptorAsync(options, clase, ct);
                if (conds.Count > 0)
                {
                    condiciones = conds.Select(c => $"{c.Id} — {c.Descripcion}").ToList();
                    detalles.Add($"Condiciones IVA receptor válidas{(clase is null ? "" : $" (clase {clase})")}: {string.Join(", ", conds.Select(c => c.Id))}.");
                }
                else
                {
                    detalles.Add("Condiciones IVA receptor: AFIP no informó la tabla.");
                }
            }
            catch (Exception ex) { detalles.Add($"Condiciones IVA receptor: no se pudo verificar ({ex.Message})."); }
        }

        return ServiceResult<DiagnosticoAfip>.Ok(new DiagnosticoAfip
        {
            ServicioOk = servicioOk,
            AutenticacionOk = autOk,
            UltimoComprobante = ultimo,
            PuntoVentaOk = pvOk,
            TipoComprobanteOk = tipoOk,
            CondicionesIvaReceptor = condiciones,
            Detalle = string.Join(" · ", detalles)
        });
    }

    private static bool CertificadoDisponible(AfipConfig config)
    {
        if (config.CertificadoContenido is not { Length: > 0 })
            return false;
        if (config.CertificadoKeyContenido is not null)
            return config.CertificadoKeyContenido.Length > 0;
        return true;
    }

    private static AfipOptions ToOptions(AfipConfig c) => new()
    {
        Cuit = c.CuitEmisor,
        CertificadoContenido = c.CertificadoContenido,
        CertificadoPassword = c.CertificadoPassword,
        CertificadoKeyContenido = c.CertificadoKeyContenido,
        UsarHomologacion = c.UsarHomologacion
    };

    private static AfipComprobanteRequest ToAfipRequest(ComprobanteAfipRequest r) => new()
    {
        CodigoComprobante = r.CodigoAfip,
        PuntoDeVenta = r.PuntoDeVenta,
        DocNroReceptor = long.TryParse(r.CuitReceptor, out var docReceptor) ? docReceptor : 0,
        CondicionIvaReceptorId = r.CondicionIvaReceptorId,
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

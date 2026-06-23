using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Afip.Wsaa;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using Microsoft.Win32;
using PuertoBB.Core.Afip;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;

namespace CentroMaritimo.UI.ViewModels;

public class ConfiguracionViewModel : PageViewModel
{
    private readonly IConfiguracionRepository _repo;
    private readonly IContadorVoucherRepository _contador;
    private readonly IBackupService _backup;
    private readonly IAfipService _afip;
    private readonly IMailService _mail;
    private readonly IOAuthInteractiveFlow _oauth;
    private readonly IDialogService _dialog;
    private Configuracion _config = new();

    // ══════════════════════════════════════════
    // EMISOR
    // ══════════════════════════════════════════
    public string    RazonSocial       { get => _config.RazonSocial;        set { _config.RazonSocial = value;        OnPropertyChanged(); } }
    public string    Cuit              { get => _config.Cuit;               set { _config.Cuit = value;               OnPropertyChanged(); } }
    public string?   IngresosBrutos   { get => _config.IngresosBrutos;     set { _config.IngresosBrutos = value;     OnPropertyChanged(); } }
    public DateTime? InicioActividades { get => _config.InicioActividades;  set { _config.InicioActividades = value;  OnPropertyChanged(); } }

    private bool _emisorEditando;
    public bool EmisorEditando   { get => _emisorEditando;  set { SetField(ref _emisorEditando, value);  OnPropertyChanged(nameof(EmisorNoEditando));  } }
    public bool EmisorNoEditando => !_emisorEditando;

    private string    _snapRazonSocial       = string.Empty;
    private string    _snapCuit              = string.Empty;
    private string?   _snapIngresosBrutos;
    private DateTime? _snapInicioActividades;

    public ICommand EditarEmisorCommand   { get; }
    public ICommand GuardarEmisorCommand  { get; }
    public ICommand CancelarEmisorCommand { get; }

    private void EditarEmisor()
    {
        _snapRazonSocial       = RazonSocial;
        _snapCuit              = Cuit;
        _snapIngresosBrutos    = IngresosBrutos;
        _snapInicioActividades = InicioActividades;
        EmisorEditando         = true;
    }
    private void CancelarEmisor()
    {
        RazonSocial        = _snapRazonSocial;
        Cuit               = _snapCuit;
        IngresosBrutos     = _snapIngresosBrutos;
        InicioActividades  = _snapInicioActividades;
        EmisorEditando     = false;
    }
    private async Task GuardarEmisorAsync()
    {
        if (string.IsNullOrWhiteSpace(Cuit)) { MostrarError("El CUIT del emisor es obligatorio."); return; }
        try   { await _repo.SaveAsync(_config); EmisorEditando = false; MostrarExito("Emisor guardado."); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // CÓDIGOS AFIP
    // ══════════════════════════════════════════
    /// <summary>Comprobantes seleccionables en el combo (Recibos + Facturas A/B/C).</summary>
    public IReadOnlyList<ComprobanteAfipTipo> ComprobantesDisponibles => CatalogoComprobantesAfip.Principales;

    public int CodigoAfipRecibo
    {
        get => _config.CodigoAfipRecibo;
        set
        {
            _config.CodigoAfipRecibo = value;
            // La Nota de Crédito se deriva de la clase fiscal del comprobante elegido.
            _config.CodigoAfipNotaDeCredito = CatalogoComprobantesAfip.NotaCreditoPara(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CodigoAfipNotaDeCredito));
            OnPropertyChanged(nameof(NotaDeCreditoDisplay));
        }
    }
    public int CodigoAfipNotaDeCredito { get => _config.CodigoAfipNotaDeCredito; set { _config.CodigoAfipNotaDeCredito = value; OnPropertyChanged(); } }

    /// <summary>Nota de Crédito derivada (solo lectura): "13 — Nota de Crédito C".</summary>
    public string NotaDeCreditoDisplay =>
        $"{CodigoAfipNotaDeCredito} — {CatalogoComprobantesAfip.DescripcionNotaCredito(CodigoAfipRecibo)}";

    private bool _codigosAfipEditando;
    public bool CodigosAfipEditando   { get => _codigosAfipEditando; set { SetField(ref _codigosAfipEditando, value); OnPropertyChanged(nameof(CodigosAfipNoEditando)); } }
    public bool CodigosAfipNoEditando => !_codigosAfipEditando;

    private int _snapCodigoRecibo;
    private int _snapCodigoNC;

    public ICommand EditarCodigosAfipCommand   { get; }
    public ICommand GuardarCodigosAfipCommand  { get; }
    public ICommand CancelarCodigosAfipCommand { get; }

    private void EditarCodigosAfip()
    {
        _snapCodigoRecibo   = CodigoAfipRecibo;
        _snapCodigoNC       = CodigoAfipNotaDeCredito;
        CodigosAfipEditando = true;
    }
    private void CancelarCodigosAfip()
    {
        CodigoAfipRecibo        = _snapCodigoRecibo;
        CodigoAfipNotaDeCredito = _snapCodigoNC;
        CodigosAfipEditando     = false;
    }
    private async Task GuardarCodigosAfipAsync()
    {
        try   { await _repo.SaveAsync(_config); CodigosAfipEditando = false; MostrarExito("Códigos AFIP guardados."); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // PUNTOS DE VENTA — lista
    // ══════════════════════════════════════════
    public ObservableCollection<PuntoDeVentaItem> PuntosDeVenta { get; } = new();

    private PuntoDeVentaItem? _puntoSeleccionado;
    public PuntoDeVentaItem? PuntoSeleccionado
    {
        get => _puntoSeleccionado;
        set { if (SetField(ref _puntoSeleccionado, value) && value is not null) CargarEnEdicion(value); }
    }

    // Formulario de edición del punto de venta.
    private int     _edId;
    private bool    _edActivo;
    private string  _edNombre              = string.Empty;
    private int     _edNumero              = 1;
    private bool    _edUsarHomologacion;
    private string? _edCertificadoRuta;
    private byte[]? _edCertificadoContenido;
    private string? _edCertificadoPassword;
    private string? _edCertificadoKeyRuta;
    private byte[]? _edCertificadoKeyContenido;
    private ModoCertificado _edModo = ModoCertificado.P12;
    private bool    _edMostrarPassword;

    /// <summary>Cómo se aporta el certificado del punto de venta. La creación de un certificado nuevo
    /// vive en el asistente (<see cref="AbrirWizardCertificadoAsync"/>), no en un modo del formulario.</summary>
    private enum ModoCertificado { P12, CrtKey }

    public string  EdNombre              { get => _edNombre;              set => SetField(ref _edNombre, value); }
    public int     EdNumero              { get => _edNumero;              set => SetField(ref _edNumero, value); }
    public bool    EdUsarHomologacion    { get => _edUsarHomologacion;    set { if (SetField(ref _edUsarHomologacion, value)) OnPropertyChanged(nameof(AmbienteEdicion)); } }
    public string? EdCertificadoRuta     { get => _edCertificadoRuta;     set => SetField(ref _edCertificadoRuta, value); }
    public string? EdCertificadoPassword { get => _edCertificadoPassword; set => SetField(ref _edCertificadoPassword, value); }
    public string? EdCertificadoKeyRuta  { get => _edCertificadoKeyRuta;  set => SetField(ref _edCertificadoKeyRuta, value); }
    // Modo de certificado: P12 (con password) | CRT+KEY (archivos).
    public bool EdModoP12     { get => _edModo == ModoCertificado.P12;     set { if (value) SetModo(ModoCertificado.P12); } }
    public bool EdModoCrtKey  { get => _edModo == ModoCertificado.CrtKey;  set { if (value) SetModo(ModoCertificado.CrtKey); } }

    private void SetModo(ModoCertificado modo)
    {
        if (_edModo == modo) return;
        _edModo = modo;
        OnPropertyChanged(nameof(EdModoP12));
        OnPropertyChanged(nameof(EdModoCrtKey));
        if (modo != ModoCertificado.P12) EdCertificadoPassword = null;
        if (modo == ModoCertificado.P12) EdCertificadoKeyRuta  = null;
        NotificarEstadoCert();
    }

    // Piezas del certificado cargadas en el form (habilitan "Exportar .p12" en modo CRT+KEY).
    public bool EdTieneCrt => _edCertificadoContenido    is { Length: > 0 };
    public bool EdTieneKey => _edCertificadoKeyContenido is { Length: > 0 };
    public bool EdPuedeExportarP12 => EdTieneCrt && EdTieneKey;

    private void NotificarEstadoCert()
    {
        OnPropertyChanged(nameof(EdTieneCrt));
        OnPropertyChanged(nameof(EdTieneKey));
        OnPropertyChanged(nameof(EdPuedeExportarP12));
    }

    public bool EdMostrarPassword
    {
        get => _edMostrarPassword;
        set { SetField(ref _edMostrarPassword, value); OnPropertyChanged(nameof(EdOcultarPassword)); }
    }
    public bool EdOcultarPassword => !_edMostrarPassword;

    private bool _pvFormEditando;
    public bool PvFormEditando   { get => _pvFormEditando; set { SetField(ref _pvFormEditando, value); OnPropertyChanged(nameof(PvFormNoEditando)); } }
    public bool PvFormNoEditando => !_pvFormEditando;

    // Snapshot para revert
    private int     _snapEdId;
    private string  _snapEdNombre              = string.Empty;
    private int     _snapEdNumero              = 1;
    private bool    _snapEdUsarHomologacion;
    private string? _snapEdCertificadoRuta;
    private byte[]? _snapEdCertificadoContenido;
    private string? _snapEdCertificadoPassword;
    private string? _snapEdCertificadoKeyRuta;
    private byte[]? _snapEdCertificadoKeyContenido;
    private ModoCertificado _snapEdModo;

    public string TituloEdicion => _edId == 0 ? "Nuevo punto de venta" : $"Editando: {EdNombre}";
    public string AmbienteEdicion => EdUsarHomologacion
        ? "⚠ Homologación (pruebas). Los comprobantes no tienen validez fiscal."
        : "Producción. Los comprobantes tienen validez fiscal.";

    private string _estadoConexion = string.Empty;
    public string EstadoConexion { get => _estadoConexion; set => SetField(ref _estadoConexion, value); }

    public ICommand NuevoPuntoCommand             { get; }
    public ICommand EditarPuntoCommand            { get; }
    public ICommand GuardarPuntoCommand           { get; }
    public ICommand CancelarPuntoCommand          { get; }
    public ICommand EliminarPuntoCommand          { get; }
    public ICommand MarcarActivoCommand           { get; }
    public ICommand SeleccionarCertificadoCommand { get; }
    public ICommand SeleccionarClaveKeyCommand    { get; }
    public ICommand ProbarConexionCommand         { get; }
    public ICommand ToggleMostrarPasswordCommand  { get; }
    public ICommand GenerarCertificadoCommand     { get; }
    public ICommand DescargarCrtCommand           { get; }
    public ICommand DescargarKeyCommand           { get; }
    public ICommand ExportarP12Command            { get; }

    private void NuevoPunto()
    {
        _edId                 = 0;
        _edActivo             = false;
        EdNombre              = string.Empty;
        EdNumero              = 1;
        EdUsarHomologacion        = false;
        EdCertificadoRuta         = null;
        _edCertificadoContenido   = null;
        EdCertificadoPassword     = null;
        EdCertificadoKeyRuta      = null;
        _edCertificadoKeyContenido = null;
        SetModo(ModoCertificado.P12);
        NotificarEstadoCert();
        EdMostrarPassword         = false;
        CapturaSnapshotPv();
        PvFormEditando = true;
        OnPropertyChanged(nameof(TituloEdicion));
    }

    private void EditarPunto()
    {
        CapturaSnapshotPv();
        PvFormEditando = true;
    }

    private void CancelarPunto()
    {
        _edId                      = _snapEdId;
        EdNombre                   = _snapEdNombre;
        EdNumero                   = _snapEdNumero;
        EdUsarHomologacion         = _snapEdUsarHomologacion;
        EdCertificadoRuta          = _snapEdCertificadoRuta;
        _edCertificadoContenido    = _snapEdCertificadoContenido;
        EdCertificadoPassword      = _snapEdCertificadoPassword;
        EdCertificadoKeyRuta       = _snapEdCertificadoKeyRuta;
        _edCertificadoKeyContenido = _snapEdCertificadoKeyContenido;
        SetModo(_snapEdModo);
        NotificarEstadoCert();
        EdMostrarPassword          = false;
        PvFormEditando = false;
        OnPropertyChanged(nameof(TituloEdicion));
    }

    private void CapturaSnapshotPv()
    {
        _snapEdId                  = _edId;
        _snapEdNombre              = EdNombre;
        _snapEdNumero              = EdNumero;
        _snapEdUsarHomologacion        = EdUsarHomologacion;
        _snapEdCertificadoRuta         = EdCertificadoRuta;
        _snapEdCertificadoContenido    = _edCertificadoContenido;
        _snapEdCertificadoPassword     = EdCertificadoPassword;
        _snapEdCertificadoKeyRuta      = EdCertificadoKeyRuta;
        _snapEdCertificadoKeyContenido = _edCertificadoKeyContenido;
        _snapEdModo                    = _edModo;
    }

    private void CargarEnEdicion(PuntoDeVentaItem item)
    {
        _edId                 = item.Id;
        _edActivo             = item.Activo;
        EdNombre              = item.Nombre;
        EdNumero              = item.Numero;
        EdUsarHomologacion         = item.UsarHomologacion;
        EdCertificadoRuta          = item.CertificadoRuta;
        _edCertificadoContenido    = item.CertificadoContenido;
        EdCertificadoPassword      = item.CertificadoPassword;
        EdCertificadoKeyRuta       = item.CertificadoKeyRuta;
        _edCertificadoKeyContenido = item.CertificadoKeyContenido;
        // La presencia de la clave indica modo CRT+KEY; en última instancia, P12.
        _edModo = item.CertificadoKeyContenido is { Length: > 0 } || item.CertificadoKeyRuta is not null
                ? ModoCertificado.CrtKey
                : ModoCertificado.P12;
        OnPropertyChanged(nameof(EdModoP12));
        OnPropertyChanged(nameof(EdModoCrtKey));
        NotificarEstadoCert();
        EdMostrarPassword          = false;
        // NO habilitar: solo el botón Editar habilita el formulario
        OnPropertyChanged(nameof(TituloEdicion));
    }

    // ══════════════════════════════════════════
    // VOUCHERS
    // ══════════════════════════════════════════
    public decimal ImporteVoucherPredeterminado { get => _config.ImporteVoucherPredeterminado; set { _config.ImporteVoucherPredeterminado = value; OnPropertyChanged(); } }

    private int _ultimoNumeroVoucher;
    public int UltimoNumeroVoucher { get => _ultimoNumeroVoucher; set => SetField(ref _ultimoNumeroVoucher, value); }

    private bool _vouchersEditando;
    public bool VouchersEditando   { get => _vouchersEditando; set { SetField(ref _vouchersEditando, value); OnPropertyChanged(nameof(VouchersNoEditando)); } }
    public bool VouchersNoEditando => !_vouchersEditando;

    private decimal _snapImporteVoucher;

    public ICommand EditarVouchersCommand   { get; }
    public ICommand GuardarVouchersCommand  { get; }
    public ICommand CancelarVouchersCommand { get; }
    public ICommand GuardarContadorCommand  { get; }

    private void EditarVouchers()
    {
        _snapImporteVoucher = ImporteVoucherPredeterminado;
        VouchersEditando    = true;
    }
    private void CancelarVouchers()
    {
        ImporteVoucherPredeterminado = _snapImporteVoucher;
        VouchersEditando             = false;
    }
    private async Task GuardarVouchersAsync()
    {
        try   { await _repo.SaveAsync(_config); VouchersEditando = false; MostrarExito("Importe predeterminado guardado."); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }
    private async Task GuardarContadorAsync()
    {
        try
        {
            var contador = await _contador.GetAsync();
            contador.UltimoNumero = UltimoNumeroVoucher;
            await _contador.SaveAsync(contador);
            MostrarExito($"Numeración actualizada. Próximo voucher: {UltimoNumeroVoucher + 1}.");
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar la numeración: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // PAGOS
    // ══════════════════════════════════════════
    public int DiasVencimiento { get => _config.DiasVencimiento; set { _config.DiasVencimiento = value; OnPropertyChanged(); } }

    private bool _pagosEditando;
    public bool PagosEditando   { get => _pagosEditando; set { SetField(ref _pagosEditando, value); OnPropertyChanged(nameof(PagosNoEditando)); } }
    public bool PagosNoEditando => !_pagosEditando;

    private int _snapDiasVencimiento;

    public ICommand EditarPagosCommand   { get; }
    public ICommand GuardarPagosCommand  { get; }
    public ICommand CancelarPagosCommand { get; }

    private void EditarPagos()
    {
        _snapDiasVencimiento = DiasVencimiento;
        PagosEditando        = true;
    }
    private void CancelarPagos()
    {
        DiasVencimiento = _snapDiasVencimiento;
        PagosEditando   = false;
    }
    private async Task GuardarPagosAsync()
    {
        try   { await _repo.SaveAsync(_config); PagosEditando = false; MostrarExito("Configuración de pagos guardada."); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // CORREO (varias cuentas; una activa)
    // ══════════════════════════════════════════
    public ObservableCollection<CuentaCorreoItem> CuentasCorreo { get; } = new();
    private List<CuentaCorreo> _cuentasClientees = new();

    private CuentaCorreoItem? _cuentaSeleccionada;
    public CuentaCorreoItem? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set { if (SetField(ref _cuentaSeleccionada, value) && value is not null) CargarCuentaPorId(value.Id); }
    }

    // Form de edición (working copy)
    private CuentaCorreo _ctaEd = new() { SmtpPort = 587, Autenticacion = 1 };
    private int  _ctaEdId;
    private bool _ctaEdActivo;
    private bool _loginSinGuardar; // hizo "Iniciar sesión…" pero todavía no guardó la cuenta

    public string  CtaNombre         { get => _ctaEd.Nombre;         set { _ctaEd.Nombre = value; OnPropertyChanged(); OnPropertyChanged(nameof(TituloCuentaEdicion)); } }
    public string? CtaSmtpHost       { get => _ctaEd.SmtpHost;       set { _ctaEd.SmtpHost = value; OnPropertyChanged(); } }
    public int     CtaSmtpPort       { get => _ctaEd.SmtpPort;       set { _ctaEd.SmtpPort = value; OnPropertyChanged(); } }
    public int     CtaSmtpSeguridad  { get => _ctaEd.SmtpSeguridad;  set { _ctaEd.SmtpSeguridad = value; OnPropertyChanged(); } }
    public string? CtaEmailRemitente { get => _ctaEd.EmailRemitente; set { _ctaEd.EmailRemitente = value; OnPropertyChanged(); } }
    public string? CtaSmtpUsuario    { get => _ctaEd.SmtpUsuario;    set { _ctaEd.SmtpUsuario = value; OnPropertyChanged(); } }
    public string? CtaSmtpPassword   { get => _ctaEd.SmtpPassword;   set { _ctaEd.SmtpPassword = value; OnPropertyChanged(); } }

    // Mostrar/ocultar secretos (contraseña básica + client secret)
    private bool _ctaMostrarSecretos;
    public bool CtaMostrarSecretos { get => _ctaMostrarSecretos; set { SetField(ref _ctaMostrarSecretos, value); OnPropertyChanged(nameof(CtaOcultarSecretos)); } }
    public bool CtaOcultarSecretos => !_ctaMostrarSecretos;

    // Autenticación (0=Ninguna, 1=Básica, 2=OAuth2)
    public int CtaAutenticacion
    {
        get => _ctaEd.Autenticacion;
        set { _ctaEd.Autenticacion = value; OnPropertyChanged(); OnPropertyChanged(nameof(CtaEsBasica)); OnPropertyChanged(nameof(CtaEsOAuth2)); }
    }
    public bool CtaEsBasica => _ctaEd.Autenticacion == 1;
    public bool CtaEsOAuth2 => _ctaEd.Autenticacion == 2;

    // Proveedor (combo): 0=Microsoft 365, 1=Outlook personal, 2=Google, 3=Otro/Personalizado
    public int CtaProveedorIndice
    {
        get => _ctaEd.OAuthProveedor switch { 0 => 0, 3 => 1, 1 => 2, _ => 3 };
        set
        {
            var (prov, nombrado) = value switch
            {
                0 => (0, true),  // Microsoft 365
                1 => (3, true),  // Outlook.com personal
                2 => (1, true),  // Google
                _ => (2, false)  // Otro / Personalizado
            };
            _ctaEd.OAuthProveedor = prov;
            if (nombrado)
            {
                CtaAutenticacion = prov == 1 ? 1 : 2; // Google → Básica (contraseña de aplicación, recomendado); Microsoft/Outlook → OAuth2
                if (prov is 1 or 3) SetCtaFlujo(0);   // Google / Outlook personal: si usan OAuth2, solo Interactivo
                AutocompletarTransporte((PuertoBB.Core.Models.Mail.OAuthProveedor)prov);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(CtaGuiaProveedor));
            NotificarVisibilidadOAuth();
        }
    }

    // Flujo OAuth (0=Interactivo, 1=Cliente)
    public bool CtaFlujoInteractivo { get => _ctaEd.OAuthFlujo == 0; set { if (value) SetCtaFlujo(0); } }
    public bool CtaFlujoCliente     { get => _ctaEd.OAuthFlujo == 1; set { if (value) SetCtaFlujo(1); } }
    private void SetCtaFlujo(int flujo)
    {
        _ctaEd.OAuthFlujo = flujo;
        OnPropertyChanged(nameof(CtaFlujoInteractivo));
        OnPropertyChanged(nameof(CtaFlujoCliente));
        NotificarVisibilidadOAuth();
    }

    public string? CtaOAuthClientId          { get => _ctaEd.OAuthClientId;          set { _ctaEd.OAuthClientId = value; OnPropertyChanged(); } }
    public string? CtaOAuthClientSecret      { get => _ctaEd.OAuthClientSecret;      set { _ctaEd.OAuthClientSecret = value; OnPropertyChanged(); } }
    public string? CtaOAuthTenantId          { get => _ctaEd.OAuthTenantId;          set { _ctaEd.OAuthTenantId = value; OnPropertyChanged(); } }
    public string? CtaOAuthScope             { get => _ctaEd.OAuthScope;             set { _ctaEd.OAuthScope = value; OnPropertyChanged(); } }
    public string? CtaOAuthAuthorizeEndpoint { get => _ctaEd.OAuthAuthorizeEndpoint; set { _ctaEd.OAuthAuthorizeEndpoint = value; OnPropertyChanged(); } }
    public string? CtaOAuthTokenEndpoint     { get => _ctaEd.OAuthTokenEndpoint;     set { _ctaEd.OAuthTokenEndpoint = value; OnPropertyChanged(); } }

    // Visibilidad de campos OAuth según proveedor/flujo
    public bool CtaMostrarTenant       => _ctaEd.OAuthProveedor is 0 or 3;                          // Microsoft / Outlook personal
    public bool CtaMostrarFlujo        => _ctaEd.OAuthProveedor is 0 or 2;                          // Microsoft empresa / Personalizado
    public bool CtaMostrarClientSecret => _ctaEd.OAuthFlujo == 1 || _ctaEd.OAuthProveedor is 1 or 2; // Cliente, o Google/Personalizado
    public bool CtaEsProveedorPersonalizado => _ctaEd.OAuthProveedor == 2;
    private void NotificarVisibilidadOAuth()
    {
        OnPropertyChanged(nameof(CtaMostrarTenant));
        OnPropertyChanged(nameof(CtaMostrarFlujo));
        OnPropertyChanged(nameof(CtaMostrarClientSecret));
        OnPropertyChanged(nameof(CtaEsProveedorPersonalizado));
    }

    public string CtaOAuthEstado => string.IsNullOrWhiteSpace(_ctaEd.OAuthRefreshToken)
        ? "Sin iniciar sesión."
        : $"✓ Conectado como {_ctaEd.OAuthUsuario ?? "(cuenta autenticada)"}.";

    /// <summary>Instructivo completo del proveedor seleccionado (se muestra en el panel de guía).</summary>
    public string CtaGuiaProveedor => _ctaEd.OAuthProveedor switch
    {
        0 => // Microsoft 365 (empresa)
            "Microsoft 365 (empresa) — OAuth2. Microsoft ya no permite contraseña (ni de aplicación) para SMTP.\n\n" +
            "1) En portal.azure.com → Microsoft Entra ID → App registrations → New registration. Anotá el «Application (client) ID» y el «Directory (tenant) ID».\n" +
            "2) Authentication → Add a platform → «Mobile and desktop applications» → redirect http://localhost.\n" +
            "3) Acá: pegá el Client ID (y el Tenant ID) y elegí el flujo:\n" +
            "   • Interactivo: tocá «Iniciar sesión…» y consentí en el navegador una vez.\n" +
            "   • Cliente (sin navegador): generá un Client Secret, agregá el permiso de aplicación SMTP.Send con admin consent, y habilitá la casilla con  Set-CASMailbox -SmtpClientAuthenticationDisabled $false.\n" +
            "4) Servidor smtp.office365.com, puerto 587, seguridad Auto. Es un setup único; después funciona permanente.",
        3 => // Outlook.com personal
            "Outlook.com / Hotmail / Live (personal) — OAuth2. No hay contraseña de aplicación: Microsoft la retiró para estas casillas.\n\n" +
            "1) En portal.azure.com → Microsoft Entra ID → App registrations → New registration, con «Supported account types» que incluya cuentas personales de Microsoft. Anotá el Client ID.\n" +
            "2) Authentication → Add a platform → «Mobile and desktop applications» → redirect http://localhost.\n" +
            "3) Acá: pegá el Client ID (Tenant vacío) y tocá «Iniciar sesión…»; consentí con tu cuenta en el navegador.\n" +
            "4) Servidor smtp-mail.outlook.com, puerto 587, seguridad Auto. Es un setup único; después funciona permanente (no vence).",
        1 => // Google / Gmail
            "Gmail / Google Workspace.\n\n" +
            "Recomendado: contraseña de aplicación (más simple y no vence). Al elegir este proveedor la cuenta ya queda en Autenticación «Básica» con el servidor autocompletado.\n" +
            "1) Activá la Verificación en 2 pasos en tu cuenta de Google.\n" +
            "2) Generá una contraseña en myaccount.google.com/apppasswords (16 letras).\n" +
            "3) Completá Usuario con tu Gmail y pegá esa contraseña (sin espacios). Servidor smtp.gmail.com, puerto 587, Auto (ya autocompletados).\n\n" +
            "OAuth2 (solo si lo necesitás): cambiá Autenticación a «OAuth2». En console.cloud.google.com creá un OAuth client ID tipo «Aplicación de escritorio» y agregá tu cuenta como usuario de prueba. Ojo: en modo prueba el token vence a los 7 días.",
        _ => // Otro / Personalizado
            "Otro proveedor (configuración manual).\n\n" +
            "Usá Autenticación «Básica» con el host/puerto del proveedor y, como contraseña, una contraseña de aplicación o una API key. Ejemplos:\n" +
            "   • Gmail: smtp.gmail.com : 587 (contraseña de aplicación)\n" +
            "   • Yahoo: smtp.mail.yahoo.com : 465\n" +
            "   • iCloud: smtp.mail.me.com : 587\n" +
            "   • Brevo: smtp-relay.brevo.com : 587 (API key)\n" +
            "   • SendGrid: smtp.sendgrid.net : 587 (usuario «apikey», API key como contraseña)\n" +
            "   • Amazon SES: email-smtp.<región>.amazonaws.com : 587\n\n" +
            "Si tu proveedor usa OAuth2, elegí Autenticación OAuth2 y cargá a mano los endpoints Authorize/Token y el Scope."
    };

    private bool _correoFormEditando;
    public bool CorreoFormEditando   { get => _correoFormEditando; set { SetField(ref _correoFormEditando, value); OnPropertyChanged(nameof(CorreoFormNoEditando)); } }
    public bool CorreoFormNoEditando => !_correoFormEditando;

    public string TituloCuentaEdicion => _ctaEdId == 0 ? "Nueva cuenta de correo" : $"Editando: {CtaNombre}";

    private string _estadoCorreo = string.Empty;
    public string EstadoCorreo { get => _estadoCorreo; set => SetField(ref _estadoCorreo, value); }

    public ICommand NuevaCuentaCommand        { get; }
    public ICommand EditarCuentaCommand       { get; }
    public ICommand GuardarCuentaCommand      { get; }
    public ICommand CancelarCuentaCommand     { get; }
    public ICommand EliminarCuentaCommand     { get; }
    public ICommand MarcarCuentaActivaCommand { get; }
    public ICommand ProbarMailCommand         { get; }
    public ICommand ProbarCuentaEnEdicionCommand { get; }
    public ICommand IniciarSesionOAuthCommand { get; }
    public ICommand ToggleMostrarSecretosCommand { get; }

    /// <summary>Autocompleta host/puerto/seguridad al elegir un proveedor con preset.</summary>
    private void AutocompletarTransporte(PuertoBB.Core.Models.Mail.OAuthProveedor proveedor)
    {
        if (OAuthPresets.SugerenciaSmtp(proveedor) is { } s)
        {
            CtaSmtpHost      = s.Host;
            CtaSmtpPort      = s.Puerto;
            CtaSmtpSeguridad = (int)s.Seguridad;
        }
    }

    private void NotificarCamposCuenta()
    {
        foreach (var p in new[] { nameof(CtaNombre), nameof(CtaSmtpHost), nameof(CtaSmtpPort), nameof(CtaSmtpSeguridad),
            nameof(CtaEmailRemitente), nameof(CtaSmtpUsuario), nameof(CtaSmtpPassword),
            nameof(CtaAutenticacion), nameof(CtaEsBasica), nameof(CtaEsOAuth2),
            nameof(CtaProveedorIndice), nameof(CtaFlujoInteractivo), nameof(CtaFlujoCliente),
            nameof(CtaOAuthClientId), nameof(CtaOAuthClientSecret), nameof(CtaOAuthTenantId), nameof(CtaOAuthScope),
            nameof(CtaOAuthAuthorizeEndpoint), nameof(CtaOAuthTokenEndpoint),
            nameof(CtaOAuthEstado), nameof(TituloCuentaEdicion), nameof(CtaGuiaProveedor),
            nameof(CtaMostrarTenant), nameof(CtaMostrarFlujo), nameof(CtaMostrarClientSecret), nameof(CtaEsProveedorPersonalizado) })
            OnPropertyChanged(p);
    }

    private static CuentaCorreo Clonar(CuentaCorreo s) => new()
    {
        Id = s.Id, ConfiguracionId = s.ConfiguracionId, Nombre = s.Nombre, Activo = s.Activo,
        SmtpHost = s.SmtpHost, SmtpPort = s.SmtpPort, SmtpSeguridad = s.SmtpSeguridad, EmailRemitente = s.EmailRemitente,
        Autenticacion = s.Autenticacion, SmtpUsuario = s.SmtpUsuario, SmtpPassword = s.SmtpPassword,
        OAuthProveedor = s.OAuthProveedor, OAuthFlujo = s.OAuthFlujo, OAuthClientId = s.OAuthClientId,
        OAuthClientSecret = s.OAuthClientSecret, OAuthTenantId = s.OAuthTenantId, OAuthScope = s.OAuthScope,
        OAuthAuthorizeEndpoint = s.OAuthAuthorizeEndpoint, OAuthTokenEndpoint = s.OAuthTokenEndpoint,
        OAuthRefreshToken = s.OAuthRefreshToken, OAuthUsuario = s.OAuthUsuario
    };

    private void CargarCuentaPorId(int id)
    {
        var ent = _cuentasClientees.FirstOrDefault(c => c.Id == id);
        if (ent is null) return;
        _ctaEd = Clonar(ent);
        _ctaEdId = ent.Id;
        _ctaEdActivo = ent.Activo;
        _loginSinGuardar = false;
        CtaMostrarSecretos = false;
        NotificarCamposCuenta();
    }

    private void NuevaCuenta()
    {
        _ctaEd = new CuentaCorreo { SmtpPort = 587, Autenticacion = 1 };
        _ctaEdId = 0;
        _ctaEdActivo = false;
        _loginSinGuardar = false;
        CtaMostrarSecretos = false;
        NotificarCamposCuenta();
        CorreoFormEditando = true;
    }

    private void EditarCuenta()
    {
        if (CuentaSeleccionada is null) { MostrarError("Seleccioná una cuenta de la lista."); return; }
        CorreoFormEditando = true;
    }

    private async Task CancelarCuentaAsync()
    {
        if (_loginSinGuardar && !await _dialog.ShowConfirmAsync(
                "Descartar conexión",
                "Iniciaste sesión pero todavía no guardaste la cuenta. Si cancelás, se pierde la conexión y vas a tener que iniciar sesión de nuevo. ¿Descartar de todas formas?",
                "Descartar", "Volver"))
            return;
        _loginSinGuardar = false;
        if (_ctaEdId != 0) CargarCuentaPorId(_ctaEdId);
        else { _ctaEd = new CuentaCorreo { SmtpPort = 587, Autenticacion = 1 }; NotificarCamposCuenta(); }
        CorreoFormEditando = false;
    }

    private async Task GuardarCuentaAsync()
    {
        if (string.IsNullOrWhiteSpace(CtaNombre)) { MostrarError("Indicá un nombre para la cuenta."); return; }
        var remitente = CtaEmailRemitente?.Trim();
        if (!string.IsNullOrEmpty(remitente) && !System.Net.Mail.MailAddress.TryCreate(remitente, out _))
        {
            MostrarError($"El email remitente «{CtaEmailRemitente}» no tiene un formato válido (ej. info@tudominio.com).");
            return;
        }
        _ctaEd.EmailRemitente = remitente;

        // Validación por modo (servidor/remitente, y credenciales según Básica/OAuth2). Reutiliza MailConfig.Validar().
        if (ConstruirMailConfig().Validar() is { } error) { MostrarError(error); return; }

        try
        {
            var esPrimera = _cuentasClientees.Count == 0;
            _ctaEd.Id = _ctaEdId;
            _ctaEd.Activo = _ctaEdId == 0 ? esPrimera : _ctaEdActivo;
            var guardada = await _repo.GuardarCuentaCorreoAsync(_ctaEd);
            _loginSinGuardar = false;
            await RecargarCuentasAsync();
            CorreoFormEditando = false;
            MostrarExito($"Cuenta «{guardada.Nombre}» guardada.");
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar la cuenta: {ex.Message}"); }
    }

    private async Task EliminarCuentaAsync()
    {
        if (CuentaSeleccionada is not { } sel) { MostrarError("Seleccioná una cuenta de la lista."); return; }
        var esActiva = _cuentasClientees.FirstOrDefault(c => c.Id == sel.Id)?.Activo ?? false;
        var mensaje = esActiva
            ? $"¿Eliminar la cuenta «{sel.Nombre}»? Es la cuenta activa: la app quedará sin cuenta para enviar correos hasta que marques otra."
            : $"¿Eliminar la cuenta «{sel.Nombre}»?";
        if (!await _dialog.ShowConfirmAsync("Eliminar cuenta", mensaje, "Eliminar", "Cancelar")) return;
        try { await _repo.EliminarCuentaCorreoAsync(sel.Id); await RecargarCuentasAsync(); MostrarExito("Cuenta eliminada."); }
        catch (Exception ex) { MostrarError($"No se pudo eliminar: {ex.Message}"); }
    }

    private async Task MarcarCuentaActivaAsync()
    {
        if (CuentaSeleccionada is not { } sel) { MostrarError("Seleccioná una cuenta de la lista."); return; }
        try { await _repo.MarcarCuentaCorreoActivaAsync(sel.Id); await RecargarCuentasAsync(); MostrarExito($"«{sel.Nombre}» quedó como cuenta activa."); }
        catch (Exception ex) { MostrarError($"No se pudo activar: {ex.Message}"); }
    }

    private async Task RecargarCuentasAsync()
    {
        _cuentasClientees = (await _repo.GetCuentasCorreoAsync()).ToList();
        CuentasCorreo.Clear();
        foreach (var c in _cuentasClientees) CuentasCorreo.Add(CuentaCorreoItem.From(c));
    }

    /// <summary>Arma un MailConfig con los valores en edición (sin pasar por la base), para el login OAuth.</summary>
    private MailConfig ConstruirMailConfig() => new()
    {
        SmtpHost = _ctaEd.SmtpHost, SmtpPort = _ctaEd.SmtpPort,
        SmtpSeguridad = (PuertoBB.Core.Models.Mail.SmtpSeguridad)_ctaEd.SmtpSeguridad,
        SmtpUsuario = _ctaEd.SmtpUsuario, SmtpPassword = _ctaEd.SmtpPassword, EmailRemitente = _ctaEd.EmailRemitente,
        Autenticacion = (MailAutenticacion)_ctaEd.Autenticacion,
        OAuthProveedor = (PuertoBB.Core.Models.Mail.OAuthProveedor)_ctaEd.OAuthProveedor,
        OAuthFlujo = (OAuthFlujo)_ctaEd.OAuthFlujo,
        OAuthClientId = _ctaEd.OAuthClientId, OAuthClientSecret = _ctaEd.OAuthClientSecret,
        OAuthTenantId = _ctaEd.OAuthTenantId, OAuthScope = _ctaEd.OAuthScope,
        OAuthAuthorizeEndpoint = _ctaEd.OAuthAuthorizeEndpoint, OAuthTokenEndpoint = _ctaEd.OAuthTokenEndpoint,
        OAuthRefreshToken = _ctaEd.OAuthRefreshToken, OAuthUsuario = _ctaEd.OAuthUsuario
    };

    private async Task IniciarSesionOAuthAsync()
    {
        if (string.IsNullOrWhiteSpace(CtaOAuthClientId)) { MostrarError("Ingresá el Client ID antes de iniciar sesión."); return; }
        EstadoCorreo = "Abriendo el navegador para iniciar sesión…";
        var res = await _oauth.AutenticarAsync(ConstruirMailConfig());
        if (!res.Success) { EstadoCorreo = res.ErrorMessage ?? "No se pudo iniciar sesión."; MostrarError(EstadoCorreo); return; }
        _ctaEd.OAuthRefreshToken = res.Data!.RefreshToken;
        _ctaEd.OAuthUsuario = res.Data.Usuario;
        _loginSinGuardar = true;
        OnPropertyChanged(nameof(CtaOAuthEstado));
        EstadoCorreo = $"✓ Conectado como {res.Data.Usuario}. Guardá la cuenta para conservar la sesión.";
        MostrarExito($"Conectado como {res.Data.Usuario}.");
    }

    private async Task ProbarMailAsync()
    {
        try
        {
            EstadoCorreo = "Probando conexión (cuenta activa)…";
            var res = await _mail.ProbarConexionAsync();
            EstadoCorreo = res.Success ? res.Data! : res.ErrorMessage ?? "Error desconocido.";
            if (res.Success) MostrarExito(EstadoCorreo);
            else             MostrarError(EstadoCorreo);
        }
        catch (Exception ex) { EstadoCorreo = ex.Message; MostrarError($"Error al probar el correo: {ex.Message}"); }
    }

    /// <summary>Prueba la conexión SMTP de la cuenta que se está editando (sin guardarla ni marcarla activa).</summary>
    private async Task ProbarCuentaEnEdicionAsync()
    {
        try
        {
            EstadoCorreo = "Probando esta cuenta…";
            var res = await _mail.ProbarConexionAsync(ConstruirMailConfig());
            EstadoCorreo = res.Success ? res.Data! : res.ErrorMessage ?? "Error desconocido.";
            if (res.Success) MostrarExito(EstadoCorreo);
            else             MostrarError(EstadoCorreo);
        }
        catch (Exception ex) { EstadoCorreo = ex.Message; MostrarError($"Error al probar el correo: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // PLANTILLA DE CORREO (global; una sola para todos los comprobantes)
    // ══════════════════════════════════════════
    public string? MailAsunto { get => _config.MailAsunto; set { _config.MailAsunto = value; OnPropertyChanged(); } }
    public string? MailCuerpo { get => _config.MailCuerpo; set { _config.MailCuerpo = value; OnPropertyChanged(); } }

    /// <summary>El cuerpo se pega/edita como HTML (diseñado por fuera) y se envía como HtmlBody; si es false, va como texto plano.</summary>
    public bool MailCuerpoEsHtml { get => _config.MailCuerpoEsHtml; set { _config.MailCuerpoEsHtml = value; OnPropertyChanged(); } }

    /// <summary>Variables disponibles para mostrarlas como ayuda/atajos en la UI.</summary>
    public IReadOnlyList<(string Token, string Descripcion)> VariablesPlantilla => PuertoBB.Core.Mail.PlantillaMail.Variables;

    private bool _plantillaEditando;
    public bool PlantillaEditando   { get => _plantillaEditando; set { SetField(ref _plantillaEditando, value); OnPropertyChanged(nameof(PlantillaNoEditando)); } }
    public bool PlantillaNoEditando => !_plantillaEditando;

    private string _estadoPlantilla = string.Empty;
    public string EstadoPlantilla { get => _estadoPlantilla; set => SetField(ref _estadoPlantilla, value); }

    // Snapshot para Cancelar
    private string? _snapMailAsunto;
    private string? _snapMailCuerpo;
    private bool    _snapMailCuerpoEsHtml;

    public ICommand EditarPlantillaCommand   { get; }
    public ICommand GuardarPlantillaCommand  { get; }
    public ICommand CancelarPlantillaCommand { get; }

    private void EditarPlantilla()
    {
        _snapMailAsunto       = _config.MailAsunto;
        _snapMailCuerpo       = _config.MailCuerpo;
        _snapMailCuerpoEsHtml = _config.MailCuerpoEsHtml;
        PlantillaEditando = true;
    }
    private void CancelarPlantilla()
    {
        MailAsunto       = _snapMailAsunto;
        MailCuerpo       = _snapMailCuerpo;
        MailCuerpoEsHtml = _snapMailCuerpoEsHtml;
        PlantillaEditando = false;
    }
    private async Task GuardarPlantillaAsync()
    {
        try   { await _repo.SaveAsync(_config); PlantillaEditando = false; MostrarExito("Plantilla de correo guardada."); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    // ══════════════════════════════════════════
    // OTROS COMANDOS
    // ══════════════════════════════════════════
    public ICommand BackupCommand               { get; }
    public ICommand RestaurarCommand            { get; }
    public ICommand AbrirCarpetaBackupsCommand  { get; }
    public ICommand VerificarIntegridadCommand  { get; }
    public ICommand VacuumCommand               { get; }
    public ICommand OptimizarCommand            { get; }

    private string _ultimoBackupTexto = "Último backup automático: —";
    /// <summary>Texto de recordatorio sobre el último backup automático (con ⚠ si está vencido).</summary>
    public string UltimoBackupTexto { get => _ultimoBackupTexto; set => SetField(ref _ultimoBackupTexto, value); }

    // ══════════════════════════════════════════
    // CONSTRUCTOR
    // ══════════════════════════════════════════
    public ConfiguracionViewModel(
        IConfiguracionRepository repo,
        IContadorVoucherRepository contador,
        IBackupService backup,
        IAfipService afip,
        IMailService mail,
        IOAuthInteractiveFlow oauth,
        IDialogService dialog)
    {
        _repo      = repo;
        _contador  = contador;
        _backup    = backup;
        _afip      = afip;
        _mail      = mail;
        _oauth     = oauth;
        _dialog    = dialog;

        // Emisor
        EditarEmisorCommand   = new RelayCommand(_ => EditarEmisor());
        GuardarEmisorCommand  = new AsyncRelayCommand(GuardarEmisorAsync);
        CancelarEmisorCommand = new RelayCommand(_ => CancelarEmisor());

        // Códigos AFIP
        EditarCodigosAfipCommand   = new RelayCommand(_ => EditarCodigosAfip());
        GuardarCodigosAfipCommand  = new AsyncRelayCommand(GuardarCodigosAfipAsync);
        CancelarCodigosAfipCommand = new RelayCommand(_ => CancelarCodigosAfip());

        // Puntos de venta
        NuevoPuntoCommand             = new RelayCommand(_ => NuevoPunto());
        EditarPuntoCommand            = new RelayCommand(_ => EditarPunto());
        GuardarPuntoCommand           = new AsyncRelayCommand(GuardarPuntoAsync);
        CancelarPuntoCommand          = new RelayCommand(_ => CancelarPunto());
        EliminarPuntoCommand          = new AsyncRelayCommand(EliminarPuntoAsync);
        MarcarActivoCommand           = new AsyncRelayCommand(MarcarActivoAsync);
        SeleccionarCertificadoCommand = new RelayCommand(_ => SeleccionarCertificado());
        SeleccionarClaveKeyCommand    = new RelayCommand(_ => SeleccionarClaveKey());
        ProbarConexionCommand         = new AsyncRelayCommand(ProbarConexionAsync);
        ToggleMostrarPasswordCommand  = new RelayCommand(_ => EdMostrarPassword = !EdMostrarPassword);
        GenerarCertificadoCommand     = new AsyncRelayCommand(AbrirWizardCertificadoAsync);
        DescargarCrtCommand           = new RelayCommand(_ => GuardarBytes(_edCertificadoContenido,    $"{AliasArchivo()}.crt", "Certificado (*.crt)|*.crt;*.pem|Todos|*.*",  "certificado"));
        DescargarKeyCommand           = new RelayCommand(_ => GuardarBytes(_edCertificadoKeyContenido, $"{AliasArchivo()}.key", "Clave privada (*.key)|*.key;*.pem|Todos|*.*", "clave privada"));
        ExportarP12Command            = new AsyncRelayCommand(ExportarP12Async);

        // Vouchers
        EditarVouchersCommand   = new RelayCommand(_ => EditarVouchers());
        GuardarVouchersCommand  = new AsyncRelayCommand(GuardarVouchersAsync);
        CancelarVouchersCommand = new RelayCommand(_ => CancelarVouchers());
        GuardarContadorCommand  = new AsyncRelayCommand(GuardarContadorAsync);

        // Pagos
        EditarPagosCommand   = new RelayCommand(_ => EditarPagos());
        GuardarPagosCommand  = new AsyncRelayCommand(GuardarPagosAsync);
        CancelarPagosCommand = new RelayCommand(_ => CancelarPagos());

        // Correo (varias cuentas)
        NuevaCuentaCommand        = new RelayCommand(_ => NuevaCuenta());
        EditarCuentaCommand       = new RelayCommand(_ => EditarCuenta());
        GuardarCuentaCommand      = new AsyncRelayCommand(GuardarCuentaAsync);
        CancelarCuentaCommand     = new AsyncRelayCommand(CancelarCuentaAsync);
        EliminarCuentaCommand     = new AsyncRelayCommand(EliminarCuentaAsync);
        MarcarCuentaActivaCommand = new AsyncRelayCommand(MarcarCuentaActivaAsync);
        ProbarMailCommand         = new AsyncRelayCommand(ProbarMailAsync);
        ProbarCuentaEnEdicionCommand = new AsyncRelayCommand(ProbarCuentaEnEdicionAsync);
        IniciarSesionOAuthCommand = new AsyncRelayCommand(IniciarSesionOAuthAsync);
        ToggleMostrarSecretosCommand = new RelayCommand(_ => CtaMostrarSecretos = !CtaMostrarSecretos);

        // Plantilla de correo
        EditarPlantillaCommand   = new RelayCommand(_ => EditarPlantilla());
        GuardarPlantillaCommand  = new AsyncRelayCommand(GuardarPlantillaAsync);
        CancelarPlantillaCommand = new RelayCommand(_ => CancelarPlantilla());

        // Otros
        BackupCommand              = new AsyncRelayCommand(BackupAsync);
        RestaurarCommand           = new AsyncRelayCommand(RestaurarAsync);
        AbrirCarpetaBackupsCommand = new RelayCommand(_ => AbrirCarpetaBackups());
        VerificarIntegridadCommand = new AsyncRelayCommand(VerificarIntegridadAsync);
        VacuumCommand              = new AsyncRelayCommand(VacuumAsync);
        OptimizarCommand           = new AsyncRelayCommand(OptimizarAsync);

        CargarSeguro(CargarAsync);
    }

    // ══════════════════════════════════════════
    // CARGA INICIAL
    // ══════════════════════════════════════════
    private async Task CargarAsync()
    {
        _config = await _repo.GetAsync();
        var contadorEntity = await _contador.GetAsync();
        UltimoNumeroVoucher = contadorEntity.UltimoNumero;
        foreach (var p in new[] { nameof(RazonSocial), nameof(Cuit), nameof(IngresosBrutos), nameof(InicioActividades),
                                   nameof(CodigoAfipRecibo), nameof(CodigoAfipNotaDeCredito),
                                   nameof(ImporteVoucherPredeterminado),
                                   nameof(DiasVencimiento),
                                   nameof(MailAsunto), nameof(MailCuerpo), nameof(MailCuerpoEsHtml) })
            OnPropertyChanged(p);
        await RecargarPuntosAsync();
        await RecargarCuentasAsync();
        RefrescarUltimoBackup();
    }

    private async Task RecargarPuntosAsync()
    {
        var puntos = await _repo.GetPuntosDeVentaAsync();
        PuntosDeVenta.Clear();
        foreach (var p in puntos) PuntosDeVenta.Add(PuntoDeVentaItem.From(p));
    }

    // ══════════════════════════════════════════
    // OPERACIONES DE PUNTOS DE VENTA
    // ══════════════════════════════════════════
    private void SeleccionarCertificado()
    {
        // En modo P12 se elige un .p12; en CRT+KEY (incluido el .crt que devuelve AFIP) un PEM.
        var filter = _edModo == ModoCertificado.P12
            ? "Certificado PKCS#12 (*.p12;*.pfx)|*.p12;*.pfx|Todos|*.*"
            : "Certificado PEM (*.crt;*.pem)|*.crt;*.pem|Todos|*.*";
        var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _edCertificadoContenido = File.ReadAllBytes(dlg.FileName);
            EdCertificadoRuta       = Path.GetFileName(dlg.FileName);
            NotificarEstadoCert();
        }
        catch (Exception ex) { MostrarError($"No se pudo leer el certificado: {ex.Message}"); }
    }

    private void SeleccionarClaveKey()
    {
        var dlg = new OpenFileDialog { Filter = "Clave privada PEM (*.key;*.pem)|*.key;*.pem|Todos|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _edCertificadoKeyContenido = File.ReadAllBytes(dlg.FileName);
            EdCertificadoKeyRuta       = Path.GetFileName(dlg.FileName);
            NotificarEstadoCert();
        }
        catch (Exception ex) { MostrarError($"No se pudo leer la clave privada: {ex.Message}"); }
    }

    // Abre el asistente para generar un certificado nuevo (clave + CSR). La clave generada queda
    // cargada en el form en modo CRT+KEY; el usuario sube el .csr a AFIP e importa luego el .crt.
    private async Task AbrirWizardCertificadoAsync()
    {
        var cuit = new string((Cuit ?? "").Where(char.IsDigit).ToArray());
        if (cuit.Length == 0)                       { MostrarError("Cargá el CUIT del emisor antes de generar el certificado."); return; }
        if (string.IsNullOrWhiteSpace(RazonSocial)) { MostrarError("Cargá la razón social del emisor antes de generar el certificado."); return; }
        if (!PvFormEditando)                        { MostrarError("Creá (Nuevo) o abrí (Editar) un punto de venta antes de generar el certificado."); return; }

        var res = await _dialog.ShowCertificadoWizardAsync(RazonSocial, cuit, EdUsarHomologacion);
        if (res is null) return;

        // Cargar la clave generada en modo CRT+KEY; el .crt se importa cuando AFIP lo devuelva.
        SetModo(ModoCertificado.CrtKey);
        _edCertificadoKeyContenido = res.ClavePrivadaPem;
        EdCertificadoKeyRuta       = $"{res.Alias}.key (generada)";
        _edCertificadoContenido    = null;
        EdCertificadoRuta          = null;
        EdCertificadoPassword      = null;
        NotificarEstadoCert();
        EstadoConexion = "Clave cargada. Guardá el punto y, cuando AFIP te devuelva el .crt, importalo en modo CRT+KEY.";
        MostrarExito("Clave generada y cargada. Guardá el punto de venta.");
    }

    // Arma un .p12 (clave + .crt de AFIP) con contraseña para reutilizar el certificado en otras apps.
    private async Task ExportarP12Async()
    {
        if (!EdPuedeExportarP12) { MostrarError("Necesitás el certificado (.crt) y la clave para exportar el .p12."); return; }
        var pass = await _dialog.ShowInputAsync(
            "Exportar .p12",
            "Contraseña para el archivo .p12",
            description: "Elegí una contraseña nueva para proteger el archivo .p12. Te la van a pedir cada vez que importes este certificado en otra aplicación o equipo; no necesita coincidir con ninguna otra.");
        if (string.IsNullOrEmpty(pass)) return;
        try
        {
            var p12 = CsrGenerator.ArmarP12(_edCertificadoContenido!, _edCertificadoKeyContenido!, pass);
            GuardarBytes(p12, $"{AliasArchivo()}.p12", "Certificado PKCS#12 (*.p12)|*.p12|Todos|*.*", "certificado");
        }
        catch (Exception ex) { MostrarError($"No se pudo exportar el .p12: {ex.Message}"); }
    }

    private string AliasArchivo()
    {
        var a = string.IsNullOrWhiteSpace(EdNombre) ? "certificado" : EdNombre.Trim();
        return string.Concat(a.Split(Path.GetInvalidFileNameChars()));
    }

    private void GuardarBytes(byte[]? datos, string nombreSugerido, string filtro, string queCosa)
    {
        if (datos is not { Length: > 0 }) { MostrarError($"No hay {queCosa} para guardar."); return; }
        var dlg = new SaveFileDialog { FileName = nombreSugerido, Filter = filtro };
        if (dlg.ShowDialog() != true) return;
        try   { File.WriteAllBytes(dlg.FileName, datos); MostrarExito($"Guardado: {Path.GetFileName(dlg.FileName)}"); }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    // Valida que el certificado esté cargado según el modo. Devuelve null si es válido, o un mensaje.
    // En CRT+KEY se permite guardar con solo la clave (el .crt de AFIP se importa más tarde).
    private string? ValidarCertificado()
    {
        if (_edModo == ModoCertificado.P12)
        {
            if (_edCertificadoContenido is not { Length: > 0 }) return "Seleccioná el archivo .p12 del certificado.";
            if (string.IsNullOrWhiteSpace(EdCertificadoPassword)) return "Ingresá la contraseña del certificado .p12.";
            return null;
        }
        // CRT+KEY
        if (_edCertificadoKeyContenido is not { Length: > 0 } && EdCertificadoKeyRuta is null)
            return "Cargá la clave privada (.key), o usá «Generar certificado nuevo…».";
        return null;
    }

    private async Task GuardarPuntoAsync()
    {
        if (string.IsNullOrWhiteSpace(EdNombre)) { MostrarError("Indique un nombre para el punto de venta."); return; }
        if (EdNumero <= 0) { MostrarError("El número de punto de venta debe ser mayor a cero."); return; }
        if (ValidarCertificado() is { } errorCert) { MostrarError(errorCert); return; }
        try
        {
            var esPrimero = PuntosDeVenta.Count == 0;
            var pv = new PuntoDeVenta
            {
                Id                      = _edId,
                Nombre                  = EdNombre.Trim(),
                Numero                  = EdNumero,
                UsarHomologacion        = EdUsarHomologacion,
                CertificadoRuta         = EdCertificadoRuta,
                CertificadoContenido    = _edCertificadoContenido,
                CertificadoPassword     = _edModo == ModoCertificado.P12 ? EdCertificadoPassword : null,
                CertificadoKeyRuta      = _edModo == ModoCertificado.P12 ? null : EdCertificadoKeyRuta,
                CertificadoKeyContenido = _edModo == ModoCertificado.P12 ? null : _edCertificadoKeyContenido,
                Activo                  = _edId == 0 ? esPrimero : _edActivo
            };
            // En CRT+KEY sin .crt todavía, el punto queda "pendiente": guardado, pero falta importar el .crt.
            var pendienteCrt = _edModo == ModoCertificado.CrtKey && _edCertificadoContenido is not { Length: > 0 };
            var guardado = await _repo.GuardarPuntoDeVentaAsync(pv);
            await RecargarPuntosAsync();
            _edId                      = 0;
            EdNombre                   = string.Empty;
            EdNumero                   = 1;
            EdUsarHomologacion         = false;
            EdCertificadoRuta          = null;
            _edCertificadoContenido    = null;
            EdCertificadoPassword      = null;
            EdCertificadoKeyRuta       = null;
            _edCertificadoKeyContenido = null;
            SetModo(ModoCertificado.P12);
            NotificarEstadoCert();
            EdMostrarPassword          = false;
            PvFormEditando             = false;
            OnPropertyChanged(nameof(TituloEdicion));
            MostrarExito(pendienteCrt
                ? $"Punto de venta «{guardado.Nombre}» guardado. Falta importar el .crt de AFIP para poder emitir."
                : $"Punto de venta «{guardado.Nombre}» guardado.");
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar el punto de venta: {ex.Message}"); }
    }

    private async Task EliminarPuntoAsync()
    {
        if (PuntoSeleccionado is not { } sel) { MostrarError("Seleccione un punto de venta de la lista."); return; }
        try
        {
            await _repo.EliminarPuntoDeVentaAsync(sel.Id);
            await RecargarPuntosAsync();
            _edId                 = 0;
            EdNombre              = string.Empty;
            EdNumero              = 1;
            EdUsarHomologacion    = false;
            EdCertificadoRuta     = null;
            _edCertificadoContenido    = null;
            EdCertificadoPassword = null;
            EdCertificadoKeyRuta  = null;
            _edCertificadoKeyContenido = null;
            SetModo(ModoCertificado.P12);
            NotificarEstadoCert();
            PvFormEditando        = false;
            OnPropertyChanged(nameof(TituloEdicion));
            MostrarExito("Punto de venta eliminado.");
        }
        catch (Exception ex) { MostrarError($"No se pudo eliminar: {ex.Message}"); }
    }

    private async Task MarcarActivoAsync()
    {
        if (PuntoSeleccionado is not { } sel) { MostrarError("Seleccione un punto de venta de la lista."); return; }
        var confirm = await _dialog.ShowConfirmAsync(
            "Confirmar activación",
            $"¿Activar «{sel.Nombre}» como punto de venta activo?",
            confirmText: "Activar");
        if (!confirm) return;
        try
        {
            await _repo.MarcarPuntoDeVentaActivoAsync(sel.Id);
            await RecargarPuntosAsync();
            MostrarExito($"«{sel.Nombre}» quedó como punto de venta activo.");
        }
        catch (Exception ex) { MostrarError($"No se pudo activar: {ex.Message}"); }
    }

    private async Task ProbarConexionAsync()
    {
        var activo = PuntosDeVenta.FirstOrDefault(p => p.Activo);
        if (activo is null) { MostrarError("No hay punto de venta activo. Marcá uno como activo y probá de nuevo."); return; }
        try
        {
            EstadoConexion = $"Probando conexión con AFIP (punto de venta activo: {activo.Nombre})…";
            var res = await _afip.ProbarConexionAsync(activo.Numero, CodigoAfipRecibo);
            if (res.Success && res.Data is { } d)
            {
                EstadoConexion = d.Detalle ?? (d.AutenticacionOk ? "Conexión OK." : "Revise la configuración.");
                if (d.AutenticacionOk) MostrarExito("Conexión con AFIP correcta.");
                else MostrarError(d.Detalle ?? "No se pudo autenticar con AFIP.");
            }
            else
            {
                EstadoConexion = res.ErrorMessage ?? "Error al probar la conexión.";
                MostrarError(EstadoConexion);
            }
        }
        catch (Exception ex)
        {
            EstadoConexion = ex.Message;
            MostrarError($"Error al probar la conexión: {ex.Message}");
        }
    }

    private async Task BackupAsync()
    {
        var dlg = new SaveFileDialog { Filter = "Base SQLite (*.db)|*.db", FileName = _backup.NombreSugerido() };
        if (dlg.ShowDialog() != true) return;
        await EjecutarOcupadoAsync("Generando backup", async () =>
        {
            var res = await _backup.BackupAsync(dlg.FileName);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo generar el backup."); return; }
            // Verificar la copia recién creada antes de darla por buena.
            var verif = await _backup.VerificarArchivoAsync(dlg.FileName);
            if (verif.Success && verif.Data == "ok")
                MostrarExito($"Backup generado y verificado en {dlg.FileName}");
            else
                MostrarAdvertencia($"El backup se generó en {dlg.FileName}, pero la verificación encontró problemas. Generá otra copia.");
        });
        RefrescarUltimoBackup();
    }

    private async Task RestaurarAsync()
    {
        var dlg = new OpenFileDialog { Filter = "Base SQLite (*.db)|*.db", Title = "Seleccioná el backup a restaurar" };
        if (dlg.ShowDialog() != true) return;

        // Validar el archivo elegido (sano y de esta app) ANTES de tocar la base actual.
        var verif = await _backup.VerificarArchivoAsync(dlg.FileName, validarEsDeEstaApp: true);
        if (!verif.Success)
        {
            await _dialog.ShowAlertAsync("Backup inválido", verif.ErrorMessage ?? "No se pudo validar el archivo seleccionado.");
            return;
        }
        if (verif.Data != "ok")
        {
            await _dialog.ShowAlertAsync("Backup dañado",
                "El archivo seleccionado está dañado y no se puede usar para restaurar:\n\n" + verif.Data);
            return;
        }

        var confirmado = await _dialog.ShowConfirmAsync(
            "Restaurar backup",
            "Esto reemplazará TODA la base de datos actual con el backup seleccionado.\n\nSe guardará una copia de seguridad de la base actual antes de reemplazarla.\n\nLa aplicación se cerrará al finalizar y deberás reabrirla para continuar.\n\n¿Querés continuar?",
            "Restaurar", "Cancelar");
        if (!confirmado) return;
        await EjecutarOcupadoAsync("Restaurando", async () =>
        {
            // Red de seguridad: respaldar la base actual antes de sobrescribirla.
            var previo = await _backup.BackupAutomaticoAsync();
            if (!previo.Success)
            {
                MostrarError("No se pudo respaldar la base actual antes de restaurar. Se canceló la operación por seguridad.");
                return;
            }

            var res = await _backup.RestaurarAsync(dlg.FileName);
            if (res.Success)
            {
                MostrarExito("Base restaurada. Cierre y vuelva a abrir la aplicación.");
                await Task.Delay(1500);
                Application.Current.Shutdown();
            }
            else MostrarError(res.ErrorMessage ?? "No se pudo restaurar el backup.");
        });
    }

    private void AbrirCarpetaBackups()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _backup.CarpetaBackups(), UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo abrir la carpeta de backups: {ex.Message}");
        }
    }

    /// <summary>Actualiza el texto del recordatorio según la antigüedad del último backup automático.</summary>
    private void RefrescarUltimoBackup()
    {
        var fecha = _backup.FechaUltimoBackup();
        if (fecha is null)
        {
            UltimoBackupTexto = "⚠ Todavía no hay backups automáticos. Se generará uno al abrir la app.";
            return;
        }
        var dias = (int)(DateTime.Now.Date - fecha.Value.Date).TotalDays;
        UltimoBackupTexto = dias switch
        {
            0 => $"Último backup automático: hoy a las {fecha:HH:mm}.",
            1 => "Último backup automático: ayer.",
            <= 7 => $"Último backup automático: hace {dias} días.",
            _ => $"⚠ Último backup automático: hace {dias} días — conviene generar uno."
        };
    }

    private async Task VerificarIntegridadAsync()
    {
        string? problemas = null;
        await EjecutarOcupadoAsync("Verificando", async () =>
        {
            var res = await _backup.VerificarIntegridadAsync();
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo verificar la integridad."); return; }
            if (res.Data == "ok") MostrarExito("La base de datos está en buen estado.");
            else problemas = res.Data ?? "Se encontraron errores en la base de datos.";
        });
        if (problemas is not null)
            await _dialog.ShowAlertAsync("Problemas encontrados", problemas);
    }

    private Task VacuumAsync()
        => EjecutarOcupadoAsync("Compactando base", async () =>
        {
            var res = await _backup.VacuumAsync();
            if (res.Success) MostrarExito("Base de datos compactada correctamente.");
            else MostrarError(res.ErrorMessage ?? "No se pudo compactar la base de datos.");
        });

    private Task OptimizarAsync()
        => EjecutarOcupadoAsync("Optimizando base", async () =>
        {
            var res = await _backup.OptimizarAsync();
            if (res.Success) MostrarExito("Estadísticas del optimizador actualizadas.");
            else MostrarError(res.ErrorMessage ?? "No se pudo optimizar la base de datos.");
        });
}

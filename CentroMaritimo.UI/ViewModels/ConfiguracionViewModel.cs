using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using CentroMaritimo.UI.ViewModels.Items;
using Microsoft.Win32;
using PuertoBB.Core.Afip;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Mail;
using PuertoBB.Services.Security;

namespace CentroMaritimo.UI.ViewModels;

public class ConfiguracionViewModel : PageViewModel
{
    private readonly IConfiguracionRepository _repo;
    private readonly IContadorVoucherRepository _contador;
    private readonly IBackupService _backup;
    private readonly IAfipService _afip;
    private readonly IMailService _mail;
    private readonly ISecretProtector _protector;
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
    // APODERADO FISCAL
    // ══════════════════════════════════════════
    public bool    UsarApoderado    { get => _config.UsarApoderado;    set { _config.UsarApoderado = value; OnPropertyChanged(); } }
    public string? NombreApoderado  { get => _config.NombreApoderado;  set { _config.NombreApoderado = value; OnPropertyChanged(); } }
    public string? CuitApoderado    { get => _config.CuitApoderado;    set { _config.CuitApoderado = value; OnPropertyChanged(); } }

    private bool _apoderadoEditando;
    public bool ApoderadoEditando   { get => _apoderadoEditando; set { SetField(ref _apoderadoEditando, value); OnPropertyChanged(nameof(ApoderadoNoEditando)); } }
    public bool ApoderadoNoEditando => !_apoderadoEditando;

    private bool    _snapUsarApoderado;
    private string? _snapNombreApoderado;
    private string? _snapCuitApoderado;

    public ICommand EditarApoderadoCommand   { get; }
    public ICommand GuardarApoderadoCommand  { get; }
    public ICommand CancelarApoderadoCommand { get; }

    private void EditarApoderado()
    {
        _snapUsarApoderado   = UsarApoderado;
        _snapNombreApoderado = NombreApoderado;
        _snapCuitApoderado   = CuitApoderado;
        ApoderadoEditando    = true;
    }
    private void CancelarApoderado()
    {
        UsarApoderado    = _snapUsarApoderado;
        NombreApoderado  = _snapNombreApoderado;
        CuitApoderado    = _snapCuitApoderado;
        ApoderadoEditando = false;
    }
    private async Task GuardarApoderadoAsync()
    {
        if (UsarApoderado && string.IsNullOrWhiteSpace(CuitApoderado)) { MostrarError("Indique el CUIT del apoderado."); return; }
        try   { await _repo.SaveAsync(_config); ApoderadoEditando = false; MostrarExito("Datos del apoderado guardados."); }
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
    private string? _edCertificadoPassword;
    private string? _edCertificadoKeyRuta;
    private bool    _edUsarModoCrtKey;
    private bool    _edMostrarPassword;

    public string  EdNombre              { get => _edNombre;              set => SetField(ref _edNombre, value); }
    public int     EdNumero              { get => _edNumero;              set => SetField(ref _edNumero, value); }
    public bool    EdUsarHomologacion    { get => _edUsarHomologacion;    set { if (SetField(ref _edUsarHomologacion, value)) OnPropertyChanged(nameof(AmbienteEdicion)); } }
    public string? EdCertificadoRuta     { get => _edCertificadoRuta;     set => SetField(ref _edCertificadoRuta, value); }
    public string? EdCertificadoPassword { get => _edCertificadoPassword; set => SetField(ref _edCertificadoPassword, value); }
    public string? EdCertificadoKeyRuta  { get => _edCertificadoKeyRuta;  set => SetField(ref _edCertificadoKeyRuta, value); }
    public bool    EdUsarModoCrtKey
    {
        get => _edUsarModoCrtKey;
        set
        {
            if (!SetField(ref _edUsarModoCrtKey, value)) return;
            OnPropertyChanged(nameof(EdUsarModoP12));
            if (value) EdCertificadoPassword = null;
            else       EdCertificadoKeyRuta  = null;
        }
    }
    public bool EdUsarModoP12 { get => !_edUsarModoCrtKey; set => EdUsarModoCrtKey = !value; }

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
    private string? _snapEdCertificadoPassword;
    private string? _snapEdCertificadoKeyRuta;
    private bool    _snapEdUsarModoCrtKey;

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

    private void NuevoPunto()
    {
        _edId                 = 0;
        _edActivo             = false;
        EdNombre              = string.Empty;
        EdNumero              = 1;
        EdUsarHomologacion    = false;
        EdCertificadoRuta     = null;
        EdCertificadoPassword = null;
        EdCertificadoKeyRuta  = null;
        EdUsarModoCrtKey      = false;
        EdMostrarPassword     = false;
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
        _edId                 = _snapEdId;
        EdNombre              = _snapEdNombre;
        EdNumero              = _snapEdNumero;
        EdUsarHomologacion    = _snapEdUsarHomologacion;
        EdCertificadoRuta     = _snapEdCertificadoRuta;
        EdCertificadoPassword = _snapEdCertificadoPassword;
        EdCertificadoKeyRuta  = _snapEdCertificadoKeyRuta;
        EdUsarModoCrtKey      = _snapEdUsarModoCrtKey;
        EdMostrarPassword     = false;
        PvFormEditando = false;
        OnPropertyChanged(nameof(TituloEdicion));
    }

    private void CapturaSnapshotPv()
    {
        _snapEdId                  = _edId;
        _snapEdNombre              = EdNombre;
        _snapEdNumero              = EdNumero;
        _snapEdUsarHomologacion    = EdUsarHomologacion;
        _snapEdCertificadoRuta     = EdCertificadoRuta;
        _snapEdCertificadoPassword = EdCertificadoPassword;
        _snapEdCertificadoKeyRuta  = EdCertificadoKeyRuta;
        _snapEdUsarModoCrtKey      = EdUsarModoCrtKey;
    }

    private void CargarEnEdicion(PuntoDeVentaItem item)
    {
        _edId                 = item.Id;
        _edActivo             = item.Activo;
        EdNombre              = item.Nombre;
        EdNumero              = item.Numero;
        EdUsarHomologacion    = item.UsarHomologacion;
        EdCertificadoRuta     = item.CertificadoRuta;
        EdCertificadoPassword = _protector.Unprotect(item.CertificadoPassword);
        EdCertificadoKeyRuta  = item.CertificadoKeyRuta;
        EdUsarModoCrtKey      = item.CertificadoKeyRuta is not null;
        EdMostrarPassword     = false;
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
    // CORREO
    // ══════════════════════════════════════════
    public string? SmtpHost       { get => _config.SmtpHost;       set { _config.SmtpHost = value; OnPropertyChanged(); } }
    public int     SmtpPort       { get => _config.SmtpPort;       set { _config.SmtpPort = value; OnPropertyChanged(); } }
    public int     SmtpSeguridad  { get => _config.SmtpSeguridad;  set { _config.SmtpSeguridad = value; OnPropertyChanged(); } }
    public string? SmtpUsuario    { get => _config.SmtpUsuario;    set { _config.SmtpUsuario = value; OnPropertyChanged(); } }
    public string? SmtpPassword   { get => _config.SmtpPassword;   set { _config.SmtpPassword = value; OnPropertyChanged(); } }
    public string? EmailRemitente { get => _config.EmailRemitente; set { _config.EmailRemitente = value; OnPropertyChanged(); } }

    private bool _correoEditando;
    public bool CorreoEditando   { get => _correoEditando; set { SetField(ref _correoEditando, value); OnPropertyChanged(nameof(CorreoNoEditando)); } }
    public bool CorreoNoEditando => !_correoEditando;

    private string _estadoCorreo = string.Empty;
    public string EstadoCorreo { get => _estadoCorreo; set => SetField(ref _estadoCorreo, value); }

    private string? _snapSmtpHost;
    private int     _snapSmtpPort;
    private int     _snapSmtpSeguridad;
    private string? _snapSmtpUsuario;
    private string? _snapSmtpPassword;
    private string? _snapEmailRemitente;

    public ICommand EditarCorreoCommand   { get; }
    public ICommand GuardarCorreoCommand  { get; }
    public ICommand CancelarCorreoCommand { get; }
    public ICommand ProbarMailCommand     { get; }

    private void EditarCorreo()
    {
        _snapSmtpHost       = SmtpHost;
        _snapSmtpPort       = SmtpPort;
        _snapSmtpSeguridad  = SmtpSeguridad;
        _snapSmtpUsuario    = SmtpUsuario;
        _snapSmtpPassword   = SmtpPassword;
        _snapEmailRemitente = EmailRemitente;
        CorreoEditando      = true;
    }
    private void CancelarCorreo()
    {
        SmtpHost       = _snapSmtpHost;
        SmtpPort       = _snapSmtpPort;
        SmtpSeguridad  = _snapSmtpSeguridad;
        SmtpUsuario    = _snapSmtpUsuario;
        SmtpPassword   = _snapSmtpPassword;
        EmailRemitente = _snapEmailRemitente;
        CorreoEditando = false;
    }
    private async Task GuardarCorreoAsync()
    {
        var rawPwd = _config.SmtpPassword;
        _config.SmtpPassword = _protector.Protect(rawPwd);
        try
        {
            await _repo.SaveAsync(_config);
            CorreoEditando = false;
            MostrarExito("Configuración de correo guardada.");
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
        finally { _config.SmtpPassword = rawPwd; }
    }
    private async Task ProbarMailAsync()
    {
        try
        {
            EstadoCorreo = "Probando conexión…";
            var res = await _mail.ProbarConexionAsync();
            EstadoCorreo = res.Success ? res.Data! : res.ErrorMessage ?? "Error desconocido.";
            if (res.Success) MostrarExito(EstadoCorreo);
            else             MostrarError(EstadoCorreo);
        }
        catch (Exception ex)
        {
            EstadoCorreo = ex.Message;
            MostrarError($"Error al probar el correo: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════
    // OTROS COMANDOS
    // ══════════════════════════════════════════
    public ICommand BackupCommand              { get; }
    public ICommand RestaurarCommand           { get; }
    public ICommand VerificarIntegridadCommand { get; }
    public ICommand VacuumCommand              { get; }
    public ICommand OptimizarCommand           { get; }

    // ══════════════════════════════════════════
    // CONSTRUCTOR
    // ══════════════════════════════════════════
    public ConfiguracionViewModel(
        IConfiguracionRepository repo,
        IContadorVoucherRepository contador,
        IBackupService backup,
        IAfipService afip,
        IMailService mail,
        ISecretProtector protector,
        IDialogService dialog)
    {
        _repo      = repo;
        _contador  = contador;
        _backup    = backup;
        _afip      = afip;
        _mail      = mail;
        _dialog    = dialog;
        _protector = protector;

        // Emisor
        EditarEmisorCommand   = new RelayCommand(_ => EditarEmisor());
        GuardarEmisorCommand  = new AsyncRelayCommand(GuardarEmisorAsync);
        CancelarEmisorCommand = new RelayCommand(_ => CancelarEmisor());

        // Apoderado
        EditarApoderadoCommand   = new RelayCommand(_ => EditarApoderado());
        GuardarApoderadoCommand  = new AsyncRelayCommand(GuardarApoderadoAsync);
        CancelarApoderadoCommand = new RelayCommand(_ => CancelarApoderado());

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

        // Vouchers
        EditarVouchersCommand   = new RelayCommand(_ => EditarVouchers());
        GuardarVouchersCommand  = new AsyncRelayCommand(GuardarVouchersAsync);
        CancelarVouchersCommand = new RelayCommand(_ => CancelarVouchers());
        GuardarContadorCommand  = new AsyncRelayCommand(GuardarContadorAsync);

        // Pagos
        EditarPagosCommand   = new RelayCommand(_ => EditarPagos());
        GuardarPagosCommand  = new AsyncRelayCommand(GuardarPagosAsync);
        CancelarPagosCommand = new RelayCommand(_ => CancelarPagos());

        // Correo
        EditarCorreoCommand   = new RelayCommand(_ => EditarCorreo());
        GuardarCorreoCommand  = new AsyncRelayCommand(GuardarCorreoAsync);
        CancelarCorreoCommand = new RelayCommand(_ => CancelarCorreo());
        ProbarMailCommand     = new AsyncRelayCommand(ProbarMailAsync);

        // Otros
        BackupCommand              = new AsyncRelayCommand(BackupAsync);
        RestaurarCommand           = new AsyncRelayCommand(RestaurarAsync);
        VerificarIntegridadCommand = new AsyncRelayCommand(VerificarIntegridadAsync);
        VacuumCommand              = new AsyncRelayCommand(VacuumAsync);
        OptimizarCommand           = new AsyncRelayCommand(OptimizarAsync);

        _ = CargarAsync();
    }

    // ══════════════════════════════════════════
    // CARGA INICIAL
    // ══════════════════════════════════════════
    private async Task CargarAsync()
    {
        _config = await _repo.GetAsync();
        _config.SmtpPassword = _protector.Unprotect(_config.SmtpPassword);
        var contadorEntity = await _contador.GetAsync();
        UltimoNumeroVoucher = contadorEntity.UltimoNumero;
        foreach (var p in new[] { nameof(RazonSocial), nameof(Cuit), nameof(IngresosBrutos), nameof(InicioActividades),
                                   nameof(CodigoAfipRecibo), nameof(CodigoAfipNotaDeCredito),
                                   nameof(UsarApoderado), nameof(NombreApoderado), nameof(CuitApoderado),
                                   nameof(ImporteVoucherPredeterminado),
                                   nameof(DiasVencimiento), nameof(SmtpHost), nameof(SmtpPort), nameof(SmtpSeguridad),
                                   nameof(SmtpUsuario), nameof(SmtpPassword), nameof(EmailRemitente) })
            OnPropertyChanged(p);
        await RecargarPuntosAsync();
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
        var filter = _edUsarModoCrtKey
            ? "Certificado PEM (*.crt;*.pem)|*.crt;*.pem|Todos|*.*"
            : "Certificado PKCS#12 (*.p12;*.pfx)|*.p12;*.pfx|Todos|*.*";
        var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog() != true) return;
        try   { EdCertificadoRuta = ImportarArchivoCertificado(dlg.FileName); }
        catch (Exception ex)
        {
            EdCertificadoRuta = dlg.FileName;
            MostrarError($"No se pudo copiar el certificado al almacén local: {ex.Message}");
        }
    }

    private void SeleccionarClaveKey()
    {
        var dlg = new OpenFileDialog { Filter = "Clave privada PEM (*.key;*.pem)|*.key;*.pem|Todos|*.*" };
        if (dlg.ShowDialog() != true) return;
        try   { EdCertificadoKeyRuta = ImportarArchivoCertificado(dlg.FileName); }
        catch (Exception ex)
        {
            EdCertificadoKeyRuta = dlg.FileName;
            MostrarError($"No se pudo copiar la clave privada al almacén local: {ex.Message}");
        }
    }

    private static string ImportarArchivoCertificado(string origen)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PuertoBB", "CentroMaritimo", "Certificados");
        Directory.CreateDirectory(dir);
        var destino = Path.Combine(dir, Path.GetFileName(origen));
        if (!string.Equals(Path.GetFullPath(origen), Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase))
            File.Copy(origen, destino, overwrite: true);
        return destino;
    }

    private async Task GuardarPuntoAsync()
    {
        if (string.IsNullOrWhiteSpace(EdNombre)) { MostrarError("Indique un nombre para el punto de venta."); return; }
        if (EdNumero <= 0) { MostrarError("El número de punto de venta debe ser mayor a cero."); return; }
        try
        {
            var esPrimero = PuntosDeVenta.Count == 0;
            var pv = new PuntoDeVenta
            {
                Id                  = _edId,
                Nombre              = EdNombre.Trim(),
                Numero              = EdNumero,
                UsarHomologacion    = EdUsarHomologacion,
                CertificadoRuta     = EdCertificadoRuta,
                CertificadoPassword = _edUsarModoCrtKey ? null : _protector.Protect(EdCertificadoPassword),
                CertificadoKeyRuta  = EdCertificadoKeyRuta,
                Activo              = _edId == 0 ? esPrimero : _edActivo
            };
            var guardado = await _repo.GuardarPuntoDeVentaAsync(pv);
            await RecargarPuntosAsync();
            _edId                 = 0;
            EdNombre              = string.Empty;
            EdNumero              = 1;
            EdUsarHomologacion    = false;
            EdCertificadoRuta     = null;
            EdCertificadoPassword = null;
            EdCertificadoKeyRuta  = null;
            EdUsarModoCrtKey      = false;
            EdMostrarPassword     = false;
            PvFormEditando        = false;
            OnPropertyChanged(nameof(TituloEdicion));
            MostrarExito($"Punto de venta «{guardado.Nombre}» guardado.");
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
            EdCertificadoPassword = null;
            EdCertificadoKeyRuta  = null;
            EdUsarModoCrtKey      = false;
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
        var res = await _backup.BackupAsync(dlg.FileName);
        if (res.Success) MostrarExito($"Backup generado en {dlg.FileName}");
        else MostrarError(res.ErrorMessage ?? "No se pudo generar el backup.");
    }

    private async Task RestaurarAsync()
    {
        var dlg = new OpenFileDialog { Filter = "Base SQLite (*.db)|*.db", Title = "Seleccioná el backup a restaurar" };
        if (dlg.ShowDialog() != true) return;
        var confirmado = await _dialog.ShowConfirmAsync(
            "Restaurar backup",
            "Esto reemplazará TODA la base de datos actual con el backup seleccionado.\n\nLa aplicación se cerrará al finalizar y deberás reabrirla para continuar.\n\n¿Querés continuar?",
            "Restaurar", "Cancelar");
        if (!confirmado) return;
        var res = await _backup.RestaurarAsync(dlg.FileName);
        if (res.Success)
        {
            MostrarExito("Base restaurada. Cierre y vuelva a abrir la aplicación.");
            await Task.Delay(1500);
            Application.Current.Shutdown();
        }
        else MostrarError(res.ErrorMessage ?? "No se pudo restaurar el backup.");
    }

    private async Task VerificarIntegridadAsync()
    {
        var res = await _backup.VerificarIntegridadAsync();
        if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo verificar la integridad."); return; }
        if (res.Data == "ok")
            MostrarExito("La base de datos está en buen estado.");
        else
            await _dialog.ShowAlertAsync("Problemas encontrados", res.Data ?? "Se encontraron errores en la base de datos.");
    }

    private async Task VacuumAsync()
    {
        var res = await _backup.VacuumAsync();
        if (res.Success) MostrarExito("Base de datos compactada correctamente.");
        else MostrarError(res.ErrorMessage ?? "No se pudo compactar la base de datos.");
    }

    private async Task OptimizarAsync()
    {
        var res = await _backup.OptimizarAsync();
        if (res.Success) MostrarExito("Estadísticas del optimizador actualizadas.");
        else MostrarError(res.ErrorMessage ?? "No se pudo optimizar la base de datos.");
    }
}

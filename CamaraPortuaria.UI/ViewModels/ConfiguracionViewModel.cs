using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using Microsoft.Win32;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;

namespace CamaraPortuaria.UI.ViewModels;

public class ConfiguracionViewModel : PageViewModel
{
    private readonly IConfiguracionRepository _repo;
    private readonly IBackupService _backup;
    private Configuracion _config = new();

    public string RazonSocial { get => _config.RazonSocial; set { _config.RazonSocial = value; OnPropertyChanged(); } }
    public string Cuit { get => _config.Cuit; set { _config.Cuit = value; OnPropertyChanged(); } }
    public int PuntoDeVenta { get => _config.PuntoDeVenta; set { _config.PuntoDeVenta = value; OnPropertyChanged(); } }
    public int CodigoAfipRecibo { get => _config.CodigoAfipRecibo; set { _config.CodigoAfipRecibo = value; OnPropertyChanged(); } }
    public int CodigoAfipNotaDeCredito { get => _config.CodigoAfipNotaDeCredito; set { _config.CodigoAfipNotaDeCredito = value; OnPropertyChanged(); } }
    public string? AfipCertificadoRuta { get => _config.AfipCertificadoRuta; set { _config.AfipCertificadoRuta = value; OnPropertyChanged(); } }
    public string? AfipCertificadoPassword { get => _config.AfipCertificadoPassword; set { _config.AfipCertificadoPassword = value; OnPropertyChanged(); } }
    public bool AfipUsarHomologacion { get => _config.AfipUsarHomologacion; set { _config.AfipUsarHomologacion = value; OnPropertyChanged(); } }
    public int DiasVencimiento { get => _config.DiasVencimiento; set { _config.DiasVencimiento = value; OnPropertyChanged(); } }
    public string? SmtpHost { get => _config.SmtpHost; set { _config.SmtpHost = value; OnPropertyChanged(); } }
    public int SmtpPort { get => _config.SmtpPort; set { _config.SmtpPort = value; OnPropertyChanged(); } }
    public string? SmtpUsuario { get => _config.SmtpUsuario; set { _config.SmtpUsuario = value; OnPropertyChanged(); } }
    public string? SmtpPassword { get => _config.SmtpPassword; set { _config.SmtpPassword = value; OnPropertyChanged(); } }
    public string? EmailRemitente { get => _config.EmailRemitente; set { _config.EmailRemitente = value; OnPropertyChanged(); } }

    public ICommand GuardarCommand { get; }
    public ICommand SeleccionarCertificadoCommand { get; }
    public ICommand BackupCommand { get; }

    public ConfiguracionViewModel(IConfiguracionRepository repo, IBackupService backup)
    {
        _repo = repo;
        _backup = backup;
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        SeleccionarCertificadoCommand = new RelayCommand(_ => SeleccionarCertificado());
        BackupCommand = new AsyncRelayCommand(BackupAsync);
        _ = CargarAsync();
    }

    private async Task BackupAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Base SQLite (*.db)|*.db",
            FileName = _backup.NombreSugerido()
        };
        if (dlg.ShowDialog() != true) return;

        var res = await _backup.BackupAsync(dlg.FileName);
        if (res.Success) MostrarExito($"Backup generado en {dlg.FileName}");
        else MostrarError(res.ErrorMessage ?? "No se pudo generar el backup.");
    }

    private async Task CargarAsync()
    {
        _config = await _repo.GetAsync();
        foreach (var p in GetType().GetProperties())
            if (p.CanRead && p.Name is not (nameof(GuardarCommand) or nameof(SeleccionarCertificadoCommand)))
                OnPropertyChanged(p.Name);
    }

    private void SeleccionarCertificado()
    {
        var dlg = new OpenFileDialog { Filter = "Certificado PKCS#12 (*.p12;*.pfx)|*.p12;*.pfx|Todos|*.*" };
        if (dlg.ShowDialog() == true) AfipCertificadoRuta = dlg.FileName;
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Cuit)) { MostrarError("El CUIT del emisor es obligatorio."); return; }
        try
        {
            await _repo.SaveAsync(_config);
            MostrarExito("Configuración guardada.");
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }
}

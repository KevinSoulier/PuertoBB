using System.Collections.ObjectModel;
using System.Windows.Input;
using CentroMaritimo.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Core.Interfaces.Services;

namespace CentroMaritimo.UI.ViewModels;

public class AgenciasViewModel : PageViewModel
{
    private readonly IAgenciaRepository _repo;
    private readonly IDialogService _dialog;
    private int _editId;
    private List<Agencia> _todasLasAgencias = [];

    public ObservableCollection<Agencia> AgenciasFiltradas { get; } = [];

    private string _filtro = string.Empty;
    public string Filtro
    {
        get => _filtro;
        set { if (SetField(ref _filtro, value)) AplicarFiltro(); }
    }

    private Agencia? _seleccionada;
    public Agencia? Seleccionada
    {
        get => _seleccionada;
        set { if (SetField(ref _seleccionada, value) && value is not null) _ = CargarEdicionAsync(value.Id); }
    }

    public string NombreEdit { get; set; } = string.Empty;
    public string RazonSocialEdit { get; set; } = string.Empty;
    public string CuitEdit { get; set; } = string.Empty;
    public string DomicilioEdit { get; set; } = string.Empty;
    public string CondicionIvaEdit { get; set; } = string.Empty;
    public string EmailsEdit { get; set; } = string.Empty;

    public ICommand NuevoCommand { get; }
    public ICommand GuardarCommand { get; }
    public ICommand EliminarCommand { get; }

    public AgenciasViewModel(IAgenciaRepository repo, IDialogService dialog)
    {
        _repo = repo;
        _dialog = dialog;
        NuevoCommand = new RelayCommand(_ => Nuevo());
        GuardarCommand = new AsyncRelayCommand(GuardarAsync);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionada is not null);
        _ = CargarListaAsync();
    }

    private async Task CargarListaAsync()
    {
        _todasLasAgencias = (await _repo.GetTodasConEmailsAsync()).ToList();
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        AgenciasFiltradas.Clear();
        var texto = _filtro.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todasLasAgencias
            : _todasLasAgencias.Where(a =>
                a.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                a.RazonSocial.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                a.Cuit.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var a in lista) AgenciasFiltradas.Add(a);
    }

    private void Nuevo()
    {
        _editId = 0;
        NombreEdit = RazonSocialEdit = CuitEdit = DomicilioEdit = CondicionIvaEdit = EmailsEdit = string.Empty;
        _seleccionada = null;
        Notificar();
        LimpiarStatus();
    }

    private async Task CargarEdicionAsync(int id)
    {
        var a = await _repo.GetConDetalleAsync(id);
        if (a is null) return;
        _editId = a.Id;
        NombreEdit = a.Nombre;
        RazonSocialEdit = a.RazonSocial;
        CuitEdit = a.Cuit;
        DomicilioEdit = a.Domicilio ?? string.Empty;
        CondicionIvaEdit = a.CondicionIva ?? string.Empty;
        EmailsEdit = string.Join(Environment.NewLine, a.Emails.Select(x => x.Email));
        Notificar();
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El nombre es obligatorio."); return; }
        if (string.IsNullOrWhiteSpace(CuitEdit)) { MostrarError("El CUIT es obligatorio."); return; }

        var emails = EmailsEdit.Split(['\n', '\r', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        var esAlta = _editId == 0;
        var nueva = esAlta ? new Agencia
        {
            Nombre = NombreEdit.Trim(),
            RazonSocial = RazonSocialEdit.Trim(),
            Cuit = CuitEdit.Trim(),
            Domicilio = string.IsNullOrWhiteSpace(DomicilioEdit) ? null : DomicilioEdit.Trim(),
            CondicionIva = string.IsNullOrWhiteSpace(CondicionIvaEdit) ? null : CondicionIvaEdit.Trim(),
            Activa = true,
            Emails = emails.Select(em => new EmailAgencia { Email = em }).ToList()
        } : null!;
        try
        {
            if (esAlta)
            {
                await _repo.AddAsync(nueva);
                MostrarExito("Agencia creada.");
            }
            else
            {
                var existente = await _repo.GetConDetalleAsync(_editId);
                if (existente is null) { MostrarError("La agencia ya no existe."); return; }
                existente.Nombre = NombreEdit.Trim();
                existente.RazonSocial = RazonSocialEdit.Trim();
                existente.Cuit = CuitEdit.Trim();
                existente.Domicilio = string.IsNullOrWhiteSpace(DomicilioEdit) ? null : DomicilioEdit.Trim();
                existente.CondicionIva = string.IsNullOrWhiteSpace(CondicionIvaEdit) ? null : CondicionIvaEdit.Trim();
                existente.Activa = true;
                existente.Emails.Clear();
                foreach (var em in emails) existente.Emails.Add(new EmailAgencia { Email = em, AgenciaId = existente.Id });
                await _repo.UpdateAsync(existente);
                MostrarExito("Agencia actualizada.");
            }
            await CargarListaAsync();

            if (esAlta && nueva.Id > 0)
            {
                var creada = AgenciasFiltradas.FirstOrDefault(a => a.Id == nueva.Id)
                             ?? _todasLasAgencias.FirstOrDefault(a => a.Id == nueva.Id);
                if (creada is not null) Seleccionada = creada;
            }
        }
        catch (Exception ex) { MostrarError($"No se pudo guardar: {ex.Message}"); }
    }

    private async Task EliminarAsync()
    {
        if (Seleccionada is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar agencia",
                $"¿Eliminar a {Seleccionada.Nombre}?", "Eliminar", "Cancelar")) return;
        try
        {
            await _repo.DeleteAsync(Seleccionada.Id);
            MostrarExito("Agencia eliminada.");
            Nuevo();
            await CargarListaAsync();
        }
        catch (Exception ex) { MostrarError($"No se pudo eliminar (¿tiene recibos/vouchers?): {ex.Message}"); }
    }

    private void Notificar()
    {
        OnPropertyChanged(nameof(NombreEdit));
        OnPropertyChanged(nameof(RazonSocialEdit));
        OnPropertyChanged(nameof(CuitEdit));
        OnPropertyChanged(nameof(DomicilioEdit));
        OnPropertyChanged(nameof(CondicionIvaEdit));
        OnPropertyChanged(nameof(EmailsEdit));
    }
}

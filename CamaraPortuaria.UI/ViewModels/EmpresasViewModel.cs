using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using PuertoBB.Core.Afip;
using PuertoBB.Core.Common;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;

namespace CamaraPortuaria.UI.ViewModels;

public class EmpresasViewModel : PageViewModel
{
    private readonly IEmpresaRepository _repo;
    private readonly IDialogService _dialog;
    private readonly IAfipPadronService _padron;
    private int _editId;
    private List<Empresa> _todasLasEmpresas = [];

    private string _snapNombre = string.Empty, _snapRazonSocial = string.Empty, _snapCuit = string.Empty;
    private string _snapDomicilio = string.Empty, _snapEmails = string.Empty;
    private int? _snapCondicionIvaId;

    public ObservableCollection<Empresa> EmpresasFiltradas { get; } = [];

    private string _filtro = string.Empty;
    public string Filtro
    {
        get => _filtro;
        set { if (SetField(ref _filtro, value)) AplicarFiltro(); }
    }

    private Empresa? _seleccionada;
    public Empresa? Seleccionada
    {
        get => _seleccionada;
        set { if (SetField(ref _seleccionada, value) && value is not null) CargarSeguro(() => MostrarAsync(value.Id)); }
    }

    public string NombreEdit { get; set; } = string.Empty;
    public string RazonSocialEdit { get; set; } = string.Empty;
    public string CuitEdit { get; set; } = string.Empty;
    public string DomicilioEdit { get; set; } = string.Empty;
    public int? CondicionIvaIdEdit { get; set; }
    public string EmailsEdit { get; set; } = string.Empty;

    /// <summary>Catálogo AFIP de condiciones frente al IVA para el combo (RG 5616).</summary>
    public IReadOnlyList<CondicionIvaReceptor> CondicionesIva { get; } = CatalogoCondicionesIvaReceptor.Todas;

    private bool _enEdicion;
    public bool EnEdicion
    {
        get => _enEdicion;
        private set { if (SetField(ref _enEdicion, value)) { OnPropertyChanged(nameof(NoEnEdicion)); CommandManager.InvalidateRequerySuggested(); } }
    }
    public bool NoEnEdicion => !EnEdicion;

    public ICommand NuevoCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand AceptarCommand { get; }
    public ICommand CancelarCommand { get; }
    public ICommand EliminarCommand { get; }
    public ICommand ValidarCuitCommand { get; }

    public EmpresasViewModel(IEmpresaRepository repo, IDialogService dialog, IAfipPadronService padron)
    {
        _repo = repo;
        _dialog = dialog;
        _padron = padron;
        NuevoCommand = new RelayCommand(_ => Nuevo(), _ => !EnEdicion);
        EditarCommand = new RelayCommand(_ => Editar(), _ => Seleccionada is not null && !EnEdicion);
        AceptarCommand = new AsyncRelayCommand(AceptarAsync, () => EnEdicion);
        CancelarCommand = new RelayCommand(_ => Cancelar(), _ => EnEdicion);
        EliminarCommand = new AsyncRelayCommand(EliminarAsync, () => Seleccionada is not null && !EnEdicion);
        ValidarCuitCommand = new AsyncRelayCommand(ValidarCuitAsync, () => EnEdicion && !string.IsNullOrWhiteSpace(CuitEdit));
        CargarSeguro(CargarListaAsync);
    }

    /// <summary>Consulta la constancia de inscripción en ARCA y autocompleta razón social,
    /// domicilio y condición IVA (no pisa datos con vacío).</summary>
    private async Task ValidarCuitAsync()
    {
        var res = await _padron.ConsultarCuitAsync(CuitEdit);
        if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo consultar el padrón."); return; }
        if (res.Data is null)
        {
            await _dialog.ShowAlertAsync("Padrón ARCA",
                "El CUIT no figura en el padrón de ARCA. En homologación es esperable: esa base no tiene los contribuyentes reales.");
            return;
        }

        var c = res.Data;
        if (!string.IsNullOrWhiteSpace(c.RazonSocial)) RazonSocialEdit = c.RazonSocial;
        if (!string.IsNullOrWhiteSpace(c.Domicilio)) DomicilioEdit = c.Domicilio;
        if (c.CondicionIvaId is not null) CondicionIvaIdEdit = c.CondicionIvaId;
        NotificarEdicion();

        if (c.Observaciones.Count > 0)
            await _dialog.ShowAlertAsync("Padrón ARCA", string.Join("\n", c.Observaciones));
        else
            MostrarExito("Datos validados contra el padrón de ARCA.");
    }

    private async Task CargarListaAsync()
    {
        _todasLasEmpresas = (await _repo.GetTodasConEmailsAsync()).ToList();
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        EmpresasFiltradas.Clear();
        var texto = _filtro.Trim();
        var lista = string.IsNullOrEmpty(texto)
            ? _todasLasEmpresas
            : _todasLasEmpresas.Where(e =>
                e.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                e.RazonSocial.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                e.Cuit.Contains(texto, StringComparison.OrdinalIgnoreCase));
        foreach (var e in lista) EmpresasFiltradas.Add(e);
    }

    private void Nuevo()
    {
        _editId = 0;
        NombreEdit = RazonSocialEdit = CuitEdit = DomicilioEdit = EmailsEdit = string.Empty;
        CondicionIvaIdEdit = null;
        _seleccionada = null;
        OnPropertyChanged(nameof(Seleccionada));
        TomarSnapshot();
        NotificarEdicion();
        EnEdicion = true;
        LimpiarStatus();
    }

    private void Editar()
    {
        TomarSnapshot();
        EnEdicion = true;
    }

    private void Cancelar()
    {
        if (_editId == 0)
        {
            NombreEdit = RazonSocialEdit = CuitEdit = DomicilioEdit = EmailsEdit = string.Empty;
            CondicionIvaIdEdit = null;
            _seleccionada = null;
            OnPropertyChanged(nameof(Seleccionada));
        }
        else
        {
            NombreEdit = _snapNombre;
            RazonSocialEdit = _snapRazonSocial;
            CuitEdit = _snapCuit;
            DomicilioEdit = _snapDomicilio;
            CondicionIvaIdEdit = _snapCondicionIvaId;
            EmailsEdit = _snapEmails;
        }
        NotificarEdicion();
        EnEdicion = false;
        LimpiarStatus();
    }

    private void TomarSnapshot()
    {
        _snapNombre = NombreEdit;
        _snapRazonSocial = RazonSocialEdit;
        _snapCuit = CuitEdit;
        _snapDomicilio = DomicilioEdit;
        _snapCondicionIvaId = CondicionIvaIdEdit;
        _snapEmails = EmailsEdit;
    }

    private async Task MostrarAsync(int id)
    {
        var e = await _repo.GetConDetalleAsync(id);
        if (e is null) return;
        _editId = e.Id;
        NombreEdit = e.Nombre;
        RazonSocialEdit = e.RazonSocial;
        CuitEdit = e.Cuit;
        DomicilioEdit = e.Domicilio ?? string.Empty;
        CondicionIvaIdEdit = e.CondicionIvaId;
        EmailsEdit = string.Join(Environment.NewLine, e.Emails.Select(x => x.Email));
        NotificarEdicion();
    }

    private async Task AceptarAsync()
    {
        if (string.IsNullOrWhiteSpace(NombreEdit)) { MostrarError("El nombre es obligatorio."); return; }
        if (string.IsNullOrWhiteSpace(CuitEdit)) { MostrarError("El CUIT es obligatorio."); return; }
        if (!CuitValidator.EsValido(CuitEdit)) { MostrarError("El CUIT no es válido."); return; }

        // El CUIT no debería repetirse, pero se permiten excepciones: avisar y dejar continuar.
        var cuitDigitos = new string(CuitEdit.Where(char.IsDigit).ToArray());
        var duplicada = _todasLasEmpresas.FirstOrDefault(e => e.Id != _editId
            && new string(e.Cuit.Where(char.IsDigit).ToArray()) == cuitDigitos);
        if (duplicada is not null && !await _dialog.ShowConfirmAsync("CUIT duplicado",
                $"Ya existe «{duplicada.Nombre}» con el CUIT {CuitEdit.Trim()}. ¿Guardar de todos modos?", "Guardar igual", "Cancelar"))
            return;

        var emails = ParsearEmails();
        try
        {
            if (_editId == 0)
            {
                var nueva = new Empresa
                {
                    Nombre = NombreEdit.Trim(),
                    RazonSocial = RazonSocialEdit.Trim(),
                    Cuit = CuitEdit.Trim(),
                    Domicilio = string.IsNullOrWhiteSpace(DomicilioEdit) ? null : DomicilioEdit.Trim(),
                    CondicionIvaId = CondicionIvaIdEdit,
                    Activa = true,
                    Emails = emails.Select(em => new EmailEmpresa { Email = em }).ToList()
                };
                await _repo.AddAsync(nueva);
                _editId = nueva.Id;
                MostrarExito("Empresa creada.");
            }
            else
            {
                var existente = await _repo.GetConDetalleAsync(_editId);
                if (existente is null) { MostrarError("La empresa ya no existe."); return; }
                existente.Nombre = NombreEdit.Trim();
                existente.RazonSocial = RazonSocialEdit.Trim();
                existente.Cuit = CuitEdit.Trim();
                existente.Domicilio = string.IsNullOrWhiteSpace(DomicilioEdit) ? null : DomicilioEdit.Trim();
                existente.CondicionIvaId = CondicionIvaIdEdit;
                existente.Activa = true;
                existente.Emails.Clear();
                foreach (var em in emails) existente.Emails.Add(new EmailEmpresa { Email = em, EmpresaId = existente.Id });
                await _repo.UpdateAsync(existente);
                MostrarExito("Empresa actualizada.");
            }
            var idGuardado = _editId;
            await CargarListaAsync();
            var guardada = EmpresasFiltradas.FirstOrDefault(e => e.Id == idGuardado)
                           ?? _todasLasEmpresas.FirstOrDefault(e => e.Id == idGuardado);
            if (guardada is not null) { _seleccionada = guardada; OnPropertyChanged(nameof(Seleccionada)); }
            EnEdicion = false;
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo guardar: {ex.Message}");
        }
    }

    private async Task EliminarAsync()
    {
        if (Seleccionada is null) return;
        if (!await _dialog.ShowConfirmAsync("Eliminar empresa",
                $"¿Eliminar a {Seleccionada.Nombre}?", "Eliminar", "Cancelar")) return;
        try
        {
            await _repo.DeleteAsync(Seleccionada.Id);
            MostrarExito("Empresa eliminada.");
            _editId = 0;
            NombreEdit = RazonSocialEdit = CuitEdit = DomicilioEdit = EmailsEdit = string.Empty;
            CondicionIvaIdEdit = null;
            _seleccionada = null;
            OnPropertyChanged(nameof(Seleccionada));
            NotificarEdicion();
            EnEdicion = false;
            await CargarListaAsync();
        }
        catch (Exception ex)
        {
            MostrarError($"No se pudo eliminar (¿tiene recibos asociados?): {ex.Message}");
        }
    }

    private List<string> ParsearEmails()
        => EmailsEdit.Split(['\n', '\r', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct().ToList();

    private void NotificarEdicion()
    {
        OnPropertyChanged(nameof(NombreEdit));
        OnPropertyChanged(nameof(RazonSocialEdit));
        OnPropertyChanged(nameof(CuitEdit));
        OnPropertyChanged(nameof(DomicilioEdit));
        OnPropertyChanged(nameof(CondicionIvaIdEdit));
        OnPropertyChanged(nameof(EmailsEdit));
    }
}

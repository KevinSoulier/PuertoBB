using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using CamaraPortuaria.UI.ViewModels.Items;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace CamaraPortuaria.UI.ViewModels;

public class EmisionMasivaViewModel : PageViewModel
{
    private static readonly CultureInfo _es = new("es-AR");

    private readonly ICamaraPortuariaReciboService _service;
    private readonly IGrupoFacturacionRepository _gruposRepo;
    private readonly IDialogService _dialog;

    public ObservableCollection<GrupoFacturacion> Grupos { get; } = [];
    public ObservableCollection<ResultadoEmisionPorEntidad> Resultados { get; } = [];
    public ObservableCollection<PreviaEmisionItem> PreviaItems { get; } = [];

    public IReadOnlyList<string> MesesNombres { get; } =
        Enumerable.Range(1, 12)
            .Select(m => _es.DateTimeFormat.GetMonthName(m))
            .Select(n => char.ToUpper(n[0]) + n[1..])
            .ToList();

    private int _mesIndex = DateTime.Today.Month - 1;
    public int MesIndex
    {
        get => _mesIndex;
        set { if (SetField(ref _mesIndex, value)) { _mes = value + 1; _ = ActualizarPreviaAsync(); } }
    }

    private int _mes = DateTime.Today.Month;

    private int _anio = DateTime.Today.Year;
    public int Anio
    {
        get => _anio;
        set { if (SetField(ref _anio, value)) _ = ActualizarPreviaAsync(); }
    }

    private GrupoFacturacion? _grupo;
    public GrupoFacturacion? Grupo
    {
        get => _grupo;
        set { if (SetField(ref _grupo, value)) _ = ActualizarPreviaAsync(); }
    }

    private bool _hayPrevia;
    public bool HayPrevia { get => _hayPrevia; set => SetField(ref _hayPrevia, value); }

    private string _resumenPrevia = string.Empty;
    public string ResumenPrevia { get => _resumenPrevia; set => SetField(ref _resumenPrevia, value); }

    public ICommand EmitirCommand { get; }

    public EmisionMasivaViewModel(
        ICamaraPortuariaReciboService service,
        IGrupoFacturacionRepository gruposRepo,
        IDialogService dialog)
    {
        _service = service;
        _gruposRepo = gruposRepo;
        _dialog = dialog;
        EmitirCommand = new AsyncRelayCommand(EmitirAsync, () => Grupo is not null);
        _ = CargarGruposAsync();
    }

    private async Task CargarGruposAsync()
    {
        foreach (var g in await _gruposRepo.GetActivosAsync())
            Grupos.Add(g);
    }

    private async Task ActualizarPreviaAsync()
    {
        PreviaItems.Clear();
        HayPrevia = false;
        ResumenPrevia = string.Empty;

        if (Grupo is null) return;

        var dup = await _service.GetDuplicadosAsync(Grupo.Id, _anio, _mes);
        var yaEmitidos = (dup.Success && dup.Data is not null)
            ? new HashSet<string>(dup.Data, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();

        var grupoConMiembros = await _gruposRepo.GetConMiembrosAsync(Grupo.Id);
        if (grupoConMiembros is null) return;

        foreach (var m in grupoConMiembros.Empresas)
        {
            var nombre = m.Empresa?.Nombre ?? $"Empresa #{m.EmpresaId}";
            var esDup = yaEmitidos.Contains(nombre);
            PreviaItems.Add(new PreviaEmisionItem
            {
                Empresa = nombre,
                Estado = esDup ? "Ya emitido" : "A generar",
                YaEmitido = esDup
            });
        }

        var totalMiembros = PreviaItems.Count;
        var aGenerar = PreviaItems.Count(p => !p.YaEmitido);
        ResumenPrevia = $"{totalMiembros} miembro(s) · {aGenerar} a emitir · {totalMiembros - aGenerar} ya emitido(s)";
        HayPrevia = totalMiembros > 0;
    }

    private async Task EmitirAsync()
    {
        if (Grupo is null) return;
        LimpiarStatus();

        var dup = await _service.GetDuplicadosAsync(Grupo.Id, _anio, _mes);
        if (dup.Success && dup.Data is { Count: > 0 })
        {
            var continuar = await _dialog.ShowConfirmAsync("Recibos duplicados",
                $"{dup.Data.Count} empresa(s) ya tienen recibo en este período y serán omitidas:\n\n" +
                string.Join(", ", dup.Data) + "\n\n¿Continuar con el resto?");
            if (!continuar) return;
        }
        else if (!await _dialog.ShowConfirmAsync("Emitir recibos",
                     $"¿Emitir recibos del grupo «{Grupo.Nombre}» por {Grupo.Importe:C2} a cada empresa?"))
        {
            return;
        }

        IsBusy = true;
        Resultados.Clear();
        try
        {
            var res = await _service.EmitirMasivoAsync(Grupo.Id, _anio, _mes);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }

            foreach (var r in res.Data!) Resultados.Add(r);
            var ok = Resultados.Count(r => r.Exito);
            var fallidos = Resultados.Count - ok;
            MostrarExito($"Emisión finalizada: {ok} emitido(s), {fallidos} con error.");
            await ActualizarPreviaAsync();
        }
        finally { IsBusy = false; }
    }
}

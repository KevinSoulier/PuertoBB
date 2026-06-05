using System.Collections.ObjectModel;
using System.Windows.Input;
using CamaraPortuaria.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Core.Models.Resultados;

namespace CamaraPortuaria.UI.ViewModels;

public class EmisionMasivaViewModel : PageViewModel
{
    private readonly ICamaraPortuariaReciboService _service;
    private readonly IGrupoFacturacionRepository _gruposRepo;
    private readonly IDialogService _dialog;

    public ObservableCollection<GrupoFacturacion> Grupos { get; } = [];
    public ObservableCollection<ResultadoEmisionPorEntidad> Resultados { get; } = [];
    public IReadOnlyList<int> Anios { get; } = Enumerable.Range(DateTime.Today.Year - 5, 7).Reverse().ToList();
    public IReadOnlyList<int> Meses { get; } = Enumerable.Range(1, 12).ToList();

    private GrupoFacturacion? _grupo;
    public GrupoFacturacion? Grupo { get => _grupo; set => SetField(ref _grupo, value); }

    private int _anio = DateTime.Today.Year;
    public int Anio { get => _anio; set => SetField(ref _anio, value); }

    private int _mes = DateTime.Today.Month;
    public int Mes { get => _mes; set => SetField(ref _mes, value); }

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

    private async Task EmitirAsync()
    {
        if (Grupo is null) return;
        LimpiarStatus();

        var dup = await _service.GetDuplicadosAsync(Grupo.Id, Anio, Mes);
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
            var res = await _service.EmitirMasivoAsync(Grupo.Id, Anio, Mes);
            if (!res.Success) { MostrarError(res.ErrorMessage ?? "No se pudo emitir."); return; }

            foreach (var r in res.Data!) Resultados.Add(r);
            var ok = Resultados.Count(r => r.Exito);
            var fallidos = Resultados.Count - ok;
            MostrarExito($"Emisión finalizada: {ok} emitido(s), {fallidos} con error.");
        }
        finally { IsBusy = false; }
    }
}

using CentroMaritimo.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace CentroMaritimo.UI.ViewModels.Items;

/// <summary>Agencia con un flag de pertenencia al grupo en edición.</summary>
public class MiembroGrupoItem : BaseViewModel
{
    public Agencia Agencia { get; }
    public int AgenciaId => Agencia.Id;
    public string Nombre => Agencia.Nombre;

    private bool _esMiembro;
    public bool EsMiembro { get => _esMiembro; set => SetField(ref _esMiembro, value); }

    public MiembroGrupoItem(Agencia agencia, bool esMiembro)
    {
        Agencia = agencia;
        _esMiembro = esMiembro;
    }
}

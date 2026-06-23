using CamaraPortuaria.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>Cliente con un flag de pertenencia al grupo en edición.</summary>
public class MiembroGrupoItem : BaseViewModel
{
    public Cliente Cliente { get; }
    public int ClienteId => Cliente.Id;
    public string Nombre => Cliente.Nombre;

    private bool _esMiembro;
    public bool EsMiembro { get => _esMiembro; set => SetField(ref _esMiembro, value); }

    public MiembroGrupoItem(Cliente empresa, bool esMiembro)
    {
        Cliente = empresa;
        _esMiembro = esMiembro;
    }
}

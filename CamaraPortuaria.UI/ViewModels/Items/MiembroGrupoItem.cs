using CamaraPortuaria.UI.ViewModels.Base;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace CamaraPortuaria.UI.ViewModels.Items;

/// <summary>Empresa con un flag de pertenencia al grupo en edición.</summary>
public class MiembroGrupoItem : BaseViewModel
{
    public Empresa Empresa { get; }
    public int EmpresaId => Empresa.Id;
    public string Nombre => Empresa.Nombre;

    private bool _esMiembro;
    public bool EsMiembro { get => _esMiembro; set => SetField(ref _esMiembro, value); }

    public MiembroGrupoItem(Empresa empresa, bool esMiembro)
    {
        Empresa = empresa;
        _esMiembro = esMiembro;
    }
}

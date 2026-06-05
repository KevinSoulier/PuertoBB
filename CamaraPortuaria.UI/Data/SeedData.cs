using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace CamaraPortuaria.UI.Data;

/// <summary>
/// Datos de ejemplo para desarrollo/demo. Se ejecuta solo si la base está vacía de empresas.
/// Permite recorrer la app de punta a punta sin cargar datos manualmente.
/// </summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(CamaraPortuariaDbContext db)
    {
        if (await db.Empresas.AnyAsync()) return;

        var grupoSocial = new GrupoFacturacion { Nombre = "Cuota Social 2026", Importe = 25000m, CreatedAt = DateTime.Now };
        var grupoExtra  = new GrupoFacturacion { Nombre = "Aporte Papelería", Importe = 8000m, CreatedAt = DateTime.Now };
        db.Grupos.AddRange(grupoSocial, grupoExtra);

        var empresas = new[]
        {
            Crear("Transportes del Sur", "Transportes del Sur S.A.", "30711234561", "ventas@tsur.com.ar"),
            Crear("Cerealera Pampa", "Cerealera Pampa S.R.L.", "30715678902", "admin@pampa.com.ar"),
            Crear("Logística Atlántica", "Logística Atlántica S.A.", "30719988773", "info@latlantica.com.ar"),
            Crear("Servicios Portuarios BB", "Servicios Portuarios BB S.A.", "30714455664", "contacto@spbb.com.ar"),
        };
        db.Empresas.AddRange(empresas);
        await db.SaveChangesAsync();

        foreach (var e in empresas)
            db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = e.Id, GrupoFacturacionId = grupoSocial.Id, CreatedAt = DateTime.Now });
        db.EmpresasGrupos.Add(new EmpresaGrupo { EmpresaId = empresas[0].Id, GrupoFacturacionId = grupoExtra.Id, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync();
    }

    private static Empresa Crear(string nombre, string razon, string cuit, string email) => new()
    {
        Nombre = nombre,
        RazonSocial = razon,
        Cuit = cuit,
        CreatedAt = DateTime.Now,
        Emails = [new EmailEmpresa { Email = email, CreatedAt = DateTime.Now }]
    };
}

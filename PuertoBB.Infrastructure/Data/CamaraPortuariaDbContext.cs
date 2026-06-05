using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data;

public class CamaraPortuariaDbContext : DbContext
{
    public CamaraPortuariaDbContext(DbContextOptions<CamaraPortuariaDbContext> options) : base(options) { }

    public DbSet<Empresa>          Empresas        => Set<Empresa>();
    public DbSet<EmailEmpresa>     EmailsEmpresa   => Set<EmailEmpresa>();
    public DbSet<GrupoFacturacion> Grupos          => Set<GrupoFacturacion>();
    public DbSet<EmpresaGrupo>     EmpresasGrupos  => Set<EmpresaGrupo>();
    public DbSet<Recibo>           Recibos         => Set<Recibo>();
    public DbSet<NotaDeCredito>    NotasDeCredito  => Set<NotaDeCredito>();
    public DbSet<Configuracion>    Configuraciones => Set<Configuracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica solo las configuraciones de la Cámara Portuaria (filtra por namespace).
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CamaraPortuariaDbContext).Assembly,
            t => t.Namespace == "PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria");
        base.OnModelCreating(modelBuilder);
    }
}

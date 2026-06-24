using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data;

public class CamaraPortuariaDbContext : PuertoBBDbContext
{
    public CamaraPortuariaDbContext(DbContextOptions<CamaraPortuariaDbContext> options) : base(options) { }

    public DbSet<Cliente>          Clientes        => Set<Cliente>();
    public DbSet<EmailCliente>     EmailsCliente   => Set<EmailCliente>();
    public DbSet<GrupoFacturacion>      Grupos          => Set<GrupoFacturacion>();
    public DbSet<GrupoFacturacionLinea> GruposLineas    => Set<GrupoFacturacionLinea>();
    public DbSet<ClienteGrupo>          ClientesGrupos  => Set<ClienteGrupo>();
    public DbSet<EmisionGrupo>          EmisionesGrupo  => Set<EmisionGrupo>();
    public DbSet<Recibo>           Recibos         => Set<Recibo>();
    public DbSet<ReciboLinea>      RecibosLineas   => Set<ReciboLinea>();
    public DbSet<ConceptoRecibo>   ConceptosRecibo => Set<ConceptoRecibo>();
    public DbSet<NotaDeCredito>    NotasDeCredito  => Set<NotaDeCredito>();
    public DbSet<Configuracion>    Configuraciones => Set<Configuracion>();
    public DbSet<PuntoDeVenta>     PuntosDeVenta   => Set<PuntoDeVenta>();
    public DbSet<CuentaCorreo>     CuentasCorreo   => Set<CuentaCorreo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica solo las configuraciones de la Cámara Portuaria (filtra por namespace).
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CamaraPortuariaDbContext).Assembly,
            t => t.Namespace == "PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria");
        base.OnModelCreating(modelBuilder);
    }
}

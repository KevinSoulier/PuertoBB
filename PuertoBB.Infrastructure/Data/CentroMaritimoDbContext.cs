using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data;

public class CentroMaritimoDbContext : DbContext
{
    public CentroMaritimoDbContext(DbContextOptions<CentroMaritimoDbContext> options) : base(options) { }

    public DbSet<Agencia>          Agencias        => Set<Agencia>();
    public DbSet<EmailAgencia>     EmailsAgencia   => Set<EmailAgencia>();
    public DbSet<GrupoFacturacion>      Grupos          => Set<GrupoFacturacion>();
    public DbSet<GrupoFacturacionLinea> GruposLineas    => Set<GrupoFacturacionLinea>();
    public DbSet<AgenciaGrupo>          AgenciasGrupos  => Set<AgenciaGrupo>();
    public DbSet<EmisionGrupo>          EmisionesGrupo  => Set<EmisionGrupo>();
    public DbSet<Barco>            Barcos          => Set<Barco>();
    public DbSet<Voucher>          Vouchers        => Set<Voucher>();
    public DbSet<ContadorVoucher>  Contadores      => Set<ContadorVoucher>();
    public DbSet<Recibo>           Recibos         => Set<Recibo>();
    public DbSet<ReciboLinea>      RecibosLineas   => Set<ReciboLinea>();
    public DbSet<ConceptoRecibo>   ConceptosRecibo => Set<ConceptoRecibo>();
    public DbSet<NotaDeCredito>    NotasDeCredito  => Set<NotaDeCredito>();
    public DbSet<Configuracion>    Configuraciones => Set<Configuracion>();
    public DbSet<PuntoDeVenta>     PuntosDeVenta   => Set<PuntoDeVenta>();
    public DbSet<CuentaCorreo>     CuentasCorreo   => Set<CuentaCorreo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CentroMaritimoDbContext).Assembly,
            t => t.Namespace == "PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo");
        base.OnModelCreating(modelBuilder);
    }
}

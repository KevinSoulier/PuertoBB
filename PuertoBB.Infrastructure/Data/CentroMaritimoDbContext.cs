using Microsoft.EntityFrameworkCore;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data;

public class CentroMaritimoDbContext : DbContext
{
    public CentroMaritimoDbContext(DbContextOptions<CentroMaritimoDbContext> options) : base(options) { }

    public DbSet<Cliente>          Clientes        => Set<Cliente>();
    public DbSet<EmailCliente>     EmailsCliente   => Set<EmailCliente>();
    public DbSet<GrupoFacturacion>      Grupos          => Set<GrupoFacturacion>();
    public DbSet<GrupoFacturacionLinea> GruposLineas    => Set<GrupoFacturacionLinea>();
    public DbSet<ClienteGrupo>          ClientesGrupos  => Set<ClienteGrupo>();
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

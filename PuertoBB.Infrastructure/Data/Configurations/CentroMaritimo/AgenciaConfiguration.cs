using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class AgenciaConfiguration : IEntityTypeConfiguration<Agencia>
{
    public void Configure(EntityTypeBuilder<Agencia> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Nombre).IsRequired().HasMaxLength(200);
        b.Property(a => a.RazonSocial).IsRequired().HasMaxLength(200);
        b.Property(a => a.Cuit).IsRequired().HasMaxLength(13);
        b.Property(a => a.Domicilio).HasMaxLength(300);
        // Sin índice por Cuit: la búsqueda y el chequeo de duplicado son en memoria (UI), y se permiten
        // CUIT repetidos a propósito; ninguna consulta SQL filtra por Cuit.

        b.HasMany(a => a.Emails).WithOne(x => x.Agencia).HasForeignKey(x => x.AgenciaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.Grupos).WithOne(x => x.Agencia).HasForeignKey(x => x.AgenciaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.Vouchers).WithOne(x => x.Agencia).HasForeignKey(x => x.AgenciaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(a => a.Recibos).WithOne(x => x.Agencia).HasForeignKey(x => x.AgenciaId).OnDelete(DeleteBehavior.Restrict);
    }
}

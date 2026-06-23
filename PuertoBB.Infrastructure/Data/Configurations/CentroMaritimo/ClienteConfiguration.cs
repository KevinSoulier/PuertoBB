using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CentroMaritimo;

namespace PuertoBB.Infrastructure.Data.Configurations.CentroMaritimo;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Nombre).IsRequired().HasMaxLength(200);
        b.Property(a => a.RazonSocial).IsRequired().HasMaxLength(200);
        b.Property(a => a.Cuit).IsRequired().HasMaxLength(13);
        b.Property(a => a.Domicilio).HasMaxLength(300);
        // Sin índice por Cuit: la búsqueda y el chequeo de duplicado son en memoria (UI), y se permiten
        // CUIT repetidos a propósito; ninguna consulta SQL filtra por Cuit.

        b.HasMany(a => a.Emails).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.Grupos).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.Vouchers).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(a => a.Recibos).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}

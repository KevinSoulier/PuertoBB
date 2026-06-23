using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PuertoBB.Core.Entities.CamaraPortuaria;

namespace PuertoBB.Infrastructure.Data.Configurations.CamaraPortuaria;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
        b.Property(e => e.RazonSocial).IsRequired().HasMaxLength(200);
        b.Property(e => e.Cuit).IsRequired().HasMaxLength(13);
        b.Property(e => e.Domicilio).HasMaxLength(300);
        // Sin índice por Cuit: la búsqueda y el chequeo de duplicado son en memoria (UI), y se permiten
        // CUIT repetidos a propósito; ninguna consulta SQL filtra por Cuit.

        b.HasMany(e => e.Emails).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Grupos).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.Recibos).WithOne(x => x.Cliente).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}

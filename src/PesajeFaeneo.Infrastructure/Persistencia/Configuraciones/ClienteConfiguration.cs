using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo)
            .IsRequired();

        builder.HasIndex(c => c.Codigo)
            .IsUnique();

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(100)
            // NOCASE para que las búsquedas de F2-06 sean insensibles a mayúsculas/minúsculas.
            .UseCollation("NOCASE");

        builder.Property(c => c.Direccion)
            .HasMaxLength(150);

        builder.Property(c => c.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreadoEn)
            .IsRequired();

        builder.HasMany(c => c.Pesajes)
            .WithOne(p => p.Cliente)
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

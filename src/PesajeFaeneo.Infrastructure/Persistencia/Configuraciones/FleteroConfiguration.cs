using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class FleteroConfiguration : IEntityTypeConfiguration<Fletero>
{
    public void Configure(EntityTypeBuilder<Fletero> builder)
    {
        builder.ToTable("Fleteros", t =>
        {
            t.HasCheckConstraint("CK_Fleteros_Orden", "Orden BETWEEN 1 AND 4");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Orden)
            .IsRequired();

        builder.HasIndex(f => f.Orden)
            .IsUnique()
            .HasFilter("Activo = 1");

        builder.Property(f => f.Nombre)
            .IsRequired()
            .HasMaxLength(100)
            // NOCASE para que las búsquedas de F2-06 sean insensibles a mayúsculas/minúsculas.
            .UseCollation("NOCASE");

        builder.Property(f => f.Celular)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(f => f.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(f => f.CreadoEn)
            .IsRequired();
    }
}

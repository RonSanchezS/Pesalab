using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class GanaderoConfiguration : IEntityTypeConfiguration<Ganadero>
{
    public void Configure(EntityTypeBuilder<Ganadero> builder)
    {
        builder.ToTable("Ganaderos", t => t.HasCheckConstraint("CK_Ganaderos_Marca", "length(Marca) BETWEEN 1 AND 5"));

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Marca)
            .IsRequired()
            .HasMaxLength(5)
            // NOCASE: "AH" y "ah" deben tratarse como la misma marca (hoy podían coexistir).
            .UseCollation("NOCASE");

        builder.HasIndex(g => g.Marca)
            .IsUnique();

        builder.Property(g => g.Nombre)
            .IsRequired()
            .HasMaxLength(100)
            // NOCASE para que las búsquedas de F2-06 sean insensibles a mayúsculas/minúsculas.
            .UseCollation("NOCASE");

        builder.Property(g => g.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(g => g.CreadoEn)
            .IsRequired();

        builder.HasMany(g => g.Pesajes)
            .WithOne(p => p.Ganadero)
            .HasForeignKey(p => p.GanaderoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

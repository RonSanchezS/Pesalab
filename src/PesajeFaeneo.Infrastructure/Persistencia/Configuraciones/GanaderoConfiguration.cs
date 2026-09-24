using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class GanaderoConfiguration : IEntityTypeConfiguration<Ganadero>
{
    public void Configure(EntityTypeBuilder<Ganadero> builder)
    {
        builder.ToTable("Ganaderos");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Marca)
            .IsRequired()
            .HasMaxLength(5);

        builder.HasIndex(g => g.Marca)
            .IsUnique();

        builder.Property(g => g.Nombre)
            .IsRequired()
            .HasMaxLength(100);

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

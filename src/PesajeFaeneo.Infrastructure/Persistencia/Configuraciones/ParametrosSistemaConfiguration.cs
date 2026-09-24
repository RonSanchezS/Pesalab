using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class ParametrosSistemaConfiguration : IEntityTypeConfiguration<ParametrosSistema>
{
    public void Configure(EntityTypeBuilder<ParametrosSistema> builder)
    {
        builder.ToTable("ParametrosSistema", t =>
        {
            t.HasCheckConstraint("CK_ParametrosSistema_Id", "Id = 1");
            t.HasCheckConstraint("CK_ParametrosSistema_SiguienteNumeroBoleta", "SiguienteNumeroBoleta >= 1");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.NombreEmpresa)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Direccion)
            .HasMaxLength(150);

        builder.Property(p => p.Telefono)
            .HasMaxLength(50);

        builder.Property(p => p.Ciudad)
            .HasMaxLength(100);

        builder.Property(p => p.RegistroSanitario)
            .HasMaxLength(100);

        builder.Property(p => p.TelefonosReclamos)
            .HasMaxLength(100);

        builder.Property(p => p.LogoPath)
            .HasMaxLength(250);

        builder.Property(p => p.SiguienteNumeroBoleta)
            .IsRequired();

        builder.Property(p => p.ObservacionPorDefecto)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(p => p.ConfiguracionInicialCompleta)
            .IsRequired();

        builder.Property(p => p.ModificadoEn)
            .IsRequired();

        builder.HasData(new ParametrosSistema
        {
            Id = 1,
            NombreEmpresa = "NOMBRE DE EMPRESA",
            Ciudad = "Santa Cruz - Bolivia",
            SiguienteNumeroBoleta = 1,
            ObservacionPorDefecto = "SIN NOVEDAD",
            ConfiguracionInicialCompleta = false,
            ModificadoEn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

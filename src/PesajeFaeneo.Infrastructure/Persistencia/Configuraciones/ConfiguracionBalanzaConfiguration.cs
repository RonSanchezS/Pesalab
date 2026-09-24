using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class ConfiguracionBalanzaConfiguration : IEntityTypeConfiguration<ConfiguracionBalanza>
{
    public void Configure(EntityTypeBuilder<ConfiguracionBalanza> builder)
    {
        builder.ToTable("ConfiguracionesBalanza", t =>
        {
            t.HasCheckConstraint("CK_ConfiguracionBalanza_Id", "Id = 1");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.TipoConexion)
            .IsRequired();

        builder.Property(c => c.PuertoCom)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.BaudRate)
            .IsRequired();

        builder.Property(c => c.DataBits)
            .IsRequired();

        builder.Property(c => c.Paridad)
            .IsRequired();

        builder.Property(c => c.BitsParada)
            .IsRequired();

        builder.Property(c => c.HostTcp)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.PuertoTcp)
            .IsRequired();

        builder.Property(c => c.PatronRegex)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(c => c.LecturasParaEstable)
            .IsRequired();

        var decimalToRealConverter = new ValueConverter<decimal, double>(
            v => (double)v,
            v => Math.Round((decimal)v, 1, MidpointRounding.AwayFromZero));

        builder.Property(c => c.ToleranciaKg)
            .HasConversion(decimalToRealConverter)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(c => c.TimeoutMs)
            .IsRequired();

        builder.Property(c => c.ModificadoEn)
            .IsRequired();

        builder.HasData(new ConfiguracionBalanza
        {
            Id = 1,
            TipoConexion = TipoConexion.Simulador,
            PuertoCom = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            Paridad = ParidadSerial.None,
            BitsParada = BitsParada.One,
            HostTcp = "192.168.1.100",
            PuertoTcp = 4001,
            PatronRegex = @"(?<peso>[-+]?\d+(?:[.,]\d+)?)",
            LecturasParaEstable = 5,
            ToleranciaKg = 0.5m,
            TimeoutMs = 3000,
            ModificadoEn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

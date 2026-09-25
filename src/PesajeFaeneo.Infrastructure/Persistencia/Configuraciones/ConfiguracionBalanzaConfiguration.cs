using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia.Conversores;

namespace PesajeFaeneo.Infrastructure.Persistencia.Configuraciones;

public class ConfiguracionBalanzaConfiguration : IEntityTypeConfiguration<ConfiguracionBalanza>
{
    public void Configure(EntityTypeBuilder<ConfiguracionBalanza> builder)
    {
        builder.ToTable("ConfiguracionesBalanza", t =>
        {
            t.HasCheckConstraint("CK_ConfiguracionBalanza_Id", "Id = 1");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_TipoConexion", "TipoConexion IN (1, 2, 3)");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_LecturasParaEstable", "LecturasParaEstable BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_TimeoutMs", "TimeoutMs BETWEEN 500 AND 60000");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_BaudRate", "BaudRate BETWEEN 110 AND 921600");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_DataBits", "DataBits BETWEEN 5 AND 8");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_Paridad", "Paridad IN (0, 1, 2, 3, 4)");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_BitsParada", "BitsParada IN (1, 2, 3)");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_PuertoTcp", "PuertoTcp BETWEEN 1 AND 65535");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_ToleranciaKg", "ToleranciaKg >= 0 AND ToleranciaKg <= 10");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_PuertoCom", "length(PuertoCom) BETWEEN 1 AND 20");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_HostTcp", "length(HostTcp) BETWEEN 1 AND 100");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_PatronRegex", "length(PatronRegex) BETWEEN 1 AND 250");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_DelimitadorTrama", "DelimitadorTrama IN (1, 2, 3)");
            t.HasCheckConstraint("CK_ConfiguracionBalanza_UnidadPeso", "UnidadPeso IN (1, 2)");
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

        builder.Property(c => c.ToleranciaKg)
            .HasConversion(DecimalARealConverter.Instancia)
            .HasColumnType("REAL")
            .IsRequired();

        builder.Property(c => c.TimeoutMs)
            .IsRequired();

        builder.Property(c => c.DelimitadorTrama)
            .IsRequired();

        builder.Property(c => c.UnidadPeso)
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
            PatronRegex = @"^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)",
            LecturasParaEstable = 5,
            ToleranciaKg = 0.5m,
            TimeoutMs = 3000,
            DelimitadorTrama = DelimitadorTrama.FinDeLinea,
            UnidadPeso = UnidadPesoTrama.Kilogramos,
            ModificadoEn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class FabricaAuditoriaTests
{
    [Fact]
    public void Crear_GanaderoConPesajesNavegacion_SerializaSoloSnapshotSinPropiedadPesajes()
    {
        var ganadero = new Ganadero
        {
            Id = 15,
            Marca = "AH",
            Nombre = "GANADERO EJEMPLO",
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        var pesaje = new Pesaje
        {
            Id = 100,
            NumeroBoleta = 12345,
            GanaderoId = 15,
            Ganadero = ganadero
        };
        ganadero.Pesajes.Add(pesaje);

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 1,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Ganadero,
            ganadero.Id.ToString(),
            despues: ganadero,
            motivo: "Alta ganadero con navegacion");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.DoesNotContain("Pesajes", auditoria.DatosDespuesJson);
        Assert.Contains("\"Id\":15", auditoria.DatosDespuesJson);
        Assert.Contains("\"Marca\":\"AH\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"Nombre\":\"GANADERO EJEMPLO\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"Activo\":true", auditoria.DatosDespuesJson);
    }

    [Fact]
    public void Crear_ConfiguracionBalanza_SerializaEnumComoTextoYSinIdNiModificadoEn()
    {
        var config = new ConfiguracionBalanza
        {
            Id = 1,
            TipoConexion = TipoConexion.Serial,
            PuertoCom = "COM3",
            BaudRate = 9600,
            DataBits = 8,
            Paridad = ParidadSerial.None,
            BitsParada = BitsParada.One,
            HostTcp = "192.168.1.100",
            PuertoTcp = 4001,
            PatronRegex = @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)",
            LecturasParaEstable = 5,
            ToleranciaKg = 0.5m,
            TimeoutMs = 3000,
            ModificadoEn = DateTime.UtcNow
        };

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 1,
            AccionAuditoria.CambioConfigBalanza,
            EntidadesAuditoria.ConfiguracionBalanza,
            "1",
            despues: config,
            motivo: "Actualización de configuración de balanza");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Contains("\"TipoConexion\":\"Serial\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"Paridad\":\"None\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"BitsParada\":\"One\"", auditoria.DatosDespuesJson);
        Assert.DoesNotContain("\"Id\"", auditoria.DatosDespuesJson);
        Assert.DoesNotContain("ModificadoEn", auditoria.DatosDespuesJson);
    }
}

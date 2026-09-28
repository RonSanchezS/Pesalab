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

    [Fact]
    public void Crear_Pesaje_SerializaSoloSnapshotSinNavegacionesYEnumsComoTexto()
    {
        var pesaje = new Pesaje
        {
            Id = 5,
            NumeroBoleta = 1001,
            FechaHora = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc),
            GanaderoId = 2,
            ClienteId = 3,
            Direccion = "CALLE 123",
            Secuencia = 1,
            ColorRaza = "BLANCO",
            Sexo = 'M',
            TipoDesposte = TipoDesposte.ResCompleta,
            PesoIzquierdo = 120.0m,
            PesoDerecho = 118.5m,
            PesoTotal = 238.5m,
            Observacion = "OBSERVACION",
            Estado = EstadoPesaje.Vigente,
            OrigenPeso = OrigenPeso.Balanza,
            UsuarioId = 4,
            CreadoEn = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc),
            Ganadero = new Ganadero { Id = 2, Marca = "AH", Nombre = "GANADERO" },
            Cliente = new Cliente { Id = 3, Codigo = 10, Nombre = "CLIENTE", Direccion = "DIR" },
            Usuario = new Usuario { Id = 4, NombreUsuario = "operador", NombreCompleto = "Op", Hash = "hash", Salt = "salt" }
        };

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 4,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Pesaje,
            pesaje.Id.ToString(),
            despues: pesaje,
            motivo: "Alta de pesaje boleta 1001");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Contains("\"Sexo\":\"M\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"TipoDesposte\":\"ResCompleta\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"OrigenPeso\":\"Balanza\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"Estado\":\"Vigente\"", auditoria.DatosDespuesJson);
        Assert.DoesNotContain("\"Ganadero\":{", auditoria.DatosDespuesJson);
        Assert.DoesNotContain("\"Cliente\":{", auditoria.DatosDespuesJson);
        Assert.DoesNotContain("\"Usuario\":{", auditoria.DatosDespuesJson);
    }
}

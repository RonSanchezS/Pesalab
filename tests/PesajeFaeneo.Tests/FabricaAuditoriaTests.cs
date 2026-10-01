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

    [Fact]
    public void Crear_ImpresionAuditoria_SerializaEnumsComoTextoYValoresCorrectos()
    {
        var registro = new PesajeFaeneo.Application.Boletas.Dtos.RegistroImpresion(
            PesajeId: 5,
            NumeroBoleta: 1234,
            EsReimpresion: true,
            Copias: 2,
            Impresora: "HP LaserJet",
            Formato: PesajeFaeneo.Application.Boletas.Dtos.FormatoBoleta.MediaCarta,
            NombreEquipo: "BALANZA-01");

        var snapshot = FabricaAuditoria.CrearSnapshot(registro);

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 1,
            AccionAuditoria.Reimprimir,
            EntidadesAuditoria.Pesaje,
            registro.PesajeId.ToString(),
            despues: snapshot,
            motivo: "Reimpresión de boleta 1234");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Contains("\"Formato\":\"MediaCarta\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"EsReimpresion\":true", auditoria.DatosDespuesJson);
        Assert.Contains("\"NumeroBoleta\":1234", auditoria.DatosDespuesJson);
        Assert.Contains("\"NombreEquipo\":\"BALANZA-01\"", auditoria.DatosDespuesJson);
    }

    [Fact]
    public void Crear_ConfiguracionImpresionAuditoria_SerializaCorrectamente()
    {
        var config = new PesajeFaeneo.Application.Boletas.Dtos.ConfiguracionImpresion(
            NombreImpresora: "EPSON",
            Formato: PesajeFaeneo.Application.Boletas.Dtos.FormatoBoleta.Ticket80mm,
            NombrePapel: "80mm",
            Copias: 1,
            ImprimirAlGrabar: true,
            DesplazamientoXMm: 1.5m,
            DesplazamientoYMm: -2.0m);

        var snapshot = FabricaAuditoria.CrearSnapshot(config, "PC-01");

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 1,
            AccionAuditoria.CambioConfigImpresion,
            EntidadesAuditoria.ConfiguracionImpresion,
            "PC-01",
            despues: snapshot,
            motivo: "Configuración actualizada");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Contains("\"Formato\":\"Ticket80mm\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"NombreEquipo\":\"PC-01\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"ImprimirAlGrabar\":true", auditoria.DatosDespuesJson);
    }

    [Fact]
    public void CrearSnapshot_PesajeConGanaderoYCliente_IncluyeDatosEnriquecidosYCamposAnulacion()
    {
        var ganadero = new Ganadero { Id = 10, Marca = "XY", Nombre = "GANADERO XY" };
        var cliente = new Cliente { Id = 20, Codigo = 555, Nombre = "CLIENTE 555" };
        var fechaAnulado = new DateTime(2026, 9, 28, 14, 0, 0, DateTimeKind.Utc);
        var pesaje = new Pesaje
        {
            Id = 50,
            NumeroBoleta = 999,
            GanaderoId = 10,
            ClienteId = 20,
            Estado = EstadoPesaje.Anulado,
            MotivoAnulacion = "ERROR PESO",
            AnuladoPorId = 2,
            AnuladoEn = fechaAnulado
        };

        var snapshot = FabricaAuditoria.CrearSnapshot(pesaje, ganadero, cliente);

        Assert.Equal("XY", snapshot.MarcaGanadero);
        Assert.Equal("GANADERO XY", snapshot.NombreGanadero);
        Assert.Equal(555, snapshot.CodigoCliente);
        Assert.Equal("CLIENTE 555", snapshot.NombreCliente);
        Assert.Equal("ERROR PESO", snapshot.MotivoAnulacion);
        Assert.Equal(2, snapshot.AnuladoPorId);
        Assert.Equal(fechaAnulado, snapshot.AnuladoEn);
    }

    [Fact]
    public void CrearSnapshot_PesajeConNavegacionesNulas_NoLanzaYPropiedadesEnriquecidasSonNulas()
    {
        var pesaje = new Pesaje
        {
            Id = 51,
            NumeroBoleta = 1000,
            GanaderoId = 10,
            ClienteId = 20
        };

        var snapshot = FabricaAuditoria.CrearSnapshot(pesaje);

        Assert.Null(snapshot.MarcaGanadero);
        Assert.Null(snapshot.NombreGanadero);
        Assert.Null(snapshot.CodigoCliente);
        Assert.Null(snapshot.NombreCliente);
        Assert.Null(snapshot.MotivoAnulacion);
    }

    [Fact]
    public void Crear_PesajeAnulado_JsonContieneEstadoAnuladoYMotivo()
    {
        var pesaje = new Pesaje
        {
            Id = 52,
            NumeroBoleta = 1002,
            Estado = EstadoPesaje.Anulado,
            MotivoAnulacion = "BOLETA DUPLICADA",
            AnuladoPorId = 1,
            AnuladoEn = DateTime.UtcNow
        };

        var auditoria = FabricaAuditoria.Crear(
            TimeProvider.System,
            usuarioId: 1,
            AccionAuditoria.Anular,
            EntidadesAuditoria.Pesaje,
            pesaje.Id.ToString(),
            despues: pesaje,
            motivo: "Anulación de prueba");

        Assert.NotNull(auditoria.DatosDespuesJson);
        Assert.Contains("\"Estado\":\"Anulado\"", auditoria.DatosDespuesJson);
        Assert.Contains("\"MotivoAnulacion\":\"BOLETA DUPLICADA\"", auditoria.DatosDespuesJson);
    }

    [Fact]
    public void EntidadesAuditoria_Todas_ContieneLasOchoEntidadesYPesaje()
    {
        Assert.Equal(8, EntidadesAuditoria.Todas.Count);
        Assert.Contains(EntidadesAuditoria.Pesaje, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.Usuario, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.Ganadero, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.Cliente, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.Fletero, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.ParametrosSistema, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.ConfiguracionBalanza, EntidadesAuditoria.Todas);
        Assert.Contains(EntidadesAuditoria.ConfiguracionImpresion, EntidadesAuditoria.Todas);
    }
}


using System.Globalization;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DiferenciasAuditoriaTests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Diff_Test",
        TimeSpan.FromHours(-4),
        "Bolivia Standard Time (Test)",
        "Bolivia Standard Time (Test)");

    [Fact]
    public void Calcular_ModificacionDeUnCampo_SoloEseCambioTrueDemasFalse()
    {
        string antes = """{"Direccion":"CALLE 1","Observacion":"OK"}""";
        string despues = """{"Direccion":"CALLE 2","Observacion":"OK"}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        Assert.Equal(2, cambios.Count);
        var cDir = Assert.Single(cambios, c => c.Campo == "Direccion");
        Assert.Equal("CALLE 1", cDir.Antes);
        Assert.Equal("CALLE 2", cDir.Despues);
        Assert.True(cDir.Cambio);

        var cObs = Assert.Single(cambios, c => c.Campo == "Observacion");
        Assert.Equal("OK", cObs.Antes);
        Assert.Equal("OK", cObs.Despues);
        Assert.False(cObs.Cambio);
    }

    [Fact]
    public void Calcular_AltaAntesNulo_TodosConAntesNull()
    {
        string despues = """{"NumeroBoleta":101,"Observacion":"NUEVO","MotivoAnulacion":null}""";

        var cambios = DiferenciasAuditoria.Calcular(null, despues, ZonaBolivia);

        Assert.Equal(3, cambios.Count);
        Assert.All(cambios, c => Assert.Null(c.Antes));

        var cNum = Assert.Single(cambios, c => c.Campo == "NumeroBoleta");
        Assert.Equal("101", cNum.Despues);
        Assert.True(cNum.Cambio);

        var cObs = Assert.Single(cambios, c => c.Campo == "Observacion");
        Assert.Equal("NUEVO", cObs.Despues);
        Assert.True(cObs.Cambio);

        var cMotivo = Assert.Single(cambios, c => c.Campo == "MotivoAnulacion");
        Assert.Null(cMotivo.Despues);
        Assert.False(cMotivo.Cambio); // ambos son null -> sin cambio
    }

    [Fact]
    public void Calcular_PropiedadPresenteSoloEnDespues_AntesNullCambioTrue()
    {
        string antes = """{"MarcaGanadero":"AH"}""";
        string despues = """{"MarcaGanadero":"AH","NombreGanadero":"ALBERTO"}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        Assert.Equal(2, cambios.Count);
        var cMarca = Assert.Single(cambios, c => c.Campo == "MarcaGanadero");
        Assert.False(cMarca.Cambio);

        var cNombre = Assert.Single(cambios, c => c.Campo == "NombreGanadero");
        Assert.Null(cNombre.Antes);
        Assert.Equal("ALBERTO", cNombre.Despues);
        Assert.True(cNombre.Cambio);
    }

    [Fact]
    public void Calcular_Numeros_MismoValorSinCambio_DistintoValorConCambio()
    {
        string antes = """{"PesoIzquierdo":238.5,"PesoDerecho":120.0}""";
        string despues = """{"PesoIzquierdo":238.5,"PesoDerecho":121.0}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        var cIzq = Assert.Single(cambios, c => c.Campo == "PesoIzquierdo");
        Assert.Equal("238.5", cIzq.Antes);
        Assert.Equal("238.5", cIzq.Despues);
        Assert.False(cIzq.Cambio);

        var cDer = Assert.Single(cambios, c => c.Campo == "PesoDerecho");
        Assert.Equal("120.0", cDer.Antes);
        Assert.Equal("121.0", cDer.Despues);
        Assert.True(cDer.Cambio);
    }

    [Fact]
    public void Calcular_Booleanos_FormateaSiNo_YEnumsSeMantienenTexto()
    {
        string antes = """{"Activo":true,"TipoDesposte":"MediaRes"}""";
        string despues = """{"Activo":false,"TipoDesposte":"MediaRes"}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        var cActivo = Assert.Single(cambios, c => c.Campo == "Activo");
        Assert.Equal("Sí", cActivo.Antes);
        Assert.Equal("No", cActivo.Despues);
        Assert.True(cActivo.Cambio);

        var cTipo = Assert.Single(cambios, c => c.Campo == "TipoDesposte");
        Assert.Equal("MediaRes", cTipo.Antes);
        Assert.Equal("MediaRes", cTipo.Despues);
        Assert.False(cTipo.Cambio);
    }

    [Fact]
    public void Calcular_FechaUtc_ConvierteALocalYFormatoFijoEsBo_IndependienteDeCurrentCulture()
    {
        var culturaAnterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");

            string antes = """{"FechaHora":"2026-09-27T14:00:00Z"}""";
            string despues = """{"FechaHora":"2026-09-27T18:30:15Z"}""";

            var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

            var cFecha = Assert.Single(cambios, c => c.Campo == "FechaHora");
            // 14:00Z - 4h = 10:00 local
            Assert.Equal("27/09/2026 10:00:00", cFecha.Antes);
            // 18:30:15Z - 4h = 14:30:15 local
            Assert.Equal("27/09/2026 14:30:15", cFecha.Despues);
            Assert.True(cFecha.Cambio);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }
    }

    [Fact]
    public void Calcular_TextoQuePareceFechaPeroSinZ_NoSeConvierte()
    {
        string antes = """{"Observacion":"2026-09-27T14:00:00"}""";
        string despues = """{"Observacion":"2026-09-27T14:00:00"}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        var cObs = Assert.Single(cambios, c => c.Campo == "Observacion");
        Assert.Equal("2026-09-27T14:00:00", cObs.Antes);
        Assert.Equal("2026-09-27T14:00:00", cObs.Despues);
        Assert.False(cObs.Cambio);
    }

    [Theory]
    [InlineData("{invalid json", "{other json")]
    [InlineData("[1,2,3]", "[4,5,6]")]
    [InlineData("\"string\"", "{}")]
    public void Calcular_JsonInvalidoORaizNoObjeto_RetornaFilaDatosSinLanzar(string a, string d)
    {
        var cambios = DiferenciasAuditoria.Calcular(a, d, ZonaBolivia);

        var c = Assert.Single(cambios);
        Assert.Equal("(datos)", c.Campo);
        Assert.Equal(a, c.Antes);
        Assert.Equal(d, c.Despues);
        Assert.True(c.Cambio);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", null)]
    [InlineData(null, "   ")]
    public void Calcular_AmbosNulosOVacios_RetornaListaVacia(string? a, string? d)
    {
        var cambios = DiferenciasAuditoria.Calcular(a, d, ZonaBolivia);
        Assert.Empty(cambios);
    }

    [Fact]
    public void Calcular_OrdenCampos_CamposDeAntesPrimeroLuegoLosNuevos()
    {
        string antes = """{"Zeta":1,"Alfa":2,"Beta":3}""";
        string despues = """{"Beta":3,"Alfa":2,"Gamma":4,"Zeta":1}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        var nombres = cambios.Select(c => c.Campo).ToList();
        Assert.Equal(new[] { "Zeta", "Alfa", "Beta", "Gamma" }, nombres);
    }

    [Fact]
    public void Calcular_ObjetoAnidado_RetornaGetRawText()
    {
        string antes = """{"Config":{"Puerto":"COM1"}}""";
        string despues = """{"Config":{"Puerto":"COM2"}}""";

        var cambios = DiferenciasAuditoria.Calcular(antes, despues, ZonaBolivia);

        var c = Assert.Single(cambios, x => x.Campo == "Config");
        Assert.Equal("""{"Puerto":"COM1"}""", c.Antes);
        Assert.Equal("""{"Puerto":"COM2"}""", c.Despues);
        Assert.True(c.Cambio);
    }

    [Fact]
    public void EtiquetaCampo_Pesaje_TraduceCamposConocidos_YDesconocidosQuedanCrudos()
    {
        Assert.Equal("N° de boleta", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "NumeroBoleta"));
        Assert.Equal("Fecha y hora", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "FechaHora"));
        Assert.Equal("Ganadero (id)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "GanaderoId"));
        // F10 (O4): "marca" pasó a "código"; la clave histórica MarcaGanadero se sigue mostrando con la etiqueta nueva.
        Assert.Equal("Ganadero (código)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "CodigoGanadero"));
        Assert.Equal("Ganadero (código)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "MarcaGanadero"));
        Assert.Equal("Fletero (id)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "FleteroId"));
        Assert.Equal("Fletero (código)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "CodigoFletero"));
        Assert.Equal("Fletero (nombre)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "NombreFletero"));
        Assert.Equal("Codigo", DiferenciasAuditoria.EtiquetaCampo("Ganadero", "Marca")); // registros anteriores a F10
        Assert.Equal("Codigo", DiferenciasAuditoria.EtiquetaCampo("Fletero", "Orden"));
        Assert.Equal("Ganadero (nombre)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "NombreGanadero"));
        Assert.Equal("Cliente (id)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "ClienteId"));
        Assert.Equal("Cliente (código)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "CodigoCliente"));
        Assert.Equal("Cliente (nombre)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "NombreCliente"));
        Assert.Equal("Dirección", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "Direccion"));
        Assert.Equal("Secuencia", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "Secuencia"));
        Assert.Equal("Color/raza", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "ColorRaza"));
        Assert.Equal("Sexo", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "Sexo"));
        Assert.Equal("Tipo de desposte", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "TipoDesposte"));
        Assert.Equal("Peso izquierdo", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "PesoIzquierdo"));
        Assert.Equal("Peso derecho", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "PesoDerecho"));
        Assert.Equal("Peso total", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "PesoTotal"));
        Assert.Equal("Observación", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "Observacion"));
        Assert.Equal("Estado", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "Estado"));
        Assert.Equal("Origen del peso", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "OrigenPeso"));
        Assert.Equal("Usuario (id)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "UsuarioId"));
        Assert.Equal("Motivo de anulación", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "MotivoAnulacion"));
        Assert.Equal("Anulado por (id)", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "AnuladoPorId"));
        Assert.Equal("Anulado en", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "AnuladoEn"));

        // Desconocido en Pesaje
        Assert.Equal("CampoInexistente", DiferenciasAuditoria.EtiquetaCampo("Pesaje", "CampoInexistente"));

        // Otra entidad -> nombre crudo
        Assert.Equal("NumeroBoleta", DiferenciasAuditoria.EtiquetaCampo("Ganadero", "NumeroBoleta"));
        Assert.Equal("Nombre", DiferenciasAuditoria.EtiquetaCampo("Cliente", "Nombre"));
    }

    [Fact]
    public void Calcular_RoundTripReal_PesajeAntesYDespuesDeModificarDireccion_MarcaSoloDireccion()
    {
        var tiempoFijo = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var pAntes = new Pesaje
        {
            Id = 1,
            NumeroBoleta = 3001,
            FechaHora = tiempoFijo,
            GanaderoId = 10,
            Ganadero = new Ganadero { Id = 10, Codigo = "AH", Nombre = "GAN 1" },
            ClienteId = 20,
            Cliente = new Cliente { Id = 20, Codigo = 100, Nombre = "CLI 1" },
            Direccion = "CALLE 1",
            Secuencia = 1,
            ColorRaza = "BLANCO",
            Sexo = 'M',
            TipoDesposte = TipoDesposte.MediaRes,
            PesoIzquierdo = 120.5m,
            PesoDerecho = 0.0m,
            PesoTotal = 120.5m,
            OrigenPeso = OrigenPeso.Balanza,
            Observacion = "OK",
            Estado = EstadoPesaje.Vigente,
            UsuarioId = 5,
            CreadoEn = tiempoFijo
        };

        var pDespues = new Pesaje
        {
            Id = 1,
            NumeroBoleta = 3001,
            FechaHora = tiempoFijo,
            GanaderoId = 10,
            Ganadero = new Ganadero { Id = 10, Codigo = "AH", Nombre = "GAN 1" },
            ClienteId = 20,
            Cliente = new Cliente { Id = 20, Codigo = 100, Nombre = "CLI 1" },
            Direccion = "CALLE 2", // CAMBIADO
            Secuencia = 1,
            ColorRaza = "BLANCO",
            Sexo = 'M',
            TipoDesposte = TipoDesposte.MediaRes,
            PesoIzquierdo = 120.5m,
            PesoDerecho = 0.0m,
            PesoTotal = 120.5m,
            OrigenPeso = OrigenPeso.Balanza,
            Observacion = "OK",
            Estado = EstadoPesaje.Vigente,
            UsuarioId = 5,
            CreadoEn = tiempoFijo,
            ModificadoEn = tiempoFijo.AddMinutes(5)
        };

        var snapAntes = FabricaAuditoria.CrearSnapshot(pAntes);
        var snapDespues = FabricaAuditoria.CrearSnapshot(pDespues);

        var timeMock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(tiempoFijo);
        var aud = FabricaAuditoria.Crear(
            timeMock,
            5,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Pesaje,
            "1",
            antes: snapAntes,
            despues: snapDespues,
            motivo: "Edición");

        var cambios = DiferenciasAuditoria.Calcular(aud.DatosAntesJson, aud.DatosDespuesJson, ZonaBolivia);

        // ModificadoEn no debe existir en los cambios porque no está en el snapshot
        Assert.DoesNotContain(cambios, c => c.Campo == "ModificadoEn");

        // Solo Direccion tiene Cambio == true
        var cambiados = cambios.Where(c => c.Cambio).ToList();
        var soloDir = Assert.Single(cambiados);
        Assert.Equal("Direccion", soloDir.Campo);
        Assert.Equal("CALLE 1", soloDir.Antes);
        Assert.Equal("CALLE 2", soloDir.Despues);
    }
}

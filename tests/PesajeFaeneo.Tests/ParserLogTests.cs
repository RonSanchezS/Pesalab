using PesajeFaeneo.Application.Utilitarios.Logs;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ParserLogTests
{
    [Fact]
    public void Test01_Analizar_TextoLiteralH7_GeneraTresEntradasYDetalle()
    {
        var texto =
            "2026-10-01 16:03:18.297 -04:00 [INF] Hola 1\r\n" +
            "2026-10-01 16:03:19.123 -04:00 [ERR] Error grave\r\n" +
            "System.InvalidOperationException: boom\r\n" +
            "linea2\r\n" +
            "2026-10-01 16:03:20.456 -04:00 [WRN] Advertencia final\r\n";

        var entradas = ParserLog.Analizar(texto);

        Assert.Equal(3, entradas.Count);

        var e1 = entradas[0];
        Assert.Equal(1, e1.Linea);
        Assert.Equal("2026-10-01 16:03:18.297 -04:00", e1.Marca);
        Assert.Equal(NivelLog.Informacion, e1.Nivel);
        Assert.Equal("Hola 1", e1.Mensaje);
        Assert.Equal("", e1.Detalle);

        var e2 = entradas[1];
        Assert.Equal(2, e2.Linea);
        Assert.Equal("2026-10-01 16:03:19.123 -04:00", e2.Marca);
        Assert.Equal(NivelLog.Error, e2.Nivel);
        Assert.Equal("Error grave", e2.Mensaje);
        Assert.Equal("System.InvalidOperationException: boom\nlinea2", e2.Detalle);

        var e3 = entradas[2];
        Assert.Equal(5, e3.Linea);
        Assert.Equal("2026-10-01 16:03:20.456 -04:00", e3.Marca);
        Assert.Equal(NivelLog.Advertencia, e3.Nivel);
        Assert.Equal("Advertencia final", e3.Mensaje);
        Assert.Equal("", e3.Detalle);
    }

    [Fact]
    public void Test02_Analizar_LineaSueltaAlPrincipio_GeneraEntradaDesconocido()
    {
        var texto =
            "Texto no estructurado al inicio\n" +
            "2026-10-01 16:03:18.297 -04:00 [INF] Hola\n";

        var entradas = ParserLog.Analizar(texto);

        Assert.Equal(2, entradas.Count);
        Assert.Equal(1, entradas[0].Linea);
        Assert.Equal("", entradas[0].Marca);
        Assert.Equal(NivelLog.Desconocido, entradas[0].Nivel);
        Assert.Equal("Texto no estructurado al inicio", entradas[0].Mensaje);

        Assert.Equal(2, entradas[1].Linea);
        Assert.Equal(NivelLog.Informacion, entradas[1].Nivel);
        Assert.Equal("Hola", entradas[1].Mensaje);
    }

    [Theory]
    [InlineData("pesaje-20261001.log", true)]
    [InlineData("pesaje-20261001_001.log", true)]
    [InlineData("../pesaje-20261001.log", false)]
    [InlineData("pesaje-2026.log", false)]
    [InlineData("otro.log", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Test03_EsNombreValido(string? nombre, bool esperado)
    {
        Assert.Equal(esperado, ParserLog.EsNombreValido(nombre!));
    }

    [Fact]
    public void Test04_FechaDeNombre()
    {
        var fecha1 = ParserLog.FechaDeNombre("pesaje-20261001.log");
        Assert.Equal(new DateOnly(2026, 10, 1), fecha1);

        var fecha2 = ParserLog.FechaDeNombre("pesaje-20261001_002.log");
        Assert.Equal(new DateOnly(2026, 10, 1), fecha2);

        var fechaInvalida = ParserLog.FechaDeNombre("invalido.log");
        Assert.Null(fechaInvalida);
    }

    [Theory]
    [InlineData("VRB", NivelLog.Detalle)]
    [InlineData("DBG", NivelLog.Depuracion)]
    [InlineData("INF", NivelLog.Informacion)]
    [InlineData("WRN", NivelLog.Advertencia)]
    [InlineData("ERR", NivelLog.Error)]
    [InlineData("FTL", NivelLog.Fatal)]
    [InlineData("XYZ", NivelLog.Desconocido)]
    public void Test05_Nivel(string codigo, NivelLog nivelEsperado)
    {
        Assert.Equal(nivelEsperado, ParserLog.Nivel(codigo));
    }
}

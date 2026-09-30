using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ValidadorConfiguracionImpresionTests
{
    [Fact]
    public void PorDefecto_NoTieneErrores()
    {
        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(ReglasImpresion.PorDefecto);

        Assert.Empty(errores);
        Assert.Null(normalizada.NombreImpresora);
        Assert.Null(normalizada.NombrePapel);
        Assert.Equal(FormatoBoleta.MediaCarta, normalizada.Formato);
        Assert.Equal(1, normalizada.Copias);
        Assert.False(normalizada.ImprimirAlGrabar);
        Assert.Equal(0m, normalizada.DesplazamientoXMm);
        Assert.Equal(0m, normalizada.DesplazamientoYMm);
    }

    [Fact]
    public void Validar_FormatoIndefinido_RetornaError()
    {
        var config = new ConfiguracionImpresion(null, (FormatoBoleta)99, null, 1, false, 0m, 0m);

        var (_, errores) = ValidadorConfiguracionImpresion.Validar(config);

        var err = Assert.Single(errores);
        Assert.Equal(CamposImpresion.Formato, err.Campo);
        Assert.Equal("Seleccione el formato de boleta.", err.Mensaje);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Validar_CopiasFueraDeRango_RetornaError(int copias)
    {
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, copias, false, 0m, 0m);

        var (_, errores) = ValidadorConfiguracionImpresion.Validar(config);

        var err = Assert.Single(errores);
        Assert.Equal(CamposImpresion.Copias, err.Campo);
        Assert.Equal("Las copias deben estar entre 1 y 3.", err.Mensaje);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Validar_CopiasValidas_NoRetornaError(int copias)
    {
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, copias, false, 0m, 0m);

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(config);

        Assert.Empty(errores);
        Assert.Equal(copias, normalizada.Copias);
    }

    [Theory]
    [InlineData(20.1)]
    [InlineData(-20.1)]
    [InlineData(25.0)]
    public void Validar_DesplazamientoXFueraDeRango_RetornaError(double x)
    {
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 1, false, (decimal)x, 0m);

        var (_, errores) = ValidadorConfiguracionImpresion.Validar(config);

        var err = Assert.Single(errores);
        Assert.Equal(CamposImpresion.DesplazamientoX, err.Campo);
        Assert.Equal("El desplazamiento horizontal debe estar entre -20 y 20 mm.", err.Mensaje);
    }

    [Theory]
    [InlineData(20.1)]
    [InlineData(-20.1)]
    [InlineData(-30.0)]
    public void Validar_DesplazamientoYFueraDeRango_RetornaError(double y)
    {
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 1, false, 0m, (decimal)y);

        var (_, errores) = ValidadorConfiguracionImpresion.Validar(config);

        var err = Assert.Single(errores);
        Assert.Equal(CamposImpresion.DesplazamientoY, err.Campo);
        Assert.Equal("El desplazamiento vertical debe estar entre -20 y 20 mm.", err.Mensaje);
    }

    [Theory]
    [InlineData(20.0)]
    [InlineData(-20.0)]
    [InlineData(0.0)]
    [InlineData(15.5)]
    [InlineData(-10.5)]
    public void Validar_DesplazamientosLimitesExactos_Validos(double valor)
    {
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 1, false, (decimal)valor, (decimal)valor);

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(config);

        Assert.Empty(errores);
        Assert.Equal((decimal)valor, normalizada.DesplazamientoXMm);
        Assert.Equal((decimal)valor, normalizada.DesplazamientoYMm);
    }

    [Fact]
    public void Validar_ImpresoraDemasiadoLarga_RetornaError()
    {
        var largo = new string('A', 201);
        var config = new ConfiguracionImpresion(largo, FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);

        var (_, errores) = ValidadorConfiguracionImpresion.Validar(config);

        var err = Assert.Single(errores);
        Assert.Equal(CamposImpresion.Impresora, err.Campo);
        Assert.Equal("El nombre de la impresora es demasiado largo.", err.Mensaje);
    }

    [Fact]
    public void Validar_PapelDemasiadoLargo_RetornaError()
    {
        var largo = new string('P', 101);
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, largo, 1, false, 0m, 0m);

        var (_, errores) = ValidadorConfiguracionImpresion.Validar(config);

        var err = Assert.Single(errores);
        Assert.Equal(CamposImpresion.Papel, err.Campo);
        Assert.Equal("El nombre del papel es demasiado largo.", err.Mensaje);
    }

    [Fact]
    public void Validar_EspaciosEnBlanco_SeNormalizanANull()
    {
        var config = new ConfiguracionImpresion("   ", FormatoBoleta.MediaCarta, "\t \n", 1, false, 0m, 0m);

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(config);

        Assert.Empty(errores);
        Assert.Null(normalizada.NombreImpresora);
        Assert.Null(normalizada.NombrePapel);
    }

    [Fact]
    public void Validar_EspaciosAlrededor_SeRecortan()
    {
        var config = new ConfiguracionImpresion("  HP LaserJet  ", FormatoBoleta.MediaCarta, "  A4  ", 1, false, 0m, 0m);

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(config);

        Assert.Empty(errores);
        Assert.Equal("HP LaserJet", normalizada.NombreImpresora);
        Assert.Equal("A4", normalizada.NombrePapel);
    }

    [Fact]
    public void Validar_RedondeoDesplazamientos_AwayFromZero()
    {
        var config = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 1, false, 2.55m, -2.55m);

        var (normalizada, errores) = ValidadorConfiguracionImpresion.Validar(config);

        Assert.Empty(errores);
        Assert.Equal(2.6m, normalizada.DesplazamientoXMm);
        Assert.Equal(-2.6m, normalizada.DesplazamientoYMm);
    }
}

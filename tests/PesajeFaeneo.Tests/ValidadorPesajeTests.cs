using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Reglas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ValidadorPesajeTests
{
    private static RegistrarPesaje CrearDtoValido() => new(
        GanaderoId: 1,
        ClienteId: 2,
        Direccion: "  Calle 1   #23  ",
        Secuencia: 5,
        ColorRaza: "  rojo   overo ",
        Sexo: 'm',
        TipoDesposte: TipoDesposte.ResCompleta,
        PesoIzquierdo: 120.04m,
        PesoDerecho: 118.06m,
        Observacion: "  sin novedad  ",
        CapturadoConSimulador: false);

    [Fact]
    public void Validar_DtoValido_NormalizaTextosYRedondeaPesosSinErrores()
    {
        var dto = CrearDtoValido();
        var (normalizado, errores) = ValidadorPesaje.Validar(dto);

        Assert.Empty(errores);
        Assert.Equal("CALLE 1 #23", normalizado.Direccion);
        Assert.Equal("ROJO OVERO", normalizado.ColorRaza);
        Assert.Equal("SIN NOVEDAD", normalizado.Observacion);
        Assert.Equal('M', normalizado.Sexo);
        Assert.Equal(120.0m, normalizado.PesoIzquierdo);
        Assert.Equal(118.1m, normalizado.PesoDerecho);
    }

    [Fact]
    public void Validar_TextosNulos_NormalizaACadenaVacia()
    {
        var dto = CrearDtoValido() with
        {
            Direccion = null,
            ColorRaza = null,
            Observacion = null
        };

        var (normalizado, errores) = ValidadorPesaje.Validar(dto);

        Assert.Empty(errores);
        Assert.Equal(string.Empty, normalizado.Direccion);
        Assert.Equal(string.Empty, normalizado.ColorRaza);
        Assert.Equal(string.Empty, normalizado.Observacion);
    }

    [Theory]
    [InlineData(0, "Seleccione el ganadero.", CamposPesaje.Ganadero)]
    [InlineData(-1, "Seleccione el ganadero.", CamposPesaje.Ganadero)]
    public void Validar_GanaderoInvalido_DevuelveError(int ganaderoId, string mensaje, string campo)
    {
        var dto = CrearDtoValido() with { GanaderoId = ganaderoId };
        var (_, errores) = ValidadorPesaje.Validar(dto);

        var err = Assert.Single(errores, e => e.Campo == campo);
        Assert.Equal(mensaje, err.Mensaje);
    }

    [Theory]
    [InlineData(0, "Seleccione el cliente.", CamposPesaje.Cliente)]
    [InlineData(-5, "Seleccione el cliente.", CamposPesaje.Cliente)]
    public void Validar_ClienteInvalido_DevuelveError(int clienteId, string mensaje, string campo)
    {
        var dto = CrearDtoValido() with { ClienteId = clienteId };
        var (_, errores) = ValidadorPesaje.Validar(dto);

        var err = Assert.Single(errores, e => e.Campo == campo);
        Assert.Equal(mensaje, err.Mensaje);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    public void Validar_SecuenciaInvalida_DevuelveError(int secuencia)
    {
        var dto = CrearDtoValido() with { Secuencia = secuencia };
        var (_, errores) = ValidadorPesaje.Validar(dto);

        var err = Assert.Single(errores, e => e.Campo == CamposPesaje.Secuencia);
        Assert.Equal("La secuencia debe estar entre 1 y 9999.", err.Mensaje);
    }

    [Theory]
    [InlineData('X')]
    [InlineData(' ')]
    [InlineData('\0')]
    public void Validar_SexoInvalido_DevuelveError(char sexo)
    {
        var dto = CrearDtoValido() with { Sexo = sexo };
        var (_, errores) = ValidadorPesaje.Validar(dto);

        var err = Assert.Single(errores, e => e.Campo == CamposPesaje.Sexo);
        Assert.Equal("Seleccione el sexo (M o H).", err.Mensaje);
    }

    [Fact]
    public void Validar_LongitudesMaximasSuperadas_DevuelvenErroresCorrespondientes()
    {
        var dto = CrearDtoValido() with
        {
            Direccion = new string('A', 151),
            ColorRaza = new string('B', 31),
            Observacion = new string('C', 251)
        };

        var (_, errores) = ValidadorPesaje.Validar(dto);

        var errDir = Assert.Single(errores, e => e.Campo == CamposPesaje.Direccion);
        Assert.Equal("La dirección no puede superar 150 caracteres.", errDir.Mensaje);

        var errColor = Assert.Single(errores, e => e.Campo == CamposPesaje.ColorRaza);
        Assert.Equal("Color/raza no puede superar 30 caracteres.", errColor.Mensaje);

        var errObs = Assert.Single(errores, e => e.Campo == CamposPesaje.Observacion);
        Assert.Equal("La observación no puede superar 250 caracteres.", errObs.Mensaje);
    }
}

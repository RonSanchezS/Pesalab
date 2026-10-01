using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Domain.Reglas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ValidadorGestionPesajesTests
{
    private static EditarPesaje CrearDtoValido(
        int ganaderoId = 1,
        int clienteId = 1,
        string? direccion = "CALLE 123",
        int secuencia = 1,
        string? colorRaza = "BLANCO",
        char sexo = 'M',
        string? observacion = "NINGUNA",
        string? motivo = null)
    {
        return new EditarPesaje(
            PesajeId: 10,
            VersionEsperada: 0,
            GanaderoId: ganaderoId,
            ClienteId: clienteId,
            Direccion: direccion,
            Secuencia: secuencia,
            ColorRaza: colorRaza,
            Sexo: sexo,
            Observacion: observacion,
            Motivo: motivo);
    }

    [Fact]
    public void ValidarEdicion_ValoresCorrectos_SinErrores()
    {
        var dto = CrearDtoValido();
        var (normalizado, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        Assert.Empty(errores);
        Assert.Equal("CALLE 123", normalizado.Direccion);
        Assert.Equal("BLANCO", normalizado.ColorRaza);
        Assert.Equal('M', normalizado.Sexo);
    }

    [Fact]
    public void ValidarEdicion_GanaderoInvalido_RetornaErrorExacto()
    {
        var dto = CrearDtoValido(ganaderoId: 0);
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Ganadero, err.Campo);
        Assert.Equal("Seleccione el ganadero.", err.Mensaje);
    }

    [Fact]
    public void ValidarEdicion_ClienteInvalido_RetornaErrorExacto()
    {
        var dto = CrearDtoValido(clienteId: -1);
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Cliente, err.Campo);
        Assert.Equal("Seleccione el cliente.", err.Mensaje);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(-5)]
    public void ValidarEdicion_SecuenciaFueraDeRango_RetornaErrorExacto(int secuencia)
    {
        var dto = CrearDtoValido(secuencia: secuencia);
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Secuencia, err.Campo);
        Assert.Equal("La secuencia debe estar entre 1 y 9999.", err.Mensaje);
    }

    [Theory]
    [InlineData('X')]
    [InlineData(' ')]
    [InlineData('1')]
    public void ValidarEdicion_SexoInvalido_RetornaErrorExacto(char sexo)
    {
        var dto = CrearDtoValido(sexo: sexo);
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Sexo, err.Campo);
        Assert.Equal("Seleccione el sexo (M o H).", err.Mensaje);
    }

    [Fact]
    public void ValidarEdicion_DireccionExcede150_RetornaErrorExacto()
    {
        var dto = CrearDtoValido(direccion: new string('A', 151));
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Direccion, err.Campo);
        Assert.Equal("La dirección no puede superar 150 caracteres.", err.Mensaje);
    }

    [Fact]
    public void ValidarEdicion_ColorRazaExcede30_RetornaErrorExacto()
    {
        var dto = CrearDtoValido(colorRaza: new string('C', 31));
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.ColorRaza, err.Campo);
        Assert.Equal("Color/raza no puede superar 30 caracteres.", err.Mensaje);
    }

    [Fact]
    public void ValidarEdicion_ObservacionExcede250_RetornaErrorExacto()
    {
        var dto = CrearDtoValido(observacion: new string('O', 251));
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposPesaje.Observacion, err.Campo);
        Assert.Equal("La observación no puede superar 250 caracteres.", err.Mensaje);
    }

    [Fact]
    public void ValidarEdicion_MotivoExcede100_RetornaErrorExacto()
    {
        var dto = CrearDtoValido(motivo: new string('M', 101));
        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposGestionPesajes.Motivo, err.Campo);
        Assert.Equal("El motivo no puede superar 100 caracteres.", err.Mensaje);
    }

    [Fact]
    public void ValidarEdicion_NormalizaMayusculasEspaciosYSexo()
    {
        var dto = CrearDtoValido(
            direccion: "   calle   principal   123   ",
            colorRaza: "  bayo   overo  ",
            sexo: 'h',
            observacion: "   cambio   de   observacion  ",
            motivo: "  correccion   de   datos  ");

        var (normalizado, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        Assert.Empty(errores);
        Assert.Equal("CALLE PRINCIPAL 123", normalizado.Direccion);
        Assert.Equal("BAYO OVERO", normalizado.ColorRaza);
        Assert.Equal('H', normalizado.Sexo);
        Assert.Equal("CAMBIO DE OBSERVACION", normalizado.Observacion);
        Assert.Equal("CORRECCION DE DATOS", normalizado.Motivo);
    }

    [Fact]
    public void ValidarEdicion_MultiplesFallas_RetornaTodas()
    {
        var dto = new EditarPesaje(
            PesajeId: 1,
            VersionEsperada: 0,
            GanaderoId: 0,
            ClienteId: 0,
            Direccion: new string('D', 155),
            Secuencia: 0,
            ColorRaza: new string('C', 35),
            Sexo: 'Z',
            Observacion: new string('O', 260),
            Motivo: new string('M', 105));

        var (_, errores) = ValidadorGestionPesajes.ValidarEdicion(dto);

        Assert.Equal(8, errores.Count);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.Ganadero);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.Cliente);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.Secuencia);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.Sexo);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.Direccion);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.ColorRaza);
        Assert.Contains(errores, e => e.Campo == CamposPesaje.Observacion);
        Assert.Contains(errores, e => e.Campo == CamposGestionPesajes.Motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    [InlineData(" ab ")]
    public void ValidarAnulacion_MotivoVacioOMenorDe5_RetornaErrorExacto(string? motivo)
    {
        var dto = new AnularPesaje(1, 0, motivo);
        var (_, errores) = ValidadorGestionPesajes.ValidarAnulacion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposGestionPesajes.Motivo, err.Campo);
        Assert.Equal("Ingrese el motivo de la anulación (mínimo 5 caracteres).", err.Mensaje);
    }

    [Fact]
    public void ValidarAnulacion_Motivo5Caracteres_EsValido()
    {
        var dto = new AnularPesaje(1, 0, "ERROR");
        var (normalizado, errores) = ValidadorGestionPesajes.ValidarAnulacion(dto);

        Assert.Empty(errores);
        Assert.Equal("ERROR", normalizado.Motivo);
    }

    [Fact]
    public void ValidarAnulacion_Motivo100Caracteres_EsValido()
    {
        var motivo100 = new string('A', 100);
        var dto = new AnularPesaje(1, 0, motivo100);
        var (normalizado, errores) = ValidadorGestionPesajes.ValidarAnulacion(dto);

        Assert.Empty(errores);
        Assert.Equal(motivo100, normalizado.Motivo);
    }

    [Fact]
    public void ValidarAnulacion_MotivoMayorDe100_RetornaErrorExacto()
    {
        var motivo101 = new string('A', 101);
        var dto = new AnularPesaje(1, 0, motivo101);
        var (_, errores) = ValidadorGestionPesajes.ValidarAnulacion(dto);

        var err = Assert.Single(errores);
        Assert.Equal(CamposGestionPesajes.Motivo, err.Campo);
        Assert.Equal("El motivo no puede superar 100 caracteres.", err.Mensaje);
    }

    [Fact]
    public void ValidarAnulacion_NormalizaMayusculasYEspacios()
    {
        var dto = new AnularPesaje(1, 0, "   error   en   peso   izquierdo   ");
        var (normalizado, errores) = ValidadorGestionPesajes.ValidarAnulacion(dto);

        Assert.Empty(errores);
        Assert.Equal("ERROR EN PESO IZQUIERDO", normalizado.Motivo);
    }
}

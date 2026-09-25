using PesajeFaeneo.Application.Catalogos;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ValidadorCatalogosTests
{
    [Theory]
    [InlineData("ah", "AH")]
    [InlineData(" a h ", "AH")]
    [InlineData("ñ.1", "Ñ.1")]
    [InlineData(null, "")]
    public void NormalizarMarca_EliminaEspaciosYConvierteAMayusculas(string? entrada, string esperado)
    {
        var resultado = ValidadorCatalogos.NormalizarMarca(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("  juan   pérez ", "JUAN PÉREZ")]
    [InlineData("cliente  uno", "CLIENTE UNO")]
    [InlineData(null, "")]
    public void NormalizarMayusculas_ColapsaEspaciosYConvierteAMayusculas(string? entrada, string esperado)
    {
        var resultado = ValidadorCatalogos.NormalizarMayusculas(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("AH", "JUAN PÉREZ", true)]
    [InlineData("ÑA", "PEÑA", true)]
    [InlineData("A-1", "ESTANCIA SANTA ROSA", true)]
    [InlineData("ABCDEF", "JUAN PÉREZ", false)] // Marca 6 caracteres
    [InlineData("", "JUAN PÉREZ", false)]       // Marca vacía
    [InlineData("A*", "JUAN PÉREZ", false)]      // Carácter inválido *
    [InlineData("AH", "123", false)]             // Nombre sin letras
    [InlineData("AH", "JU", false)]              // Nombre < 3 caracteres
    public void ValidarGanadero_CasosValidosEInvalidos(string marca, string nombre, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarGanadero(marca, nombre);
        if (esValido)
        {
            Assert.Empty(errores);
        }
        else
        {
            Assert.NotEmpty(errores);
        }
    }

    [Fact]
    public void ValidarGanadero_NombreDe101Caracteres_LanzaError()
    {
        var nombreLargo = new string('A', 101);
        var errores = ValidadorCatalogos.ValidarGanadero("AH", nombreLargo);
        Assert.Contains(errores, e => e.Campo == CamposCatalogo.Nombre);
    }

    [Theory]
    [InlineData(110, "CLIENTE EJEMPLO", "DIRECCION CORTA", true)]
    [InlineData(1, "CLIENTE 1", "", true)]
    [InlineData(999_999, "CLIENTE MAX", "", true)]
    [InlineData(0, "CLIENTE CERO", "", false)]
    [InlineData(1_000_000, "CLIENTE MILLON", "", false)]
    public void ValidarCliente_CasosValidosEInvalidos(int codigo, string nombre, string direccion, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarCliente(codigo, nombre, direccion);
        if (esValido)
        {
            Assert.Empty(errores);
        }
        else
        {
            Assert.NotEmpty(errores);
        }
    }

    [Fact]
    public void ValidarCliente_DireccionMayorA150Caracteres_LanzaError()
    {
        var direccionLarga = new string('D', 151);
        var errores = ValidadorCatalogos.ValidarCliente(110, "CLIENTE VALIDO", direccionLarga);
        Assert.Contains(errores, e => e.Campo == CamposCatalogo.Direccion);
    }

    [Theory]
    [InlineData(1, "FLETERO 1", "7123-4567", true)]
    [InlineData(4, "FLETERO 4", "+591 71234567", true)]
    [InlineData(2, "FLETERO 2", "(591) 3 345678", true)]
    [InlineData(0, "FLETERO INVALIDO", "7123-4567", false)] // Orden 0
    [InlineData(5, "FLETERO INVALIDO", "7123-4567", false)] // Orden 5
    [InlineData(1, "FLETERO 1", "12345", false)]           // Celular < 6 chars
    [InlineData(1, "FLETERO 1", "abcdefg", false)]         // Sin dígitos
    [InlineData(1, "FLETERO 1", "(+)-- --", false)]        // Sin dígitos suficientes
    public void ValidarFletero_CasosValidosEInvalidos(int orden, string nombre, string celular, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarFletero(orden, nombre, celular);
        if (esValido)
        {
            Assert.Empty(errores);
        }
        else
        {
            Assert.NotEmpty(errores);
        }
    }
}

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
    public void NormalizarCodigoGanadero_EliminaEspaciosYConvierteAMayusculas(string? entrada, string esperado)
    {
        var resultado = ValidadorCatalogos.NormalizarCodigoGanadero(entrada);
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
    [InlineData("JUAN PÉREZ", true)]
    [InlineData("PEÑA", true)]
    [InlineData("ESTANCIA SANTA ROSA", true)]
    [InlineData("123", false)]  // Nombre sin letras
    [InlineData("JU", false)]   // Nombre < 3 caracteres
    [InlineData("", false)]
    public void ValidarGanadero_SoloValidaElNombre(string nombre, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarGanadero(nombre);
        if (esValido)
        {
            Assert.Empty(errores);
        }
        else
        {
            Assert.Contains(errores, e => e.Campo == CamposCatalogo.Nombre);
        }
    }

    [Fact]
    public void ValidarGanadero_NombreDe101Caracteres_LanzaError()
    {
        var nombreLargo = new string('A', 101);
        var errores = ValidadorCatalogos.ValidarGanadero(nombreLargo);
        Assert.Contains(errores, e => e.Campo == CamposCatalogo.Nombre);
    }

    // La validación del código (formato legado alfanumérico) solo se mantiene para la importación.
    [Theory]
    [InlineData("AH", "JUAN PÉREZ", true)]
    [InlineData("ÑA", "PEÑA", true)]
    [InlineData("A-1", "ESTANCIA SANTA ROSA", true)]
    [InlineData("12", "ESTANCIA SANTA ROSA", true)]
    [InlineData("ABCDEF", "JUAN PÉREZ", false)] // Código de 6 caracteres
    [InlineData("", "JUAN PÉREZ", false)]       // Código vacío
    [InlineData("A*", "JUAN PÉREZ", false)]      // Carácter inválido *
    [InlineData("AH", "123", false)]             // Nombre sin letras
    [InlineData("AH", "JU", false)]              // Nombre < 3 caracteres
    public void ValidarGanaderoImportado_CasosValidosEInvalidos(string codigo, string nombre, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarGanaderoImportado(codigo, nombre);
        if (esValido)
        {
            Assert.Empty(errores);
        }
        else
        {
            Assert.NotEmpty(errores);
        }
    }

    [Theory]
    [InlineData("CLIENTE EJEMPLO", "DIRECCION CORTA", true)]
    [InlineData("CLIENTE 1", "", true)]
    [InlineData("12", "", false)]
    [InlineData("", "", false)]
    public void ValidarCliente_CasosValidosEInvalidos(string nombre, string direccion, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarCliente(nombre, direccion);
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
        var errores = ValidadorCatalogos.ValidarCliente("CLIENTE VALIDO", direccionLarga);
        Assert.Contains(errores, e => e.Campo == CamposCatalogo.Direccion);
    }

    [Theory]
    [InlineData(110, "CLIENTE EJEMPLO", "DIRECCION CORTA", true)]
    [InlineData(1, "CLIENTE 1", "", true)]
    [InlineData(999_999, "CLIENTE MAX", "", true)]
    [InlineData(0, "CLIENTE CERO", "", false)]
    [InlineData(1_000_000, "CLIENTE MILLON", "", false)]
    public void ValidarClienteImportado_ValidaElCodigoDelArchivo(int codigo, string nombre, string direccion, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarClienteImportado(codigo, nombre, direccion);
        if (esValido)
        {
            Assert.Empty(errores);
        }
        else
        {
            Assert.NotEmpty(errores);
        }
    }

    [Theory]
    [InlineData("FLETERO 1", "7123-4567", true)]
    [InlineData("FLETERO 4", "+591 71234567", true)]
    [InlineData("FLETERO 12", "(591) 3 345678", true)]
    [InlineData("FLETERO 1", "12345", false)]           // Celular < 6 chars
    [InlineData("FLETERO 1", "abcdefg", false)]         // Sin dígitos
    [InlineData("FLETERO 1", "(+)-- --", false)]        // Sin dígitos suficientes
    [InlineData("1", "7123-4567", false)]               // Nombre sin letras
    public void ValidarFletero_CasosValidosEInvalidos(string nombre, string celular, bool esValido)
    {
        var errores = ValidadorCatalogos.ValidarFletero(nombre, celular);
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

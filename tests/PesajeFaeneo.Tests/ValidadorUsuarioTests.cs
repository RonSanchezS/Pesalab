using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ValidadorUsuarioTests
{
    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("  Admin  ", "admin")]
    [InlineData("OPERADOR.1", "operador.1")]
    [InlineData(null, "")]
    public void NormalizarNombreUsuario_AplicaTrimYMinusculas(string? entrada, string esperado)
    {
        Assert.Equal(esperado, ValidadorUsuario.NormalizarNombreUsuario(entrada));
    }

    [Theory]
    [InlineData("Juan  Perez   Gomez", "Juan Perez Gomez")]
    [InlineData("  Maria   Lopez  ", "Maria Lopez")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizarNombreCompleto_ColapsaEspacios(string? entrada, string esperado)
    {
        Assert.Equal(esperado, ValidadorUsuario.NormalizarNombreCompleto(entrada));
    }

    [Fact]
    public void ValidarAlta_DatosValidos_SinErrores()
    {
        var errores = ValidadorUsuario.ValidarAlta("operador1", "Operador de Planta", Rol.Operador, "clave123", "clave123");
        Assert.Empty(errores);
    }

    [Theory]
    [InlineData("ab")] // muy corto
    [InlineData("un_nombre_de_usuario_demasiado_largo_que_supera_el_limite")] // muy largo
    [InlineData("usuario@email")] // caracter invalido @
    [InlineData("usuario espacio")] // espacio invalido
    public void ValidarAlta_NombreUsuarioInvalido_RetornaError(string usuarioInvalido)
    {
        var errores = ValidadorUsuario.ValidarAlta(usuarioInvalido, "Operador de Planta", Rol.Operador, "clave123", "clave123");
        Assert.Contains(errores, e => e.Campo == CamposUsuario.NombreUsuario && e.Mensaje == "Use de 3 a 30 caracteres: letras minúsculas, números, punto, guion o guion bajo.");
    }

    [Fact]
    public void ValidarAlta_NombreCompletoFueraDeRango_RetornaError()
    {
        var errores = ValidadorUsuario.ValidarAlta("operador1", "ab", Rol.Operador, "clave123", "clave123");
        Assert.Contains(errores, e => e.Campo == CamposUsuario.NombreCompleto && e.Mensaje == "Ingrese el nombre completo (3 a 100 caracteres).");
    }

    [Fact]
    public void ValidarAlta_RolInvalido_RetornaError()
    {
        var errores = ValidadorUsuario.ValidarAlta("operador1", "Operador de Planta", (Rol)99, "clave123", "clave123");
        Assert.Contains(errores, e => e.Campo == CamposUsuario.Rol && e.Mensaje == "Seleccione un rol válido.");
    }

    [Theory]
    [InlineData("12345")] // 5 caracteres < 6
    public void ValidarAlta_PasswordCorta_RetornaError(string passCorta)
    {
        var errores = ValidadorUsuario.ValidarAlta("operador1", "Operador de Planta", Rol.Operador, passCorta, passCorta);
        Assert.Contains(errores, e => e.Campo == CamposUsuario.Password && e.Mensaje == "La contraseña debe tener entre 6 y 100 caracteres.");
    }

    [Fact]
    public void ValidarAlta_PasswordIgualAUsuario_RetornaError()
    {
        var errores = ValidadorUsuario.ValidarAlta("operador1", "Operador de Planta", Rol.Operador, "OPERADOR1", "OPERADOR1");
        Assert.Contains(errores, e => e.Campo == CamposUsuario.Password && e.Mensaje == "La contraseña no puede ser igual al nombre de usuario.");
    }

    [Fact]
    public void ValidarAlta_ConfirmacionDistinta_RetornaError()
    {
        var errores = ValidadorUsuario.ValidarAlta("operador1", "Operador de Planta", Rol.Operador, "clave123", "clave456");
        Assert.Contains(errores, e => e.Campo == CamposUsuario.Confirmacion && e.Mensaje == "La confirmación no coincide con la contraseña.");
    }

    [Fact]
    public void ValidarPasswordNueva_NuevaIgualAActual_RetornaError()
    {
        var errores = ValidadorUsuario.ValidarPasswordNueva("operador1", "clave123", "clave123", "clave123");
        Assert.Contains(errores, e => e.Campo == CamposUsuario.Password && e.Mensaje == "La nueva contraseña debe ser distinta de la actual.");
    }
}

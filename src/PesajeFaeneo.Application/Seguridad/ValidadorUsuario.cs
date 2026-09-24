using System.Text.RegularExpressions;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Seguridad;

public static class CamposUsuario
{
    public const string NombreUsuario = "NombreUsuario";
    public const string NombreCompleto = "NombreCompleto";
    public const string Rol = "Rol";
    public const string Password = "Password";
    public const string Confirmacion = "Confirmacion";
    public const string PasswordActual = "PasswordActual";
}

public static class ValidadorUsuario
{
    public static string NormalizarNombreUsuario(string? valor)
    {
        return valor?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public static string NormalizarNombreCompleto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        return Regex.Replace(valor.Trim(), @"\s+", " ");
    }

    public static IReadOnlyList<ErrorValidacion> ValidarAlta(
        string nombreUsuario,
        string nombreCompleto,
        Rol rol,
        string password,
        string confirmacion)
    {
        var errores = new List<ErrorValidacion>();
        var normalizadoUsuario = NormalizarNombreUsuario(nombreUsuario);

        if (!Regex.IsMatch(normalizadoUsuario, PoliticaSeguridad.PatronNombreUsuario))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.NombreUsuario, "Use de 3 a 30 caracteres: letras minúsculas, números, punto, guion o guion bajo."));
        }

        var normalizadoCompleto = NormalizarNombreCompleto(nombreCompleto);
        if (normalizadoCompleto.Length < 3 || normalizadoCompleto.Length > 100)
        {
            errores.Add(new ErrorValidacion(CamposUsuario.NombreCompleto, "Ingrese el nombre completo (3 a 100 caracteres)."));
        }

        if (!Enum.IsDefined(typeof(Rol), rol))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Rol, "Seleccione un rol válido."));
        }

        if (string.IsNullOrEmpty(password) || password.Length < PoliticaSeguridad.LongitudMinimaPassword || password.Length > PoliticaSeguridad.LongitudMaximaPassword)
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Password, "La contraseña debe tener entre 6 y 100 caracteres."));
        }
        else if (!string.IsNullOrEmpty(normalizadoUsuario) && string.Equals(password, normalizadoUsuario, StringComparison.OrdinalIgnoreCase))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Password, "La contraseña no puede ser igual al nombre de usuario."));
        }

        if (!string.Equals(password, confirmacion, StringComparison.Ordinal))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Confirmacion, "La confirmación no coincide con la contraseña."));
        }

        return errores.AsReadOnly();
    }

    public static IReadOnlyList<ErrorValidacion> ValidarEdicion(string nombreCompleto, Rol rol)
    {
        var errores = new List<ErrorValidacion>();
        var normalizadoCompleto = NormalizarNombreCompleto(nombreCompleto);

        if (normalizadoCompleto.Length < 3 || normalizadoCompleto.Length > 100)
        {
            errores.Add(new ErrorValidacion(CamposUsuario.NombreCompleto, "Ingrese el nombre completo (3 a 100 caracteres)."));
        }

        if (!Enum.IsDefined(typeof(Rol), rol))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Rol, "Seleccione un rol válido."));
        }

        return errores.AsReadOnly();
    }

    public static IReadOnlyList<ErrorValidacion> ValidarPasswordNueva(
        string nombreUsuario,
        string nueva,
        string confirmacion,
        string? actual)
    {
        var errores = new List<ErrorValidacion>();
        var normalizadoUsuario = NormalizarNombreUsuario(nombreUsuario);

        if (string.IsNullOrEmpty(nueva) || nueva.Length < PoliticaSeguridad.LongitudMinimaPassword || nueva.Length > PoliticaSeguridad.LongitudMaximaPassword)
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Password, "La contraseña debe tener entre 6 y 100 caracteres."));
        }
        else if (!string.IsNullOrEmpty(normalizadoUsuario) && string.Equals(nueva, normalizadoUsuario, StringComparison.OrdinalIgnoreCase))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Password, "La contraseña no puede ser igual al nombre de usuario."));
        }
        else if (actual is not null && string.Equals(nueva, actual, StringComparison.Ordinal))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Password, "La nueva contraseña debe ser distinta de la actual."));
        }

        if (!string.Equals(nueva, confirmacion, StringComparison.Ordinal))
        {
            errores.Add(new ErrorValidacion(CamposUsuario.Confirmacion, "La confirmación no coincide con la contraseña."));
        }

        return errores.AsReadOnly();
    }
}

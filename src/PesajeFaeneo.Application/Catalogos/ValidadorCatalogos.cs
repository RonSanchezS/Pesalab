using System.Text.RegularExpressions;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Catalogos;

public static class ValidadorCatalogos
{
    private static readonly Regex EspaciosRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex CodigoGanaderoRegex = new(ReglasCatalogos.PatronCodigoGanadero, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex LetraRegex = new(@"\p{L}", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex CelularRegex = new(ReglasCatalogos.PatronCelular, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    public static string NormalizarTexto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return string.Empty;

        return EspaciosRegex.Replace(valor.Trim(), " ");
    }

    public static string NormalizarMayusculas(string? valor)
    {
        return NormalizarTexto(valor).ToUpperInvariant();
    }

    /// <summary>Código de ganadero en mayúsculas y sin espacios (solo lo usa la importación del sistema legado).</summary>
    public static string NormalizarCodigoGanadero(string? valor)
    {
        var mayus = NormalizarMayusculas(valor);
        return mayus.Replace(" ", string.Empty);
    }

    /// <summary>Alta/edición manual de un ganadero: solo el nombre (el código lo asigna el sistema, F10 O6).</summary>
    public static IReadOnlyList<ErrorValidacion> ValidarGanadero(string nombreNormalizado)
    {
        var errores = new List<ErrorValidacion>();
        ValidarNombre(nombreNormalizado, errores);
        return errores.AsReadOnly();
    }

    /// <summary>Importación del sistema legado: el código viene del archivo (alfanumérico de 1 a 5 caracteres).</summary>
    public static IReadOnlyList<ErrorValidacion> ValidarGanaderoImportado(string codigoNormalizado, string nombreNormalizado)
    {
        var errores = new List<ErrorValidacion>();

        if (string.IsNullOrEmpty(codigoNormalizado) ||
            codigoNormalizado.Length > ReglasCatalogos.LongitudMaximaCodigoGanadero ||
            !CodigoGanaderoRegex.IsMatch(codigoNormalizado))
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Codigo,
                "Ingrese el código: 1 a 5 caracteres (letras, números o & . / -), sin espacios."));
        }

        ValidarNombre(nombreNormalizado, errores);
        return errores.AsReadOnly();
    }

    /// <summary>Alta/edición manual de un cliente: nombre y dirección (el código lo asigna el sistema).</summary>
    public static IReadOnlyList<ErrorValidacion> ValidarCliente(string nombreNormalizado, string direccionNormalizada)
    {
        var errores = new List<ErrorValidacion>();
        ValidarNombre(nombreNormalizado, errores);
        ValidarDireccion(direccionNormalizada, errores);
        return errores.AsReadOnly();
    }

    /// <summary>Importación del sistema legado: el código numérico viene del archivo.</summary>
    public static IReadOnlyList<ErrorValidacion> ValidarClienteImportado(int codigo, string nombreNormalizado, string direccionNormalizada)
    {
        var errores = new List<ErrorValidacion>();

        if (codigo < ReglasCatalogos.CodigoClienteMinimo || codigo > ReglasCatalogos.CodigoClienteMaximo)
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Codigo,
                "El código debe ser un número entre 1 y 999999."));
        }

        ValidarNombre(nombreNormalizado, errores);
        ValidarDireccion(direccionNormalizada, errores);
        return errores.AsReadOnly();
    }

    /// <summary>Alta/edición de un fletero: nombre y celular (el código lo asigna el sistema; sin límite de fleteros).</summary>
    public static IReadOnlyList<ErrorValidacion> ValidarFletero(string nombreNormalizado, string celularNormalizado)
    {
        var errores = new List<ErrorValidacion>();

        ValidarNombre(nombreNormalizado, errores);

        var digitos = celularNormalizado?.Count(char.IsDigit) ?? 0;
        if (string.IsNullOrEmpty(celularNormalizado) ||
            celularNormalizado.Length < ReglasCatalogos.LongitudMinimaCelular ||
            celularNormalizado.Length > ReglasCatalogos.LongitudMaximaCelular ||
            !CelularRegex.IsMatch(celularNormalizado) ||
            digitos < ReglasCatalogos.DigitosMinimosCelular)
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Celular,
                "Ingrese un celular válido (6 a 20 caracteres: números, espacios, +, -, paréntesis)."));
        }

        return errores.AsReadOnly();
    }

    private static void ValidarNombre(string nombreNormalizado, List<ErrorValidacion> errores)
    {
        if (string.IsNullOrEmpty(nombreNormalizado) ||
            nombreNormalizado.Length < ReglasCatalogos.LongitudMinimaNombre ||
            nombreNormalizado.Length > ReglasCatalogos.LongitudMaximaNombre ||
            !LetraRegex.IsMatch(nombreNormalizado))
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Nombre,
                "Ingrese el nombre (3 a 100 caracteres)."));
        }
    }

    private static void ValidarDireccion(string direccionNormalizada, List<ErrorValidacion> errores)
    {
        if (direccionNormalizada.Length > ReglasCatalogos.LongitudMaximaDireccion)
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Direccion,
                "La dirección no puede superar 150 caracteres."));
        }
    }
}

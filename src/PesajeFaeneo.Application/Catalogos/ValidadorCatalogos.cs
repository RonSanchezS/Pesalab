using System.Text.RegularExpressions;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Catalogos;

public static class ValidadorCatalogos
{
    private static readonly Regex EspaciosRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex MarcaRegex = new(ReglasCatalogos.PatronMarca, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
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

    public static string NormalizarMarca(string? valor)
    {
        var mayus = NormalizarMayusculas(valor);
        return mayus.Replace(" ", string.Empty);
    }

    public static IReadOnlyList<ErrorValidacion> ValidarGanadero(string marcaNormalizada, string nombreNormalizado)
    {
        var errores = new List<ErrorValidacion>();

        if (string.IsNullOrEmpty(marcaNormalizada) ||
            marcaNormalizada.Length > ReglasCatalogos.LongitudMaximaMarca ||
            !MarcaRegex.IsMatch(marcaNormalizada))
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Marca,
                "Ingrese la marca: 1 a 5 caracteres (letras, números o & . / -), sin espacios."));
        }

        if (string.IsNullOrEmpty(nombreNormalizado) ||
            nombreNormalizado.Length < ReglasCatalogos.LongitudMinimaNombre ||
            nombreNormalizado.Length > ReglasCatalogos.LongitudMaximaNombre ||
            !LetraRegex.IsMatch(nombreNormalizado))
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Nombre,
                "Ingrese el nombre (3 a 100 caracteres)."));
        }

        return errores.AsReadOnly();
    }

    public static IReadOnlyList<ErrorValidacion> ValidarCliente(int codigo, string nombreNormalizado, string direccionNormalizada)
    {
        var errores = new List<ErrorValidacion>();

        if (codigo < ReglasCatalogos.CodigoClienteMinimo || codigo > ReglasCatalogos.CodigoClienteMaximo)
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Codigo,
                "El código debe ser un número entre 1 y 999999."));
        }

        if (string.IsNullOrEmpty(nombreNormalizado) ||
            nombreNormalizado.Length < ReglasCatalogos.LongitudMinimaNombre ||
            nombreNormalizado.Length > ReglasCatalogos.LongitudMaximaNombre ||
            !LetraRegex.IsMatch(nombreNormalizado))
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Nombre,
                "Ingrese el nombre (3 a 100 caracteres)."));
        }

        if (direccionNormalizada.Length > ReglasCatalogos.LongitudMaximaDireccion)
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Direccion,
                "La dirección no puede superar 150 caracteres."));
        }

        return errores.AsReadOnly();
    }

    public static IReadOnlyList<ErrorValidacion> ValidarFletero(int orden, string nombreNormalizado, string celularNormalizado)
    {
        var errores = new List<ErrorValidacion>();

        if (orden < ReglasCatalogos.OrdenFleteroMinimo || orden > ReglasCatalogos.OrdenFleteroMaximo)
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Orden,
                "Seleccione un orden entre F1 y F4."));
        }

        if (string.IsNullOrEmpty(nombreNormalizado) ||
            nombreNormalizado.Length < ReglasCatalogos.LongitudMinimaNombre ||
            nombreNormalizado.Length > ReglasCatalogos.LongitudMaximaNombre ||
            !LetraRegex.IsMatch(nombreNormalizado))
        {
            errores.Add(new ErrorValidacion(
                CamposCatalogo.Nombre,
                "Ingrese el nombre (3 a 100 caracteres)."));
        }

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
}

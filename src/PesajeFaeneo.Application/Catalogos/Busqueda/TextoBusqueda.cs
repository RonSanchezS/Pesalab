using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PesajeFaeneo.Application.Catalogos.Busqueda;

public static class TextoBusqueda
{
    private static readonly Regex EspaciosRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Plegar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var normalizadoD = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizadoD.Length);

        foreach (var c in normalizadoD)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var sinAcentos = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        var colapsado = EspaciosRegex.Replace(sinAcentos, " ").Trim();
        return colapsado;
    }
}

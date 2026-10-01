using System.Globalization;

namespace PesajeFaeneo.Infrastructure.Utilitarios.Importacion;

internal static class ConversorValoresOrigen
{
    public static string? ATexto(object? valor)
    {
        if (valor is null or DBNull)
        {
            return null;
        }

        if (valor is string s)
        {
            var recortado = s.Trim();
            return string.IsNullOrEmpty(recortado) ? null : recortado;
        }

        if (valor is DateTime d)
        {
            return d.TimeOfDay == TimeSpan.Zero
                ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        if (valor is bool b)
        {
            return b ? "true" : "false";
        }

        if (valor is decimal dec)
        {
            return dec.ToString(CultureInfo.InvariantCulture);
        }

        if (valor is int or long or short or byte or sbyte or uint or ulong or ushort)
        {
            return Convert.ToString(valor, CultureInfo.InvariantCulture);
        }

        if (valor is double dbl)
        {
            return dbl.ToString("R", CultureInfo.InvariantCulture);
        }

        if (valor is float flt)
        {
            return flt.ToString("R", CultureInfo.InvariantCulture);
        }

        if (valor is byte[])
        {
            return null;
        }

        var str = Convert.ToString(valor, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
    }
}

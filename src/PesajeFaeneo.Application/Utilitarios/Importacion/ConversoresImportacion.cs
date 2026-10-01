using System.Globalization;
using System.Text.RegularExpressions;
using PesajeFaeneo.Application.Catalogos.Busqueda;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public static class ConversoresImportacion
{
    private static readonly Regex MilesRegex = new(@"^\d{1,3}([.,]\d{3})+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] FormatosFecha = new[]
    {
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
        "d/M/yyyy H:mm:ss", "d/M/yyyy H:mm", "d/M/yyyy",
        "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy HH:mm", "dd-MM-yyyy",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss", "yyyyMMdd", "dd/MM/yy", "d/M/yy"
    };

    private static readonly string[] FormatosHora = new[]
    {
        "HH:mm:ss", "HH:mm", "H:mm:ss", "H:mm",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm"
    };

    public static string ClaveColumna(string? texto)
    {
        var plegado = TextoBusqueda.Plegar(texto);
        if (string.IsNullOrEmpty(plegado))
        {
            return string.Empty;
        }

        var chars = new char[plegado.Length];
        int count = 0;
        for (int i = 0; i < plegado.Length; i++)
        {
            if (char.IsLetterOrDigit(plegado[i]))
            {
                chars[count++] = plegado[i];
            }
        }

        return new string(chars, 0, count);
    }

    public static bool TryDecimal(string? texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var s = texto.Replace(" ", "").Replace("\u00A0", "");
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        int lastComma = s.LastIndexOf(',');
        int lastDot = s.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
            {
                // La coma aparece última: es decimal, el punto es miles
                s = s.Replace(".", "");
                int cIdx = s.LastIndexOf(',');
                s = s.Remove(cIdx, 1).Insert(cIdx, ".");
            }
            else
            {
                // El punto aparece último: es decimal, la coma es miles
                s = s.Replace(",", "");
            }
        }
        else if (lastComma >= 0)
        {
            int count = s.Count(c => c == ',');
            if (count == 1)
            {
                s = s.Replace(',', '.');
            }
            else
            {
                s = s.Replace(",", "");
            }
        }
        else if (lastDot >= 0)
        {
            int count = s.Count(c => c == '.');
            if (count == 1)
            {
                // Queda como punto decimal
            }
            else
            {
                s = s.Replace(".", "");
            }
        }

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor);
    }

    public static bool TryEntero(string? texto, out long valor)
    {
        valor = 0L;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var recortado = texto.Trim();
        if (MilesRegex.IsMatch(recortado))
        {
            var limpio = recortado.Replace(".", "").Replace(",", "");
            return long.TryParse(limpio, NumberStyles.None, CultureInfo.InvariantCulture, out valor);
        }

        if (TryDecimal(recortado, out var dec))
        {
            if (decimal.Truncate(dec) == dec && dec >= long.MinValue && dec <= long.MaxValue)
            {
                valor = (long)dec;
                return true;
            }
        }

        return false;
    }

    public static bool TryFechaHora(string? fecha, string? hora, out DateTime local)
    {
        local = default;
        if (string.IsNullOrWhiteSpace(fecha))
        {
            return false;
        }

        if (!DateTime.TryParseExact(fecha.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var f))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(hora))
        {
            if (!DateTime.TryParseExact(hora.Trim(), FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var h))
            {
                return false;
            }

            local = DateTime.SpecifyKind(f.Date + h.TimeOfDay, DateTimeKind.Unspecified);
            return true;
        }

        local = DateTime.SpecifyKind(f, DateTimeKind.Unspecified);
        return true;
    }

    public static bool? Booleano(string? texto)
    {
        var clave = ClaveColumna(texto);
        return clave switch
        {
            "1" or "S" or "SI" or "Y" or "YES" or "T" or "TRUE" or "V" or "VERDADERO" or "ACTIVO" or "A" => true,
            "0" or "N" or "NO" or "F" or "FALSE" or "FALSO" or "INACTIVO" or "I" => false,
            _ => null
        };
    }

    public static bool TryDesposte(string? texto, out TipoDesposte? tipo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            tipo = null;
            return true;
        }

        var clave = ClaveColumna(texto);
        if (clave is "1" or "MR" or "M" || clave.StartsWith("MEDIA", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoDesposte.MediaRes;
            return true;
        }

        if (clave is "2" or "RC" or "C" or "ENTERA" || clave.Contains("COMPLETA", StringComparison.OrdinalIgnoreCase))
        {
            tipo = TipoDesposte.ResCompleta;
            return true;
        }

        tipo = null;
        return false;
    }

    public static bool TrySexo(string? texto, out char? sexo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            sexo = null;
            return true;
        }

        var clave = ClaveColumna(texto);
        if (clave is "M" or "MACHO" or "MASCULINO")
        {
            sexo = 'M';
            return true;
        }

        if (clave is "H" or "HEMBRA" or "F" or "FEMENINO")
        {
            sexo = 'H';
            return true;
        }

        sexo = null;
        return false;
    }

    public static bool TryEstado(string? texto, bool columnaBooleana, out EstadoPesaje estado)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            estado = EstadoPesaje.Vigente;
            return true;
        }

        if (columnaBooleana)
        {
            var b = Booleano(texto);
            if (b == true)
            {
                estado = EstadoPesaje.Anulado;
                return true;
            }
            if (b == false)
            {
                estado = EstadoPesaje.Vigente;
                return true;
            }

            estado = EstadoPesaje.Vigente;
            return false;
        }

        var clave = ClaveColumna(texto);
        if (clave is "1" or "V" or "VIGENTE" or "ACTIVA" or "ACTIVO")
        {
            estado = EstadoPesaje.Vigente;
            return true;
        }

        if (clave is "2" or "A" or "ANULADA" or "ANULADO")
        {
            estado = EstadoPesaje.Anulado;
            return true;
        }

        estado = EstadoPesaje.Vigente;
        return false;
    }

    public static string Recortar(string texto, int maximo, out bool recortado)
    {
        if (texto.Length > maximo)
        {
            recortado = true;
            return texto[..maximo];
        }

        recortado = false;
        return texto;
    }

    public static DateTime LocalAUtc(DateTime local, TimeZoneInfo zona)
    {
        if (zona.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var sinKind = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(sinKind, zona), DateTimeKind.Utc);
    }
}

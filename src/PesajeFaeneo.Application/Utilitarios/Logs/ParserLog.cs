using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Logs;

public static class ParserLog
{
    private static readonly TimeSpan TimeoutRegex = TimeSpan.FromMilliseconds(250);

    private static readonly Regex FormatoLineaRegex = new(
        @"^(?<marca>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(?<nivel>[A-Z]{3})\] (?<mensaje>.*)$",
        RegexOptions.Compiled,
        TimeoutRegex);

    private static readonly Regex NombreArchivoRegex = new(
        @"^pesaje-\d{8}(_\d{3})?\.log$",
        RegexOptions.Compiled,
        TimeoutRegex);

    public static NivelLog Nivel(string codigo)
    {
        return codigo switch
        {
            "VRB" => NivelLog.Detalle,
            "DBG" => NivelLog.Depuracion,
            "INF" => NivelLog.Informacion,
            "WRN" => NivelLog.Advertencia,
            "ERR" => NivelLog.Error,
            "FTL" => NivelLog.Fatal,
            _ => NivelLog.Desconocido
        };
    }

    public static IReadOnlyList<EntradaLog> Analizar(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return Array.Empty<EntradaLog>();
        }

        var lineas = texto.Split('\n');
        var entradas = new List<EntradaLog>();
        EntradaLog? actual = null;
        var detalleActual = new StringBuilder();

        int fin = lineas.Length - 1;
        while (fin >= 0 && string.IsNullOrWhiteSpace(lineas[fin]))
        {
            fin--;
        }

        for (int i = 0; i <= fin; i++)
        {
            var raw = lineas[i];
            var linea = raw.EndsWith('\r') ? raw[..^1] : raw;
            int numeroLinea = i + 1;

            var match = FormatoLineaRegex.Match(linea);
            if (match.Success)
            {
                if (actual != null)
                {
                    entradas.Add(actual with { Detalle = detalleActual.ToString() });
                    detalleActual.Clear();
                }

                var marca = match.Groups["marca"].Value;
                var nivelStr = match.Groups["nivel"].Value;
                var mensaje = match.Groups["mensaje"].Value;
                var nivel = Nivel(nivelStr);

                actual = new EntradaLog(numeroLinea, marca, nivel, mensaje, string.Empty);
            }
            else
            {
                if (actual != null)
                {
                    if (detalleActual.Length > 0)
                    {
                        detalleActual.Append('\n');
                    }
                    detalleActual.Append(linea);
                }
                else
                {
                    entradas.Add(new EntradaLog(numeroLinea, string.Empty, NivelLog.Desconocido, linea, string.Empty));
                }
            }
        }

        if (actual != null)
        {
            entradas.Add(actual with { Detalle = detalleActual.ToString() });
        }

        return entradas;
    }

    public static bool EsNombreValido(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return false;
        }

        return NombreArchivoRegex.IsMatch(nombre);
    }

    public static DateOnly? FechaDeNombre(string nombre)
    {
        if (!EsNombreValido(nombre))
        {
            return null;
        }

        var strFecha = nombre.Substring(7, 8);
        if (DateOnly.TryParseExact(strFecha, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            return fecha;
        }

        return null;
    }
}

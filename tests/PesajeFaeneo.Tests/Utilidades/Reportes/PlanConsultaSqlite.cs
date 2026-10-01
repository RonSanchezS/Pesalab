using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PesajeFaeneo.Tests.Utilidades.Reportes;

/// <summary>
/// EXPLAIN QUERY PLAN del SQL que EF genera para una consulta (ToQueryString), con los parámetros de las líneas
/// ".param set" que EF antepone. Devuelve la columna "detail" de cada fila del plan.
/// </summary>
internal static class PlanConsultaSqlite
{
    private const string PrefijoParametro = ".param set ";

    public static async Task<IReadOnlyList<string>> ObtenerAsync(SqliteConnection conexion, IQueryable consulta)
    {
        var lineas = consulta.ToQueryString().Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        await using var cmd = conexion.CreateCommand();
        foreach (var linea in lineas.Where(l => l.StartsWith(PrefijoParametro, StringComparison.Ordinal)))
        {
            var partes = linea.Substring(PrefijoParametro.Length).Split(' ', 2);
            cmd.Parameters.AddWithValue(partes[0], partes[1].Trim('\''));
        }

        cmd.CommandText = "EXPLAIN QUERY PLAN " + string.Join("\n", lineas.Where(l => !l.StartsWith(PrefijoParametro, StringComparison.Ordinal)));

        var detalles = new List<string>();
        await using var lector = await cmd.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            detalles.Add(lector.GetString(3));
        }

        return detalles;
    }

    /// <summary>La línea del plan correspondiente a la tabla Pesajes (alias "p" en el SQL de EF).</summary>
    public static string LineaPesajes(IReadOnlyList<string> plan) =>
        plan.Single(d => d.StartsWith("SEARCH p ", StringComparison.Ordinal) || d.StartsWith("SCAN p", StringComparison.Ordinal));
}

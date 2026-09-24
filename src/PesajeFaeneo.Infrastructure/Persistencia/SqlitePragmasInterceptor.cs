using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PesajeFaeneo.Infrastructure.Persistencia;

/// <summary>
/// Aplica los pragmas de SQLite que son propios de cada conexión (no persistentes en el archivo
/// de base de datos), de modo que toda conexión que abra el pool de Microsoft.Data.Sqlite quede
/// configurada igual: espera ante bloqueos, escritura NORMAL y claves foráneas activas.
/// </summary>
public class SqlitePragmasInterceptor : DbConnectionInterceptor
{
    private const string ComandoPragmas =
        "PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        EjecutarPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await EjecutarPragmasAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void EjecutarPragmas(DbConnection connection)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = ComandoPragmas;
        comando.ExecuteNonQuery();
    }

    private static async Task EjecutarPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var comando = connection.CreateCommand();
        await using (comando.ConfigureAwait(false))
        {
            comando.CommandText = ComandoPragmas;
            await comando.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

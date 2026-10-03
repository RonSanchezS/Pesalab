using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;
    private readonly ILogger<DatabaseInitializer>? _logger;

    public DatabaseInitializer(IDbContextFactory<PesajeDbContext> contextFactory, ILogger<DatabaseInitializer>? logger = null)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<EstadoEsquemaBaseDatos> EvaluarAsync(CancellationToken cancellationToken = default)
    {
        // Abrir una base inexistente la crea vacía (H10): en ese caso no hay aplicadas y todo está pendiente.
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var conocidas = context.Database.GetMigrations().ToList();
        var aplicadas = (await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        return new EstadoEsquemaBaseDatos(
            aplicadas,
            conocidas.Except(aplicadas, StringComparer.Ordinal).ToList(),
            aplicadas.Except(conocidas, StringComparer.Ordinal).ToList());
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        // journal_mode=WAL es el único pragma que persiste en el archivo de la base de datos; el resto
        // (busy_timeout, synchronous, foreign_keys) es por conexión y lo aplica SqlitePragmasInterceptor
        // en cada apertura del pool, no solo en esta conexión de inicialización.
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        // Estadísticas del planificador (F7-02). Sin sqlite_stat1, SQLite resuelve el reporte por cliente con el índice
        // (Estado, FechaHora) y recorre todas las boletas vigentes del rango en vez de usar (ClienteId, FechaHora).
        // analysis_limit acota el costo (ANALYZE por muestreo) para que el arranque no crezca con la base; se recalcula
        // en cada arranque para seguir la distribución real de los datos. Es una optimización: si falla, la aplicación
        // arranca igual (los reportes solo serían más lentos).
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA analysis_limit=1000; ANALYZE;", cancellationToken);
        }
        catch (SqliteException ex)
        {
            _logger?.LogWarning(ex, "No se pudieron actualizar las estadísticas de la base de datos (ANALYZE).");
        }
    }
}

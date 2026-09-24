using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones;

namespace PesajeFaeneo.Infrastructure.Persistencia;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<PesajeDbContext> _contextFactory;

    public DatabaseInitializer(IDbContextFactory<PesajeDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        // journal_mode=WAL es el único pragma que persiste en el archivo de la base de datos; el resto
        // (busy_timeout, synchronous, foreign_keys) es por conexión y lo aplica SqlitePragmasInterceptor
        // en cada apertura del pool, no solo en esta conexión de inicialización.
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
    }
}

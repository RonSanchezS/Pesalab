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

        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", cancellationToken);
    }
}

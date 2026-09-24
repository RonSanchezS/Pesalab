using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Infrastructure.Persistencia;

namespace PesajeFaeneo.Tests.Fixtures;

public sealed class BaseDatosEnMemoria : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public IDbContextFactory<PesajeDbContext> ContextFactory { get; }
    public IUnidadDeTrabajoFactory UnidadDeTrabajoFactory { get; }

    private BaseDatosEnMemoria(
        SqliteConnection connection,
        IDbContextFactory<PesajeDbContext> contextFactory,
        IUnidadDeTrabajoFactory uowFactory)
    {
        _connection = connection;
        ContextFactory = contextFactory;
        UnidadDeTrabajoFactory = uowFactory;
    }

    public static async Task<BaseDatosEnMemoria> CrearAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        var contextFactory = new TestDbContextFactory(options);

        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        var uowFactory = new UnidadDeTrabajoFactory(contextFactory);

        return new BaseDatosEnMemoria(connection, contextFactory, uowFactory);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private class TestDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public TestDbContextFactory(DbContextOptions<PesajeDbContext> options)
        {
            _options = options;
        }

        public PesajeDbContext CreateDbContext() => new(_options);

        public Task<PesajeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new PesajeDbContext(_options));
    }
}

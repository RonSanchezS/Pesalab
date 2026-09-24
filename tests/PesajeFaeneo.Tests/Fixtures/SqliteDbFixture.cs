using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests.Fixtures;

public class SqliteDbFixture : IAsyncLifetime
{
    private SqliteConnection? _connection;
    public IDbContextFactory<PesajeDbContext> ContextFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PesajeDbContext>()
            .UseSqlite(_connection)
            .Options;

        ContextFactory = new TestDbContextFactory(options);

        await using var context = await ContextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    private class TestDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public TestDbContextFactory(DbContextOptions<PesajeDbContext> options)
        {
            _options = options;
        }

        public PesajeDbContext CreateDbContext()
        {
            return new PesajeDbContext(_options);
        }

        public Task<PesajeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PesajeDbContext(_options));
        }
    }
}

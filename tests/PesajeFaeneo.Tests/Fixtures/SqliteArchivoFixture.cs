using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests.Fixtures;

public class SqliteArchivoFixture : IAsyncLifetime
{
    public string FilePath { get; } = Path.Combine(Path.GetTempPath(), $"pesaje_test_{Guid.NewGuid():N}.db");
    public IDbContextFactory<PesajeDbContext> ContextFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite($"Data Source={FilePath}");
        var options = optionsBuilder.Options;

        ContextFactory = new FileTestDbContextFactory(options);

        await using var context = await ContextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (File.Exists(FilePath))
        {
            try { File.Delete(FilePath); } catch { /* ignore */ }
        }

        var wal = $"{FilePath}-wal";
        if (File.Exists(wal))
        {
            try { File.Delete(wal); } catch { /* ignore */ }
        }

        var shm = $"{FilePath}-shm";
        if (File.Exists(shm))
        {
            try { File.Delete(shm); } catch { /* ignore */ }
        }

        await Task.CompletedTask;
    }

    private class FileTestDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public FileTestDbContextFactory(DbContextOptions<PesajeDbContext> options)
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

namespace PesajeFaeneo.Tests.Utilidades.Backup;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using PesajeFaeneo.Tests.Builders;

public sealed class EntornoArchivosPrueba : IAsyncDisposable
{
    private readonly string _dir;
    private int _ultimoNumeroBoleta;

    public AppPaths AppPaths { get; }
    public IDbContextFactory<PesajeDbContext> ContextFactory { get; }
    public FakeTimeProvider Tiempo { get; }

    private EntornoArchivosPrueba(string dir, AppPaths appPaths, IDbContextFactory<PesajeDbContext> contextFactory, FakeTimeProvider tiempo)
    {
        _dir = dir;
        AppPaths = appPaths;
        ContextFactory = contextFactory;
        Tiempo = tiempo;
    }

    public static async Task<EntornoArchivosPrueba> CrearAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pesaje_f8_{Guid.NewGuid():N}");
        var appPaths = new AppPaths(dir);
        appPaths.AsegurarDirectorios();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(new SqliteConnectionStringBuilder(appPaths.ConnectionString) { Pooling = false }.ToString());
        var contextFactory = new ArchivoDbContextFactory(optionsBuilder.Options);

        var initializer = new DatabaseInitializer(contextFactory);
        await initializer.InicializarAsync();
        SqliteConnection.ClearAllPools();

        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4"));

        return new EntornoArchivosPrueba(dir, appPaths, contextFactory, tiempo);
    }

    public async Task<int> SembrarAsync(int pesajes, string codigo = "AB")
    {
        await using var ctx = await ContextFactory.CreateDbContextAsync();

        var admin = new UsuarioBuilder()
            .ConRol(Rol.Administrador)
            .ConNombreUsuario($"admin_{Guid.NewGuid():N}"[..15])
            .Build();
        ctx.Usuarios.Add(admin);

        var ganadero = await ctx.Ganaderos.FirstOrDefaultAsync(g => g.Codigo == codigo);
        if (ganadero is null)
        {
            ganadero = new Ganadero
            {
                Codigo = codigo,
                Nombre = "GANADERO TEST",
                Activo = true,
                CreadoEn = Tiempo.GetUtcNow().UtcDateTime
            };
            ctx.Ganaderos.Add(ganadero);
        }

        var cliente = new Cliente
        {
            Codigo = Random.Shared.Next(1000, 99999),
            Nombre = "CLIENTE TEST",
            Direccion = "DIRECCION TEST",
            Activo = true,
            CreadoEn = Tiempo.GetUtcNow().UtcDateTime
        };
        ctx.Clientes.Add(cliente);

        await ctx.SaveChangesAsync();

        if (pesajes > 0)
        {
            var list = new List<Pesaje>();
            for (int i = 0; i < pesajes; i++)
            {
                _ultimoNumeroBoleta++;
                var p = new PesajeBuilder()
                    .ConNumeroBoleta(_ultimoNumeroBoleta)
                    .ConGanaderoId(ganadero.Id)
                    .ConClienteId(cliente.Id)
                    .ConUsuarioId(admin.Id)
                    .ConPesos(100m, 100m)
                    .Build();
                list.Add(p);
            }
            ctx.Pesajes.AddRange(list);
            await ctx.SaveChangesAsync();
            SqliteConnection.ClearAllPools();
        }

        return admin.Id;
    }

    public SqliteBackupService CrearMotor(string equipo = "EQUIPO-TEST")
    {
        return new SqliteBackupService(
            AppPaths,
            ContextFactory,
            Tiempo,
            NullLogger<SqliteBackupService>.Instance,
            equipo);
    }

    public async Task<int> ContarPesajesAsync()
    {
        await using var ctx = await ContextFactory.CreateDbContextAsync();
        return await ctx.Pesajes.CountAsync();
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (Directory.Exists(_dir))
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        await Task.CompletedTask;
    }

    private class ArchivoDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public ArchivoDbContextFactory(DbContextOptions<PesajeDbContext> options)
        {
            _options = options;
        }

        public PesajeDbContext CreateDbContext() => new(_options);
        public Task<PesajeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PesajeDbContext(_options));
    }
}

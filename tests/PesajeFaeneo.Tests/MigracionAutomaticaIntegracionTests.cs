namespace PesajeFaeneo.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Arranque;
using PesajeFaeneo.Application.Arranque.Dtos;
using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Common;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Utilitarios.Backup;
using Xunit;

public sealed class MigracionAutomaticaIntegracionTests : IAsyncDisposable
{
    private readonly string _dir;
    private readonly AppPaths _appPaths;
    private readonly TestDbContextFactory _contextFactory;
    private readonly FakeTimeProvider _tiempo;
    private readonly DatabaseInitializer _initializer;
    private readonly SqliteBackupService _backupService;
    private readonly UnidadDeTrabajoFactory _uowFactory;
    private readonly ActualizadorBaseDatos _actualizador;

    private sealed class TestDbContextFactory : IDbContextFactory<PesajeDbContext>
    {
        private readonly DbContextOptions<PesajeDbContext> _options;

        public TestDbContextFactory(DbContextOptions<PesajeDbContext> options)
        {
            _options = options;
        }

        public PesajeDbContext CreateDbContext() => new(_options);
        public Task<PesajeDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PesajeDbContext(_options));
    }

    public MigracionAutomaticaIntegracionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"pesaje_mig_test_{Guid.NewGuid():N}");
        _appPaths = new AppPaths(_dir);
        _appPaths.AsegurarDirectorios();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(new SqliteConnectionStringBuilder(_appPaths.ConnectionString) { Pooling = false }.ToString());
        _contextFactory = new TestDbContextFactory(optionsBuilder.Options);

        _tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));
        _initializer = new DatabaseInitializer(_contextFactory);
        _backupService = new SqliteBackupService(_appPaths, _contextFactory, _tiempo, NullLogger<SqliteBackupService>.Instance, "EQUIPO-PRUEBA");
        _uowFactory = new UnidadDeTrabajoFactory(_contextFactory);
        _actualizador = new ActualizadorBaseDatos(_initializer, _backupService, _uowFactory, _tiempo, NullLogger<ActualizadorBaseDatos>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir))
        {
            try
            {
                Directory.Delete(_dir, recursive: true);
            }
            catch
            {
            }
        }
        await Task.CompletedTask;
    }

    [Fact]
    public async Task PrepararAsync_BaseConMigracionesAnteriores_ActualizaYGeneraBackupPreMigracion()
    {
        // 1. Migrar hasta F4_Pesaje e insertar datos de prueba
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            var migrator = ctx.GetService<IMigrator>();
            await migrator.MigrateAsync("20260927144821_F4_Pesaje");

            await ctx.Database.ExecuteSqlRawAsync(@"
                INSERT INTO Usuarios (Id, NombreUsuario, Hash, Salt, Rol, NombreCompleto, Activo, DebeCambiarPassword, IntentosFallidos, CreadoEn)
                VALUES (10, 'operador_f6', 'hash', 'salt', 3, 'Operador F6', 1, 0, 0, '2026-09-20 12:00:00');

                INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn)
                VALUES (20, 'GF6', 'Ganadero F6', 1, '2026-09-20 12:00:00');

                INSERT INTO Clientes (Id, Codigo, Nombre, Direccion, Activo, CreadoEn)
                VALUES (30, 80001, 'Cliente F6', 'Direccion F6', 1, '2026-09-20 12:00:00');

                INSERT INTO Pesajes (Id, NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, OrigenPeso, Observacion, Estado, UsuarioId, CreadoEn)
                VALUES (1, 80001, '2026-09-20 12:00:00', 20, 30, 'Direccion 1', 1, 'BLANCO', 'M', 1, 106.0, 0.0, 106.0, 1, 'VIGENTE', 1, 10, '2026-09-20 12:00:00');
            ");
        }

        // 2. Preparar actualización
        var resultado = await _actualizador.PrepararAsync();

        Assert.Equal(EstadoActualizacionBaseDatos.Actualizada, resultado.Estado);
        Assert.NotEmpty(resultado.MigracionesAplicadas);
        Assert.NotNull(resultado.ArchivoBackup);

        // 3. Verificar que en backups\seguridad hay UN zip premigracion
        var carpetaSeguridad = Path.Combine(_appPaths.BackupsDir, "seguridad");
        var zips = Directory.GetFiles(carpetaSeguridad, "PesajeFaeneo_premigracion_*.zip");
        var zip = Assert.Single(zips);

        var manifiesto = ArchivoBackupZip.LeerManifiesto(zip);
        Assert.NotNull(manifiesto);
        Assert.Equal(TipoBackup.PreMigracion, manifiesto.Tipo);
        Assert.Equal("20260927144821_F4_Pesaje", manifiesto.UltimaMigracion);

        // 4. EvaluarAsync posterior: Pendientes queda vacía
        var evaluacion = await _initializer.EvaluarAsync();
        Assert.Empty(evaluacion.Pendientes);

        // 5. El pesaje sigue ahí
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == 1);
            Assert.Equal(80001, p.NumeroBoleta);
            Assert.Equal(EstadoPesaje.Vigente, p.Estado);
        }
    }

    [Fact]
    public async Task PrepararAsync_BaseAlDia_DevuelveAlDiaYSinCopia()
    {
        await _initializer.InicializarAsync();

        var resultado = await _actualizador.PrepararAsync();

        Assert.Equal(EstadoActualizacionBaseDatos.AlDia, resultado.Estado);
        Assert.Empty(resultado.MigracionesAplicadas);
        Assert.Null(resultado.ArchivoBackup);

        var carpetaSeguridad = Path.Combine(_appPaths.BackupsDir, "seguridad");
        if (Directory.Exists(carpetaSeguridad))
        {
            Assert.Empty(Directory.GetFiles(carpetaSeguridad, "*.zip"));
        }
    }

    [Fact]
    public async Task PrepararAsync_CarpetaSinBase_NuevaInstalacion_SinCopia_BaseCreadaYAlDia()
    {
        Assert.False(File.Exists(_appPaths.DatabasePath));

        var resultado = await _actualizador.PrepararAsync();

        Assert.Equal(EstadoActualizacionBaseDatos.NuevaInstalacion, resultado.Estado);
        Assert.Empty(resultado.MigracionesAplicadas);
        Assert.Null(resultado.ArchivoBackup);

        Assert.True(File.Exists(_appPaths.DatabasePath));

        var evaluacion = await _initializer.EvaluarAsync();
        Assert.Empty(evaluacion.Pendientes);

        var carpetaSeguridad = Path.Combine(_appPaths.BackupsDir, "seguridad");
        if (Directory.Exists(carpetaSeguridad))
        {
            Assert.Empty(Directory.GetFiles(carpetaSeguridad, "*.zip"));
        }
    }

    [Fact]
    public async Task PrepararAsync_BaseConMigracionDesconocida_LanzaVersionMasNueva_SinCopiaNiModificacion()
    {
        await _initializer.InicializarAsync();

        int cantidadFilasAntes;
        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_Futura', '8.0.11');");

            cantidadFilasAntes = await ctx.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM __EFMigrationsHistory").SingleAsync();
        }

        var ex = await Assert.ThrowsAsync<ArranqueBaseDatosException>(() => _actualizador.PrepararAsync());
        Assert.Equal(MotivoFalloArranque.VersionMasNueva, ex.Motivo);

        var carpetaSeguridad = Path.Combine(_appPaths.BackupsDir, "seguridad");
        if (Directory.Exists(carpetaSeguridad))
        {
            Assert.Empty(Directory.GetFiles(carpetaSeguridad, "*.zip"));
        }

        await using (var ctx = await _contextFactory.CreateDbContextAsync())
        {
            var cantidadFilasDespues = await ctx.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM __EFMigrationsHistory").SingleAsync();
            Assert.Equal(cantidadFilasAntes, cantidadFilasDespues);
        }
    }
}

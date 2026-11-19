using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>
/// Migración F11_ModoDesposte: ParametrosSistema.ModoDesposte (nullable, CHECK 1..3). Las instalaciones ya puestas en
/// marcha quedan en Ambos; las que no terminaron el asistente, sin definir.
/// </summary>
public class MigracionF11Tests
{
    private const string UltimaAnterior = "20261004133527_F10_ObservacionesCliente";

    private static async Task<(SqliteConnection Conexion, DbContextOptions<PesajeDbContext> Opciones)> CrearBaseF10Async(bool configuracionCompleta)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(UltimaAnterior);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE ParametrosSistema SET NombreEmpresa = 'FRIGORIFICO F10', SiguienteNumeroBoleta = 500, ConfiguracionInicialCompleta = {0} WHERE Id = 1;",
            configuracionCompleta ? 1 : 0);

        return (connection, options);
    }

    [Theory]
    [InlineData(true, ModoDesposte.Ambos)]
    [InlineData(false, null)]
    public async Task MigrarDesdeF10_FijaAmbosSoloSiLaPuestaEnMarchaEstabaCompleta(bool completa, ModoDesposte? esperado)
    {
        var (connection, options) = await CrearBaseF10Async(completa);
        await using var _ = connection;

        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync();

        var p = await context.ParametrosSistema.AsNoTracking().SingleAsync();
        Assert.Equal(esperado, p.ModoDesposte);
        Assert.Equal("FRIGORIFICO F10", p.NombreEmpresa);
        Assert.Equal(500, p.SiguienteNumeroBoleta);
        Assert.Equal(completa, p.ConfiguracionInicialCompleta);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Check_RechazaValoresFueraDeRango(int valor)
    {
        var (connection, options) = await CrearBaseF10Async(true);
        await using var _ = connection;
        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync();

        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE ParametrosSistema SET ModoDesposte = {0} WHERE Id = 1;", valor));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Check_AceptaLosTresModosYNull(int valor)
    {
        var (connection, options) = await CrearBaseF10Async(true);
        await using var _ = connection;
        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync();

        await context.Database.ExecuteSqlRawAsync("UPDATE ParametrosSistema SET ModoDesposte = {0} WHERE Id = 1;", valor);
        await context.Database.ExecuteSqlRawAsync("UPDATE ParametrosSistema SET ModoDesposte = NULL WHERE Id = 1;");

        var p = await context.ParametrosSistema.AsNoTracking().SingleAsync();
        Assert.Null(p.ModoDesposte);
    }

    [Fact]
    public async Task BaseNueva_NaceConModoSinDefinir()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);

        await using var context = new PesajeDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();

        var aplicadas = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Equal("20261008185354_F11_ModoDesposte", aplicadas[^1]);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        var p = await context.ParametrosSistema.AsNoTracking().SingleAsync();
        Assert.Null(p.ModoDesposte);
        Assert.False(p.ConfiguracionInicialCompleta);
    }
}

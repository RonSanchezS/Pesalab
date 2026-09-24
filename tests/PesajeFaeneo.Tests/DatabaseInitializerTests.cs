using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DatabaseInitializerTests : IClassFixture<SqliteArchivoFixture>
{
    private readonly SqliteArchivoFixture _fixture;

    public DatabaseInitializerTests(SqliteArchivoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InicializarAsync_AplicaPragmasATodaConexionNuevaDelPool()
    {
        var initializer = new DatabaseInitializer(_fixture.ContextFactory);
        await initializer.InicializarAsync();

        // Se abre un contexto (y por lo tanto una conexión) DISTINTO al usado por el inicializador,
        // para comprobar que el interceptor de pragmas se aplica a toda conexión del pool y no solo
        // a la primera. journal_mode es el único pragma persistente en el archivo de la base de datos.
        await using var context = await _fixture.ContextFactory.CreateDbContextAsync();
        var conexion = (SqliteConnection)context.Database.GetDbConnection();
        await conexion.OpenAsync();

        Assert.Equal("wal", await LeerPragmaAsync(conexion, "journal_mode"));
        Assert.Equal("1", await LeerPragmaAsync(conexion, "synchronous"));
        Assert.Equal("5000", await LeerPragmaAsync(conexion, "busy_timeout"));
        Assert.Equal("1", await LeerPragmaAsync(conexion, "foreign_keys"));
    }

    [Fact]
    public async Task InicializarAsync_MigraYSiembraDatosIniciales_ConHasData()
    {
        var initializer = new DatabaseInitializer(_fixture.ContextFactory);
        await initializer.InicializarAsync();

        await using var context = await _fixture.ContextFactory.CreateDbContextAsync();

        var parametros = await context.ParametrosSistema.SingleAsync(p => p.Id == 1);
        Assert.Equal(1, parametros.SiguienteNumeroBoleta);

        var configuracionBalanza = await context.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
        Assert.NotNull(configuracionBalanza);
    }

    [Fact]
    public async Task InicializarAsync_EjecutadoDosVeces_EsIdempotente()
    {
        var initializer = new DatabaseInitializer(_fixture.ContextFactory);

        await initializer.InicializarAsync();
        await initializer.InicializarAsync();

        await using var context = await _fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.ParametrosSistema.CountAsync());
        Assert.Equal(1, await context.ConfiguracionesBalanza.CountAsync());
    }

    private static async Task<string> LeerPragmaAsync(SqliteConnection conexion, string pragma)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"PRAGMA {pragma};";
        var resultado = await comando.ExecuteScalarAsync();
        return resultado?.ToString() ?? string.Empty;
    }
}

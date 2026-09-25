using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

public class MigracionF3Tests
{
    [Fact]
    public async Task MigrarDesdeF2_ActualizaPatronYConservaDatosConfiguracion()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        // 1. Migrar hasta F2_Catalogos
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260924183125_F2_Catalogos");

            // 2. Verificar que la fila 1 de ConfiguracionesBalanza tiene el patrón viejo
            var patronViejo = await context.Database.SqlQueryRaw<string>(
                "SELECT PatronRegex AS Value FROM ConfiguracionesBalanza WHERE Id = 1").SingleAsync();
            Assert.Equal(@"(?<peso>[-+]?\d+(?:[.,]\d+)?)", patronViejo);

            // 3. Modificar con SQL crudo LecturasParaEstable = 7, TimeoutMs = 2500
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE ConfiguracionesBalanza SET LecturasParaEstable = 7, TimeoutMs = 2500 WHERE Id = 1;");
        }

        // 4. Migrar hasta la última (F3_Balanza)
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();

            // 5. Verificar datos preservados y patrón actualizado
            var config = await context.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            Assert.Equal(7, config.LecturasParaEstable);
            Assert.Equal(2500, config.TimeoutMs);
            Assert.Equal(@"^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)", config.PatronRegex);

            // El patrón sembrado por defecto NO declara el grupo opcional "estable": debe funcionar
            // out-of-the-box con el simulador (que nunca emite un indicador de estabilidad), dejando la
            // detección exclusivamente a cargo de la dispersión (D5, vía matemática). Un técnico puede
            // configurar un patrón con (?<estable>...) para hardware que sí reporta estabilidad en la trama.
            var regex = new System.Text.RegularExpressions.Regex(config.PatronRegex);
            Assert.DoesNotContain("estable", regex.GetGroupNames());
            Assert.Contains("peso", regex.GetGroupNames());

            // Verificar PRAGMA foreign_key_check
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check;";
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.False(await reader.ReadAsync(), "PRAGMA foreign_key_check devolvió violaciones de FK tras la migración F3.");
            }

            // Verificar que UPDATE con TimeoutMs = 100 falla por CHECK constraint
            await Assert.ThrowsAnyAsync<SqliteException>(async () =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE ConfiguracionesBalanza SET TimeoutMs = 100 WHERE Id = 1;");
            });
        }
    }

    [Fact]
    public async Task MigrarProtocoloTrama_ConservaConfiguracionExistente_YUsaValoresPorDefecto()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        await using (var context = new PesajeDbContext(options))
        {
            await context.GetService<IMigrator>().MigrateAsync("20260925142602_F3_Balanza");
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE ConfiguracionesBalanza SET TipoConexion = 1, PuertoCom = 'COM4', BaudRate = 9600 WHERE Id = 1;");
        }

        await using (var context = new PesajeDbContext(options))
        {
            await context.GetService<IMigrator>().MigrateAsync();

            var config = await context.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            Assert.Equal(PesajeFaeneo.Domain.Enums.TipoConexion.Serial, config.TipoConexion);
            Assert.Equal("COM4", config.PuertoCom);
            Assert.Equal(PesajeFaeneo.Domain.Enums.DelimitadorTrama.FinDeLinea, config.DelimitadorTrama);
            Assert.Equal(PesajeFaeneo.Domain.Enums.UnidadPesoTrama.Kilogramos, config.UnidadPeso);

            await Assert.ThrowsAnyAsync<SqliteException>(() =>
                context.Database.ExecuteSqlRawAsync("UPDATE ConfiguracionesBalanza SET DelimitadorTrama = 0 WHERE Id = 1;"));
            await Assert.ThrowsAnyAsync<SqliteException>(() =>
                context.Database.ExecuteSqlRawAsync("UPDATE ConfiguracionesBalanza SET UnidadPeso = 3 WHERE Id = 1;"));

            await context.Database.ExecuteSqlRawAsync(
                "UPDATE ConfiguracionesBalanza SET DelimitadorTrama = 2, UnidadPeso = 2 WHERE Id = 1;");
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE ConfiguracionesBalanza SET DelimitadorTrama = 3 WHERE Id = 1;");
            await Assert.ThrowsAnyAsync<SqliteException>(() =>
                context.Database.ExecuteSqlRawAsync("UPDATE ConfiguracionesBalanza SET DelimitadorTrama = 4 WHERE Id = 1;"));
        }
    }

    [Theory]
    [InlineData("BaudRate", "50")]
    [InlineData("BitsParada", "9")]
    [InlineData("DataBits", "1")]
    [InlineData("Paridad", "99")]
    [InlineData("PuertoCom", "''")]
    [InlineData("PuertoTcp", "0")]
    [InlineData("PuertoTcp", "70000")]
    [InlineData("ToleranciaKg", "-1")]
    [InlineData("ToleranciaKg", "11")]
    [InlineData("LecturasParaEstable", "0")]
    [InlineData("LecturasParaEstable", "51")]
    [InlineData("HostTcp", "''")]
    [InlineData("PatronRegex", "''")]
    public async Task RestriccionesCheckDeF3_RechazanValoresFueraDeRango(string columna, string valor)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        await using var context = new PesajeDbContext(options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync();

        string sql = "UPDATE ConfiguracionesBalanza SET " + columna + " = " + valor + " WHERE Id = 1;";
        await Assert.ThrowsAnyAsync<SqliteException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(sql);
        });
    }
}

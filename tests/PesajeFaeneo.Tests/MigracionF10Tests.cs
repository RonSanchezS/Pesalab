using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>
/// Migración F10_ObservacionesCliente: Ganaderos.Marca -> Codigo, Fleteros.Orden -> Codigo (único entre todos, sin tope de 4)
/// y Pesajes.FleteroId. Se prueba desde una base con datos de la versión anterior (F6).
/// </summary>
public class MigracionF10Tests
{
    private const string UltimaAnterior = "20260930142130_F6_VersionPesaje";

    private static async Task<(SqliteConnection Conexion, DbContextOptions<PesajeDbContext> Opciones)> CrearBaseF6ConDatosAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(UltimaAnterior);

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO Usuarios (Id, NombreUsuario, Hash, Salt, Rol, NombreCompleto, Activo, DebeCambiarPassword, IntentosFallidos, CreadoEn)
            VALUES (10, 'operador_f10', 'hash', 'salt', 3, 'Operador F10', 1, 0, 0, '2026-09-20 12:00:00');

            INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn) VALUES (1, 'AH', 'GANADERO LEGADO', 1, '2026-09-20 12:00:00');
            INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn) VALUES (2, 'ÑA', 'PEÑA', 1, '2026-09-20 12:00:00');
            INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn) VALUES (3, '15', 'GANADERO NUMERICO', 0, '2026-09-20 12:00:00');

            INSERT INTO Clientes (Id, Codigo, Nombre, Direccion, Activo, CreadoEn) VALUES (30, 80001, 'CLIENTE F10', 'DIR', 1, '2026-09-20 12:00:00');

            -- Fleteros F1..F4 activos y tres inactivos que repiten el orden de otros (el índice solo era único entre activos).
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (1, 1, 'FLETERO UNO', '70000001', 1, '2026-09-20 12:00:00');
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (2, 2, 'FLETERO DOS', '70000002', 1, '2026-09-20 12:00:00');
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (3, 3, 'FLETERO TRES', '70000003', 1, '2026-09-20 12:00:00');
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (4, 4, 'FLETERO CUATRO', '70000004', 1, '2026-09-20 12:00:00');
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (5, 2, 'INACTIVO DOS A', '70000005', 0, '2026-09-20 12:00:00');
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (6, 2, 'INACTIVO DOS B', '70000006', 0, '2026-09-20 12:00:00');
            INSERT INTO Fleteros (Id, Orden, Nombre, Celular, Activo, CreadoEn) VALUES (7, 4, 'INACTIVO CUATRO', '70000007', 0, '2026-09-20 12:00:00');

            INSERT INTO Pesajes (Id, NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, OrigenPeso, Observacion, Estado, UsuarioId, CreadoEn)
            VALUES (1, 80001, '2026-09-20 12:00:00', 1, 30, 'DIR 1', 1, 'NELORE', 'M', 1, 106.0, 0.0, 106.0, 1, 'VIGENTE', 1, 10, '2026-09-20 12:00:00');
            INSERT INTO Pesajes (Id, NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, OrigenPeso, Observacion, Estado, MotivoAnulacion, AnuladoPorId, AnuladoEn, UsuarioId, CreadoEn)
            VALUES (2, 80002, '2026-09-20 12:30:00', 2, 30, 'DIR 2', 2, 'ANGUS', 'H', 1, 108.0, 0.0, 108.0, 1, 'ANULADO', 2, 'ERROR', 10, '2026-09-20 13:00:00', 10, '2026-09-20 12:30:00');
        ");

        return (connection, options);
    }

    [Fact]
    public async Task MigrarDesdeF6_ConservaDatos_RenombraCodigos_RenumeraInactivosDuplicadosYAgregaFleteroId()
    {
        var (connection, options) = await CrearBaseF6ConDatosAsync();
        await using var _ = connection;

        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync();

        // Ganaderos: los códigos alfanuméricos y numéricos se conservan tal cual.
        var ganaderos = await context.Ganaderos.AsNoTracking().OrderBy(g => g.Id).ToListAsync();
        Assert.Equal(new[] { "AH", "ÑA", "15" }, ganaderos.Select(g => g.Codigo));
        Assert.Equal("PEÑA", ganaderos[1].Nombre);
        Assert.False(ganaderos[2].Activo);

        // Fleteros: los activos conservan su orden como código; los inactivos que chocaban se renumeran para ser únicos.
        var fleteros = await context.Fleteros.AsNoTracking().OrderBy(f => f.Id).ToListAsync();
        Assert.Equal(7, fleteros.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, fleteros.Take(4).Select(f => f.Codigo));
        Assert.Equal(7, fleteros.Select(f => f.Codigo).Distinct().Count());
        Assert.All(fleteros.Skip(4), f => Assert.True(f.Codigo > 4));
        Assert.Equal("INACTIVO DOS A", fleteros[4].Nombre);
        Assert.Equal("70000005", fleteros[4].Celular);
        Assert.All(fleteros.Skip(4), f => Assert.False(f.Activo));

        // Pesajes: intactos y con FleteroId nulo.
        var p1 = await context.Pesajes.AsNoTracking().SingleAsync(p => p.Id == 1);
        var p2 = await context.Pesajes.AsNoTracking().SingleAsync(p => p.Id == 2);
        Assert.Equal(80001, p1.NumeroBoleta);
        Assert.Equal("NELORE", p1.ColorRaza);
        Assert.Null(p1.FleteroId);
        Assert.Equal(EstadoPesaje.Anulado, p2.Estado);
        Assert.Equal("ERROR", p2.MotivoAnulacion);
        Assert.Null(p2.FleteroId);
        Assert.Equal(0, p2.Version);

        // Integridad referencial.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.False(await reader.ReadAsync(), "PRAGMA foreign_key_check devolvió violaciones tras la migración F10.");
        }
    }

    [Fact]
    public async Task MigrarDesdeF6_ConservaCollationNocase_IndicesYChecksNuevos()
    {
        var (connection, options) = await CrearBaseF6ConDatosAsync();
        await using var _ = connection;
        await using var context = new PesajeDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync();

        // NOCASE sobrevive en Ganaderos.Codigo: "ah" choca con "AH".
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Ganaderos (Codigo, Nombre, Activo, CreadoEn) VALUES ('ah', 'REPETIDO', 1, '2026-10-01 10:00:00');"));

        // CHECK del código del ganadero: 1 a 5 caracteres.
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Ganaderos (Codigo, Nombre, Activo, CreadoEn) VALUES ('ABCDEF', 'LARGO', 1, '2026-10-01 10:00:00');"));

        // Fleteros: sin tope de 4 (código 5, 250), pero único incluso entre inactivos y >= 1.
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Fleteros (Codigo, Nombre, Celular, Activo, CreadoEn) VALUES (250, 'FLETERO 250', '70000250', 1, '2026-10-01 10:00:00');");
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Fleteros (Codigo, Nombre, Celular, Activo, CreadoEn) VALUES (250, 'OTRO', '70000251', 0, '2026-10-01 10:00:00');"));
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Fleteros (Codigo, Nombre, Celular, Activo, CreadoEn) VALUES (0, 'CERO', '70000252', 1, '2026-10-01 10:00:00');"));

        // Los CHECK de Pesajes siguen vivos tras reconstruir la tabla.
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE Pesajes SET Estado = 2 WHERE Id = 1;")); // anulación sin motivo

        // Un pesaje puede quedar con fletero y la FK lo protege.
        await context.Database.ExecuteSqlRawAsync("UPDATE Pesajes SET FleteroId = 1 WHERE Id = 1;");
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync("UPDATE Pesajes SET FleteroId = 999 WHERE Id = 1;"));
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync("DELETE FROM Fleteros WHERE Id = 1;"));

        // El índice de FleteroId existe.
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name IN ('IX_Pesajes_FleteroId', 'IX_Fleteros_Codigo', 'IX_Ganaderos_Codigo');";
        Assert.Equal(3L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task MigrarDesdeCeroSinDatos_AplicaLaMigracionYElModeloCoincide()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);

        await using var context = new PesajeDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();

        var aplicadas = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.Contains("20261004133527_F10_ObservacionesCliente", aplicadas);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

public class MigracionF4Tests
{
    [Fact]
    public async Task MigrarDesdeF3_AgregaOrigenPeso_ConservaDatosYVerificaRestricciones()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        // 1. Migrar hasta F3_DelimitadorSignoIgual
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260925171853_F3_DelimitadorSignoIgual");

            // 2. Insertar con SQL crudo un usuario, un ganadero, un cliente y un pesaje válido
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO Usuarios (Id, NombreUsuario, Hash, Salt, Rol, NombreCompleto, Activo, DebeCambiarPassword, IntentosFallidos, CreadoEn)
                VALUES (10, 'operador_migracion', 'hash', 'salt', 3, 'Operador Migracion', 1, 0, 0, '2026-09-20 12:00:00');

                INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn)
                VALUES (20, 'GMIG', 'Ganadero Migracion', 1, '2026-09-20 12:00:00');

                INSERT INTO Clientes (Id, Codigo, Nombre, Direccion, Activo, CreadoEn)
                VALUES (30, 80001, 'Cliente Migracion', 'Direccion Migracion', 1, '2026-09-20 12:00:00');

                INSERT INTO Pesajes (Id, NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, Observacion, Estado, UsuarioId, CreadoEn)
                VALUES (1, 80001, '2026-09-20 12:00:00', 20, 30, 'Direccion Migracion', 1, 'BLANCO', 'M', 1, 106.0, 0.0, 106.0, 'SIN NOVEDAD', 1, 10, '2026-09-20 12:00:00');
            ");
        }

        // 3. Migrar hasta la última migración (F4_Pesaje)
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();

            // 4. Verificar: el pesaje sigue ahí con sus datos y OrigenPeso = 1 (Balanza)
            var pesaje = await context.Pesajes.AsNoTracking().SingleAsync(p => p.Id == 1);
            Assert.Equal(80001, pesaje.NumeroBoleta);
            Assert.Equal("GMIG", (await context.Ganaderos.FindAsync(pesaje.GanaderoId))!.Marca);
            Assert.Equal(106.0m, pesaje.PesoIzquierdo);
            Assert.Equal(0.0m, pesaje.PesoDerecho);
            Assert.Equal(106.0m, pesaje.PesoTotal);
            Assert.Equal(OrigenPeso.Balanza, pesaje.OrigenPeso);

            // Verificar PRAGMA foreign_key_check
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check;";
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.False(await reader.ReadAsync(), "PRAGMA foreign_key_check devolvió violaciones de FK tras la migración F4.");
            }

            // Verificar que UPDATE con OrigenPeso = 9 falla por CHECK constraint
            await Assert.ThrowsAnyAsync<SqliteException>(async () =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE Pesajes SET OrigenPeso = 9 WHERE Id = 1;");
            });
        }
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Tests.Builders;
using Xunit;

namespace PesajeFaeneo.Tests;

public class MigracionF2Tests
{
    [Fact]
    public async Task MigrarDesdeF1_ConservaDatosYRelaciones()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        // 1. Migrar hasta F1_BloqueoUsuario
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260924161919_F1_BloqueoUsuario");

            // 2. Insertar usuario, ganadero con Ñ, cliente 110 y pesaje. SQL crudo: el modelo actual ya no coincide con el
            //    esquema de F1 (p. ej. la columna Ganaderos.Marca pasó a Codigo en F10).
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO Usuarios (Id, NombreUsuario, Hash, Salt, Rol, NombreCompleto, Activo, DebeCambiarPassword, IntentosFallidos, CreadoEn)
                VALUES (1, 'operador_migracion', 'hash_dummy', 'salt_dummy', 3, 'Operador Migración', 1, 0, 0, '2026-09-20 12:00:00');

                INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn)
                VALUES (1, 'ÑA', 'PEÑA', 1, '2026-09-20 12:00:00');

                INSERT INTO Clientes (Id, Codigo, Nombre, Direccion, Activo, CreadoEn)
                VALUES (1, 110, 'CLIENTE 110', 'DIRECCION 110', 1, '2026-09-20 12:00:00');

                INSERT INTO Pesajes (NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, Observacion, Estado, UsuarioId, CreadoEn)
                VALUES (5001, '2026-09-20 12:00:00', 1, 1, 'FRIAL EJEMPLO', 1, 'BLANCO', 'M', 1, 106.0, 0.0, 106.0, 'SIN NOVEDAD', 1, 1, '2026-09-20 12:00:00');
            ");
        }

        // 3. Migrar hasta la última (F2_Catalogos)
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();

            // 4. Verificar datos intactos
            var ganaderoRecuperado = await context.Ganaderos.SingleOrDefaultAsync(g => g.Codigo == "ÑA");
            Assert.NotNull(ganaderoRecuperado);
            Assert.Equal("PEÑA", ganaderoRecuperado.Nombre);

            var clienteRecuperado = await context.Clientes.SingleOrDefaultAsync(c => c.Codigo == 110);
            Assert.NotNull(clienteRecuperado);
            Assert.Equal("CLIENTE 110", clienteRecuperado.Nombre);

            var pesajeRecuperado = await context.Pesajes.SingleOrDefaultAsync(p => p.NumeroBoleta == 5001);
            Assert.NotNull(pesajeRecuperado);
            Assert.Equal(ganaderoRecuperado.Id, pesajeRecuperado.GanaderoId);
            Assert.Equal(clienteRecuperado.Id, pesajeRecuperado.ClienteId);

            // Verificar PRAGMA foreign_key_check
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check;";
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.False(await reader.ReadAsync(), "PRAGMA foreign_key_check devolvió violaciones de FK tras la migración.");
            }

            // Verificar PRAGMA foreign_keys = 1
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys;";
                var fkActivas = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                Assert.Equal(1L, fkActivas);
            }

            // Verificar que collation NOCASE sobrevivió (insertar "NA" y luego "na" debe fallar)
            var ganaderoNA = new Ganadero
            {
                Codigo = "NA",
                Nombre = "GANADERO NA",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };
            context.Ganaderos.Add(ganaderoNA);
            await context.SaveChangesAsync();

            var ganaderoNaMinuscula = new Ganadero
            {
                Codigo = "na",
                Nombre = "GANADERO NA MINUSCULA",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };
            context.Ganaderos.Add(ganaderoNaMinuscula);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}

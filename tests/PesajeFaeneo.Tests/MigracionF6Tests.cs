using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using Xunit;

namespace PesajeFaeneo.Tests;

public class MigracionF6Tests
{
    [Fact]
    public async Task MigrarDesdeF4_AgregaVersion_ConservaDatosYVerificaRestricciones()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var optionsBuilder = new DbContextOptionsBuilder<PesajeDbContext>();
        optionsBuilder.ConfigurarSqlite(connection);
        var options = optionsBuilder.Options;

        // 1. Migrar hasta 20260927144821_F4_Pesaje
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260927144821_F4_Pesaje");

            // 2. Insertar con SQL crudo un usuario, un ganadero, un cliente, un pesaje vigente y otro anulado
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO Usuarios (Id, NombreUsuario, Hash, Salt, Rol, NombreCompleto, Activo, DebeCambiarPassword, IntentosFallidos, CreadoEn)
                VALUES (10, 'operador_f6', 'hash', 'salt', 3, 'Operador F6', 1, 0, 0, '2026-09-20 12:00:00');

                INSERT INTO Ganaderos (Id, Marca, Nombre, Activo, CreadoEn)
                VALUES (20, 'GF6', 'Ganadero F6', 1, '2026-09-20 12:00:00');

                INSERT INTO Clientes (Id, Codigo, Nombre, Direccion, Activo, CreadoEn)
                VALUES (30, 80001, 'Cliente F6', 'Direccion F6', 1, '2026-09-20 12:00:00');

                INSERT INTO Pesajes (Id, NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, OrigenPeso, Observacion, Estado, UsuarioId, CreadoEn)
                VALUES (1, 80001, '2026-09-20 12:00:00', 20, 30, 'Direccion 1', 1, 'BLANCO', 'M', 1, 106.0, 0.0, 106.0, 1, 'VIGENTE', 1, 10, '2026-09-20 12:00:00');

                INSERT INTO Pesajes (Id, NumeroBoleta, FechaHora, GanaderoId, ClienteId, Direccion, Secuencia, ColorRaza, Sexo, TipoDesposte, PesoIzquierdo, PesoDerecho, PesoTotal, OrigenPeso, Observacion, Estado, MotivoAnulacion, AnuladoPorId, AnuladoEn, UsuarioId, CreadoEn)
                VALUES (2, 80002, '2026-09-20 12:30:00', 20, 30, 'Direccion 2', 2, 'NEGRO', 'H', 1, 108.0, 0.0, 108.0, 1, 'ANULADO', 2, 'ERROR TIPEO', 10, '2026-09-20 13:00:00', 10, '2026-09-20 12:30:00');
            ");
        }

        // 3. Migrar hasta la última migración (F6_VersionPesaje)
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();

            // 4. Verificar: ambos pesajes siguen ahí, con sus datos y Version == 0
            var p1 = await context.Pesajes.AsNoTracking().SingleAsync(p => p.Id == 1);
            Assert.Equal(80001, p1.NumeroBoleta);
            Assert.Equal(EstadoPesaje.Vigente, p1.Estado);
            Assert.Equal(0, p1.Version);

            var p2 = await context.Pesajes.AsNoTracking().SingleAsync(p => p.Id == 2);
            Assert.Equal(80002, p2.NumeroBoleta);
            Assert.Equal(EstadoPesaje.Anulado, p2.Estado);
            Assert.Equal("ERROR TIPEO", p2.MotivoAnulacion);
            Assert.Equal(10, p2.AnuladoPorId);
            Assert.Equal(0, p2.Version);

            // 5. Verificar PRAGMA foreign_key_check
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_key_check;";
                await using var reader = await cmd.ExecuteReaderAsync();
                Assert.False(await reader.ReadAsync(), "PRAGMA foreign_key_check devolvió violaciones de FK tras la migración F6.");
            }

            // 6. Verificar que UPDATE sin motivo al pasar a Anulado falla por CHECK constraint CK_Pesajes_Anulacion
            await Assert.ThrowsAnyAsync<SqliteException>(async () =>
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE Pesajes SET Estado = 2 WHERE Id = 1;");
            });
        }
    }
}

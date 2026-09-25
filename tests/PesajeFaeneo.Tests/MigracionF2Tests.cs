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

            // 2. Insertar usuario, ganadero con Ñ, cliente 110 y pesaje
            var usuario = new Usuario
            {
                NombreUsuario = "operador_migracion",
                NombreCompleto = "Operador Migración",
                Rol = Rol.Operador,
                Hash = "hash_dummy",
                Salt = "salt_dummy",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };
            var ganadero = new Ganadero
            {
                Marca = "ÑA",
                Nombre = "PEÑA",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };
            var cliente = new Cliente
            {
                Codigo = 110,
                Nombre = "CLIENTE 110",
                Direccion = "DIRECCION 110",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };

            context.Usuarios.Add(usuario);
            context.Ganaderos.Add(ganadero);
            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();

            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(5001)
                .ConGanaderoId(ganadero.Id)
                .ConClienteId(cliente.Id)
                .ConUsuarioId(usuario.Id)
                .Build();

            context.Pesajes.Add(pesaje);
            await context.SaveChangesAsync();
        }

        // 3. Migrar hasta la última (F2_Catalogos)
        await using (var context = new PesajeDbContext(options))
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();

            // 4. Verificar datos intactos
            var ganaderoRecuperado = await context.Ganaderos.SingleOrDefaultAsync(g => g.Marca == "ÑA");
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
                Marca = "NA",
                Nombre = "GANADERO NA",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };
            context.Ganaderos.Add(ganaderoNA);
            await context.SaveChangesAsync();

            var ganaderoNaMinuscula = new Ganadero
            {
                Marca = "na",
                Nombre = "GANADERO NA MINUSCULA",
                Activo = true,
                CreadoEn = DateTime.UtcNow
            };
            context.Ganaderos.Add(ganaderoNaMinuscula);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}

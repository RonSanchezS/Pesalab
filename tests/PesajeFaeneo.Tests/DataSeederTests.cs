using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class DataSeederTests : IClassFixture<SqliteDbFixture>
{
    private readonly SqliteDbFixture _fixture;

    public DataSeederTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SembrarAsync_EnBdVacia_CreaAdminIdempotente()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var seeder = new DataSeeder(_fixture.ContextFactory, hasher);

        // Limpiar usuarios por si otro test insertó algo
        await using (var ctx = await _fixture.ContextFactory.CreateDbContextAsync())
        {
            ctx.Usuarios.RemoveRange(ctx.Usuarios);
            await ctx.SaveChangesAsync();
        }

        // 1ra ejecución
        await seeder.SembrarAsync();

        await using (var ctx = await _fixture.ContextFactory.CreateDbContextAsync())
        {
            var usuarios = await ctx.Usuarios.ToListAsync();
            Assert.Single(usuarios);

            var admin = usuarios[0];
            Assert.Equal("admin", admin.NombreUsuario);
            Assert.Equal(Rol.Administrador, admin.Rol);
            Assert.True(admin.Activo);
            Assert.True(admin.DebeCambiarPassword);
            Assert.NotEqual("admin", admin.Hash);
            Assert.True(hasher.Verificar("admin", admin.Hash, admin.Salt));
        }

        // 2da ejecución (idempotencia)
        await seeder.SembrarAsync();

        await using (var ctx = await _fixture.ContextFactory.CreateDbContextAsync())
        {
            var usuarios = await ctx.Usuarios.ToListAsync();
            Assert.Single(usuarios);
        }
    }
}

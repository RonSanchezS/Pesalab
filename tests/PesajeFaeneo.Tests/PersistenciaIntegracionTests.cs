using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class PersistenciaIntegracionTests : IClassFixture<SqliteDbFixture>
{
    private readonly SqliteDbFixture _fixture;

    public PersistenciaIntegracionTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Ganadero_MarcaDuplicada_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var g1 = new GanaderoBuilder().ConMarca("AH").ConNombre("Ganadero 1").Build();
        var g2 = new GanaderoBuilder().ConMarca("AH").ConNombre("Ganadero 2").Build();

        ctx.Ganaderos.Add(g1);
        await ctx.SaveChangesAsync();

        ctx.Ganaderos.Add(g2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Cliente_CodigoDuplicado_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var c1 = new ClienteBuilder().ConCodigo(999).ConNombre("Cliente 1").Build();
        var c2 = new ClienteBuilder().ConCodigo(999).ConNombre("Cliente 2").Build();

        ctx.Clientes.Add(c1);
        await ctx.SaveChangesAsync();

        ctx.Clientes.Add(c2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Fletero_OrdenFueraDeRango_LanzaDbUpdateException()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var fleteroInvalido = new Fletero
        {
            Orden = 5, // Inválido: CHECK es BETWEEN 1 AND 4
            Nombre = "Fletero Inválido",
            Celular = "77712345",
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        ctx.Fleteros.Add(fleteroInvalido);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Fletero_IndiceFiltrado_PermiteDosInactivosConMismoOrdenPeroNoDosActivos()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var fActivo1 = new Fletero { Orden = 2, Nombre = "F1", Celular = "70000001", Activo = true, CreadoEn = DateTime.UtcNow };
        var fInactivo1 = new Fletero { Orden = 2, Nombre = "F2", Celular = "70000002", Activo = false, CreadoEn = DateTime.UtcNow };
        var fActivo2 = new Fletero { Orden = 2, Nombre = "F3", Celular = "70000003", Activo = true, CreadoEn = DateTime.UtcNow };

        ctx.Fleteros.AddRange(fActivo1, fInactivo1);
        await ctx.SaveChangesAsync(); // Debe permitir activo + inactivo con mismo orden

        ctx.Fleteros.Add(fActivo2);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync()); // Debe fallar segundo activo con orden 2
    }

    [Fact]
    public async Task Pesaje_PesoDecimal_AlmacenadoComoReal_RecuperaValorExacto()
    {
        await using var ctx = await _fixture.ContextFactory.CreateDbContextAsync();

        var ganadero = new GanaderoBuilder().ConMarca("ZZ").ConNombre("Ganadero ZZ").Build();
        var cliente = new ClienteBuilder().ConCodigo(888).ConNombre("Cliente 888").Build();
        var usuario = new UsuarioBuilder().ConNombreUsuario("pesador1").Build();

        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var pesaje = new PesajeBuilder()
            .ConNumeroBoleta(99991)
            .ConGanaderoId(ganadero.Id)
            .ConClienteId(cliente.Id)
            .ConUsuarioId(usuario.Id)
            .ConPesos(120.5m, 0.0m)
            .Build();

        ctx.Pesajes.Add(pesaje);
        await ctx.SaveChangesAsync();

        // Limpiar seguimiento y releer
        ctx.ChangeTracker.Clear();

        var releido = await ctx.Pesajes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pesaje.Id);
        Assert.NotNull(releido);
        Assert.Equal(120.5m, releido.PesoIzquierdo);
        Assert.Equal(0.0m, releido.PesoDerecho);
        Assert.Equal(120.5m, releido.PesoTotal);
    }
}

using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class PesajeVersionTests
{
    private static async Task<(int GanaderoId, int ClienteId, int UsuarioId)> SembrarCatalogosAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var g = new GanaderoBuilder().ConCodigo("GV").ConNombre("Ganadero Version").Build();
        var c = new ClienteBuilder().ConCodigo(901).ConNombre("Cliente Version").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("operador_v").ConNombreCompleto("Operador V").Build();

        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();

        return (g.Id, c.Id, u.Id);
    }

    [Fact]
    public async Task InsertarPesaje_VersionInicialEsCero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(1001)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
            Assert.Equal(0, p.Version);
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var leido = await ctx.Pesajes.AsNoTracking().SingleAsync(p => p.Id == pesajeId);
            Assert.Equal(0, leido.Version);
        }
    }

    [Fact]
    public async Task ModificarPesaje_ConSaveChangesAsync_IncrementaVersionConsecutivamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(1002)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Modificar una vez
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.SingleAsync(x => x.Id == pesajeId);
            p.Observacion = "PRIMERA EDICION";
            await ctx.SaveChangesAsync();
            Assert.Equal(1, p.Version);
        }

        // Modificar segunda vez
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(1, p.Version);
            p.Observacion = "SEGUNDA EDICION";
            await ctx.SaveChangesAsync();
            Assert.Equal(2, p.Version);
        }

        // Verificar en BD
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(2, p.Version);
            Assert.Equal("SEGUNDA EDICION", p.Observacion);
        }
    }

    [Fact]
    public async Task ModificarPesaje_ConSaveChangesSincrono_IncrementaVersion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(1003)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            ctx.SaveChanges();
            pesajeId = p.Id;
            Assert.Equal(0, p.Version);
        }

        using (var ctx = db.ContextFactory.CreateDbContext())
        {
            var p = ctx.Pesajes.Single(x => x.Id == pesajeId);
            p.Observacion = "MODIFICADO SINCRONO";
            ctx.SaveChanges();
            Assert.Equal(1, p.Version);
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(1, p.Version);
            Assert.Equal("MODIFICADO SINCRONO", p.Observacion);
        }
    }

    [Fact]
    public async Task GuardarSinModificar_NoIncrementaVersion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(1004)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.SingleAsync(x => x.Id == pesajeId);
            // No hacemos cambios
            await ctx.SaveChangesAsync();
            Assert.Equal(0, p.Version);
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(0, p.Version);
        }
    }

    [Fact]
    public async Task ConcurrenciaOptimista_DosContextosModificanMismoPesaje_SegundoFallaConDbUpdateConcurrencyException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(1005)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        await using var ctx1 = await db.ContextFactory.CreateDbContextAsync();
        await using var ctx2 = await db.ContextFactory.CreateDbContextAsync();

        var p1 = await ctx1.Pesajes.SingleAsync(x => x.Id == pesajeId);
        var p2 = await ctx2.Pesajes.SingleAsync(x => x.Id == pesajeId);

        p1.Observacion = "CAMBIO CONTEXTO 1";
        p2.Observacion = "CAMBIO CONTEXTO 2";

        // El primer contexto guarda exitosamente
        await ctx1.SaveChangesAsync();
        Assert.Equal(1, p1.Version);

        // El segundo contexto falla con DbUpdateConcurrencyException
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await ctx2.SaveChangesAsync();
        });

        // La base de datos tiene el valor del primer contexto y Version == 1
        await using (var ctxCheck = await db.ContextFactory.CreateDbContextAsync())
        {
            var leido = await ctxCheck.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(1, leido.Version);
            Assert.Equal("CAMBIO CONTEXTO 1", leido.Observacion);
        }
    }

    [Fact]
    public async Task AnulacionDePesaje_IncrementaVersion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(1006)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.SingleAsync(x => x.Id == pesajeId);
            p.Estado = EstadoPesaje.Anulado;
            p.MotivoAnulacion = "ANULACION POR TEST";
            p.AnuladoPorId = uId;
            p.AnuladoEn = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
            Assert.Equal(1, p.Version);
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.Pesajes.AsNoTracking().SingleAsync(x => x.Id == pesajeId);
            Assert.Equal(1, p.Version);
            Assert.Equal(EstadoPesaje.Anulado, p.Estado);
            Assert.Equal("ANULACION POR TEST", p.MotivoAnulacion);
        }
    }

    [Fact]
    public async Task PesajeAgregado_NoModificaVersion_QuedaEnCero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var p = new PesajeBuilder()
            .ConNumeroBoleta(1007)
            .ConGanaderoId(gId)
            .ConClienteId(cId)
            .ConUsuarioId(uId)
            .Build();

        ctx.Pesajes.Add(p);
        Assert.Equal(EntityState.Added, ctx.Entry(p).State);
        await ctx.SaveChangesAsync();

        Assert.Equal(0, p.Version);
    }
}

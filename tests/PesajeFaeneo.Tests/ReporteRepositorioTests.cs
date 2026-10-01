using Microsoft.EntityFrameworkCore;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia.Repositorios;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ReporteRepositorioTests
{
    private static async Task<(int GanaderoId1, int GanaderoId2, int ClienteId1, int ClienteId2, int UsuarioId)> SembrarCatalogosAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var g1 = new GanaderoBuilder().ConMarca("G1").ConNombre("Ganadero 1").Build();
        var g2 = new GanaderoBuilder().ConMarca("G2").ConNombre("Ganadero 2").Build();
        var c1 = new ClienteBuilder().ConCodigo(101).ConNombre("Cliente 1").Build();
        var c2 = new ClienteBuilder().ConCodigo(102).ConNombre("Cliente 2").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("operador_rep").ConNombreCompleto("Operador Reporte").Build();

        ctx.Ganaderos.AddRange(g1, g2);
        ctx.Clientes.AddRange(c1, c2);
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();

        return (g1.Id, g2.Id, c1.Id, c2.Id, u.Id);
    }

    [Fact]
    public async Task ListarParaReporteAsync_Rango_LimiteInferiorInclusivoYSuperiorExclusivo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, _, c1, _, u) = await SembrarCatalogosAsync(db);

        var desdeUtc = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var hastaUtc = new DateTime(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p1 = new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(desdeUtc).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();
            var p2 = new PesajeBuilder().ConNumeroBoleta(2).ConFechaHora(desdeUtc.AddHours(2)).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();
            var p3 = new PesajeBuilder().ConNumeroBoleta(3).ConFechaHora(hastaUtc).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build(); // justo en HastaUtc: NO debe salir
            var p4 = new PesajeBuilder().ConNumeroBoleta(4).ConFechaHora(desdeUtc.AddMinutes(-1)).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build(); // antes: NO debe salir

            ctx.Pesajes.AddRange(p1, p2, p3, p4);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var criterios = new CriteriosReporte(desdeUtc, hastaUtc, EstadoPesaje.Vigente, null, null);
            var resultados = await repo.ListarParaReporteAsync(criterios, 100);

            Assert.Equal(2, resultados.Count);
            Assert.Contains(resultados, p => p.NumeroBoleta == 1);
            Assert.Contains(resultados, p => p.NumeroBoleta == 2);
            Assert.DoesNotContain(resultados, p => p.NumeroBoleta == 3);
            Assert.DoesNotContain(resultados, p => p.NumeroBoleta == 4);
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_Estado_FiltraVigenteOAnuladoEstrictamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, _, c1, _, u) = await SembrarCatalogosAsync(db);

        var fechaUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var pVigente = new PesajeBuilder().ConNumeroBoleta(10).ConFechaHora(fechaUtc).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();
            var pAnulado = new PesajeBuilder().ConNumeroBoleta(11).ConFechaHora(fechaUtc.AddMinutes(5)).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u)
                .Anulado("ERROR DE PRUEBA", u).Build();

            ctx.Pesajes.AddRange(pVigente, pAnulado);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var criteriosVig = new CriteriosReporte(fechaUtc.AddHours(-1), fechaUtc.AddHours(1), EstadoPesaje.Vigente, null, null);
            var vigentes = await repo.ListarParaReporteAsync(criteriosVig, 100);

            Assert.Single(vigentes);
            Assert.Equal(10, vigentes[0].NumeroBoleta);

            var criteriosAnul = new CriteriosReporte(fechaUtc.AddHours(-1), fechaUtc.AddHours(1), EstadoPesaje.Anulado, null, null);
            var anuladas = await repo.ListarParaReporteAsync(criteriosAnul, 100);

            Assert.Single(anuladas);
            Assert.Equal(11, anuladas[0].NumeroBoleta);
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_FiltroGanaderoYCliente_FiltraIndividualYCombinado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, g2, c1, c2, u) = await SembrarCatalogosAsync(db);

        var fechaUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p1 = new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(fechaUtc).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();
            var p2 = new PesajeBuilder().ConNumeroBoleta(2).ConFechaHora(fechaUtc).ConGanaderoId(g1).ConClienteId(c2).ConUsuarioId(u).Build();
            var p3 = new PesajeBuilder().ConNumeroBoleta(3).ConFechaHora(fechaUtc).ConGanaderoId(g2).ConClienteId(c1).ConUsuarioId(u).Build();

            ctx.Pesajes.AddRange(p1, p2, p3);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var rangoD = fechaUtc.AddHours(-1);
            var rangoH = fechaUtc.AddHours(1);

            // Solo ganadero g1
            var porG = await repo.ListarParaReporteAsync(new CriteriosReporte(rangoD, rangoH, EstadoPesaje.Vigente, g1, null), 100);
            Assert.Equal(2, porG.Count);

            // Solo cliente c1
            var porC = await repo.ListarParaReporteAsync(new CriteriosReporte(rangoD, rangoH, EstadoPesaje.Vigente, null, c1), 100);
            Assert.Equal(2, porC.Count);

            // Combinado g1 + c1
            var porAmbos = await repo.ListarParaReporteAsync(new CriteriosReporte(rangoD, rangoH, EstadoPesaje.Vigente, g1, c1), 100);
            Assert.Single(porAmbos);
            Assert.Equal(1, porAmbos[0].NumeroBoleta);

            // Sin filtros ganadero/cliente
            var todos = await repo.ListarParaReporteAsync(new CriteriosReporte(rangoD, rangoH, EstadoPesaje.Vigente, null, null), 100);
            Assert.Equal(3, todos.Count);
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_Orden_FechaHoraAscendenteYNumeroBoletaAscendente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, _, c1, _, u) = await SembrarCatalogosAsync(db);

        var t1 = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Mismo FechaHora, números 20 y 10: debe salir 10 primero
            var p1 = new PesajeBuilder().ConNumeroBoleta(20).ConFechaHora(t2).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();
            var p2 = new PesajeBuilder().ConNumeroBoleta(10).ConFechaHora(t2).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();

            // FechaHora anterior (t1) con número mayor (99): debe salir primero por fecha (R31)
            var p3 = new PesajeBuilder().ConNumeroBoleta(99).ConFechaHora(t1).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();

            ctx.Pesajes.AddRange(p1, p2, p3);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var res = await repo.ListarParaReporteAsync(new CriteriosReporte(t1.AddHours(-1), t2.AddHours(1), EstadoPesaje.Vigente, null, null), 100);

            Assert.Equal(3, res.Count);
            Assert.Equal(99, res[0].NumeroBoleta); // t1
            Assert.Equal(10, res[1].NumeroBoleta); // t2, boleta 10
            Assert.Equal(20, res[2].NumeroBoleta); // t2, boleta 20
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_Maximo_RespetaLimiteDeFilas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, _, c1, _, u) = await SembrarCatalogosAsync(db);

        var fechaUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 5; i++)
            {
                ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(i).ConFechaHora(fechaUtc.AddMinutes(i)).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build());
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var res = await repo.ListarParaReporteAsync(new CriteriosReporte(fechaUtc.AddHours(-1), fechaUtc.AddHours(1), EstadoPesaje.Vigente, null, null), 3);

            Assert.Equal(3, res.Count);
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_MaximoCeroOMenor_LanzaArgumentOutOfRangeException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var repo = new PesajeRepositorio(ctx);

        var criterios = new CriteriosReporte(DateTime.UtcNow, DateTime.UtcNow.AddDays(1), EstadoPesaje.Vigente, null, null);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ListarParaReporteAsync(criterios, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ListarParaReporteAsync(criterios, -5));
    }

    [Fact]
    public async Task ListarParaReporteAsync_CargaNavegacionesSinTracking()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, _, c1, _, u) = await SembrarCatalogosAsync(db);

        var fechaUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var pVigente = new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(fechaUtc).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build();
            var pAnulado = new PesajeBuilder().ConNumeroBoleta(2).ConFechaHora(fechaUtc.AddMinutes(5)).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u)
                .Anulado("MOTIVO ANULACION", u).Build();

            ctx.Pesajes.AddRange(pVigente, pAnulado);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);

            var vigentes = await repo.ListarParaReporteAsync(new CriteriosReporte(fechaUtc.AddHours(-1), fechaUtc.AddHours(1), EstadoPesaje.Vigente, null, null), 10);
            Assert.Single(vigentes);
            Assert.NotNull(vigentes[0].Ganadero);
            Assert.NotNull(vigentes[0].Cliente);
            Assert.Null(vigentes[0].AnuladoPor);

            var anuladas = await repo.ListarParaReporteAsync(new CriteriosReporte(fechaUtc.AddHours(-1), fechaUtc.AddHours(1), EstadoPesaje.Anulado, null, null), 10);
            Assert.Single(anuladas);
            Assert.NotNull(anuladas[0].Ganadero);
            Assert.NotNull(anuladas[0].Cliente);
            Assert.NotNull(anuladas[0].AnuladoPor);

            Assert.Empty(ctx.ChangeTracker.Entries());
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_PesosDecimalesExactos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, _, c1, _, u) = await SembrarCatalogosAsync(db);

        var fechaUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(50)
                .ConFechaHora(fechaUtc)
                .ConGanaderoId(g1)
                .ConClienteId(c1)
                .ConUsuarioId(u)
                .ConPesos(109.3m, 106.7m)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var res = await repo.ListarParaReporteAsync(new CriteriosReporte(fechaUtc.AddHours(-1), fechaUtc.AddHours(1), EstadoPesaje.Vigente, null, null), 10);

            Assert.Single(res);
            Assert.Equal(109.3m, res[0].PesoIzquierdo);
            Assert.Equal(106.7m, res[0].PesoDerecho);
            Assert.Equal(216.0m, res[0].PesoTotal);
        }
    }

    [Fact]
    public async Task ListarParaReporteAsync_SinCoincidencias_RetornaListaVacia()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var repo = new PesajeRepositorio(ctx);

        var criterios = new CriteriosReporte(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc), EstadoPesaje.Vigente, null, null);
        var res = await repo.ListarParaReporteAsync(criterios, 10);

        Assert.Empty(res);
    }

    [Fact]
    public async Task ConsultaReporte_CriteriosNulos_LanzaArgumentNullException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var repo = new PesajeRepositorio(ctx);

        Assert.Throws<ArgumentNullException>(() => repo.ConsultaReporte(null!, 10));
    }
}

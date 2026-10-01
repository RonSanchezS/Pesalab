using System.Globalization;
using Microsoft.EntityFrameworkCore;
using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Persistencia.Repositorios;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class GestionRepositoriosTests
{
    private static async Task<(int GanaderoId, int ClienteId, int UsuarioId)> SembrarCatalogosAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var g = new GanaderoBuilder().ConMarca("GH").ConNombre("Ganadero Repos").Build();
        var c = new ClienteBuilder().ConCodigo(777).ConNombre("Cliente Repos").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("operador_repo").ConNombreCompleto("Operador Repo").Build();

        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();

        return (g.Id, c.Id, u.Id);
    }

    [Fact]
    public async Task ObtenerParaEdicionAsync_DevuelveEntidadTrackedYPersisteCambios()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(2001)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        // Leer con ObtenerParaEdicionAsync en un nuevo contexto: debe estar TRACKED
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var pesaje = await repo.ObtenerParaEdicionAsync(pesajeId);

            Assert.NotNull(pesaje);
            Assert.NotNull(pesaje.Ganadero);
            Assert.NotNull(pesaje.Cliente);
            Assert.Single(ctx.ChangeTracker.Entries<Pesaje>());

            pesaje.Observacion = "OBSERVACION MODIFICADA TRACKED";
            await ctx.SaveChangesAsync();
        }

        // Releer con otro contexto para comprobar persistencia
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var leido = await ctx.Pesajes.AsNoTracking().SingleAsync(p => p.Id == pesajeId);
            Assert.Equal("OBSERVACION MODIFICADA TRACKED", leido.Observacion);
        }
    }

    [Fact]
    public async Task ObtenerParaEdicionAsync_Inexistente_RetornaNull()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var repo = new PesajeRepositorio(ctx);

        var pesaje = await repo.ObtenerParaEdicionAsync(99999);
        Assert.Null(pesaje);
    }

    [Fact]
    public async Task BuscarAsync_PorRangoDeFechas_LimiteSuperiorEsExclusivo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        var d1 = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
        var d3 = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc); // Límite superior exacto

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.AddRange(
                new PesajeBuilder().ConNumeroBoleta(2010).ConFechaHora(d1).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(2011).ConFechaHora(d2).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(2012).ConFechaHora(d3).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build()
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var criterios = new CriteriosBusquedaPesaje(d1, d3, null, null, null, null);
            var resultados = await repo.BuscarAsync(criterios, 100);

            // Debe incluir d1 y d2, pero EXCLUIR d3 porque HastaUtc es exclusivo
            Assert.Equal(2, resultados.Count);
            Assert.Contains(resultados, p => p.NumeroBoleta == 2010);
            Assert.Contains(resultados, p => p.NumeroBoleta == 2011);
            Assert.DoesNotContain(resultados, p => p.NumeroBoleta == 2012);
        }
    }

    [Fact]
    public async Task BuscarAsync_PorNumeroBoleta_RetornaBoletaExacta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.AddRange(
                new PesajeBuilder().ConNumeroBoleta(3001).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(3002).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build()
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var criterios = new CriteriosBusquedaPesaje(null, null, 3002, null, null, null);
            var resultados = await repo.BuscarAsync(criterios, 10);

            var b = Assert.Single(resultados);
            Assert.Equal(3002, b.NumeroBoleta);
        }
    }

    [Fact]
    public async Task BuscarAsync_PorGanaderoClienteYEstado_FiltraCorrectamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, c1, uId) = await SembrarCatalogosAsync(db);

        int g2, c2;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var gan2 = new GanaderoBuilder().ConMarca("G2").ConNombre("Ganadero 2").Build();
            var cli2 = new ClienteBuilder().ConCodigo(888).ConNombre("Cliente 2").Build();
            ctx.Ganaderos.Add(gan2);
            ctx.Clientes.Add(cli2);
            await ctx.SaveChangesAsync();
            g2 = gan2.Id;
            c2 = cli2.Id;

            ctx.Pesajes.AddRange(
                new PesajeBuilder().ConNumeroBoleta(4001).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(4002).ConGanaderoId(g2).ConClienteId(c1).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(4003).ConGanaderoId(g1).ConClienteId(c2).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(4004).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(uId).Anulado("ANULADA", uId).Build()
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);

            // Filtrar por Ganadero G1
            var resG1 = await repo.BuscarAsync(new CriteriosBusquedaPesaje(null, null, null, g1, null, null), 10);
            Assert.Equal(3, resG1.Count);

            // Filtrar por Cliente C2
            var resC2 = await repo.BuscarAsync(new CriteriosBusquedaPesaje(null, null, null, null, c2, null), 10);
            Assert.Single(resC2);
            Assert.Equal(4003, resC2[0].NumeroBoleta);

            // Filtrar por Estado Anulado
            var resAnulados = await repo.BuscarAsync(new CriteriosBusquedaPesaje(null, null, null, null, null, EstadoPesaje.Anulado), 10);
            Assert.Single(resAnulados);
            Assert.Equal(4004, resAnulados[0].NumeroBoleta);

            // Combinación AND: Ganadero G1 Y Estado Vigente
            var resAnd = await repo.BuscarAsync(new CriteriosBusquedaPesaje(null, null, null, g1, null, EstadoPesaje.Vigente), 10);
            Assert.Equal(2, resAnd.Count);
            Assert.Contains(resAnd, p => p.NumeroBoleta == 4001);
            Assert.Contains(resAnd, p => p.NumeroBoleta == 4003);
        }
    }

    [Fact]
    public async Task BuscarAsync_SinFiltros_DevuelveTodoOrdenadoPorNumeroDescendente_RespetandoMaximoYSinTracking()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (gId, cId, uId) = await SembrarCatalogosAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 5; i++)
            {
                ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(5000 + i).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var criterios = new CriteriosBusquedaPesaje(null, null, null, null, null, null);
            var resultados = await repo.BuscarAsync(criterios, 3);

            // maximo = 3 respetado
            Assert.Equal(3, resultados.Count);
            // Orden descendente por NumeroBoleta: 5005, 5004, 5003
            Assert.Equal(5005, resultados[0].NumeroBoleta);
            Assert.Equal(5004, resultados[1].NumeroBoleta);
            Assert.Equal(5003, resultados[2].NumeroBoleta);

            // Navegaciones cargadas
            Assert.NotNull(resultados[0].Ganadero);
            Assert.NotNull(resultados[0].Cliente);

            // Sin tracking en el contexto
            Assert.Empty(ctx.ChangeTracker.Entries());
        }
    }

    [Fact]
    public async Task ConsultarAuditoriaAsync_PorRangoDeFechas_LimiteSuperiorExclusivo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, _, uId) = await SembrarCatalogosAsync(db);

        var d1 = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var d3 = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Auditorias.AddRange(
                new AuditoriaEntidad { FechaHora = d1, UsuarioId = uId, Accion = AccionAuditoria.Crear, Entidad = "Pesaje", EntidadId = "1" },
                new AuditoriaEntidad { FechaHora = d2, UsuarioId = uId, Accion = AccionAuditoria.Modificar, Entidad = "Pesaje", EntidadId = "1" },
                new AuditoriaEntidad { FechaHora = d3, UsuarioId = uId, Accion = AccionAuditoria.Anular, Entidad = "Pesaje", EntidadId = "1" }
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new AuditoriaRepositorio(ctx);
            var criterios = new CriteriosAuditoria(d1, d3, null, null, null, null);
            var res = await repo.ConsultarAsync(criterios, 50);

            Assert.Equal(2, res.Count);
            Assert.Contains(res, a => a.Accion == AccionAuditoria.Crear);
            Assert.Contains(res, a => a.Accion == AccionAuditoria.Modificar);
            Assert.DoesNotContain(res, a => a.Accion == AccionAuditoria.Anular);
        }
    }

    [Fact]
    public async Task ConsultarAuditoriaAsync_PorEntidadAccionUsuarioYEntidadId()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, _, uId1) = await SembrarCatalogosAsync(db);

        int uId2;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var u2 = new UsuarioBuilder().ConNombreUsuario("operador_2").Build();
            ctx.Usuarios.Add(u2);
            await ctx.SaveChangesAsync();
            uId2 = u2.Id;

            ctx.Auditorias.AddRange(
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow, UsuarioId = uId1, Accion = AccionAuditoria.Crear, Entidad = "Pesaje", EntidadId = "10" },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow, UsuarioId = uId2, Accion = AccionAuditoria.Modificar, Entidad = "Pesaje", EntidadId = "10" },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow, UsuarioId = uId1, Accion = AccionAuditoria.Modificar, Entidad = "Ganadero", EntidadId = "2" }
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new AuditoriaRepositorio(ctx);

            // Filtrar por Entidad
            var porEntidad = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, "Ganadero", null, null, null), 10);
            Assert.Single(porEntidad);
            Assert.Equal("Ganadero", porEntidad[0].Entidad);

            // Filtrar por Accion
            var porAccion = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, null, AccionAuditoria.Crear, null, null), 10);
            Assert.Single(porAccion);
            Assert.Equal("10", porAccion[0].EntidadId);

            // Filtrar por Usuario
            var porUsuario = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, null, null, uId2, null), 10);
            Assert.Single(porUsuario);
            Assert.Equal(uId2, porUsuario[0].UsuarioId);

            // Filtrar por EntidadId
            var porEntidadId = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, null, null, null, "10"), 10);
            Assert.Equal(2, porEntidadId.Count);

            // Combinación AND: Entidad Pesaje Y Accion Modificar Y Usuario uId2
            var porAnd = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, "Pesaje", AccionAuditoria.Modificar, uId2, null), 10);
            Assert.Single(porAnd);
            Assert.Equal(uId2, porAnd[0].UsuarioId);
        }
    }

    [Fact]
    public async Task ConsultarAuditoriaAsync_OrdenFechaHoraDescendenteYDesempateIdDescendente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, _, uId) = await SembrarCatalogosAsync(db);

        var mismaFecha = new DateTime(2026, 9, 28, 14, 0, 0, DateTimeKind.Utc);

        long id1, id2, id3;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var a1 = new AuditoriaEntidad { FechaHora = mismaFecha, UsuarioId = uId, Accion = AccionAuditoria.Crear, Entidad = "Pesaje", EntidadId = "100" };
            var a2 = new AuditoriaEntidad { FechaHora = mismaFecha, UsuarioId = uId, Accion = AccionAuditoria.Modificar, Entidad = "Pesaje", EntidadId = "100" };
            var a3 = new AuditoriaEntidad { FechaHora = mismaFecha.AddHours(1), UsuarioId = uId, Accion = AccionAuditoria.Anular, Entidad = "Pesaje", EntidadId = "100" };

            ctx.Auditorias.AddRange(a1, a2, a3);
            await ctx.SaveChangesAsync();
            id1 = a1.Id;
            id2 = a2.Id;
            id3 = a3.Id;
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new AuditoriaRepositorio(ctx);
            var res = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, null, null, null, null), 10);

            Assert.Equal(3, res.Count);
            // El más reciente en FechaHora primero (a3)
            Assert.Equal(id3, res[0].Id);
            // Con la misma fecha, desempate por Id desc (a2 > a1)
            Assert.Equal(id2, res[1].Id);
            Assert.Equal(id1, res[2].Id);
        }
    }

    [Fact]
    public async Task ConsultarAuditoriaAsync_UsuarioNulo_DevuelveRegistroYNoRompeInclude()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Login fallido sin usuario asociado en BD
            ctx.Auditorias.Add(new AuditoriaEntidad
            {
                FechaHora = DateTime.UtcNow,
                UsuarioId = null,
                Accion = AccionAuditoria.LoginFallido,
                Entidad = "Usuario",
                EntidadId = "usuario_inexistente",
                Motivo = "Credenciales inválidas"
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new AuditoriaRepositorio(ctx);
            var res = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, null, null, null, null), 10);

            var a = Assert.Single(res);
            Assert.Null(a.UsuarioId);
            Assert.Null(a.Usuario);
            Assert.Equal(AccionAuditoria.LoginFallido, a.Accion);
        }
    }

    [Fact]
    public async Task ConsultarAuditoriaAsync_RespetaMaximo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, _, uId) = await SembrarCatalogosAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            for (int i = 0; i < 5; i++)
            {
                ctx.Auditorias.Add(new AuditoriaEntidad
                {
                    FechaHora = DateTime.UtcNow.AddMinutes(i),
                    UsuarioId = uId,
                    Accion = AccionAuditoria.Modificar,
                    Entidad = "Pesaje",
                    EntidadId = i.ToString(CultureInfo.InvariantCulture)
                });
            }
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new AuditoriaRepositorio(ctx);
            var res = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, null, null, null, null), 3);

            Assert.Equal(3, res.Count);
        }
    }

    [Fact]
    public async Task BuscarAsync_CombinacionRangoYGanadero_AplicaAnd()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (g1, c1, uId) = await SembrarCatalogosAsync(db);

        int g2;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var gan2 = new GanaderoBuilder().ConMarca("G2B").ConNombre("Ganadero 2B").Build();
            ctx.Ganaderos.Add(gan2);
            await ctx.SaveChangesAsync();
            g2 = gan2.Id;

            var d1 = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
            var d2 = new DateTime(2026, 9, 28, 16, 0, 0, DateTimeKind.Utc);

            ctx.Pesajes.AddRange(
                new PesajeBuilder().ConNumeroBoleta(6001).ConFechaHora(d1).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(6002).ConFechaHora(d1).ConGanaderoId(g2).ConClienteId(c1).ConUsuarioId(uId).Build(),
                new PesajeBuilder().ConNumeroBoleta(6003).ConFechaHora(d2).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(uId).Build()
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new PesajeRepositorio(ctx);
            var desde = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc);
            var hasta = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
            var res = await repo.BuscarAsync(new CriteriosBusquedaPesaje(desde, hasta, null, g1, null, null), 10);

            var b = Assert.Single(res);
            Assert.Equal(6001, b.NumeroBoleta);
        }
    }

    [Fact]
    public async Task ConsultarAuditoriaAsync_CombinacionEntidadYAccion_AplicaAnd()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, _, uId) = await SembrarCatalogosAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Auditorias.AddRange(
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow, UsuarioId = uId, Accion = AccionAuditoria.Crear, Entidad = "Pesaje", EntidadId = "1" },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow, UsuarioId = uId, Accion = AccionAuditoria.Modificar, Entidad = "Pesaje", EntidadId = "1" },
                new AuditoriaEntidad { FechaHora = DateTime.UtcNow, UsuarioId = uId, Accion = AccionAuditoria.Crear, Entidad = "Ganadero", EntidadId = "2" }
            );
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var repo = new AuditoriaRepositorio(ctx);
            var res = await repo.ConsultarAsync(new CriteriosAuditoria(null, null, "Pesaje", AccionAuditoria.Crear, null, null), 10);

            var a = Assert.Single(res);
            Assert.Equal("Pesaje", a.Entidad);
            Assert.Equal(AccionAuditoria.Crear, a.Accion);
        }
    }
}

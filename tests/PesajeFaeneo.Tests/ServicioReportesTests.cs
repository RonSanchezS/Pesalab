using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;

using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioReportesTests
{
    private static readonly TimeZoneInfo ZonaUtcMenos4 = TimeZoneInfo.CreateCustomTimeZone(
        "Zona_Test_UTC_Minus_4",
        TimeSpan.FromHours(-4),
        "UTC-4",
        "UTC-4");

    private static (ServicioReportes Servicio, SesionActual Sesion, FakeTimeProvider Tiempo, AlmacenLogoFalso Logo) CrearServicio(
        BaseDatosEnMemoria db,
        int? maximoFilas = null,
        string? rutaLogo = null)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(ZonaUtcMenos4);

        var sesion = new SesionActual();
        var logo = new AlmacenLogoFalso(rutaLogo);

        var servicio = new ServicioReportes(
            db.UnidadDeTrabajoFactory,
            sesion,
            tiempo,
            logo,
            NullLogger<ServicioReportes>.Instance);

        if (maximoFilas.HasValue)
        {
            servicio = new ServicioReportes(
                db.UnidadDeTrabajoFactory,
                sesion,
                tiempo,
                logo,
                NullLogger<ServicioReportes>.Instance)
            {
                MaximoFilas = maximoFilas.Value
            };
        }

        return (servicio, sesion, tiempo, logo);
    }

    private static async Task<(int GanaderoId, int ClienteId, int UsuarioId)> SembrarDatosBaseAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();

        var g = new GanaderoBuilder().ConMarca("G1").ConNombre("Ganadero Base").Build();
        var c = new ClienteBuilder().ConCodigo(101).ConNombre("Cliente Base").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("operador_rep").ConNombreCompleto("Operador Reportes").Build();

        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();

        return (g.Id, c.Id, u.Id);
    }

    [Fact]
    public async Task Permisos_SinSesion_LanzaNoAutorizadoExceptionEnAmbosMetodos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _, _) = CrearServicio(db);

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerOpcionesAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.GenerarAsync(filtro));
    }

    [Fact]
    public async Task Permisos_Operador_LanzaNoAutorizadoExceptionEnAmbosMetodos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerOpcionesAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.GenerarAsync(filtro));
    }

    [Theory]
    [InlineData(Rol.Supervisor)]
    [InlineData(Rol.Administrador)]
    public async Task Permisos_SupervisorYAdministrador_GeneranExitosamente(Rol rol)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, rol);

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);

        var opciones = await servicio.ObtenerOpcionesAsync();
        Assert.NotNull(opciones);

        var reporte = await servicio.GenerarAsync(filtro);
        Assert.NotNull(reporte);
    }

    [Fact]
    public async Task GenerarAsync_CasoLegado_CalculaTotalesExactos()
    {
        // 8 res completa (109,0 + 106,0) + 3 media res (106,0 izquierda) vigentes del día -> 11 boletas, reses 9.5m, neto 2038.0m (HU-F7-01, R2)
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var fechaDia = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc); // 10:00 local UTC-4

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 8; i++)
            {
                ctx.Pesajes.Add(new PesajeBuilder()
                    .ConNumeroBoleta(i)
                    .ConFechaHora(fechaDia.AddMinutes(i))
                    .ConGanaderoId(gId)
                    .ConClienteId(cId)
                    .ConUsuarioId(uId)
                    .ConDesposte(TipoDesposte.ResCompleta)
                    .ConPesos(109.0m, 106.0m)
                    .Build());
            }
            for (int i = 9; i <= 11; i++)
            {
                ctx.Pesajes.Add(new PesajeBuilder()
                    .ConNumeroBoleta(i)
                    .ConFechaHora(fechaDia.AddMinutes(i))
                    .ConGanaderoId(gId)
                    .ConClienteId(cId)
                    .ConUsuarioId(uId)
                    .ConDesposte(TipoDesposte.MediaRes)
                    .ConPesos(106.0m, 0.0m)
                    .Build());
            }
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var reporte = await servicio.GenerarAsync(filtro);

        Assert.Equal(11, reporte.Filas.Count);
        Assert.Equal(11, reporte.Totales.Boletas);
        Assert.Equal(8, reporte.Totales.ResesCompletas);
        Assert.Equal(3, reporte.Totales.MediasReses);
        Assert.Equal(9.5m, reporte.Totales.Reses);
        Assert.Equal(2038.0m, reporte.Totales.TotalNetoKg);
    }

    [Fact]
    public async Task GenerarAsync_BoletaAnulada_SeExcluyeDePesajesDelDiaYEntreFechas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var fechaDia = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var pVigente = new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(fechaDia).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).ConPesos(100m, 100m).Build();
            var pAnulado = new PesajeBuilder().ConNumeroBoleta(2).ConFechaHora(fechaDia.AddMinutes(1)).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).ConPesos(100m, 100m).Anulado("ERROR", uId).Build();

            ctx.Pesajes.AddRange(pVigente, pAnulado);
            await ctx.SaveChangesAsync();
        }

        var fDia = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rDia = await servicio.GenerarAsync(fDia);
        Assert.Single(rDia.Filas);
        Assert.Equal(1, rDia.Filas[0].NumeroBoleta);
        Assert.Equal(200m, rDia.Totales.TotalNetoKg);

        var fFechas = new FiltroReporte(TipoReporte.PesajesEntreFechas, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rFechas = await servicio.GenerarAsync(fFechas);
        Assert.Single(rFechas.Filas);
        Assert.Equal(1, rFechas.Filas[0].NumeroBoleta);
    }

    [Fact]
    public async Task GenerarAsync_BoletaSimulada_SeIncluyeYSumaEnTotales()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var fechaDia = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var pSimulado = new PesajeBuilder()
                .ConNumeroBoleta(5)
                .ConFechaHora(fechaDia)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .ConOrigen(OrigenPeso.Simulador)
                .ConPesos(150m, 150m)
                .Build();

            ctx.Pesajes.Add(pSimulado);
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var reporte = await servicio.GenerarAsync(filtro);

        Assert.Single(reporte.Filas);
        Assert.Equal(1, reporte.Totales.Simuladas);
        Assert.Equal(300m, reporte.Totales.TotalNetoKg);
    }

    [Fact]
    public async Task GenerarAsync_DiaLocal_PesajeNochePerteneceAlDiaLocal()
    {
        // En UTC-4: 2026-09-28T02:30:00Z corresponde a 2026-09-27T22:30:00 local
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var fechaNocheUtc = new DateTime(2026, 9, 28, 2, 30, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder()
                .ConNumeroBoleta(77)
                .ConFechaHora(fechaNocheUtc)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Build();

            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
        }

        // Del día 27: debe incluirla
        var f27 = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 27), new DateOnly(2026, 9, 27), null, null);
        var r27 = await servicio.GenerarAsync(f27);
        Assert.Single(r27.Filas);
        Assert.Equal(77, r27.Filas[0].NumeroBoleta);

        // Del día 28: NO debe incluirla
        var f28 = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 28), null, null);
        var r28 = await servicio.GenerarAsync(f28);
        Assert.Empty(r28.Filas);
    }

    [Fact]
    public async Task GenerarAsync_PesajesDelDia_UsaSoloDesdeEIgnoraHasta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var dia1LocalUtc = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc);
        var dia2LocalUtc = new DateTime(2026, 9, 26, 14, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(dia1LocalUtc).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(2).ConFechaHora(dia2LocalUtc).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            await ctx.SaveChangesAsync();
        }

        // Aunque se pase Hasta = 2026-09-26, al ser PesajesDelDia debe usar solo Desde = 2026-09-25
        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 26), null, null);
        var reporte = await servicio.GenerarAsync(filtro);

        Assert.Equal(new DateOnly(2026, 9, 25), reporte.Desde);
        Assert.Equal(new DateOnly(2026, 9, 25), reporte.Hasta);
        Assert.Single(reporte.Filas);
        Assert.Equal(1, reporte.Filas[0].NumeroBoleta);
    }

    [Fact]
    public async Task GenerarAsync_DesdeMayorQueHasta_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroReporte(TipoReporte.PesajesEntreFechas, new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 25), null, null);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(filtro));

        Assert.Contains(ex.Errores, e => e.Campo == CamposReportes.FechaHasta);
    }

    [Fact]
    public async Task GenerarAsync_FechasLocales_TienenKindUnspecified()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var fechaUtc = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc); // 10:00 local

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(fechaUtc).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var reporte = await servicio.GenerarAsync(filtro);

        // El reloj del servicio marca 2026-09-30T14:00Z: en UTC-4 son las 10:00 locales.
        Assert.Equal(new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Unspecified), reporte.GeneradoEnLocal);
        Assert.Equal(DateTimeKind.Unspecified, reporte.GeneradoEnLocal.Kind);
        Assert.Equal(DateTimeKind.Unspecified, reporte.Filas[0].FechaHoraLocal.Kind);
        Assert.Equal(new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Unspecified), reporte.Filas[0].FechaHoraLocal);
    }

    [Fact]
    public async Task GenerarAsync_PorGanadero_ValidacionesYFiltro()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int gInactivoId, cId, uId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().ConMarca("GINA").ConNombre("Ganadero Inactivo").Inactivo().Build();
            var c = new ClienteBuilder().ConCodigo(301).ConNombre("Cliente G").Build();
            var u = new UsuarioBuilder().ConNombreUsuario("user_g").Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            ctx.Usuarios.Add(u);
            await ctx.SaveChangesAsync();
            gInactivoId = g.Id;
            cId = c.Id;
            uId = u.Id;

            // Boleta de OTRO ganadero en el mismo rango: el filtro debe dejarla fuera.
            var gOtro = new GanaderoBuilder().ConMarca("GOTR").ConNombre("Otro Ganadero").Build();
            ctx.Ganaderos.Add(gOtro);
            await ctx.SaveChangesAsync();

            var t = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(10).ConFechaHora(t).ConGanaderoId(gInactivoId).ConClienteId(cId).ConUsuarioId(uId).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(11).ConFechaHora(t.AddMinutes(5)).ConGanaderoId(gOtro.Id).ConClienteId(cId).ConUsuarioId(uId).Build());
            await ctx.SaveChangesAsync();
        }

        // Sin ganadero
        var fSinG = new FiltroReporte(TipoReporte.PorGanadero, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var exSinG = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(fSinG));
        Assert.Contains(exSinG.Errores, e => e.Campo == CamposReportes.Ganadero && e.Mensaje == "Seleccione el ganadero.");

        // Ganadero inexistente
        var fInexistente = new FiltroReporte(TipoReporte.PorGanadero, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), 99999, null);
        var exInex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(fInexistente));
        Assert.Contains(exInex.Errores, e => e.Campo == CamposReportes.Ganadero && e.Mensaje == "El ganadero seleccionado no existe.");

        // Ganadero inactivo con pesajes
        var fOk = new FiltroReporte(TipoReporte.PorGanadero, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), gInactivoId, null);
        var rep = await servicio.GenerarAsync(fOk);
        Assert.Single(rep.Filas);
        Assert.Equal(10, rep.Filas[0].NumeroBoleta);
        Assert.Equal("GINA", rep.ClaveFiltro);
        Assert.Contains("— Ganadero: GINA - Ganadero Inactivo", rep.Subtitulo);
    }

    [Fact]
    public async Task GenerarAsync_PorCliente_ValidacionesYFiltro()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        int gId, cInactivoId, uId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().ConMarca("GC").ConNombre("Ganadero C").Build();
            var c = new ClienteBuilder().ConCodigo(505).ConNombre("Cliente Inactivo").Inactivo().Build();
            var u = new UsuarioBuilder().ConNombreUsuario("user_c").Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            ctx.Usuarios.Add(u);
            await ctx.SaveChangesAsync();
            gId = g.Id;
            cInactivoId = c.Id;
            uId = u.Id;

            // Boleta de OTRO cliente en el mismo rango: el filtro debe dejarla fuera.
            var cOtro = new ClienteBuilder().ConCodigo(506).ConNombre("Otro Cliente").Build();
            ctx.Clientes.Add(cOtro);
            await ctx.SaveChangesAsync();

            var t = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(20).ConFechaHora(t).ConGanaderoId(gId).ConClienteId(cInactivoId).ConUsuarioId(uId).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(21).ConFechaHora(t.AddMinutes(5)).ConGanaderoId(gId).ConClienteId(cOtro.Id).ConUsuarioId(uId).Build());
            await ctx.SaveChangesAsync();
        }

        // Sin cliente
        var fSinC = new FiltroReporte(TipoReporte.PorCliente, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var exSinC = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(fSinC));
        Assert.Contains(exSinC.Errores, e => e.Campo == CamposReportes.Cliente && e.Mensaje == "Seleccione el cliente.");

        // Cliente inexistente
        var fInex = new FiltroReporte(TipoReporte.PorCliente, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, 99999);
        var exInex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(fInex));
        Assert.Contains(exInex.Errores, e => e.Campo == CamposReportes.Cliente && e.Mensaje == "El cliente seleccionado no existe.");

        // Cliente inactivo
        var fOk = new FiltroReporte(TipoReporte.PorCliente, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, cInactivoId);
        var rep = await servicio.GenerarAsync(fOk);
        Assert.Single(rep.Filas);
        Assert.Equal(20, rep.Filas[0].NumeroBoleta);
        Assert.Equal("505", rep.ClaveFiltro);
        Assert.Contains("— Cliente: 505 - Cliente Inactivo", rep.Subtitulo);
    }

    [Fact]
    public async Task GenerarAsync_PesajesEntreFechas_IgnoraGanaderoYClienteSobrantes()
    {
        // R8: un ganadero o cliente que quedó seleccionado en la pantalla no filtra un reporte entre fechas
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (g1, c1, u) = await SembrarDatosBaseAsync(db);

        int g2, c2;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().ConMarca("G2").ConNombre("Ganadero 2").Build();
            var c = new ClienteBuilder().ConCodigo(202).ConNombre("Cliente 2").Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();
            g2 = g.Id;
            c2 = c.Id;

            var t = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(t).ConGanaderoId(g1).ConClienteId(c1).ConUsuarioId(u).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(2).ConFechaHora(t.AddMinutes(5)).ConGanaderoId(g2).ConClienteId(c2).ConUsuarioId(u).Build());
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesEntreFechas, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), GanaderoId: g1, ClienteId: c1);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Equal(2, rep.Filas.Count);
        Assert.Null(rep.ClaveFiltro);
    }

    [Fact]
    public async Task GenerarAsync_Anuladas_TraeSoloAnuladasDelRangoConDatosCompletos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        string motivo100 = new string('A', 100);
        var fechaBoleta = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc); // 10:00 local

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Anulada dentro del rango; se anuló DOS DÍAS DESPUÉS de emitida (el filtro es por fecha de la boleta).
            var pAnulada = new PesajeBuilder()
                .ConNumeroBoleta(100)
                .ConFechaHora(fechaBoleta)
                .ConGanaderoId(gId)
                .ConClienteId(cId)
                .ConUsuarioId(uId)
                .Anulado(motivo100, uId)
                .Build();
            pAnulada.AnuladoEn = new DateTime(2026, 10, 2, 14, 0, 0, DateTimeKind.Utc);

            // Vigente en el mismo rango: no debe salir en "anuladas".
            var pVigente = new PesajeBuilder().ConNumeroBoleta(101).ConFechaHora(fechaBoleta.AddMinutes(1))
                .ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build();

            // Anulada FUERA del rango por fecha de boleta (anulada el 30/09, emitida el 28/09): tampoco sale.
            var pFuera = new PesajeBuilder()
                .ConNumeroBoleta(102)
                .ConFechaHora(new DateTime(2026, 9, 28, 14, 0, 0, DateTimeKind.Utc))
                .ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId)
                .Anulado("MOTIVO FUERA", uId)
                .Build();
            pFuera.AnuladoEn = fechaBoleta;

            ctx.Pesajes.AddRange(pAnulada, pVigente, pFuera);
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.Anuladas, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.True(rep.EsAnuladas);
        Assert.Single(rep.Filas);
        var fila = rep.Filas[0];
        Assert.Equal(100, fila.NumeroBoleta);
        Assert.Equal("Operador Reportes", fila.AnuladoPor);
        Assert.Equal(motivo100, fila.MotivoAnulacion);
        // 2026-10-02T14:00Z en UTC-4 = 02/10/2026 10:00 local.
        Assert.Equal(new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Unspecified), fila.AnuladoEnLocal);
        Assert.Equal(DateTimeKind.Unspecified, fila.AnuladoEnLocal!.Value.Kind);
    }

    [Fact]
    public async Task GenerarAsync_Anuladas_SinNombreCompleto_UsaNombreUsuario()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, _) = await SembrarDatosBaseAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var anulador = new UsuarioBuilder().ConNombreUsuario("sin_nombre").ConNombreCompleto("").Build();
            ctx.Usuarios.Add(anulador);
            await ctx.SaveChangesAsync();

            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(200)
                .ConFechaHora(new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc))
                .ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(anulador.Id)
                .Anulado("ERROR DE PESAJE", anulador.Id)
                .Build());
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.Anuladas, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Equal("sin_nombre", Assert.Single(rep.Filas).AnuladoPor);
    }

    [Fact]
    public async Task GenerarAsync_Vigentes_TienenCamposAnulacionNulos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc)).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Single(rep.Filas);
        Assert.Null(rep.Filas[0].AnuladoPor);
        Assert.Null(rep.Filas[0].AnuladoEnLocal);
        Assert.Null(rep.Filas[0].MotivoAnulacion);
    }

    [Fact]
    public async Task GenerarAsync_TipoInvalido_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroReporte((TipoReporte)99, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(filtro));

        Assert.Contains(ex.Errores, e => e.Campo == CamposReportes.Tipo);
    }

    [Fact]
    public async Task GenerarAsync_MultiplesErrores_AcumulaTodosLosCampos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroReporte(TipoReporte.PorGanadero, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 20), null, null);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(filtro));

        Assert.Equal(2, ex.Errores.Count);
        Assert.Contains(ex.Errores, e => e.Campo == CamposReportes.FechaHasta);
        Assert.Contains(ex.Errores, e => e.Campo == CamposReportes.Ganadero);
    }

    [Fact]
    public async Task GenerarAsync_OrdenYSecuencia_ItemCorrelativoUnoAN()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var t1 = new DateTime(2026, 9, 30, 13, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(200).ConFechaHora(t2).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(100).ConFechaHora(t2).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(999).ConFechaHora(t1).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Equal(3, rep.Filas.Count);
        Assert.Equal(1, rep.Filas[0].Item);
        Assert.Equal(999, rep.Filas[0].NumeroBoleta);

        Assert.Equal(2, rep.Filas[1].Item);
        Assert.Equal(100, rep.Filas[1].NumeroBoleta);

        Assert.Equal(3, rep.Filas[2].Item);
        Assert.Equal(200, rep.Filas[2].NumeroBoleta);
    }

    [Fact]
    public async Task GenerarAsync_Limite_SuperaMaximoFilas_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db, maximoFilas: 10);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var t = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 11; i++)
            {
                ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(i).ConFechaHora(t.AddMinutes(i)).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            }
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(filtro));

        Assert.Contains(ex.Errores, e => e.Campo == CamposReportes.FechaHasta && e.Mensaje == TextosReporte.MaximoSuperado);
    }

    [Fact]
    public async Task GenerarAsync_Limite_DiezExactos_GeneraExitosamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db, maximoFilas: 10);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        var t = new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            for (int i = 1; i <= 10; i++)
            {
                ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(i).ConFechaHora(t.AddMinutes(i)).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build());
            }
            await ctx.SaveChangesAsync();
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Equal(10, rep.Filas.Count);
    }

    [Fact]
    public async Task ServicioReportes_MaximoFilasPorDefecto_CoincideConReglasReportes()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _, _) = CrearServicio(db);
        Assert.Equal(ReglasReportes.MaximoFilas, servicio.MaximoFilas);
    }

    [Fact]
    public async Task GenerarAsync_Empresa_UsaParametrosOValorPorDefecto()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);

        // Sin nombre configurado -> EmpresaPorDefecto
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.ParametrosSistema.SingleAsync();
            p.NombreEmpresa = "   ";
            await ctx.SaveChangesAsync();
        }

        var repDefault = await servicio.GenerarAsync(filtro);
        Assert.Equal(TextosReporte.EmpresaPorDefecto, repDefault.Empresa);

        // Con nombre configurado
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = await ctx.ParametrosSistema.SingleAsync();
            p.NombreEmpresa = "  FRIGORIFICO CENTRAL  ";
            await ctx.SaveChangesAsync();
        }


        var repConfig = await servicio.GenerarAsync(filtro);
        Assert.Equal("FRIGORIFICO CENTRAL", repConfig.Empresa);
    }

    [Fact]
    public async Task GenerarAsync_Logo_ResuelveRutaCompletaOQuedaNulo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servConLogo, sesCon, _, _) = CrearServicio(db, rutaLogo: @"C:\logo\pesalab.png");
        await SesionesDePrueba.IniciarAsync(db, sesCon, Rol.Supervisor);

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var repCon = await servConLogo.GenerarAsync(filtro);
        Assert.Equal(@"C:\logo\pesalab.png", repCon.RutaLogo);

        var (servSinLogo, sesSin, _, _) = CrearServicio(db, rutaLogo: null);
        await SesionesDePrueba.IniciarAsync(db, sesSin, Rol.Supervisor, "admin2");
        var repSin = await servSinLogo.GenerarAsync(filtro);
        Assert.Null(repSin.RutaLogo);
    }

    [Fact]
    public async Task GenerarAsync_GeneradoPor_TomaNombreCompletoONombreUsuario()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor, "super_rep");

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Equal(usuario.NombreCompleto, rep.GeneradoPor);
    }

    [Fact]
    public async Task GenerarAsync_GeneradoPor_SinNombreCompleto_UsaNombreUsuario()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor, "super_sin_nombre");
        sesion.Iniciar(new UsuarioSesion(usuario.Id, usuario.NombreUsuario, string.Empty, usuario.Rol, usuario.DebeCambiarPassword));

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Equal("super_sin_nombre", rep.GeneradoPor);
    }

    [Fact]
    public async Task GenerarAsync_SinBoletas_RetornaReporteVacioConTotalesEnCero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        var rep = await servicio.GenerarAsync(filtro);

        Assert.Empty(rep.Filas);
        Assert.Equal(0, rep.Totales.Boletas);
        Assert.Equal(0m, rep.Totales.TotalNetoKg);
    }

    [Fact]
    public async Task GenerarAsync_EsSoloLectura_NoModificaAuditoriasNiVersion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var (gId, cId, uId) = await SembrarDatosBaseAsync(db);

        int pesajeId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var p = new PesajeBuilder().ConNumeroBoleta(1).ConFechaHora(new DateTime(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc)).ConGanaderoId(gId).ConClienteId(cId).ConUsuarioId(uId).Build();
            ctx.Pesajes.Add(p);
            await ctx.SaveChangesAsync();
            pesajeId = p.Id;
        }

        int auditoriasAntes;
        int versionAntes;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            auditoriasAntes = await ctx.Auditorias.CountAsync();
            versionAntes = (await ctx.Pesajes.SingleAsync(p => p.Id == pesajeId)).Version;
        }

        var filtro = new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null);
        await servicio.GenerarAsync(filtro);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            int auditoriasDespues = await ctx.Auditorias.CountAsync();
            int versionDespues = (await ctx.Pesajes.SingleAsync(p => p.Id == pesajeId)).Version;

            Assert.Equal(auditoriasAntes, auditoriasDespues);
            Assert.Equal(versionAntes, versionDespues);
        }
    }

    [Fact]
    public async Task ObtenerOpcionesAsync_IncluyeGanaderosYClientesInactivos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Ganaderos.Add(new GanaderoBuilder().ConMarca("ACT1").Inactivo().Build());
            ctx.Clientes.Add(new ClienteBuilder().ConCodigo(901).Inactivo().Build());
            await ctx.SaveChangesAsync();
        }

        var opciones = await servicio.ObtenerOpcionesAsync();

        Assert.Contains(opciones.Ganaderos, g => g.Codigo == "ACT1" && !g.Activo);
        Assert.Contains(opciones.Clientes, c => c.Codigo == "901" && !c.Activo);
    }
}

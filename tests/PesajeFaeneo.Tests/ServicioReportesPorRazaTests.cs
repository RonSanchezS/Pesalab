using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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

/// <summary>F10: reporte por raza (O8) y columna Fletero (O5) del servicio de reportes.</summary>
public class ServicioReportesPorRazaTests
{
    private static readonly TimeZoneInfo ZonaUtcMenos4 = TimeZoneInfo.CreateCustomTimeZone("Zona_Raza_Test", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");

    private static (ServicioReportes Servicio, SesionActual Sesion) CrearServicio(BaseDatosEnMemoria db)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 14, 0, 0, TimeSpan.Zero));
        tiempo.SetLocalTimeZone(ZonaUtcMenos4);
        var sesion = new SesionActual();
        var servicio = new ServicioReportes(db.UnidadDeTrabajoFactory, sesion, tiempo, new AlmacenLogoFalso(null), NullLogger<ServicioReportes>.Instance);
        return (servicio, sesion);
    }

    private sealed record Base(int GanaderoId, int ClienteId, int UsuarioId, int FleteroId);

    private static async Task<Base> SembrarBaseAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var g = new GanaderoBuilder().ConCodigo("7").ConNombre("Ganadero Base").Build();
        var c = new ClienteBuilder().ConCodigo(101).ConNombre("Cliente Base").Build();
        var u = new UsuarioBuilder().ConNombreUsuario("operador_raza").ConNombreCompleto("Operador Raza").Build();
        var f = new Fletero { Codigo = 2, Nombre = "FLETERO DOS", Celular = "70000002", Activo = true, CreadoEn = DateTime.UtcNow };
        ctx.Ganaderos.Add(g);
        ctx.Clientes.Add(c);
        ctx.Usuarios.Add(u);
        ctx.Fleteros.Add(f);
        await ctx.SaveChangesAsync();
        return new Base(g.Id, c.Id, u.Id, f.Id);
    }

    // Hora local 10:00 del 30/09/2026 = 14:00 UTC.
    private static readonly DateTime Hoy = new(2026, 9, 30, 14, 0, 0, DateTimeKind.Utc);

    private static async Task AgregarPesajeAsync(
        BaseDatosEnMemoria db, Base b, long numero, string colorRaza,
        TipoDesposte desposte = TipoDesposte.ResCompleta, bool anulado = false, DateTime? fecha = null, int? fleteroId = null,
        OrigenPeso origen = OrigenPeso.Balanza)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var builder = new PesajeBuilder()
            .ConNumeroBoleta(numero)
            .ConFechaHora(fecha ?? Hoy.AddMinutes(numero))
            .ConGanaderoId(b.GanaderoId)
            .ConClienteId(b.ClienteId)
            .ConUsuarioId(b.UsuarioId)
            .ConDesposte(desposte)
            .ConOrigen(origen);
        builder = desposte == TipoDesposte.MediaRes ? builder.ConPesos(106.0m, 0m) : builder.ConPesos(109.0m, 106.0m);
        if (anulado)
        {
            builder = builder.Anulado("ERROR", b.UsuarioId);
        }

        var p = builder.Build();
        p.ColorRaza = colorRaza;
        p.FleteroId = fleteroId;
        ctx.Pesajes.Add(p);
        await ctx.SaveChangesAsync();
    }

    private static FiltroReporte FiltroRaza(string? raza, DateOnly? desde = null, DateOnly? hasta = null) =>
        new(TipoReporte.PorRaza, desde ?? new DateOnly(2026, 9, 30), hasta ?? new DateOnly(2026, 9, 30), null, null, raza);

    [Fact]
    public async Task GenerarAsync_PorRaza_FiltraPorRazaYExcluyeOtrasRazasYAnuladas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "NELORE");
        await AgregarPesajeAsync(db, b, 2, "NELORE", TipoDesposte.MediaRes);
        await AgregarPesajeAsync(db, b, 3, "BRAHMAN");
        await AgregarPesajeAsync(db, b, 4, "NELORE", anulado: true);

        var reporte = await servicio.GenerarAsync(FiltroRaza("NELORE"));

        Assert.Equal(TipoReporte.PorRaza, reporte.Tipo);
        Assert.Equal(new long[] { 1, 2 }, reporte.Filas.Select(f => f.NumeroBoleta));
        // Mismas reglas de totales (D3): res completa 1 + media res 0,5; 215,0 + 106,0 kg.
        Assert.Equal(1.5m, reporte.Totales.Reses);
        Assert.Equal(321.0m, reporte.Totales.TotalNetoKg);
        Assert.Equal("NELORE", reporte.ClaveFiltro);
        Assert.Equal("Pesajes por raza: NELORE", reporte.Titulo);
        Assert.Contains("Raza: NELORE", reporte.Subtitulo);
        Assert.False(reporte.EsAnuladas);
    }

    [Theory]
    [InlineData("nelore")]
    [InlineData("  Nelore  ")]
    [InlineData("NELORE")]
    public async Task GenerarAsync_PorRaza_LaComparacionEsNormalizada(string entrada)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "NELORE");
        await AgregarPesajeAsync(db, b, 2, "OTRA");

        var reporte = await servicio.GenerarAsync(FiltroRaza(entrada));

        Assert.Equal(1L, Assert.Single(reporte.Filas).NumeroBoleta);
        Assert.Equal("NELORE", reporte.ClaveFiltro);
    }

    [Fact]
    public async Task GenerarAsync_PorRaza_RespetaElRangoDeFechasLocales()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "NELORE", fecha: Hoy);
        await AgregarPesajeAsync(db, b, 2, "NELORE", fecha: Hoy.AddDays(-3));
        await AgregarPesajeAsync(db, b, 3, "NELORE", fecha: Hoy.AddDays(-10));

        var reporte = await servicio.GenerarAsync(FiltroRaza("NELORE", new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 30)));

        Assert.Equal(new long[] { 2, 1 }, reporte.Filas.Select(f => f.NumeroBoleta)); // orden cronológico ascendente
    }

    [Fact]
    public async Task GenerarAsync_PorRaza_SinRazaLanzaValidacion_YSimuladasSeMarcan()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "NELORE", origen: OrigenPeso.Simulador);

        foreach (var vacia in new[] { null, "", "   " })
        {
            var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GenerarAsync(FiltroRaza(vacia)));
            Assert.Contains(ex.Errores, e => e.Campo == CamposReportes.Raza);
        }

        var reporte = await servicio.GenerarAsync(FiltroRaza("nelore"));
        Assert.Equal(1, reporte.Totales.Simuladas); // D4: las simuladas entran y se marcan
    }

    [Fact]
    public async Task ObtenerOpcionesAsync_ListaLasRazasDistintasNormalizadasYSinVacios_IncluyendoAnuladas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "NELORE");
        await AgregarPesajeAsync(db, b, 2, "  nelore ");   // dato importado sin normalizar
        await AgregarPesajeAsync(db, b, 3, "BRAHMAN", anulado: true);
        await AgregarPesajeAsync(db, b, 4, "");
        await AgregarPesajeAsync(db, b, 5, "   ");
        await AgregarPesajeAsync(db, b, 6, "ANGUS NEGRO");

        var opciones = await servicio.ObtenerOpcionesAsync();

        Assert.NotNull(opciones.Razas);
        Assert.Equal(new[] { "ANGUS NEGRO", "BRAHMAN", "NELORE" }, opciones.Razas);
    }

    [Fact]
    public async Task GenerarAsync_ConFletero_LlenaNombreFleteroYSinFleteroQuedaVacio()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "NELORE", fleteroId: b.FleteroId);
        await AgregarPesajeAsync(db, b, 2, "NELORE");

        var reporte = await servicio.GenerarAsync(new FiltroReporte(TipoReporte.PesajesDelDia, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), null, null));

        Assert.Equal("FLETERO DOS", reporte.Filas[0].NombreFletero);
        Assert.Equal(string.Empty, reporte.Filas[1].NombreFletero);
        Assert.Equal("7", reporte.Filas[0].CodigoGanadero);
    }

    [Fact]
    public async Task TextosReporte_TituloSubtituloYArchivoDelReportePorRaza()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Supervisor);
        var b = await SembrarBaseAsync(db);
        await AgregarPesajeAsync(db, b, 1, "ANGUS NEGRO");

        var reporte = await servicio.GenerarAsync(FiltroRaza("angus negro"));

        Assert.Equal("Pesajes por raza", TextosReporte.NombreTipo(TipoReporte.PorRaza));
        Assert.Equal("Raza_ANGUS_NEGRO_2026-09-30.pdf", TextosReporte.NombreArchivo(reporte, ".pdf"));
    }
}

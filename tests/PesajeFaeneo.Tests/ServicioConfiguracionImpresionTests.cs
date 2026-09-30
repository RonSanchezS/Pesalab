using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Boletas;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioConfiguracionImpresionTests
{
    [Fact]
    public async Task SinSesion_ObtenerAsync_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var almacen = new AlmacenConfiguracionImpresionFalso();
        var servicio = new ServicioConfiguracionImpresion(
            almacen,
            db.UnidadDeTrabajoFactory,
            sesion,
            TimeProvider.System,
            NullLogger<ServicioConfiguracionImpresion>.Instance);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync());
    }

    [Fact]
    public async Task Operador_PuedeObtenerPeroNoGuardar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var almacen = new AlmacenConfiguracionImpresionFalso(null) { NombreEquipo = "PC-PRUEBA" };
        var servicio = new ServicioConfiguracionImpresion(
            almacen,
            db.UnidadDeTrabajoFactory,
            sesion,
            TimeProvider.System,
            NullLogger<ServicioConfiguracionImpresion>.Instance);

        var equipo = await servicio.ObtenerAsync();
        Assert.False(equipo.Configurada);
        Assert.Equal("PC-PRUEBA", equipo.NombreEquipo);
        Assert.Equal(ReglasImpresion.PorDefecto, equipo.Configuracion);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.GuardarAsync(ReglasImpresion.PorDefecto));
    }

    [Fact]
    public async Task Administrador_GuardaValido_GeneraAuditoriaYActualizaAlmacen()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var almacen = new AlmacenConfiguracionImpresionFalso(null) { NombreEquipo = "PC-PRUEBA" };
        var servicio = new ServicioConfiguracionImpresion(
            almacen,
            db.UnidadDeTrabajoFactory,
            sesion,
            TimeProvider.System,
            NullLogger<ServicioConfiguracionImpresion>.Instance);

        var config1 = new ConfiguracionImpresion("  HP LaserJet  ", FormatoBoleta.MediaCarta, "  A4  ", 2, true, 2.55m, -1.0m);
        await servicio.GuardarAsync(config1);

        // Almacén tiene configuración normalizada
        Assert.NotNull(almacen.Configuracion);
        Assert.Equal("HP LaserJet", almacen.Configuracion.NombreImpresora);
        Assert.Equal("A4", almacen.Configuracion.NombrePapel);
        Assert.Equal(2.6m, almacen.Configuracion.DesplazamientoXMm);

        // 1 auditoría CambioConfigImpresion con Antes = null
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var aud1 = await ctx.Auditorias.SingleAsync();
            Assert.Equal(AccionAuditoria.CambioConfigImpresion, aud1.Accion);
            Assert.Equal(EntidadesAuditoria.ConfiguracionImpresion, aud1.Entidad);
            Assert.Equal("PC-PRUEBA", aud1.EntidadId);
            Assert.Null(aud1.DatosAntesJson);
            Assert.NotNull(aud1.DatosDespuesJson);
            Assert.Contains("\"NombreImpresora\":\"HP LaserJet\"", aud1.DatosDespuesJson);
        }

        // Segundo guardado
        var config2 = new ConfiguracionImpresion("EPSON", FormatoBoleta.Ticket80mm, null, 1, false, 0m, 0m);
        await servicio.GuardarAsync(config2);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var totalAud = await ctx.Auditorias.CountAsync();
            Assert.Equal(2, totalAud);

            var aud2 = await ctx.Auditorias.OrderByDescending(a => a.Id).FirstAsync();
            Assert.NotNull(aud2.DatosAntesJson);
            Assert.Contains("\"NombreImpresora\":\"HP LaserJet\"", aud2.DatosAntesJson);
            Assert.NotNull(aud2.DatosDespuesJson);
            Assert.Contains("\"NombreImpresora\":\"EPSON\"", aud2.DatosDespuesJson);
        }

        var equipo = await servicio.ObtenerAsync();
        Assert.True(equipo.Configurada);
        Assert.Equal("EPSON", equipo.Configuracion.NombreImpresora);
    }

    [Fact]
    public async Task GuardarAsync_CopiasInvalidas_LanzaValidacionExceptionYNoRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var almacen = new AlmacenConfiguracionImpresionFalso(null);
        var servicio = new ServicioConfiguracionImpresion(
            almacen,
            db.UnidadDeTrabajoFactory,
            sesion,
            TimeProvider.System,
            NullLogger<ServicioConfiguracionImpresion>.Instance);

        var configInvalida = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 5, false, 0m, 0m);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.GuardarAsync(configInvalida));
        Assert.Contains(ex.Errores, e => e.Campo == CamposImpresion.Copias);

        Assert.Null(almacen.Configuracion);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await ctx.Auditorias.CountAsync());
    }

    [Fact]
    public async Task GuardarAsync_FalloDeDiscoEnAlmacen_HaceRollbackDeAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var almacen = new AlmacenConfiguracionImpresionFalso(null)
        {
            LanzarErrorAlGuardar = true
        };

        var servicio = new ServicioConfiguracionImpresion(
            almacen,
            db.UnidadDeTrabajoFactory,
            sesion,
            TimeProvider.System,
            NullLogger<ServicioConfiguracionImpresion>.Instance);

        await Assert.ThrowsAsync<IOException>(() => servicio.GuardarAsync(ReglasImpresion.PorDefecto));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var totalAud = await ctx.Auditorias.CountAsync();
        Assert.Equal(0, totalAud);
    }
}

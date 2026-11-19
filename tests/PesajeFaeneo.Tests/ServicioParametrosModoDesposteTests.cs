using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Parametros;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F11 (D4, D5, D11): lectura y cambio del tipo de pesaje en ServicioParametros.</summary>
public class ServicioParametrosModoDesposteTests
{
    private sealed class AlmacenLogoNulo : IAlmacenLogo
    {
        public Task<string> GuardarAsync(string rutaOrigen, CancellationToken cancellationToken = default) => Task.FromResult("logo.png");
        public string? ObtenerRutaCompleta(string? nombreArchivo) => null;
        public void Eliminar(string? nombreArchivo) { }
    }

    private static (ServicioParametros Servicio, SesionActual Sesion) CrearServicio(BaseDatosEnMemoria db)
    {
        var sesion = new SesionActual();
        var servicio = new ServicioParametros(
            db.UnidadDeTrabajoFactory,
            sesion,
            new AlmacenLogoNulo(),
            new FakeTimeProvider(new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)),
            NullLogger<ServicioParametros>.Instance);
        return (servicio, sesion);
    }

    [Fact]
    public async Task SinSesion_LecturaYCambioLanzanNoAutorizado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerModoDesposteAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerConfiguracionDesposteAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarModoDesposteAsync(ModoDesposte.SoloResCompleta));
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    public async Task OperadorYSupervisor_LeenElModoPeroNoLoCambian(Rol rol)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, rol);

        Assert.Equal(ModoDesposte.Ambos, await servicio.ObtenerModoDesposteAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerConfiguracionDesposteAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarModoDesposteAsync(ModoDesposte.SoloResCompleta));
    }

    [Fact]
    public async Task BaseNueva_ModoSinDefinir_EfectivoAmbos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var config = await servicio.ObtenerConfiguracionDesposteAsync();
        Assert.Null(config.Modo);
        Assert.Equal(ModoDesposte.Ambos, await servicio.ObtenerModoDesposteAsync());
    }

    [Fact]
    public async Task Cambiar_DesdeSinDefinir_GuardaYAuditaConAntesYDespues()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var admin = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CambiarModoDesposteAsync(ModoDesposte.SoloResCompleta);

        Assert.Equal(ModoDesposte.SoloResCompleta, await servicio.ObtenerModoDesposteAsync());
        Assert.Equal(ModoDesposte.SoloResCompleta, (await servicio.ObtenerConfiguracionDesposteAsync()).Modo);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema);
        Assert.Equal(AccionAuditoria.CambioParametros, aud.Accion);
        Assert.Equal(admin.Id, aud.UsuarioId);
        Assert.Equal("1", aud.EntidadId);

        var cambios = DiferenciasAuditoria.Calcular(aud.DatosAntesJson, aud.DatosDespuesJson, TimeZoneInfo.Utc);
        var cambio = Assert.Single(cambios);
        Assert.Equal("ModoDesposte", cambio.Campo);
        Assert.Equal("Sin definir", cambio.Antes);
        Assert.Equal("Solo res completa", cambio.Despues);
        Assert.Equal("Tipo de pesaje", DiferenciasAuditoria.EtiquetaCampo(EntidadesAuditoria.ParametrosSistema, cambio.Campo));
    }

    [Fact]
    public async Task Cambiar_DesdeSinDefinirAAmbos_TambienSeAuditaPorqueLaEleccionQuedaExplicita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CambiarModoDesposteAsync(ModoDesposte.Ambos);

        Assert.Equal(ModoDesposte.Ambos, (await servicio.ObtenerConfiguracionDesposteAsync()).Modo);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.Auditorias.CountAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema));
    }

    [Fact]
    public async Task Cambiar_AlMismoModo_NoGuardaNiAudita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CambiarModoDesposteAsync(ModoDesposte.SoloMediaRes);
        await servicio.CambiarModoDesposteAsync(ModoDesposte.SoloMediaRes);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.Auditorias.CountAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Cambiar_ValorInvalido_LanzaValidacion(int valor)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CambiarModoDesposteAsync((ModoDesposte)valor));
        Assert.Contains(ex.Errores, e => e.Campo == CamposParametros.ModoDesposte);
    }

    [Fact]
    public async Task Cambiar_NoModificaBoletasGrabadas_YConfiguracionCuentaSoloVigentesPorTipo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion) = CrearServicio(db);
        var admin = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var g = new GanaderoBuilder().Build();
            var c = new ClienteBuilder().Build();
            ctx.Ganaderos.Add(g);
            ctx.Clientes.Add(c);
            await ctx.SaveChangesAsync();

            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(admin.Id)
                .ConDesposte(TipoDesposte.MediaRes).ConPesos(110m, 0m).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(2).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(admin.Id)
                .ConDesposte(TipoDesposte.ResCompleta).ConPesos(110m, 112m).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(3).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(admin.Id)
                .ConDesposte(TipoDesposte.ResCompleta).ConPesos(100m, 101m).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(4).ConGanaderoId(g.Id).ConClienteId(c.Id).ConUsuarioId(admin.Id)
                .ConDesposte(TipoDesposte.MediaRes).ConPesos(0m, 99m).Anulado("ERROR DE PESAJE", admin.Id).Build());
            await ctx.SaveChangesAsync();
        }

        await servicio.CambiarModoDesposteAsync(ModoDesposte.SoloResCompleta);

        var config = await servicio.ObtenerConfiguracionDesposteAsync();
        Assert.Equal(2, config.BoletasResCompleta);
        Assert.Equal(1, config.BoletasMediaRes);

        await using var verif = await db.ContextFactory.CreateDbContextAsync();
        var tipos = await verif.Pesajes.OrderBy(p => p.NumeroBoleta).Select(p => p.TipoDesposte).ToListAsync();
        Assert.Equal(new[] { TipoDesposte.MediaRes, TipoDesposte.ResCompleta, TipoDesposte.ResCompleta, TipoDesposte.MediaRes }, tipos);
    }
}

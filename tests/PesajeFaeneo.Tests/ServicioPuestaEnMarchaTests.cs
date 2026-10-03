using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Application.PuestaEnMarcha;
using PesajeFaeneo.Application.PuestaEnMarcha.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioPuestaEnMarchaTests
{
    private sealed class AlmacenImpresionFalso : IAlmacenConfiguracionImpresion
    {
        public ConfiguracionImpresion? Configuracion { get; set; }
        public string NombreEquipo => "EQUIPO-TEST";
        public Task<ConfiguracionImpresion?> LeerAsync(CancellationToken cancellationToken = default) => Task.FromResult(Configuracion);
        public Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default)
        {
            Configuracion = configuracion;
            return Task.CompletedTask;
        }
    }

    private static readonly TimeZoneInfo ZonaTest = TimeZoneInfo.CreateCustomTimeZone("UTC-4", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");
    private static readonly DateTimeOffset Ahora = new(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);

    private static (ServicioPuestaEnMarcha Servicio, ISesionActual Sesion, AlmacenImpresionFalso Almacen) CrearServicio(BaseDatosEnMemoria db)
    {
        var tiempo = new FakeTimeProvider(Ahora);
        tiempo.SetLocalTimeZone(ZonaTest);
        var sesion = new SesionActual();
        var almacen = new AlmacenImpresionFalso();
        var servicio = new ServicioPuestaEnMarcha(db.UnidadDeTrabajoFactory, sesion, almacen, tiempo, NullLogger<ServicioPuestaEnMarcha>.Instance);
        return (servicio, sesion, almacen);
    }

    private static async Task CambiarAsync(BaseDatosEnMemoria db, string? nombreEmpresa = null, TipoConexion? tipo = null)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        if (nombreEmpresa is not null)
        {
            var p = await ctx.ParametrosSistema.SingleAsync();
            p.NombreEmpresa = nombreEmpresa;
        }

        if (tipo is not null)
        {
            var b = await ctx.ConfiguracionesBalanza.SingleAsync();
            b.TipoConexion = tipo.Value;
        }

        await ctx.SaveChangesAsync();
    }

    private static async Task<int> ContarAuditoriasAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        return await ctx.Auditorias.CountAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema);
    }

    [Fact]
    public async Task ObtenerEstado_BaseRecienSembrada_NoEstaCompleta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var estado = await servicio.ObtenerEstadoAsync();

        Assert.False(estado.Completa);
        Assert.False(estado.EmpresaConfigurada);
        Assert.Equal("NOMBRE DE EMPRESA", estado.NombreEmpresa);
        Assert.Equal(TipoConexion.Simulador, estado.TipoConexionBalanza);
        Assert.False(estado.ImpresionConfigurada);
    }

    [Fact]
    public async Task NombreEmpresaPorDefecto_CoincideConLaFilaSembrada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var p = await ctx.ParametrosSistema.SingleAsync();

        Assert.Equal(ReglasPuestaEnMarcha.NombreEmpresaPorDefecto, p.NombreEmpresa);
    }

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    public async Task Metodos_SinPermiso_LanzanNoAutorizado(Rol rol)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, rol);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerEstadoAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.MarcarCompletaAsync(true));
    }

    [Fact]
    public async Task MarcarCompleta_EmpresaPorDefecto_LanzaValidacionYNoAudita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.MarcarCompletaAsync(true));

        Assert.Contains(ex.Errores, e => e.Campo == CamposPuestaEnMarcha.Empresa);
        Assert.False((await servicio.ObtenerEstadoAsync()).Completa);
        Assert.Equal(0, await ContarAuditoriasAsync(db));
    }

    [Fact]
    public async Task MarcarCompleta_SimuladorSinConfirmar_LanzaValidacionDeBalanza()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        await CambiarAsync(db, nombreEmpresa: "FRIGORIFICO X");

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.MarcarCompletaAsync(false));

        Assert.Contains(ex.Errores, e => e.Campo == CamposPuestaEnMarcha.Balanza);
        Assert.Equal(0, await ContarAuditoriasAsync(db));
    }

    [Fact]
    public async Task MarcarCompleta_SimuladorConfirmado_CompletaYAuditaConMotivoDeSimulador()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        await CambiarAsync(db, nombreEmpresa: "FRIGORIFICO X");

        await servicio.MarcarCompletaAsync(true);

        Assert.True((await servicio.ObtenerEstadoAsync()).Completa);
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema);
        Assert.Equal(AccionAuditoria.CambioParametros, aud.Accion);
        Assert.Equal("1", aud.EntidadId);
        Assert.Equal("Puesta en marcha completada (balanza en modo simulador)", aud.Motivo);
        Assert.Equal(usuario.Id, aud.UsuarioId);
        var p = await ctx.ParametrosSistema.SingleAsync();
        Assert.Equal(Ahora.UtcDateTime, p.ModificadoEn);
    }

    [Fact]
    public async Task MarcarCompleta_BalanzaSerial_CompletaConMotivoSimple()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        await CambiarAsync(db, nombreEmpresa: "FRIGORIFICO X", tipo: TipoConexion.Serial);

        await servicio.MarcarCompletaAsync(false);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.ParametrosSistema);
        Assert.Equal("Puesta en marcha completada", aud.Motivo);
    }

    [Fact]
    public async Task MarcarCompleta_DosVeces_AuditaUnaSolaVez()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        await CambiarAsync(db, nombreEmpresa: "FRIGORIFICO X", tipo: TipoConexion.Serial);

        await servicio.MarcarCompletaAsync(false);
        await servicio.MarcarCompletaAsync(false);

        Assert.Equal(1, await ContarAuditoriasAsync(db));
    }

    [Fact]
    public async Task ObtenerEstado_ConImpresionYUsuarios_CuentaCorrectamente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, almacen) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        almacen.Configuracion = new ConfiguracionImpresion(null, FormatoBoleta.MediaCarta, null, 1, false, 0m, 0m);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Usuarios.Add(new UsuarioBuilder().ConNombreUsuario("op1").ConRol(Rol.Operador).Build());
            ctx.Usuarios.Add(new UsuarioBuilder().ConNombreUsuario("op2").ConRol(Rol.Operador).Build());
            ctx.Usuarios.Add(new UsuarioBuilder().ConNombreUsuario("sup1").ConRol(Rol.Supervisor).Inactivo().Build());
            await ctx.SaveChangesAsync();
        }

        var estado = await servicio.ObtenerEstadoAsync();

        Assert.True(estado.ImpresionConfigurada);
        Assert.Equal(2, estado.UsuariosNoAdministradoresActivos);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  nombre de empresa ", false)]
    [InlineData("FRIGORIFICO X", true)]
    public void EmpresaConfigurada_SegunNombre(string? nombre, bool esperado)
    {
        Assert.Equal(esperado, ReglasPuestaEnMarcha.EmpresaConfigurada(nombre));
    }

    [Theory]
    [InlineData(false, TipoConexion.Simulador, false, false)]
    [InlineData(false, TipoConexion.Serial, false, false)]
    [InlineData(true, TipoConexion.Simulador, false, false)]
    [InlineData(true, TipoConexion.Simulador, true, true)]
    [InlineData(true, TipoConexion.Serial, false, true)]
    [InlineData(true, TipoConexion.Tcp, false, true)]
    [InlineData(false, TipoConexion.Simulador, true, false)]
    public void PuedeFinalizar_Combinaciones(bool empresa, TipoConexion tipo, bool confirma, bool esperado)
    {
        var estado = new EstadoPuestaEnMarcha(false, empresa, "X", tipo, false, 0, 0);

        Assert.Equal(esperado, ReglasPuestaEnMarcha.PuedeFinalizar(estado, confirma));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests.Balanza;

public class ServicioConfiguracionBalanzaTests
{
    private class EnumeradorPuertosFalso : IEnumeradorPuertosSerie
    {
        public List<string> Puertos { get; } = new() { "COM1", "COM2" };
        public IReadOnlyList<string> Listar() => Puertos.ToList();
    }

    private class ServicioBalanzaConFalloReinicio : ServicioBalanzaFalso
    {
        public override Task ReiniciarAsync(CancellationToken cancellationToken = default)
        {
            base.ReiniciarAsync(cancellationToken);
            throw new InvalidOperationException("Fallo forzado al reiniciar balanza.");
        }
    }

    private static ConfiguracionBalanzaDto CrearValida() => new(
        TipoConexion: TipoConexion.Serial,
        PuertoCom: "COM1",
        BaudRate: 9600,
        DataBits: 8,
        Paridad: ParidadSerial.None,
        BitsParada: BitsParada.One,
        HostTcp: "127.0.0.1",
        PuertoTcp: 4001,
        PatronRegex: @"(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)",
        LecturasParaEstable: 5,
        ToleranciaKg: 0.5m,
        TimeoutMs: 1500);

    [Theory]
    [InlineData(Rol.Operador)]
    [InlineData(Rol.Supervisor)]
    public async Task Metodos_SinPermisoConfigurarBalanza_LanzanNoAutorizadoException(Rol rol)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(2, "usuario", "Usuario Prueba", rol, false));

        var balanza = new ServicioBalanzaFalso();
        var enumerador = new EnumeradorPuertosFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanza,
            enumerador,
            tiempo,
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ActualizarAsync(CrearValida()));
        Assert.Throws<NoAutorizadoException>(() => servicio.ListarPuertosSerie());
    }

    [Fact]
    public async Task ObtenerAsync_ConAdministrador_DevuelveConfiguracionSembrada()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));

        var balanza = new ServicioBalanzaFalso();
        var enumerador = new EnumeradorPuertosFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanza,
            enumerador,
            tiempo,
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        var leida = await servicio.ObtenerAsync();

        Assert.NotNull(leida);
        Assert.NotNull(leida.Configuracion);
        Assert.Equal(@"^[^-+\d]*(?<peso>[-+]?\s*\d+(?:[.,]\d+)?)", leida.Configuracion.PatronRegex);
        Assert.Equal(TipoConexion.Simulador, leida.Configuracion.TipoConexion);

        var puertos = servicio.ListarPuertosSerie();
        Assert.Equal(2, puertos.Count);
    }

    [Fact]
    public async Task ActualizarAsync_CambioValido_PersisteAuditaYReiniciaBalanza()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        var usuarioSesion = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var balanza = new ServicioBalanzaFalso();
        var enumerador = new EnumeradorPuertosFalso();
        var fechaFijada = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var tiempo = new FakeTimeProvider(fechaFijada);

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanza,
            enumerador,
            tiempo,
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        var dto = CrearValida() with { PuertoCom = " com3 ", TipoConexion = TipoConexion.Serial };

        bool resultado = await servicio.ActualizarAsync(dto);

        Assert.True(resultado);
        Assert.Equal(1, balanza.VecesReiniciado);

        // Verificar en BD
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            Assert.Equal("COM3", config.PuertoCom);
            Assert.Equal(fechaFijada.UtcDateTime, config.ModificadoEn);

            var auditorias = await ctx.Auditorias.ToListAsync();
            var audit = Assert.Single(auditorias);
            Assert.Equal(AccionAuditoria.CambioConfigBalanza, audit.Accion);
            Assert.Equal("1", audit.EntidadId);
            Assert.Equal(usuarioSesion.Id, audit.UsuarioId);
            Assert.Contains("COM3", audit.DatosDespuesJson);
        }
    }

    [Fact]
    public async Task ActualizarAsync_SoloCambiaDelimitadorYUnidad_SeDetectaPersisteYAudita()
    {
        // Si el snapshot de auditoría no incluyera estos campos, el servicio creería que no hubo cambios.
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);
        var balanza = new ServicioBalanzaFalso();

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanza,
            new EnumeradorPuertosFalso(),
            new FakeTimeProvider(),
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        var leida = await servicio.ObtenerAsync();
        var dto = leida.Configuracion with
        {
            DelimitadorTrama = DelimitadorTrama.Espacio,
            UnidadPeso = UnidadPesoTrama.Gramos
        };

        Assert.True(await servicio.ActualizarAsync(dto));
        Assert.Equal(1, balanza.VecesReiniciado);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
        Assert.Equal(DelimitadorTrama.Espacio, config.DelimitadorTrama);
        Assert.Equal(UnidadPesoTrama.Gramos, config.UnidadPeso);

        var audit = Assert.Single(await ctx.Auditorias.ToListAsync());
        Assert.Contains("Gramos", audit.DatosDespuesJson);
        Assert.Contains("Kilogramos", audit.DatosAntesJson);
    }

    [Fact]
    public async Task ActualizarAsync_SinCambios_DevuelveFalseSinAuditarNiReiniciar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var balanza = new ServicioBalanzaFalso();
        var enumerador = new EnumeradorPuertosFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanza,
            enumerador,
            tiempo,
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        var leida = await servicio.ObtenerAsync();

        bool resultado = await servicio.ActualizarAsync(leida.Configuracion);

        Assert.False(resultado);
        Assert.Equal(0, balanza.VecesReiniciado);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            Assert.Empty(await ctx.Auditorias.ToListAsync());
        }
    }

    [Fact]
    public async Task ActualizarAsync_DtoInvalido_LanzaValidacionException_SinPersistirNiReiniciar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var balanza = new ServicioBalanzaFalso();
        var enumerador = new EnumeradorPuertosFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanza,
            enumerador,
            tiempo,
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        var dtoInvalido = CrearValida() with { PuertoCom = "INVALIDO" };

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.ActualizarAsync(dtoInvalido));
        Assert.Contains(ex.Errores, e => e.Campo == CamposBalanza.PuertoCom);

        Assert.Equal(0, balanza.VecesReiniciado);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            Assert.Empty(await ctx.Auditorias.ToListAsync());
        }
    }

    [Fact]
    public async Task ActualizarAsync_CuandoReiniciarBalanzaLanza_NoFallaYPersiste()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesion = new SesionActual();
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var balanzaConFallo = new ServicioBalanzaConFalloReinicio();
        var enumerador = new EnumeradorPuertosFalso();
        var tiempo = new FakeTimeProvider();

        var servicio = new ServicioConfiguracionBalanza(
            db.UnidadDeTrabajoFactory,
            sesion,
            balanzaConFallo,
            enumerador,
            tiempo,
            NullLogger<ServicioConfiguracionBalanza>.Instance);

        var dto = CrearValida() with { PuertoCom = "COM99" };

        bool resultado = await servicio.ActualizarAsync(dto);

        Assert.True(resultado);
        Assert.Equal(1, balanzaConFallo.VecesReiniciado);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var config = await ctx.ConfiguracionesBalanza.SingleAsync(c => c.Id == 1);
            Assert.Equal("COM99", config.PuertoCom);
        }
    }
}

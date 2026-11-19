using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Balanza.Dtos;
using PesajeFaeneo.Application.Balanza;
using PesajeFaeneo.Application.Pesajes;
using PesajeFaeneo.Application.Pesajes.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Domain.Reglas;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using PesajeFaeneo.Tests.Utilidades.Balanza;
using Xunit;

namespace PesajeFaeneo.Tests;

/// <summary>F10: fletero por pesaje (O5) y consulta de pesajes por día para el historial (O3).</summary>
public class ServicioPesajesF10Tests
{
    private static readonly TimeZoneInfo ZonaBolivia = TimeZoneInfo.CreateCustomTimeZone(
        "Bolivia_Test", TimeSpan.FromHours(-4), "Bolivia (Test)", "Bolivia (Test)");

    private static (ServicioPesajes Servicio, SesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(BaseDatosEnMemoria db)
    {
        var tiempo = new FakeTimeProvider(new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.FromHours(-4)));
        tiempo.SetLocalTimeZone(ZonaBolivia);
        var sesion = new SesionActual();
        var balanza = new ServicioBalanzaFalso
        {
            Instantanea = new InstantaneaBalanza(1, EstadoBalanza.Conectada, 120.0m, true, DateTimeOffset.UtcNow, null, "Conectada", false)
        };
        return (new ServicioPesajes(db.UnidadDeTrabajoFactory, sesion, balanza, new PesajeFaeneo.Tests.Utilidades.Licencias.ServicioLicenciaFalso(), tiempo, NullLogger<ServicioPesajes>.Instance), sesion, tiempo);
    }

    private static async Task<(int GanaderoId, int ClienteId)> SembrarAsync(BaseDatosEnMemoria db)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = new GanaderoBuilder().ConCodigo("1").ConNombre("GANADERO TEST").Build();
        var cliente = new ClienteBuilder().ConCodigo(100).ConNombre("CLIENTE TEST").Build();
        ctx.Ganaderos.Add(ganadero);
        ctx.Clientes.Add(cliente);
        await ctx.SaveChangesAsync();
        return (ganadero.Id, cliente.Id);
    }

    private static async Task<int> SembrarFleteroAsync(BaseDatosEnMemoria db, int codigo, bool activo = true)
    {
        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var fletero = new Fletero { Codigo = codigo, Nombre = $"FLETERO {codigo}", Celular = "70000000", Activo = activo, CreadoEn = DateTime.UtcNow };
        ctx.Fleteros.Add(fletero);
        await ctx.SaveChangesAsync();
        return fletero.Id;
    }

    private static RegistrarPesaje Dto(int ganaderoId, int clienteId, int? fleteroId = null) => new(
        GanaderoId: ganaderoId,
        ClienteId: clienteId,
        Direccion: "Calle Test 123",
        Secuencia: 1,
        ColorRaza: "Overo",
        Sexo: 'M',
        TipoDesposte: TipoDesposte.ResCompleta,
        PesoIzquierdo: 120.0m,
        PesoDerecho: 118.5m,
        Observacion: "Obs",
        CapturadoConSimulador: false,
        FleteroId: fleteroId);

    [Fact]
    public async Task RegistrarAsync_SinFletero_GrabaConFleteroNulo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);

        var r = await servicio.RegistrarAsync(Dto(g, c));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var pesaje = await ctx.Pesajes.SingleAsync(p => p.Id == r.Id);
        Assert.Null(pesaje.FleteroId);
        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.Pesaje && a.Accion == AccionAuditoria.Crear);
        Assert.Contains("\"FleteroId\":null", aud.DatosDespuesJson);
    }

    [Fact]
    public async Task RegistrarAsync_ConFletero_GuardaElFleteroYLoAuditaConCodigoYNombre()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);
        var fleteroId = await SembrarFleteroAsync(db, codigo: 12);

        var r = await servicio.RegistrarAsync(Dto(g, c, fleteroId));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(fleteroId, (await ctx.Pesajes.SingleAsync(p => p.Id == r.Id)).FleteroId);
        var aud = await ctx.Auditorias.SingleAsync(a => a.Entidad == EntidadesAuditoria.Pesaje && a.Accion == AccionAuditoria.Crear);
        Assert.Contains($"\"FleteroId\":{fleteroId}", aud.DatosDespuesJson);
        Assert.Contains("\"CodigoFletero\":12", aud.DatosDespuesJson);
        Assert.Contains("\"NombreFletero\":\"FLETERO 12\"", aud.DatosDespuesJson);
    }

    [Fact]
    public async Task RegistrarAsync_FleteroInactivo_LanzaValidacionYNoConsumeNumeroDeBoleta()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);
        var inactivo = await SembrarFleteroAsync(db, codigo: 3, activo: false);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.RegistrarAsync(Dto(g, c, inactivo)));
        Assert.Contains(ex.Errores, e => e.Campo == CamposPesaje.Fletero);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(ctx.Pesajes);
        Assert.Equal(1, (await ctx.ParametrosSistema.SingleAsync()).SiguienteNumeroBoleta);
    }

    [Fact]
    public async Task RegistrarAsync_FleteroInexistente_LanzaValidacion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.RegistrarAsync(Dto(g, c, fleteroId: 9999)));
        Assert.Contains(ex.Errores, e => e.Campo == CamposPesaje.Fletero);
    }

    [Fact]
    public async Task ListarPorDiaAsync_DevuelveSoloElDiaLocalIndicado_ConAnuladasYFletero()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);
        var fleteroId = await SembrarFleteroAsync(db, codigo: 4);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            // Día 26 (local UTC-4): 23:30 local = 03:30 UTC del 27 -> pertenece al día 26 local.
            var tarde26 = new PesajeBuilder().ConNumeroBoleta(1).ConGanaderoId(g).ConClienteId(c).ConUsuarioId(usuario.Id)
                .ConFechaHora(new DateTime(2026, 9, 27, 3, 30, 0, DateTimeKind.Utc)).Build();
            var manana27 = new PesajeBuilder().ConNumeroBoleta(2).ConGanaderoId(g).ConClienteId(c).ConUsuarioId(usuario.Id)
                .ConFechaHora(new DateTime(2026, 9, 27, 13, 0, 0, DateTimeKind.Utc)).Build();
            manana27.FleteroId = fleteroId;
            var anulada27 = new PesajeBuilder().ConNumeroBoleta(3).ConGanaderoId(g).ConClienteId(c).ConUsuarioId(usuario.Id)
                .ConFechaHora(new DateTime(2026, 9, 27, 14, 0, 0, DateTimeKind.Utc)).Anulado("ERROR", usuario.Id).Build();
            var dia28 = new PesajeBuilder().ConNumeroBoleta(4).ConGanaderoId(g).ConClienteId(c).ConUsuarioId(usuario.Id)
                .ConFechaHora(new DateTime(2026, 9, 28, 13, 0, 0, DateTimeKind.Utc)).Build();
            ctx.Pesajes.AddRange(tarde26, manana27, anulada27, dia28);
            await ctx.SaveChangesAsync();
        }

        var d27 = await servicio.ListarPorDiaAsync(new DateOnly(2026, 9, 27));
        Assert.Equal(new long[] { 3, 2 }, d27.Select(p => p.NumeroBoleta)); // más reciente primero; incluye la anulada
        Assert.Equal(EstadoPesaje.Anulado, d27[0].Estado);
        Assert.Equal("FLETERO 4", d27[1].NombreFletero);
        Assert.Null(d27[0].NombreFletero);
        Assert.Equal("1", d27[0].Codigo);

        var d26 = await servicio.ListarPorDiaAsync(new DateOnly(2026, 9, 26));
        Assert.Equal(1L, Assert.Single(d26).NumeroBoleta);

        Assert.Empty(await servicio.ListarPorDiaAsync(new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public async Task ListarPorDiaAsync_SinSesion_LanzaNoAutorizado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarPorDiaAsync(new DateOnly(2026, 9, 27)));
    }

    [Fact]
    public async Task ListarDelDiaAsync_SigueListandoSoloHoy()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usuario = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);
        var (g, c) = await SembrarAsync(db);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(1).ConGanaderoId(g).ConClienteId(c).ConUsuarioId(usuario.Id)
                .ConFechaHora(new DateTime(2026, 9, 27, 13, 0, 0, DateTimeKind.Utc)).Build());
            ctx.Pesajes.Add(new PesajeBuilder().ConNumeroBoleta(2).ConGanaderoId(g).ConClienteId(c).ConUsuarioId(usuario.Id)
                .ConFechaHora(new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc)).Build());
            await ctx.SaveChangesAsync();
        }

        Assert.Equal(1L, Assert.Single(await servicio.ListarDelDiaAsync()).NumeroBoleta);
    }
}

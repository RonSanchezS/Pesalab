using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioFleterosTests
{
    private (ServicioFleteros Servicio, ISesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        DateTime? tiempoInicial = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var sesion = new SesionActual();
        var servicio = new ServicioFleteros(db.UnidadDeTrabajoFactory, sesion, tiempo, NullLogger<ServicioFleteros>.Instance);
        return (servicio, sesion, tiempo);
    }

    [Fact]
    public async Task Metodos_SinSesion_LanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarActivosAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarParaBusquedaAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearFletero("Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarFletero(1, "Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));
    }

    [Fact]
    public async Task Metodos_ConSesionOperador_GestionLanzaNoAutorizadoYListarParaBusquedaTieneExito()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearFletero("Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarFletero(1, "Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));

        Assert.NotNull(await servicio.ListarActivosAsync());
        Assert.NotNull(await servicio.ListarParaBusquedaAsync()); // el Operador elige fletero al pesar
    }

    [Fact]
    public async Task CrearAsync_AsignaCodigosCorrelativos_SinLimiteDeFleteros()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        // F10 (O5): el 5.º y el 10.º fletero se dan de alta sin problema (antes: FLE_MAXIMO al quinto).
        var ids = new List<int>();
        for (var i = 1; i <= 10; i++)
        {
            ids.Add(await servicio.CrearAsync(new CrearFletero($"FLETERO {i:00}", $"7000-00{i:00}")));
        }

        var todos = await servicio.ListarAsync(true);
        Assert.Equal(10, todos.Count);
        Assert.Equal(Enumerable.Range(1, 10), todos.OrderBy(f => f.Codigo).Select(f => f.Codigo));
        Assert.Equal(5, (await servicio.ObtenerAsync(ids[4])).Codigo);
        Assert.Equal(10, (await servicio.ObtenerAsync(ids[9])).Codigo);
    }

    [Fact]
    public async Task CrearAsync_ElCodigoIncluyeInactivos_NoSeReutiliza_YLosInactivosConviven()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearFletero("FLETERO 1", "7000-0001"));
        await servicio.CambiarEstadoAsync(id1, false);
        var id2 = await servicio.CrearAsync(new CrearFletero("FLETERO 2", "7000-0002"));
        await servicio.CambiarEstadoAsync(id2, false);
        var id3 = await servicio.CrearAsync(new CrearFletero("FLETERO 3", "7000-0003"));

        var todos = await servicio.ListarAsync(true);
        Assert.Equal(3, todos.Count);
        Assert.Equal(3, (await servicio.ObtenerAsync(id3)).Codigo);
        Assert.Equal(2, todos.Count(f => !f.Activo));
    }

    [Fact]
    public async Task CrearAsync_ConflictoPorCodigoRepetido_ReintentaUnaVezConElSiguienteCodigo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, sesion, tiempo) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var factory = new UowFactoryConConflictoEnAlta(db.UnidadDeTrabajoFactory, fallos: 1, alFallar: async () =>
        {
            await using var otro = await db.ContextFactory.CreateDbContextAsync();
            await otro.Database.ExecuteSqlRawAsync(
                "INSERT INTO Fleteros (Codigo, Nombre, Celular, Activo, CreadoEn) VALUES (1, 'GANADOR', '70000000', 1, '2026-01-01 00:00:00')");
        });
        var servicio = new ServicioFleteros(factory, sesion, tiempo, NullLogger<ServicioFleteros>.Instance);

        var id = await servicio.CrearAsync(new CrearFletero("PERDEDOR", "7000-0009"));

        Assert.Equal(2, (await servicio.ObtenerAsync(id)).Codigo);
    }

    [Fact]
    public async Task CrearAsync_DatosInvalidos_LanzaValidacionException_SinCampoCodigo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearFletero("1", "abc")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Nombre);
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Celular);
        Assert.DoesNotContain(ex.Errores, e => e.Campo == CamposCatalogo.Codigo);
    }

    [Fact]
    public async Task EditarAsync_ModificaNombreYCelular_NoCambiaElCodigo_RegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearFletero("FLETERO ORIGINAL", "7000-0001"));

        await servicio.EditarAsync(new EditarFletero(id, "FLETERO EDITADO", "7000-0002"));

        var obtenido = await servicio.ObtenerAsync(id);
        Assert.Equal("FLETERO EDITADO", obtenido.Nombre);
        Assert.Equal("7000-0002", obtenido.Celular);
        Assert.Equal(1, obtenido.Codigo);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var auditorias = await ctx.Auditorias
            .Where(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Fletero)
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Equal(2, auditorias.Count);
        Assert.Equal(AccionAuditoria.Modificar, auditorias[1].Accion);
    }

    [Fact]
    public async Task CambiarEstadoAsync_ReactivarSiempreEsPosible_ElCodigoEsUnico()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearFletero("FLETERO ANTIGUO", "7000-0001"));
        await servicio.CambiarEstadoAsync(id1, false);
        await servicio.CrearAsync(new CrearFletero("FLETERO NUEVO", "7000-0002"));

        // Antes fallaba con FLE_ORDEN_OCUPADO: ahora cada fletero tiene su propio código.
        await servicio.CambiarEstadoAsync(id1, true);

        Assert.True((await servicio.ObtenerAsync(id1)).Activo);
    }

    [Fact]
    public async Task ListarActivosAsync_OrdenadoPorCodigo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearFletero("FLETERO TRES", "7000-0003"));
        await servicio.CrearAsync(new CrearFletero("FLETERO UNO", "7000-0001"));
        await servicio.CrearAsync(new CrearFletero("FLETERO DOS", "7000-0002"));

        var activos = await servicio.ListarActivosAsync();
        Assert.Equal(3, activos.Count);
        Assert.Equal(new[] { 1, 2, 3 }, activos.Select(f => f.Codigo));
        Assert.Equal("FLETERO TRES", activos[0].Nombre); // el primero en crearse recibe el código 1
    }

    [Fact]
    public async Task ListarParaBusquedaAsync_SoloActivos_ConCodigoYCelular()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearFletero("FLETERO UNO", "7000-0001"));
        var id2 = await servicio.CrearAsync(new CrearFletero("FLETERO DOS", "7000-0002"));
        await servicio.CambiarEstadoAsync(id2, false);

        var busqueda = await servicio.ListarParaBusquedaAsync();

        var unico = Assert.Single(busqueda);
        Assert.Equal("1", unico.Codigo);
        Assert.Equal("FLETERO UNO", unico.Descripcion);
        Assert.Equal("7000-0001", unico.Detalle);
    }
}

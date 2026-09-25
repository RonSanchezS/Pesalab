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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearFletero(1, "Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarFletero(1, 1, "Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));
    }

    [Fact]
    public async Task Metodos_ConSesionOperador_GestionLanzaNoAutorizadoYListarActivosTieneExito()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearFletero(1, "Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarFletero(1, 1, "Fletero", "7000-0000")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));

        var listaActivos = await servicio.ListarActivosAsync();
        Assert.NotNull(listaActivos);
    }

    [Fact]
    public async Task CrearAsync_OrdenOcupado_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearFletero(1, "FLETERO UNO", "7111-1111"));

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearFletero(1, "OTRO FLETERO", "7222-2222")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Orden && e.Mensaje.Contains("F1 ya está asignado"));
    }

    [Fact]
    public async Task CrearAsync_QuintoActivo_LanzaReglaNegocioExceptionFLE_MAXIMO()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearFletero(1, "FLETERO 1", "7000-0001"));
        await servicio.CrearAsync(new CrearFletero(2, "FLETERO 2", "7000-0002"));
        await servicio.CrearAsync(new CrearFletero(3, "FLETERO 3", "7000-0003"));
        await servicio.CrearAsync(new CrearFletero(4, "FLETERO 4", "7000-0004"));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearAsync(new CrearFletero(1, "FLETERO 5", "7000-0005")));
        Assert.Equal("FLE_MAXIMO", ex.Codigo);
    }

    [Fact]
    public async Task Inactivos_DosInactivosConMismoOrdenConviven()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearFletero(1, "FLETERO 1", "7000-0001"));
        await servicio.CambiarEstadoAsync(id1, false);

        var id2 = await servicio.CrearAsync(new CrearFletero(1, "FLETERO 2", "7000-0002"));
        await servicio.CambiarEstadoAsync(id2, false);

        var todos = await servicio.ListarAsync(true);
        Assert.Equal(2, todos.Count(f => f.Orden == 1 && !f.Activo));
    }

    [Fact]
    public async Task CambiarEstadoAsync_ReactivarConOrdenOcupado_LanzaReglaNegocioExceptionFLE_ORDEN_OCUPADO()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearFletero(2, "FLETERO ANTIGUO", "7000-0001"));
        await servicio.CambiarEstadoAsync(id1, false);

        // Crear otro activo con orden 2
        await servicio.CrearAsync(new CrearFletero(2, "FLETERO NUEVO", "7000-0002"));

        // Intentar reactivar id1 debe fallar porque orden 2 está ocupado
        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CambiarEstadoAsync(id1, true));
        Assert.Equal("FLE_ORDEN_OCUPADO", ex.Codigo);
    }

    [Fact]
    public async Task ListarActivosAsync_OrdenadoPorColumnaOrden()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearFletero(3, "FLETERO TRES", "7000-0003"));
        await servicio.CrearAsync(new CrearFletero(1, "FLETERO UNO", "7000-0001"));
        await servicio.CrearAsync(new CrearFletero(2, "FLETERO DOS", "7000-0002"));

        var activos = await servicio.ListarActivosAsync();
        Assert.Equal(3, activos.Count);
        Assert.Equal(1, activos[0].Orden);
        Assert.Equal(2, activos[1].Orden);
        Assert.Equal(3, activos[2].Orden);
    }
}

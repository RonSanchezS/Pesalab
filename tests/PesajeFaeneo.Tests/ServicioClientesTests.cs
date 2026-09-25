using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Catalogos;
using PesajeFaeneo.Application.Catalogos.Dtos;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using PesajeFaeneo.Tests.Utilidades;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioClientesTests
{
    private (ServicioClientes Servicio, ISesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        DateTime? tiempoInicial = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var sesion = new SesionActual();
        var servicio = new ServicioClientes(db.UnidadDeTrabajoFactory, sesion, tiempo, NullLogger<ServicioClientes>.Instance);
        return (servicio, sesion, tiempo);
    }

    [Fact]
    public async Task Metodos_SinSesion_LanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarParaBusquedaAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.SugerirSiguienteCodigoAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearCliente(100, "Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarCliente(1, 100, "Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));
    }

    [Fact]
    public async Task Metodos_ConSesionOperador_GestionLanzaNoAutorizadoYBusquedaTieneExito()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.SugerirSiguienteCodigoAsync());
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearCliente(100, "Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarCliente(1, 100, "Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));

        var listaBusqueda = await servicio.ListarParaBusquedaAsync();
        Assert.NotNull(listaBusqueda);
    }

    [Fact]
    public async Task SugerirSiguienteCodigoAsync_BaseVaciaDevuelveUno_ConInactivosDevuelveMaxMasUno()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var primero = await servicio.SugerirSiguienteCodigoAsync();
        Assert.Equal(1, primero);

        var id1 = await servicio.CrearAsync(new CrearCliente(50, "CLIENTE 50", null));
        var id2 = await servicio.CrearAsync(new CrearCliente(100, "CLIENTE 100", null));
        await servicio.CambiarEstadoAsync(id2, false); // Desactivar el 100

        var siguiente = await servicio.SugerirSiguienteCodigoAsync();
        Assert.Equal(101, siguiente);
    }

    [Fact]
    public async Task CrearAsync_Valido_NormalizaMayusculasYGuardaAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var fechaEsperada = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, _) = CrearServicio(db, fechaEsperada);
        var usuarioSesion = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente(150, "  frial   los   amigos ", " calle   1 "));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var cliente = await ctx.Clientes.FindAsync(id);
        Assert.NotNull(cliente);
        Assert.Equal(150, cliente.Codigo);
        Assert.Equal("FRIAL LOS AMIGOS", cliente.Nombre);
        Assert.Equal("CALLE 1", cliente.Direccion);
        Assert.True(cliente.Activo);
        Assert.Equal(fechaEsperada, cliente.CreadoEn);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Cliente);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usuarioSesion.Id, auditoria.UsuarioId);
        Assert.Contains("\"Codigo\":150", auditoria.DatosDespuesJson);
    }

    [Fact]
    public async Task CrearAsync_CodigoDuplicado_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearCliente(200, "CLIENTE 200", null));

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearCliente(200, "OTRO CLIENTE", null)));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Codigo && e.Mensaje.Contains("Ya existe un cliente"));
    }

    [Fact]
    public async Task CrearAsync_CodigoPerteneceAInactivo_LanzaValidacionExceptionConMensajeReactivar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente(200, "CLIENTE A DESACTIVAR", null));
        await servicio.CambiarEstadoAsync(id, false);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearCliente(200, "NUEVO CLIENTE", null)));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Codigo && e.Mensaje.Contains("Reactívelo en lugar de crear uno nuevo."));
    }

    [Fact]
    public async Task EditarAsync_ModificaDatos_RegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente(200, "CLIENTE ORIGINAL", "DIR ANT"));

        await servicio.EditarAsync(new EditarCliente(id, 200, "CLIENTE EDITADO", "DIR NUEVA"));

        var obtenido = await servicio.ObtenerAsync(id);
        Assert.Equal("CLIENTE EDITADO", obtenido.Nombre);
        Assert.Equal("DIR NUEVA", obtenido.Direccion);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var auditorias = await ctx.Auditorias
            .Where(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Cliente)
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, auditorias.Count);
        Assert.Equal(AccionAuditoria.Modificar, auditorias[1].Accion);
    }

    [Fact]
    public async Task EditarAsync_SinCambios_NoRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente(200, "CLIENTE FIJO", "DIRECCION FIJA"));

        await servicio.EditarAsync(new EditarCliente(id, 200, "  cliente   fijo  ", "  direccion   fija  "));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var conteo = await ctx.Auditorias.CountAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Cliente);
        Assert.Equal(1, conteo);
    }

    [Fact]
    public async Task EditarAsync_CambioCodigoConPesajes_LanzaReglaNegocioExceptionCLI_CODIGO_EN_USO()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente(200, "CLIENTE CON PESAJES", null));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var ganadero = new GanaderoBuilder().ConMarca("GAN").Build();
            ctx.Ganaderos.Add(ganadero);
            await ctx.SaveChangesAsync();

            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(9001)
                .ConGanaderoId(ganadero.Id)
                .ConClienteId(id)
                .ConUsuarioId(usr.Id)
                .Build();
            ctx.Pesajes.Add(pesaje);
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.EditarAsync(new EditarCliente(id, 201, "CLIENTE CON PESAJES", null)));
        Assert.Equal("CLI_CODIGO_EN_USO", ex.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesactivarYReactivar_ActualizaEstadoYAuditorias()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente(300, "CLIENTE ESTADO", null));

        await servicio.CambiarEstadoAsync(id, false);
        var listadoSinInactivos = await servicio.ListarAsync(false);
        Assert.DoesNotContain(listadoSinInactivos, c => c.Id == id);

        await servicio.CambiarEstadoAsync(id, true);
        var listadoConActivos = await servicio.ListarAsync(false);
        Assert.Contains(listadoConActivos, c => c.Id == id);
    }
}

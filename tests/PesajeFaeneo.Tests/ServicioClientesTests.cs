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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearCliente("Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearRapidoAsync(new CrearCliente("Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarCliente(1, "Cliente", null)));
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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearCliente("Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarCliente(1, "Cliente", null)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));

        var listaBusqueda = await servicio.ListarParaBusquedaAsync();
        Assert.NotNull(listaBusqueda);
    }

    [Fact]
    public async Task CrearAsync_AsignaCodigosCorrelativos_IncluyendoInactivos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearCliente("CLIENTE UNO", null));
        var id2 = await servicio.CrearAsync(new CrearCliente("CLIENTE DOS", null));
        await servicio.CambiarEstadoAsync(id2, false); // el código 2 no se reutiliza
        var id3 = await servicio.CrearAsync(new CrearCliente("CLIENTE TRES", null));

        Assert.Equal(1, (await servicio.ObtenerAsync(id1)).Codigo);
        Assert.Equal(2, (await servicio.ObtenerAsync(id2)).Codigo);
        Assert.Equal(3, (await servicio.ObtenerAsync(id3)).Codigo);
    }

    [Fact]
    public async Task CrearAsync_ContinuaDespuesDelMayorCodigoExistente()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Clientes.Add(new ClienteBuilder().ConCodigo(50).ConNombre("IMPORTADO 50").Build());
            ctx.Clientes.Add(new ClienteBuilder().ConCodigo(110).ConNombre("IMPORTADO 110").Inactivo().Build());
            await ctx.SaveChangesAsync();
        }

        var id = await servicio.CrearAsync(new CrearCliente("NUEVO", null));

        Assert.Equal(111, (await servicio.ObtenerAsync(id)).Codigo);
    }

    [Fact]
    public async Task CrearAsync_AlAgotarseElUltimoCodigo_LanzaReglaNegocioException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Clientes.Add(new ClienteBuilder().ConCodigo(ReglasCatalogos.CodigoClienteMaximo).Build());
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearAsync(new CrearCliente("SIN CODIGO", null)));
        Assert.Equal("CLI_CODIGO_AGOTADO", ex.Codigo);
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
                "INSERT INTO Clientes (Codigo, Nombre, Direccion, Activo, CreadoEn) VALUES (1, 'GANADOR', '', 1, '2026-01-01 00:00:00')");
        });
        var servicio = new ServicioClientes(factory, sesion, tiempo, NullLogger<ServicioClientes>.Instance);

        var id = await servicio.CrearAsync(new CrearCliente("PERDEDOR", null));

        Assert.Equal(2, (await servicio.ObtenerAsync(id)).Codigo);
        Assert.Equal(3, factory.LlamadasAGuardar); // intento fallido + reintento + guardado de la auditoría
    }

    [Fact]
    public async Task CrearAsync_Valido_NormalizaMayusculasYGuardaAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var fechaEsperada = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, _) = CrearServicio(db, fechaEsperada);
        var usuarioSesion = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente("  frial   los   amigos ", " calle   1 "));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var cliente = await ctx.Clientes.FindAsync(id);
        Assert.NotNull(cliente);
        Assert.Equal(1, cliente.Codigo);
        Assert.Equal("FRIAL LOS AMIGOS", cliente.Nombre);
        Assert.Equal("CALLE 1", cliente.Direccion);
        Assert.True(cliente.Activo);
        Assert.Equal(fechaEsperada, cliente.CreadoEn);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Cliente);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usuarioSesion.Id, auditoria.UsuarioId);
        Assert.Contains("\"Codigo\":1", auditoria.DatosDespuesJson);
    }

    [Fact]
    public async Task CrearAsync_NombreInvalido_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearCliente("12", null)));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Nombre);
        Assert.DoesNotContain(ex.Errores, e => e.Campo == CamposCatalogo.Codigo);
    }

    [Fact]
    public async Task CrearRapidoAsync_ConSesionOperador_CreaClienteYRegistraAuditoriaConMotivoAltaRapida()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var id = await servicio.CrearRapidoAsync(new CrearCliente("CLIENTE RAPIDO", null));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var cliente = await ctx.Clientes.FindAsync(id);
        Assert.NotNull(cliente);
        Assert.Equal(1, cliente.Codigo);
        Assert.Equal("CLIENTE RAPIDO", cliente.Nombre);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Cliente);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usr.Id, auditoria.UsuarioId);
        Assert.Equal("Alta rápida desde pesaje", auditoria.Motivo);
    }

    [Fact]
    public async Task EditarAsync_ModificaDatos_NoCambiaElCodigo_RegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente("CLIENTE ORIGINAL", "DIR ANT"));

        await servicio.EditarAsync(new EditarCliente(id, "CLIENTE EDITADO", "DIR NUEVA"));

        var obtenido = await servicio.ObtenerAsync(id);
        Assert.Equal("CLIENTE EDITADO", obtenido.Nombre);
        Assert.Equal("DIR NUEVA", obtenido.Direccion);
        Assert.Equal(1, obtenido.Codigo);

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

        var id = await servicio.CrearAsync(new CrearCliente("CLIENTE FIJO", "DIRECCION FIJA"));

        await servicio.EditarAsync(new EditarCliente(id, "  cliente   fijo  ", "  direccion   fija  "));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var conteo = await ctx.Auditorias.CountAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Cliente);
        Assert.Equal(1, conteo);
    }

    [Fact]
    public async Task EditarAsync_ConPesajes_PermiteCambiarElNombre()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente("CLIENTE CON PESAJES", null));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var ganadero = new GanaderoBuilder().ConCodigo("GAN").Build();
            ctx.Ganaderos.Add(ganadero);
            await ctx.SaveChangesAsync();

            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(9001)
                .ConGanaderoId(ganadero.Id)
                .ConClienteId(id)
                .ConUsuarioId(usr.Id)
                .Build());
            await ctx.SaveChangesAsync();
        }

        await servicio.EditarAsync(new EditarCliente(id, "CLIENTE RENOMBRADO", null));

        var obtenido = await servicio.ObtenerAsync(id);
        Assert.Equal("CLIENTE RENOMBRADO", obtenido.Nombre);
        Assert.Equal(1, obtenido.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesactivarYReactivar_ActualizaEstadoYAuditorias()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearCliente("CLIENTE ESTADO", null));

        await servicio.CambiarEstadoAsync(id, false);
        var listadoSinInactivos = await servicio.ListarAsync(false);
        Assert.DoesNotContain(listadoSinInactivos, c => c.Id == id);

        await servicio.CambiarEstadoAsync(id, true);
        var listadoConActivos = await servicio.ListarAsync(false);
        Assert.Contains(listadoConActivos, c => c.Id == id);
    }
}

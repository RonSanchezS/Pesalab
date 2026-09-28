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

public class ServicioGanaderosTests
{
    private (ServicioGanaderos Servicio, ISesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        DateTime? tiempoInicial = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var sesion = new SesionActual();
        var servicio = new ServicioGanaderos(db.UnidadDeTrabajoFactory, sesion, tiempo, NullLogger<ServicioGanaderos>.Instance);
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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearGanadero("AH", "Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearRapidoAsync(new CrearGanadero("AH", "Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarGanadero(1, "AH", "Ganadero")));
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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearGanadero("AH", "Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarGanadero(1, "AH", "Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));

        var listaBusqueda = await servicio.ListarParaBusquedaAsync();
        Assert.NotNull(listaBusqueda);
    }

    [Fact]
    public async Task CrearAsync_Valido_NormalizaMayusculasYGuardaAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var fechaEsperada = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, _) = CrearServicio(db, fechaEsperada);
        var usuarioSesion = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("  a h  ", "  juan   pérez "));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = await ctx.Ganaderos.FindAsync(id);
        Assert.NotNull(ganadero);
        Assert.Equal("AH", ganadero.Marca);
        Assert.Equal("JUAN PÉREZ", ganadero.Nombre);
        Assert.True(ganadero.Activo);
        Assert.Equal(fechaEsperada, ganadero.CreadoEn);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usuarioSesion.Id, auditoria.UsuarioId);
        Assert.Contains("\"Marca\":\"AH\"", auditoria.DatosDespuesJson);
    }

    [Fact]
    public async Task CrearAsync_MarcaDuplicadaDistintaCapitalizacion_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await servicio.CrearAsync(new CrearGanadero("AH", "GANADERO ORIGINAL"));

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearGanadero("ah", "GANADERO MINUSCULA")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Marca && e.Mensaje.Contains("Ya existe un ganadero"));
    }

    [Fact]
    public async Task CrearAsync_MarcaPerteneceAInactivo_LanzaValidacionExceptionConMensajeReactivar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("AH", "GANADERO A DESACTIVAR"));
        await servicio.CambiarEstadoAsync(id, false);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearGanadero("AH", "OTRO GANADERO")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Marca && e.Mensaje.Contains("Reactívelo en lugar de crear uno nuevo."));
    }

    [Fact]
    public async Task CrearRapidoAsync_ConSesionOperador_CreaGanaderoYRegistraAuditoriaConMotivoAltaRapida()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var id = await servicio.CrearRapidoAsync(new CrearGanadero("GR", "GANADERO RAPIDO"));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = await ctx.Ganaderos.FindAsync(id);
        Assert.NotNull(ganadero);
        Assert.Equal("GR", ganadero.Marca);
        Assert.Equal("GANADERO RAPIDO", ganadero.Nombre);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usr.Id, auditoria.UsuarioId);
        Assert.Equal("Alta rápida desde pesaje", auditoria.Motivo);
    }

    [Fact]
    public async Task CrearRapidoAsync_MarcaPerteneceAInactivo_LanzaValidacionExceptionConMensajePidaASupervisor()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador, "admin_inactivo");

        var id = await servicio.CrearAsync(new CrearGanadero("IN", "GANADERO INACTIVO"));
        await servicio.CambiarEstadoAsync(id, false);

        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador, "operador_inactivo");
        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearRapidoAsync(new CrearGanadero("IN", "OTRO")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Marca && e.Mensaje.Contains("Pida a un supervisor que lo reactive."));
    }

    [Fact]
    public async Task CrearAsync_DatosInvalidos_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearGanadero("", "123")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Marca);
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Nombre);
    }

    [Fact]
    public async Task EditarAsync_ModificaNombre_RegistraAuditoriaConAntesYDespues()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("AH", "NOMBRE ANTERIOR"));

        await servicio.EditarAsync(new EditarGanadero(id, "AH", "NOMBRE NUEVO"));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = await ctx.Ganaderos.FindAsync(id);
        Assert.NotNull(ganadero);
        Assert.Equal("NOMBRE NUEVO", ganadero.Nombre);

        var auditorias = await ctx.Auditorias
            .Where(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero)
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, auditorias.Count);
        var modAud = auditorias[1];
        Assert.Equal(AccionAuditoria.Modificar, modAud.Accion);
        Assert.Contains("NOMBRE ANTERIOR", modAud.DatosAntesJson);
        Assert.Contains("NOMBRE NUEVO", modAud.DatosDespuesJson);
    }

    [Fact]
    public async Task EditarAsync_SinCambios_NoRegistraAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("AH", "NOMBRE FIJO"));

        await servicio.EditarAsync(new EditarGanadero(id, "ah", "  nombre   fijo  "));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var conteoAuditorias = await ctx.Auditorias
            .CountAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero);

        Assert.Equal(1, conteoAuditorias); // Solo la de alta
    }

    [Fact]
    public async Task EditarAsync_CambioMarcaSinPesajes_Permitido()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("AH", "GANADERO SIN PESAJES"));

        await servicio.EditarAsync(new EditarGanadero(id, "AH2", "GANADERO SIN PESAJES"));

        var obtenido = await servicio.ObtenerAsync(id);
        Assert.Equal("AH2", obtenido.Marca);
    }

    [Fact]
    public async Task EditarAsync_CambioMarcaConPesajes_LanzaReglaNegocioExceptionGAN_MARCA_EN_USO()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("AH", "GANADERO CON PESAJES"));

        // Insertar un pesaje que referencia a este ganadero
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var cliente = new ClienteBuilder().ConCodigo(200).Build();
            ctx.Clientes.Add(cliente);
            await ctx.SaveChangesAsync();

            var pesaje = new PesajeBuilder()
                .ConNumeroBoleta(8001)
                .ConGanaderoId(id)
                .ConClienteId(cliente.Id)
                .ConUsuarioId(usr.Id)
                .Build();
            ctx.Pesajes.Add(pesaje);
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.EditarAsync(new EditarGanadero(id, "OTRA", "GANADERO CON PESAJES")));
        Assert.Equal("GAN_MARCA_EN_USO", ex.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesactivarYReactivar_GeneraAuditoriasYExcluyeDeBusqueda()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("AH", "GANADERO PRUEBA ESTADO"));

        // Desactivar
        await servicio.CambiarEstadoAsync(id, false);

        var listadoSinInactivos = await servicio.ListarAsync(false);
        Assert.DoesNotContain(listadoSinInactivos, g => g.Id == id);

        var busqueda = await servicio.ListarParaBusquedaAsync();
        Assert.DoesNotContain(busqueda, b => b.Id == id);

        // Desactivar otra vez (mismo estado) no hace nada
        await servicio.CambiarEstadoAsync(id, false);

        // Reactivar
        await servicio.CambiarEstadoAsync(id, true);

        var listadoReactivado = await servicio.ListarAsync(false);
        Assert.Contains(listadoReactivado, g => g.Id == id);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var auditorias = await ctx.Auditorias
            .Where(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero && a.Accion == AccionAuditoria.Modificar)
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(2, auditorias.Count);
        Assert.Equal("Desactivación", auditorias[0].Motivo);
        Assert.Equal("Activación", auditorias[1].Motivo);
    }
}

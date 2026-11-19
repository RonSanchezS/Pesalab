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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearGanadero("Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearRapidoAsync(new CrearGanadero("Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarGanadero(1, "Ganadero")));
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
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearGanadero("Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarGanadero(1, "Ganadero")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));

        var listaBusqueda = await servicio.ListarParaBusquedaAsync();
        Assert.NotNull(listaBusqueda);
    }

    [Fact]
    public async Task CrearAsync_Valido_NormalizaMayusculasAsignaCodigoYGuardaAuditoria()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var fechaEsperada = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, _) = CrearServicio(db, fechaEsperada);
        var usuarioSesion = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("  juan   pérez "));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = await ctx.Ganaderos.FindAsync(id);
        Assert.NotNull(ganadero);
        Assert.Equal("1", ganadero.Codigo); // F10 (O6): lo asigna el sistema
        Assert.Equal("JUAN PÉREZ", ganadero.Nombre);
        Assert.True(ganadero.Activo);
        Assert.Equal(fechaEsperada, ganadero.CreadoEn);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usuarioSesion.Id, auditoria.UsuarioId);
        Assert.Contains("\"Codigo\":\"1\"", auditoria.DatosDespuesJson);
    }

    [Fact]
    public async Task CrearAsync_AsignaCodigosCorrelativos()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearGanadero("PRIMERO"));
        var id2 = await servicio.CrearAsync(new CrearGanadero("SEGUNDO"));
        var id3 = await servicio.CrearAsync(new CrearGanadero("TERCERO"));

        Assert.Equal("1", (await servicio.ObtenerAsync(id1)).Codigo);
        Assert.Equal("2", (await servicio.ObtenerAsync(id2)).Codigo);
        Assert.Equal("3", (await servicio.ObtenerAsync(id3)).Codigo);
    }

    [Fact]
    public async Task CrearAsync_IgnoraLosCodigosAlfanumericosHeredadosDelSistemaLegado()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Ganaderos.Add(new GanaderoBuilder().ConCodigo("AH").ConNombre("LEGADO UNO").Build());
            ctx.Ganaderos.Add(new GanaderoBuilder().ConCodigo("B7").ConNombre("LEGADO DOS").Build());
            ctx.Ganaderos.Add(new GanaderoBuilder().ConCodigo("12").ConNombre("NUMERICO").Build());
            await ctx.SaveChangesAsync();
        }

        var id = await servicio.CrearAsync(new CrearGanadero("NUEVO"));

        Assert.Equal("13", (await servicio.ObtenerAsync(id)).Codigo);
    }

    [Fact]
    public async Task CrearAsync_ElCodigoIncluyeALosInactivos_NoSeReutiliza()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id1 = await servicio.CrearAsync(new CrearGanadero("PRIMERO"));
        var id2 = await servicio.CrearAsync(new CrearGanadero("SEGUNDO"));
        await servicio.CambiarEstadoAsync(id2, false);

        var id3 = await servicio.CrearAsync(new CrearGanadero("TERCERO"));

        Assert.Equal("1", (await servicio.ObtenerAsync(id1)).Codigo);
        Assert.Equal("3", (await servicio.ObtenerAsync(id3)).Codigo);
    }

    [Fact]
    public async Task CrearAsync_AlAgotarseElUltimoCodigoNumerico_LanzaReglaNegocioException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Ganaderos.Add(new GanaderoBuilder().ConCodigo("99999").Build());
            await ctx.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CrearAsync(new CrearGanadero("SIN CODIGO")));
        Assert.Equal("GAN_CODIGO_AGOTADO", ex.Codigo);

        await using var ctx2 = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await ctx2.Ganaderos.CountAsync()); // la transacción no dejó nada
    }

    [Fact]
    public async Task CrearAsync_ConflictoPorCodigoRepetido_ReintentaUnaVezConElSiguienteCodigo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, sesion, tiempo) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        // Otra alta "simultánea" se lleva el código 1 justo antes de que se guarde la nuestra.
        var factory = new UowFactoryConConflictoEnAlta(db.UnidadDeTrabajoFactory, fallos: 1, alFallar: async () =>
        {
            await using var otro = await db.ContextFactory.CreateDbContextAsync();
            await otro.Database.ExecuteSqlRawAsync(
                "INSERT INTO Ganaderos (Codigo, Nombre, Activo, CreadoEn) VALUES ('1', 'GANADOR DE LA CARRERA', 1, '2026-01-01 00:00:00')");
        });
        var servicio = new ServicioGanaderos(factory, sesion, tiempo, NullLogger<ServicioGanaderos>.Instance);

        var id = await servicio.CrearAsync(new CrearGanadero("PERDEDOR"));

        var creado = await servicio.ObtenerAsync(id);
        Assert.Equal("2", creado.Codigo);
        Assert.Equal("PERDEDOR", creado.Nombre);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(2, await ctx.Ganaderos.CountAsync());
        Assert.Single(await ctx.Auditorias.Where(a => a.Entidad == EntidadesAuditoria.Ganadero && a.Accion == AccionAuditoria.Crear).ToListAsync());
    }

    [Fact]
    public async Task CrearAsync_ConflictoPersistente_PropagaTrasUnSoloReintento()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (_, sesion, tiempo) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var factory = new UowFactoryConConflictoEnAlta(db.UnidadDeTrabajoFactory, fallos: 5);
        var servicio = new ServicioGanaderos(factory, sesion, tiempo, NullLogger<ServicioGanaderos>.Instance);

        await Assert.ThrowsAsync<ConflictoException>(() => servicio.CrearAsync(new CrearGanadero("NUNCA")));

        Assert.Equal(2, factory.LlamadasAGuardar); // intento original + un reintento
    }

    [Fact]
    public async Task CrearRapidoAsync_ConSesionOperador_CreaGanaderoYRegistraAuditoriaConMotivoAltaRapida()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Operador);

        var id = await servicio.CrearRapidoAsync(new CrearGanadero("GANADERO RAPIDO"));
        Assert.True(id > 0);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = await ctx.Ganaderos.FindAsync(id);
        Assert.NotNull(ganadero);
        Assert.Equal("1", ganadero.Codigo);
        Assert.Equal("GANADERO RAPIDO", ganadero.Nombre);

        var auditoria = await ctx.Auditorias.FirstOrDefaultAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero);
        Assert.NotNull(auditoria);
        Assert.Equal(AccionAuditoria.Crear, auditoria.Accion);
        Assert.Equal(usr.Id, auditoria.UsuarioId);
        Assert.Equal("Alta rápida desde pesaje", auditoria.Motivo);
    }

    [Fact]
    public async Task CrearAsync_NombreInvalido_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => servicio.CrearAsync(new CrearGanadero("123")));
        Assert.Contains(ex.Errores, e => e.Campo == CamposCatalogo.Nombre);
        Assert.DoesNotContain(ex.Errores, e => e.Campo == CamposCatalogo.Codigo);
    }

    [Fact]
    public async Task EditarAsync_ModificaNombre_NoCambiaElCodigo_RegistraAuditoriaConAntesYDespues()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("NOMBRE ANTERIOR"));

        await servicio.EditarAsync(new EditarGanadero(id, "NOMBRE NUEVO"));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var ganadero = await ctx.Ganaderos.FindAsync(id);
        Assert.NotNull(ganadero);
        Assert.Equal("NOMBRE NUEVO", ganadero.Nombre);
        Assert.Equal("1", ganadero.Codigo); // el código no se edita

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

        var id = await servicio.CrearAsync(new CrearGanadero("NOMBRE FIJO"));

        await servicio.EditarAsync(new EditarGanadero(id, "  nombre   fijo  "));

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var conteoAuditorias = await ctx.Auditorias
            .CountAsync(a => a.EntidadId == id.ToString() && a.Entidad == EntidadesAuditoria.Ganadero);

        Assert.Equal(1, conteoAuditorias); // Solo la de alta
    }

    [Fact]
    public async Task EditarAsync_ConPesajes_PermiteCambiarElNombre_ElCodigoQuedaIgual()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        var usr = await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("GANADERO CON PESAJES"));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var cliente = new ClienteBuilder().ConCodigo(200).Build();
            ctx.Clientes.Add(cliente);
            await ctx.SaveChangesAsync();

            ctx.Pesajes.Add(new PesajeBuilder()
                .ConNumeroBoleta(8001)
                .ConGanaderoId(id)
                .ConClienteId(cliente.Id)
                .ConUsuarioId(usr.Id)
                .Build());
            await ctx.SaveChangesAsync();
        }

        await servicio.EditarAsync(new EditarGanadero(id, "GANADERO RENOMBRADO"));

        var obtenido = await servicio.ObtenerAsync(id);
        Assert.Equal("GANADERO RENOMBRADO", obtenido.Nombre);
        Assert.Equal("1", obtenido.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesactivarYReactivar_GeneraAuditoriasYExcluyeDeBusqueda()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);
        await SesionesDePrueba.IniciarAsync(db, sesion, Rol.Administrador);

        var id = await servicio.CrearAsync(new CrearGanadero("GANADERO PRUEBA ESTADO"));

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

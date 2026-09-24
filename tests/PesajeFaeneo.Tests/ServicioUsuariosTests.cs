using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioUsuariosTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly NullLogger<ServicioUsuarios> _logger = NullLogger<ServicioUsuarios>.Instance;

    private (ServicioUsuarios Servicio, ISesionActual Sesion, TimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        UsuarioSesion? sesionInicial = null)
    {
        var tiempo = TimeProvider.System;
        var sesion = new SesionActual();
        if (sesionInicial != null)
        {
            sesion.Iniciar(sesionInicial);
        }
        var servicio = new ServicioUsuarios(db.UnidadDeTrabajoFactory, _hasher, sesion, tiempo, _logger);
        return (servicio, sesion, tiempo);
    }

    [Fact]
    public async Task Metodos_SinSesion_LanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db, sesionInicial: null);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearUsuario("usr", "Nom", Rol.Operador, "pass123", "pass123")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarUsuario(1, "Nom", Rol.Operador)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.RestablecerPasswordAsync(1, "pass123", "pass123"));
    }

    [Fact]
    public async Task Metodos_ConSesionSupervisor_LanzanNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesionSupervisor = new UsuarioSesion(5, "sup", "Supervisor", Rol.Supervisor, false);
        var (servicio, _, _) = CrearServicio(db, sesionSupervisor);

        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ListarAsync(true));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.ObtenerAsync(1));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CrearAsync(new CrearUsuario("usr", "Nom", Rol.Operador, "pass123", "pass123")));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.EditarAsync(new EditarUsuario(1, "Nom", Rol.Operador)));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.CambiarEstadoAsync(1, false));
        await Assert.ThrowsAsync<NoAutorizadoException>(() => servicio.RestablecerPasswordAsync(1, "pass123", "pass123"));
    }

    [Fact]
    public async Task CrearAsync_AltaValida_QuedaConDebeCambiarPassword_YAuditoriaCrear()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.Add(admin);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        var nuevoId = await servicio.CrearAsync(new CrearUsuario("operador_nuevo", "Operador Nuevo", Rol.Operador, "clave123", "clave123"));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var creado = await ctx.Usuarios.SingleAsync(u => u.Id == nuevoId);
            Assert.Equal("operador_nuevo", creado.NombreUsuario);
            Assert.Equal("Operador Nuevo", creado.NombreCompleto);
            Assert.Equal(Rol.Operador, creado.Rol);
            Assert.True(creado.Activo);
            Assert.True(creado.DebeCambiarPassword);

            var aud = await ctx.Auditorias.SingleAsync(a => a.EntidadId == "operador_nuevo");
            Assert.Equal(AccionAuditoria.Crear, aud.Accion);
            Assert.Equal(adminId, aud.UsuarioId);
        }
    }

    [Fact]
    public async Task CrearAsync_AltaDuplicadaConOtraCapitalizacion_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder().ConNombreUsuario("juan").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.Add(u);
            await ctx.SaveChangesAsync();
            adminId = u.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "juan", "Juan", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        var ex = await Assert.ThrowsAsync<ValidacionException>(
            () => servicio.CrearAsync(new CrearUsuario("JUAN", "Juan Perez", Rol.Operador, "clave123", "clave123")));

        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.NombreUsuario && e.Mensaje == "Ya existe un usuario con ese nombre.");
    }

    [Fact]
    public async Task CrearAsync_AltaInvalida_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var sesionAdmin = new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        // Password corta, nombre vacío, confirmacion distinta
        var ex = await Assert.ThrowsAsync<ValidacionException>(
            () => servicio.CrearAsync(new CrearUsuario("", "", Rol.Operador, "123", "456")));

        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.NombreUsuario);
        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.NombreCompleto);
        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.Password);
        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.Confirmacion);
    }

    [Fact]
    public async Task EditarAsync_EdicionValida_ModificaYAudita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId, operadorId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            var op = new UsuarioBuilder().ConNombreUsuario("op1").ConRol(Rol.Operador).Build();
            ctx.Usuarios.AddRange(admin, op);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
            operadorId = op.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        await servicio.EditarAsync(new EditarUsuario(operadorId, "Nuevo Nombre Operador", Rol.Supervisor));

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var op = await ctx.Usuarios.SingleAsync(u => u.Id == operadorId);
            Assert.Equal("Nuevo Nombre Operador", op.NombreCompleto);
            Assert.Equal(Rol.Supervisor, op.Rol);

            var aud = await ctx.Auditorias.SingleAsync(a => a.EntidadId == "op1");
            Assert.Equal(AccionAuditoria.Modificar, aud.Accion);
        }
    }

    [Fact]
    public async Task EditarAsync_CambioDelPropioRol_LanzaReglaNegocioException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.Add(admin);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.EditarAsync(new EditarUsuario(adminId, "Admin Mod", Rol.Operador)));

        Assert.Equal("USR_PROPIO_ROL", ex.Codigo);
    }

    [Fact]
    public async Task EditarAsync_DegradarAlUltimoAdministradorActivo_LanzaReglaNegocioException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int admin1Id, admin2Id;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin1 = new UsuarioBuilder().ConNombreUsuario("admin1").ConRol(Rol.Administrador).Build();
            var admin2 = new UsuarioBuilder().ConNombreUsuario("admin2").ConRol(Rol.Administrador).Inactivo().Build();
            ctx.Usuarios.AddRange(admin1, admin2);
            await ctx.SaveChangesAsync();
            admin1Id = admin1.Id;
            admin2Id = admin2.Id;
        }

        // admin2 está inactivo, así que admin1 es el único activo. Supongamos que otra sesión intenta degradar admin1:
        var sesion = new UsuarioSesion(99, "superuser", "Super", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesion);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.EditarAsync(new EditarUsuario(admin1Id, "Admin 1", Rol.Operador)));

        Assert.Equal("USR_ULTIMO_ADMIN", ex.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesactivarseASiMismo_LanzaReglaNegocioException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.Add(admin);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.CambiarEstadoAsync(adminId, activo: false));

        Assert.Equal("USR_PROPIO_ESTADO", ex.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_DesactivarAlUltimoAdministradorActivo_LanzaReglaNegocioException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int admin1Id;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin1 = new UsuarioBuilder().ConNombreUsuario("admin1").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.Add(admin1);
            await ctx.SaveChangesAsync();
            admin1Id = admin1.Id;
        }

        var sesion = new UsuarioSesion(99, "otro_admin", "Otro", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesion);

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(
            () => servicio.CambiarEstadoAsync(admin1Id, activo: false));

        Assert.Equal("USR_ULTIMO_ADMIN", ex.Codigo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_ConDosAdministradoresActivos_PermiteDesactivarUno()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int admin1Id, admin2Id;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin1 = new UsuarioBuilder().ConNombreUsuario("admin1").ConRol(Rol.Administrador).Build();
            var admin2 = new UsuarioBuilder().ConNombreUsuario("admin2").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.AddRange(admin1, admin2);
            await ctx.SaveChangesAsync();
            admin1Id = admin1.Id;
            admin2Id = admin2.Id;
        }

        var sesionAdmin1 = new UsuarioSesion(admin1Id, "admin1", "Admin 1", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin1);

        await servicio.CambiarEstadoAsync(admin2Id, activo: false);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin2 = await ctx.Usuarios.SingleAsync(u => u.Id == admin2Id);
            Assert.False(admin2.Activo);

            var aud = await ctx.Auditorias.SingleAsync(a => a.EntidadId == "admin2");
            Assert.Equal("Desactivación", aud.Motivo);
        }
    }

    [Fact]
    public async Task CambiarEstadoAsync_Reactivar_ActualizaYAudita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId, opId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            var op = new UsuarioBuilder().ConNombreUsuario("op_inactivo").Inactivo().Build();
            ctx.Usuarios.AddRange(admin, op);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
            opId = op.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        await servicio.CambiarEstadoAsync(opId, activo: true);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var op = await ctx.Usuarios.SingleAsync(u => u.Id == opId);
            Assert.True(op.Activo);

            var aud = await ctx.Auditorias.SingleAsync(a => a.EntidadId == "op_inactivo");
            Assert.Equal("Activación", aud.Motivo);
        }
    }

    [Fact]
    public async Task CambiarEstadoAsync_EstadoSinCambio_NoHaceNadaNiAudita()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId, opId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            var op = new UsuarioBuilder().ConNombreUsuario("op_activo").Build();
            ctx.Usuarios.AddRange(admin, op);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
            opId = op.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        // Ya está activo, enviar activo: true no debe generar auditoría
        await servicio.CambiarEstadoAsync(opId, activo: true);

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            Assert.Empty(await ctx.Auditorias.ToListAsync());
        }
    }

    [Fact]
    public async Task RestablecerPasswordAsync_DesbloqueaYFuerzaCambio_YLoginPideCambiarPassword()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        int adminId, opId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            var op = new UsuarioBuilder()
                .ConNombreUsuario("op_bloqueado")
                .ConPassword("clave_vieja", _hasher)
                .ConBloqueo(5, DateTime.UtcNow.AddMinutes(5))
                .ConDebeCambiarPassword(false)
                .Build();
            ctx.Usuarios.AddRange(admin, op);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
            opId = op.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        await servicio.RestablecerPasswordAsync(opId, "temporal123", "temporal123");

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var op = await ctx.Usuarios.SingleAsync(u => u.Id == opId);
            Assert.True(op.DebeCambiarPassword);
            Assert.Equal(0, op.IntentosFallidos);
            Assert.Null(op.BloqueadoHasta);

            var aud = await ctx.Auditorias.SingleAsync(a => a.EntidadId == "op_bloqueado");
            Assert.Equal(AccionAuditoria.RestablecerPassword, aud.Accion);
        }

        // Probar login con la clave restablecida usando ServicioAutenticacion
        var authService = new ServicioAutenticacion(
            db.UnidadDeTrabajoFactory,
            _hasher,
            new SesionActual(),
            TimeProvider.System,
            NullLogger<ServicioAutenticacion>.Instance);

        var loginRes = await authService.IniciarSesionAsync("op_bloqueado", "temporal123");
        Assert.Equal(ResultadoLoginTipo.Exitoso, loginRes.Tipo);
        Assert.True(loginRes.DebeCambiarPassword);
    }

    [Fact]
    public async Task Seguridad_NingunaAuditoriaContienePasswordNiHashNiSalt()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();

        const string claveSecreta = "ClaveAdminSecreta!99";
        int adminId;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var admin = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            ctx.Usuarios.Add(admin);
            await ctx.SaveChangesAsync();
            adminId = admin.Id;
        }

        var sesionAdmin = new UsuarioSesion(adminId, "admin", "Admin", Rol.Administrador, false);
        var (servicio, _, _) = CrearServicio(db, sesionAdmin);

        var nuevoId = await servicio.CrearAsync(new CrearUsuario("usr_seguridad", "Usuario Seguridad", Rol.Operador, claveSecreta, claveSecreta));
        await servicio.EditarAsync(new EditarUsuario(nuevoId, "Usuario Editado", Rol.Supervisor));
        await servicio.CambiarEstadoAsync(nuevoId, false);
        await servicio.CambiarEstadoAsync(nuevoId, true);
        await servicio.RestablecerPasswordAsync(nuevoId, "NuevaClaveTemporal123", "NuevaClaveTemporal123");

        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var creado = await ctx.Usuarios.SingleAsync(u => u.Id == nuevoId);
            var todasAuditorias = await ctx.Auditorias.ToListAsync();

            Assert.NotEmpty(todasAuditorias);

            foreach (var a in todasAuditorias)
            {
                var texto = $"{a.DatosAntesJson} {a.DatosDespuesJson} {a.Motivo}";
                Assert.DoesNotContain(claveSecreta, texto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("NuevaClaveTemporal123", texto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(creado.Hash, texto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(creado.Salt, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

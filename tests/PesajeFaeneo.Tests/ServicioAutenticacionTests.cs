using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioAutenticacionTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly NullLogger<ServicioAutenticacion> _logger = NullLogger<ServicioAutenticacion>.Instance;

    private (ServicioAutenticacion Servicio, ISesionActual Sesion, FakeTimeProvider Tiempo) CrearServicio(
        BaseDatosEnMemoria db,
        DateTime? tiempoInicial = null)
    {
        var tiempo = new FakeTimeProvider(tiempoInicial ?? new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var sesion = new SesionActual();
        var servicio = new ServicioAutenticacion(db.UnidadDeTrabajoFactory, _hasher, sesion, tiempo, _logger);
        return (servicio, sesion, tiempo);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("admin", "")]
    [InlineData("", "password")]
    [InlineData("   ", "password")]
    public async Task IniciarSesionAsync_CamposVacios_RetornaCredencialesInvalidasSinAuditar(string usuario, string pass)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        var resultado = await servicio.IniciarSesionAsync(usuario, pass);

        Assert.Equal(ResultadoLoginTipo.CredencialesInvalidas, resultado.Tipo);
        Assert.Equal("Ingrese usuario y contraseña.", resultado.Mensaje);

        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Auditorias.ToListAsync());
    }

    [Fact]
    public async Task IniciarSesionAsync_UsuarioInexistente_AuditaConUsuarioIdNull_YRetornaCredencialesInvalidas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        var resultado = await servicio.IniciarSesionAsync("no_existe", "clave123");

        Assert.Equal(ResultadoLoginTipo.CredencialesInvalidas, resultado.Tipo);
        Assert.Equal("Usuario o contraseña incorrectos.", resultado.Mensaje);

        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var aud = await context.Auditorias.SingleAsync();
        Assert.Null(aud.UsuarioId);
        Assert.Equal("no_existe", aud.EntidadId);
        Assert.Equal("Usuario inexistente", aud.Motivo);
        Assert.Equal(AccionAuditoria.LoginFallido, aud.Accion);
    }

    [Fact]
    public async Task IniciarSesionAsync_PasswordIncorrecta_IncrementaContador()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder().ConNombreUsuario("operador").ConPassword("correcta123", _hasher).Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("operador", "incorrecta123");

        Assert.Equal(ResultadoLoginTipo.CredencialesInvalidas, resultado.Tipo);
        Assert.Equal("Usuario o contraseña incorrectos.", resultado.Mensaje);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = await context.Usuarios.SingleAsync(x => x.NombreUsuario == "operador");
            Assert.Equal(1, u.IntentosFallidos);
            Assert.Null(u.BloqueadoHasta);

            var aud = await context.Auditorias.SingleAsync();
            Assert.Equal(u.Id, aud.UsuarioId);
            Assert.Equal("Contraseña incorrecta", aud.Motivo);
        }
    }

    [Fact]
    public async Task IniciarSesionAsync_QuintoFallo_Bloquea5Minutos_YReiniciaContadorA0()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var inicio = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var (servicio, _, _) = CrearServicio(db, inicio);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador")
                .ConPassword("correcta123", _hasher)
                .ConBloqueo(4, null)
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("operador", "incorrecta123");

        Assert.Equal(ResultadoLoginTipo.UsuarioBloqueado, resultado.Tipo);
        Assert.Contains("Usuario bloqueado temporalmente por intentos fallidos", resultado.Mensaje);
        Assert.Contains("5 minuto(s)", resultado.Mensaje);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = await context.Usuarios.SingleAsync(x => x.NombreUsuario == "operador");
            Assert.Equal(0, u.IntentosFallidos);
            Assert.NotNull(u.BloqueadoHasta);
            Assert.Equal(inicio.AddMinutes(5), u.BloqueadoHasta.Value);

            var aud = await context.Auditorias.OrderByDescending(a => a.Id).FirstAsync();
            Assert.Equal("Contraseña incorrecta; usuario bloqueado", aud.Motivo);
        }
    }

    [Fact]
    public async Task IniciarSesionAsync_DuranteBloqueo_AunConPasswordCorrecta_RechazaSinCambiarContador()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var inicio = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var (servicio, _, _) = CrearServicio(db, inicio);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador")
                .ConPassword("correcta123", _hasher)
                .ConBloqueo(0, inicio.AddMinutes(4))
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("operador", "correcta123");

        Assert.Equal(ResultadoLoginTipo.UsuarioBloqueado, resultado.Tipo);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = await context.Usuarios.SingleAsync(x => x.NombreUsuario == "operador");
            Assert.Equal(0, u.IntentosFallidos);
            Assert.Equal(inicio.AddMinutes(4), u.BloqueadoHasta);

            var aud = await context.Auditorias.SingleAsync();
            Assert.Equal("Usuario bloqueado", aud.Motivo);
        }
    }

    [Fact]
    public async Task IniciarSesionAsync_BloqueoVencido_PermiteLoginExitoso()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var inicio = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, tiempo) = CrearServicio(db, inicio);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador")
                .ConPassword("correcta123", _hasher)
                .ConBloqueo(0, inicio.AddMinutes(5))
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        // Avanzar tiempo 5 minutos y 1 segundo
        tiempo.Advance(TimeSpan.FromSeconds(301));

        var resultado = await servicio.IniciarSesionAsync("operador", "correcta123");

        Assert.Equal(ResultadoLoginTipo.Exitoso, resultado.Tipo);
        Assert.True(sesion.EstaAutenticada);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = await context.Usuarios.SingleAsync(x => x.NombreUsuario == "operador");
            Assert.Equal(0, u.IntentosFallidos);
            Assert.Null(u.BloqueadoHasta);
            Assert.NotNull(u.UltimoAcceso);
        }
    }

    [Fact]
    public async Task IniciarSesionAsync_InactivoConPasswordCorrecta_RetornaUsuarioInactivo()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador_inactivo")
                .ConPassword("clave123", _hasher)
                .Inactivo()
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("operador_inactivo", "clave123");

        Assert.Equal(ResultadoLoginTipo.UsuarioInactivo, resultado.Tipo);
        Assert.Equal("El usuario está desactivado. Contacte al administrador.", resultado.Mensaje);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var aud = await context.Auditorias.SingleAsync();
            Assert.Equal("Usuario inactivo", aud.Motivo);
        }
    }

    [Fact]
    public async Task IniciarSesionAsync_InactivoConPasswordIncorrecta_RetornaCredencialesInvalidas()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador_inactivo")
                .ConPassword("clave123", _hasher)
                .Inactivo()
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("operador_inactivo", "clave_mala");

        Assert.Equal(ResultadoLoginTipo.CredencialesInvalidas, resultado.Tipo);
        Assert.Equal("Usuario o contraseña incorrectos.", resultado.Mensaje);
    }

    [Fact]
    public async Task IniciarSesionAsync_Exito_ActualizaUltimoAcceso_ReiniciaContador_AuditaLogin_AbreSesion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var inicio = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var (servicio, sesion, _) = CrearServicio(db, inicio);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador")
                .ConNombreCompleto("Operador Uno")
                .ConPassword("clave123", _hasher)
                .ConBloqueo(2, null)
                .ConDebeCambiarPassword(true)
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("operador", "clave123");

        Assert.Equal(ResultadoLoginTipo.Exitoso, resultado.Tipo);
        Assert.True(resultado.DebeCambiarPassword);

        Assert.True(sesion.EstaAutenticada);
        Assert.NotNull(sesion.Usuario);
        Assert.Equal("operador", sesion.Usuario.NombreUsuario);
        Assert.True(sesion.Usuario.DebeCambiarPassword);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = await context.Usuarios.SingleAsync(x => x.NombreUsuario == "operador");
            Assert.Equal(0, u.IntentosFallidos);
            Assert.Null(u.BloqueadoHasta);
            Assert.Equal(inicio, u.UltimoAcceso);

            var aud = await context.Auditorias.SingleAsync();
            Assert.Equal(AccionAuditoria.Login, aud.Accion);
            Assert.Equal(u.Id, aud.UsuarioId);
        }
    }

    [Fact]
    public async Task IniciarSesionAsync_NombreConMayusculasYEspacios_Exito()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("admin")
                .ConPassword("admin123", _hasher)
                .ConRol(Rol.Administrador)
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        var resultado = await servicio.IniciarSesionAsync("  ADMIN ", "admin123");

        Assert.Equal(ResultadoLoginTipo.Exitoso, resultado.Tipo);
        Assert.True(sesion.EstaAutenticada);
    }

    [Fact]
    public async Task CerrarSesionAsync_AuditaLogoutYCierraSesion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        int usuarioId;
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder().ConNombreUsuario("admin").ConRol(Rol.Administrador).Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
            usuarioId = u.Id;
        }

        sesion.Iniciar(new UsuarioSesion(usuarioId, "admin", "Administrador", Rol.Administrador, false));

        await servicio.CerrarSesionAsync();

        Assert.False(sesion.EstaAutenticada);
        Assert.Null(sesion.Usuario);

        await using var ctx = await db.ContextFactory.CreateDbContextAsync();
        var aud = await ctx.Auditorias.SingleAsync();
        Assert.Equal(AccionAuditoria.Logout, aud.Accion);
        Assert.Equal(usuarioId, aud.UsuarioId);
    }

    [Fact]
    public async Task CerrarSesionAsync_ErrorAlGuardarAuditoria_CierraSesionIgualYPropagaExcepcion()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        // Usuario inexistente en esta base: el insert de la auditoría viola la FK Restrict
        // hacia Usuarios y GuardarCambiosAsync falla.
        sesion.Iniciar(new UsuarioSesion(9999, "admin", "Administrador", Rol.Administrador, false));

        var ex = await Assert.ThrowsAsync<ReglaNegocioException>(() => servicio.CerrarSesionAsync());
        Assert.Equal("BD_FK", ex.Codigo);

        Assert.False(sesion.EstaAutenticada);
        Assert.Null(sesion.Usuario);
    }

    [Fact]
    public async Task Seguridad_NingunaFilaDeAuditorias_ContienePasswordHashNiSalt()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        const string claveSecreta = "ClaveUltraSecreta!99";

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador_audit")
                .ConPassword(claveSecreta, _hasher)
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
        }

        await servicio.IniciarSesionAsync("operador_audit", "clave_erronea");
        await servicio.IniciarSesionAsync("operador_audit", claveSecreta);

        await servicio.CambiarPasswordPropiaAsync(claveSecreta, "NuevaClaveSuperSecreta!88", "NuevaClaveSuperSecreta!88");
        await servicio.CerrarSesionAsync();

        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = await context.Usuarios.SingleAsync(x => x.NombreUsuario == "operador_audit");
            var todasLasAuditorias = await context.Auditorias.ToListAsync();

            Assert.NotEmpty(todasLasAuditorias);

            foreach (var a in todasLasAuditorias)
            {
                var textoCompleto = $"{a.DatosAntesJson} {a.DatosDespuesJson} {a.Motivo}";
                Assert.DoesNotContain(claveSecreta, textoCompleto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("NuevaClaveSuperSecreta!88", textoCompleto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(u.Hash, textoCompleto, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(u.Salt, textoCompleto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task CambiarPasswordPropiaAsync_Exito_LoginPosteriorFuncionaConNuevaYFallaConAnterior()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        int usuarioId;
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("operador_cambio")
                .ConPassword("anterior123", _hasher)
                .ConDebeCambiarPassword(true)
                .Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
            usuarioId = u.Id;
        }

        sesion.Iniciar(new UsuarioSesion(usuarioId, "operador_cambio", "Operador", Rol.Operador, DebeCambiarPassword: true));

        await servicio.CambiarPasswordPropiaAsync("anterior123", "nuevaClave456", "nuevaClave456");

        Assert.False(sesion.Usuario!.DebeCambiarPassword);

        // Cerrar y probar login
        sesion.Cerrar();
        var intentoAnterior = await servicio.IniciarSesionAsync("operador_cambio", "anterior123");
        Assert.Equal(ResultadoLoginTipo.CredencialesInvalidas, intentoAnterior.Tipo);

        var intentoNuevo = await servicio.IniciarSesionAsync("operador_cambio", "nuevaClave456");
        Assert.Equal(ResultadoLoginTipo.Exitoso, intentoNuevo.Tipo);
    }

    [Fact]
    public async Task CambiarPasswordPropiaAsync_ActualIncorrecta_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        int usuarioId;
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder().ConNombreUsuario("operador_cambio2").ConPassword("actual123", _hasher).Build();
            context.Usuarios.Add(u);
            await context.SaveChangesAsync();
            usuarioId = u.Id;
        }

        sesion.Iniciar(new UsuarioSesion(usuarioId, "operador_cambio2", "Operador", Rol.Operador, false));

        var ex = await Assert.ThrowsAsync<ValidacionException>(
            () => servicio.CambiarPasswordPropiaAsync("actual_mala", "nueva123", "nueva123"));

        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.PasswordActual && e.Mensaje == "La contraseña actual es incorrecta.");
    }

    [Fact]
    public async Task CambiarPasswordPropiaAsync_NuevaIgualAActual_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador", Rol.Operador, false));

        var ex = await Assert.ThrowsAsync<ValidacionException>(
            () => servicio.CambiarPasswordPropiaAsync("misma123", "misma123", "misma123"));

        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.Password && e.Mensaje == "La nueva contraseña debe ser distinta de la actual.");
    }

    [Fact]
    public async Task CambiarPasswordPropiaAsync_NuevaIgualAUsuario_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador", Rol.Operador, false));

        var ex = await Assert.ThrowsAsync<ValidacionException>(
            () => servicio.CambiarPasswordPropiaAsync("anterior123", "OPERADOR", "OPERADOR"));

        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.Password && e.Mensaje == "La contraseña no puede ser igual al nombre de usuario.");
    }

    [Fact]
    public async Task CambiarPasswordPropiaAsync_ConfirmacionDistinta_LanzaValidacionException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, sesion, _) = CrearServicio(db);

        sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador", Rol.Operador, false));

        var ex = await Assert.ThrowsAsync<ValidacionException>(
            () => servicio.CambiarPasswordPropiaAsync("anterior123", "nueva123", "distinta123"));

        Assert.Contains(ex.Errores, e => e.Campo == CamposUsuario.Confirmacion && e.Mensaje == "La confirmación no coincide con la contraseña.");
    }

    [Fact]
    public async Task CambiarPasswordPropiaAsync_SinSesion_LanzaNoAutorizadoException()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var (servicio, _, _) = CrearServicio(db);

        await Assert.ThrowsAsync<NoAutorizadoException>(
            () => servicio.CambiarPasswordPropiaAsync("act123", "nueva123", "nueva123"));
    }
}

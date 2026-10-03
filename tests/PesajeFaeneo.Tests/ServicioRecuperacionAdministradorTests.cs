using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Infrastructure.Seguridad;
using PesajeFaeneo.Tests.Builders;
using PesajeFaeneo.Tests.Fixtures;
using Xunit;

namespace PesajeFaeneo.Tests;

public class ServicioRecuperacionAdministradorTests
{
    private const string PasswordTemporal = "Temporal7xK";

    private static readonly DateTimeOffset Ahora = new(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private ServicioRecuperacionAdministrador CrearServicio(BaseDatosEnMemoria db)
    {
        var tiempo = new FakeTimeProvider(Ahora);
        return new ServicioRecuperacionAdministrador(
            db.UnidadDeTrabajoFactory, _hasher, tiempo, NullLogger<ServicioRecuperacionAdministrador>.Instance);
    }

    [Fact]
    public async Task Restablecer_AdminExistenteInactivoYBloqueado_QuedaListoParaIngresar()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        int idAdmin;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            var u = new UsuarioBuilder()
                .ConNombreUsuario("admin")
                .ConRol(Rol.Operador)
                .Inactivo()
                .ConBloqueo(5, Ahora.UtcDateTime.AddHours(1))
                .ConDebeCambiarPassword(false)
                .Build();
            ctx.Usuarios.Add(u);
            await ctx.SaveChangesAsync();
            idAdmin = u.Id;
        }

        var resultado = await CrearServicio(db).RestablecerAsync(PasswordTemporal);

        Assert.Equal("admin", resultado.NombreUsuario);
        Assert.False(resultado.Creado);

        await using var verif = await db.ContextFactory.CreateDbContextAsync();
        var admin = await verif.Usuarios.SingleAsync(u => u.Id == idAdmin);
        Assert.True(admin.Activo);
        Assert.Equal(Rol.Administrador, admin.Rol);
        Assert.True(admin.DebeCambiarPassword);
        Assert.Equal(0, admin.IntentosFallidos);
        Assert.Null(admin.BloqueadoHasta);
        Assert.True(_hasher.Verificar(PasswordTemporal, admin.Hash, admin.Salt));

        var aud = await verif.Auditorias.SingleAsync(a => a.Accion == AccionAuditoria.RestablecerPassword);
        Assert.Null(aud.UsuarioId);
        Assert.Equal(EntidadesAuditoria.Usuario, aud.Entidad);
        Assert.Equal(idAdmin.ToString(), aud.EntidadId);
        Assert.Equal(ServicioRecuperacionAdministrador.Motivo, aud.Motivo);
        foreach (var json in new[] { aud.DatosAntesJson, aud.DatosDespuesJson })
        {
            Assert.NotNull(json);
            Assert.DoesNotContain(PasswordTemporal, json);
            Assert.DoesNotContain(admin.Hash, json);
            Assert.DoesNotContain(admin.Salt, json);
        }
    }

    [Fact]
    public async Task Restablecer_SinCuentaAdmin_LaCreaConAuditoriaDeIdReal()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            ctx.Usuarios.Add(new UsuarioBuilder().ConNombreUsuario("jefe").ConRol(Rol.Administrador).Build());
            await ctx.SaveChangesAsync();
        }

        var resultado = await CrearServicio(db).RestablecerAsync(PasswordTemporal);

        Assert.True(resultado.Creado);
        await using var verif = await db.ContextFactory.CreateDbContextAsync();
        var admin = await verif.Usuarios.SingleAsync(u => u.NombreUsuario == "admin");
        Assert.Equal(Rol.Administrador, admin.Rol);
        Assert.True(admin.Activo);
        Assert.True(admin.DebeCambiarPassword);
        Assert.Equal(Ahora.UtcDateTime, admin.CreadoEn);

        var aud = await verif.Auditorias.SingleAsync(a => a.Accion == AccionAuditoria.Crear && a.Entidad == EntidadesAuditoria.Usuario);
        Assert.Equal(admin.Id.ToString(), aud.EntidadId);
        Assert.NotEqual("0", aud.EntidadId);
        Assert.Null(aud.UsuarioId);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData(null)]
    public async Task Restablecer_PasswordInvalida_LanzaYNoCambiaLaBase(string? password)
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        var servicio = CrearServicio(db);
        int usuariosAntes;
        int auditoriasAntes;
        await using (var ctx = await db.ContextFactory.CreateDbContextAsync())
        {
            usuariosAntes = await ctx.Usuarios.CountAsync();
            auditoriasAntes = await ctx.Auditorias.CountAsync();
        }

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RestablecerAsync(password!));

        await using var verif = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(usuariosAntes, await verif.Usuarios.CountAsync());
        Assert.Equal(auditoriasAntes, await verif.Auditorias.CountAsync());
    }

    [Fact]
    public async Task Restablecer_LuegoElAdminPuedeIniciarSesionYDebeCambiarLaPassword()
    {
        await using var db = await BaseDatosEnMemoria.CrearAsync();
        await CrearServicio(db).RestablecerAsync(PasswordTemporal);

        var tiempo = new FakeTimeProvider(Ahora);
        var autenticacion = new ServicioAutenticacion(
            db.UnidadDeTrabajoFactory, _hasher, new SesionActual(), tiempo, NullLogger<ServicioAutenticacion>.Instance);

        var login = await autenticacion.IniciarSesionAsync("admin", PasswordTemporal);

        Assert.Equal(ResultadoLoginTipo.Exitoso, login.Tipo);
        Assert.True(login.DebeCambiarPassword);
    }
}

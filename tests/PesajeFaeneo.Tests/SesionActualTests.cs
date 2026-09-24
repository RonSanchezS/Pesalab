using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;
using Xunit;

namespace PesajeFaeneo.Tests;

public class SesionActualTests
{
    [Fact]
    public void SinSesion_TienePermisoDevuelveFalse_YExigirPermisoLanzaNoAutorizadoException()
    {
        var sesion = new SesionActual();

        Assert.False(sesion.EstaAutenticada);
        Assert.Null(sesion.Usuario);
        Assert.False(sesion.TienePermiso(Permiso.Pesar));
        Assert.Throws<NoAutorizadoException>(() => sesion.ExigirPermiso(Permiso.Pesar));
        Assert.Throws<NoAutorizadoException>(() => sesion.ExigirSesion());
    }

    [Fact]
    public void Iniciar_EstableceUsuario_DisparaEvento_YActualizaEstaAutenticada()
    {
        var sesion = new SesionActual();
        var eventos = 0;
        sesion.SesionCambiada += (_, _) => eventos++;

        var usuario = new UsuarioSesion(1, "admin", "Administrador", Rol.Administrador, DebeCambiarPassword: true);
        sesion.Iniciar(usuario);

        Assert.True(sesion.EstaAutenticada);
        Assert.NotNull(sesion.Usuario);
        Assert.Equal("admin", sesion.Usuario.NombreUsuario);
        Assert.Equal(1, eventos);
        Assert.True(sesion.TienePermiso(Permiso.GestionarUsuarios));
    }

    [Fact]
    public void Cerrar_LimpiaSesion_YDisparaEvento()
    {
        var sesion = new SesionActual();
        var usuario = new UsuarioSesion(1, "operador", "Operador 1", Rol.Operador, DebeCambiarPassword: false);
        sesion.Iniciar(usuario);

        var eventos = 0;
        sesion.SesionCambiada += (_, _) => eventos++;

        sesion.Cerrar();

        Assert.False(sesion.EstaAutenticada);
        Assert.Null(sesion.Usuario);
        Assert.Equal(1, eventos);
    }

    [Fact]
    public void MarcarPasswordCambiada_ActualizaBandera_YDisparaEvento()
    {
        var sesion = new SesionActual();
        var usuario = new UsuarioSesion(1, "admin", "Administrador", Rol.Administrador, DebeCambiarPassword: true);
        sesion.Iniciar(usuario);

        var eventos = 0;
        sesion.SesionCambiada += (_, _) => eventos++;

        sesion.MarcarPasswordCambiada();

        Assert.NotNull(sesion.Usuario);
        Assert.False(sesion.Usuario.DebeCambiarPassword);
        Assert.Equal(1, eventos);
    }

    [Fact]
    public void ExigirPermiso_ConPermisoAdecuado_NoLanzaExcepcion()
    {
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "admin", "Admin", Rol.Administrador, false));

        // No debe lanzar
        sesion.ExigirPermiso(Permiso.GestionarUsuarios);
    }

    [Fact]
    public void ExigirPermiso_SinPermisoAdecuado_LanzaNoAutorizadoException()
    {
        var sesion = new SesionActual();
        sesion.Iniciar(new UsuarioSesion(1, "operador", "Operador", Rol.Operador, false));

        Assert.Throws<NoAutorizadoException>(() => sesion.ExigirPermiso(Permiso.GestionarUsuarios));
    }
}

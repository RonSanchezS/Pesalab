using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Seguridad;

public sealed record UsuarioSesion(int Id, string NombreUsuario, string NombreCompleto, Rol Rol, bool DebeCambiarPassword);

public interface ISesionActual
{
    UsuarioSesion? Usuario { get; }
    bool EstaAutenticada { get; }
    bool TienePermiso(Permiso permiso);          // false si no hay sesión
    void ExigirPermiso(Permiso permiso);         // lanza NoAutorizadoException
    UsuarioSesion ExigirSesion();                // lanza NoAutorizadoException("Debe iniciar sesión.")
    event EventHandler? SesionCambiada;
    // Solo los usa el servicio de autenticación:
    void Iniciar(UsuarioSesion usuario);
    void Cerrar();
    void MarcarPasswordCambiada();              // DebeCambiarPassword = false en la sesión en memoria
}

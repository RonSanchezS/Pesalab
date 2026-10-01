using PesajeFaeneo.Application.Seguridad;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Tests.Utilidades.Gestion;

public class SesionFalsa : ISesionActual
{
    public UsuarioSesion? Usuario { get; set; }
    public bool EstaAutenticada => Usuario != null;
    public HashSet<Permiso> Permisos { get; } = new();

    public event EventHandler? SesionCambiada;

    public bool TienePermiso(Permiso permiso) => Permisos.Contains(permiso);

    public void ExigirPermiso(Permiso permiso)
    {
        if (!TienePermiso(permiso))
        {
            throw new NoAutorizadoException($"No tiene el permiso requerido: {permiso}");
        }
    }

    public UsuarioSesion ExigirSesion()
    {
        return Usuario ?? throw new NoAutorizadoException("Debe iniciar sesión.");
    }

    public void Iniciar(UsuarioSesion usuario)
    {
        Usuario = usuario;
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public void Cerrar()
    {
        Usuario = null;
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public void MarcarPasswordCambiada()
    {
        if (Usuario != null)
        {
            Usuario = Usuario with { DebeCambiarPassword = false };
        }
    }
}

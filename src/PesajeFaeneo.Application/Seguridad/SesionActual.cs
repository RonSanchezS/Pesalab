using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Seguridad;

public sealed class SesionActual : ISesionActual
{
    private readonly object _bloqueo = new();
    private UsuarioSesion? _usuario;

    public UsuarioSesion? Usuario
    {
        get
        {
            lock (_bloqueo)
            {
                return _usuario;
            }
        }
    }

    public bool EstaAutenticada
    {
        get
        {
            lock (_bloqueo)
            {
                return _usuario is not null;
            }
        }
    }

    public event EventHandler? SesionCambiada;

    public bool TienePermiso(Permiso permiso)
    {
        lock (_bloqueo)
        {
            if (_usuario is null)
            {
                return false;
            }

            return MatrizPermisos.Tiene(_usuario.Rol, permiso);
        }
    }

    public void ExigirPermiso(Permiso permiso)
    {
        lock (_bloqueo)
        {
            if (_usuario is null || !MatrizPermisos.Tiene(_usuario.Rol, permiso))
            {
                throw new NoAutorizadoException();
            }
        }
    }

    public UsuarioSesion ExigirSesion()
    {
        lock (_bloqueo)
        {
            if (_usuario is null)
            {
                throw new NoAutorizadoException("Debe iniciar sesión.");
            }

            return _usuario;
        }
    }

    public void Iniciar(UsuarioSesion usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        lock (_bloqueo)
        {
            _usuario = usuario;
        }
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public void Cerrar()
    {
        lock (_bloqueo)
        {
            _usuario = null;
        }
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }

    public void MarcarPasswordCambiada()
    {
        lock (_bloqueo)
        {
            if (_usuario is not null)
            {
                _usuario = _usuario with { DebeCambiarPassword = false };
            }
        }
        SesionCambiada?.Invoke(this, EventArgs.Empty);
    }
}

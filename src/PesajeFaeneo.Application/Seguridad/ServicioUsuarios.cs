using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Seguridad.Dtos;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Seguridad;

public class ServicioUsuarios : IServicioUsuarios
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly IPasswordHasher _hasher;
    private readonly ISesionActual _sesionActual;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioUsuarios> _logger;

    public ServicioUsuarios(
        IUnidadDeTrabajoFactory uowFactory,
        IPasswordHasher hasher,
        ISesionActual sesionActual,
        TimeProvider timeProvider,
        ILogger<ServicioUsuarios> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<UsuarioListado>> ListarAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarUsuarios);

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuarios = await uow.Usuarios.ListarAsync(incluirInactivos, cancellationToken);

        return usuarios.Select(u => new UsuarioListado(
            u.Id,
            u.NombreUsuario,
            u.NombreCompleto,
            u.Rol,
            u.Activo,
            u.BloqueadoHasta.HasValue && u.BloqueadoHasta.Value > ahoraUtc,
            u.UltimoAcceso)).ToList().AsReadOnly();
    }

    public async Task<UsuarioListado> ObtenerAsync(int id, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarUsuarios);

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuario = await uow.Usuarios.ObtenerPorIdAsync(id, cancellationToken);

        if (usuario is null)
        {
            throw new EntidadNoEncontradaException("Usuario", id);
        }

        return new UsuarioListado(
            usuario.Id,
            usuario.NombreUsuario,
            usuario.NombreCompleto,
            usuario.Rol,
            usuario.Activo,
            usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > ahoraUtc,
            usuario.UltimoAcceso);
    }

    public async Task<int> CrearAsync(CrearUsuario dto, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarUsuarios);
        var sesion = _sesionActual.ExigirSesion();

        var nombreNormalizado = ValidadorUsuario.NormalizarNombreUsuario(dto.NombreUsuario);
        var nombreCompletoNormalizado = ValidadorUsuario.NormalizarNombreCompleto(dto.NombreCompleto);

        var errores = ValidadorUsuario.ValidarAlta(dto.NombreUsuario, dto.NombreCompleto, dto.Rol, dto.Password, dto.Confirmacion);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);

        if (await uow.Usuarios.ExisteNombreUsuarioAsync(nombreNormalizado, cancellationToken))
        {
            throw new ValidacionException(CamposUsuario.NombreUsuario, "Ya existe un usuario con ese nombre.");
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var (hash, salt) = _hasher.Hashear(dto.Password);

        var nuevoUsuario = new Usuario
        {
            NombreUsuario = nombreNormalizado,
            NombreCompleto = nombreCompletoNormalizado,
            Rol = dto.Rol,
            Hash = hash,
            Salt = salt,
            Activo = true,
            DebeCambiarPassword = true,
            IntentosFallidos = 0,
            BloqueadoHasta = null,
            CreadoEn = ahoraUtc
        };

        uow.Usuarios.Agregar(nuevoUsuario);

        var despues = FabricaAuditoria.CrearSnapshot(nuevoUsuario);

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Crear,
            EntidadesAuditoria.Usuario,
            nombreNormalizado,
            antes: null,
            despues: despues,
            motivo: "Alta de usuario");

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Usuario '{Usuario}' creado exitosamente por '{Operador}'", nombreNormalizado, sesion.NombreUsuario);
        return nuevoUsuario.Id;
    }

    public async Task EditarAsync(EditarUsuario dto, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarUsuarios);
        var sesion = _sesionActual.ExigirSesion();

        var errores = ValidadorUsuario.ValidarEdicion(dto.NombreCompleto, dto.Rol);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuario = await uow.Usuarios.ObtenerPorIdAsync(dto.Id, cancellationToken);
        if (usuario is null)
        {
            throw new EntidadNoEncontradaException("Usuario", dto.Id);
        }

        if (usuario.Id == sesion.Id && usuario.Rol != dto.Rol)
        {
            throw new ReglaNegocioException("USR_PROPIO_ROL", "No puede cambiar su propio rol.");
        }

        if (usuario.Rol == Rol.Administrador && dto.Rol != Rol.Administrador && usuario.Activo)
        {
            var adminsActivos = await uow.Usuarios.ContarAdministradoresActivosAsync(cancellationToken);
            if (adminsActivos <= 1)
            {
                throw new ReglaNegocioException("USR_ULTIMO_ADMIN", "Debe existir al menos un administrador activo.");
            }
        }

        var antes = FabricaAuditoria.CrearSnapshot(usuario);

        usuario.NombreCompleto = ValidadorUsuario.NormalizarNombreCompleto(dto.NombreCompleto);
        usuario.Rol = dto.Rol;

        var despues = FabricaAuditoria.CrearSnapshot(usuario);

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Usuario,
            usuario.NombreUsuario,
            antes: antes,
            despues: despues,
            motivo: "Modificación de datos de usuario");

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Usuario '{Usuario}' editado por '{Operador}'", usuario.NombreUsuario, sesion.NombreUsuario);
    }

    public async Task CambiarEstadoAsync(int id, bool activo, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarUsuarios);
        var sesion = _sesionActual.ExigirSesion();

        if (id == sesion.Id && !activo)
        {
            throw new ReglaNegocioException("USR_PROPIO_ESTADO", "No puede desactivar su propio usuario.");
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuario = await uow.Usuarios.ObtenerPorIdAsync(id, cancellationToken);
        if (usuario is null)
        {
            throw new EntidadNoEncontradaException("Usuario", id);
        }

        if (usuario.Activo == activo)
        {
            return;
        }

        if (!activo && usuario.Rol == Rol.Administrador)
        {
            var adminsActivos = await uow.Usuarios.ContarAdministradoresActivosAsync(cancellationToken);
            if (adminsActivos <= 1)
            {
                throw new ReglaNegocioException("USR_ULTIMO_ADMIN", "Debe existir al menos un administrador activo.");
            }
        }

        var antes = FabricaAuditoria.CrearSnapshot(usuario);

        usuario.Activo = activo;

        var despues = FabricaAuditoria.CrearSnapshot(usuario);

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.Modificar,
            EntidadesAuditoria.Usuario,
            usuario.NombreUsuario,
            antes: antes,
            despues: despues,
            motivo: activo ? "Activación" : "Desactivación");

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Estado de usuario '{Usuario}' cambiado a Activo={Activo} por '{Operador}'",
            usuario.NombreUsuario, activo, sesion.NombreUsuario);
    }

    public async Task RestablecerPasswordAsync(int id, string nueva, string confirmacion, CancellationToken cancellationToken = default)
    {
        _sesionActual.ExigirPermiso(Permiso.GestionarUsuarios);
        var sesion = _sesionActual.ExigirSesion();

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuario = await uow.Usuarios.ObtenerPorIdAsync(id, cancellationToken);
        if (usuario is null)
        {
            throw new EntidadNoEncontradaException("Usuario", id);
        }

        var errores = ValidadorUsuario.ValidarPasswordNueva(usuario.NombreUsuario, nueva, confirmacion, actual: null);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        var antes = FabricaAuditoria.CrearSnapshot(usuario);

        var (hash, salt) = _hasher.Hashear(nueva);
        usuario.Hash = hash;
        usuario.Salt = salt;
        usuario.DebeCambiarPassword = true;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;

        var despues = FabricaAuditoria.CrearSnapshot(usuario);

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            sesion.Id,
            AccionAuditoria.RestablecerPassword,
            EntidadesAuditoria.Usuario,
            usuario.NombreUsuario,
            antes: antes,
            despues: despues,
            motivo: "Restablecimiento por administrador");

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _logger.LogInformation("Contraseña del usuario '{Usuario}' restablecida por '{Operador}'",
            usuario.NombreUsuario, sesion.NombreUsuario);
    }
}

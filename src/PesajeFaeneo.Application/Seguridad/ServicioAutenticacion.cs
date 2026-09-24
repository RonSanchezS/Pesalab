using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Seguridad;

public class ServicioAutenticacion : IServicioAutenticacion
{
    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly IPasswordHasher _hasher;
    private readonly ISesionActual _sesionActual;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServicioAutenticacion> _logger;
    private readonly Lazy<(string Hash, string Salt)> _hashFicticio;

    public ServicioAutenticacion(
        IUnidadDeTrabajoFactory uowFactory,
        IPasswordHasher hasher,
        ISesionActual sesionActual,
        TimeProvider timeProvider,
        ILogger<ServicioAutenticacion> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _sesionActual = sesionActual ?? throw new ArgumentNullException(nameof(sesionActual));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _hashFicticio = new Lazy<(string Hash, string Salt)>(() => _hasher.Hashear("clave_ficticia_timing_defense"));
    }

    public async Task<ResultadoLogin> IniciarSesionAsync(
        string? nombreUsuario,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var nombreNormalizado = ValidadorUsuario.NormalizarNombreUsuario(nombreUsuario);

        // 1. Si el nombre o la contraseña están vacíos -> CredencialesInvalidas sin auditar
        if (string.IsNullOrEmpty(nombreNormalizado) || string.IsNullOrEmpty(password))
        {
            return new ResultadoLogin(ResultadoLoginTipo.CredencialesInvalidas, "Ingrese usuario y contraseña.");
        }

        var ahoraUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuario = await uow.Usuarios.ObtenerPorNombreUsuarioAsync(nombreNormalizado, cancellationToken);

        // 3. Usuario inexistente
        if (usuario is null)
        {
            _hasher.Verificar(password, _hashFicticio.Value.Hash, _hashFicticio.Value.Salt);

            var audInexistente = FabricaAuditoria.Crear(
                _timeProvider,
                usuarioId: null,
                AccionAuditoria.LoginFallido,
                EntidadesAuditoria.Usuario,
                nombreNormalizado,
                motivo: "Usuario inexistente");

            uow.Auditoria.Registrar(audInexistente);
            await uow.GuardarCambiosAsync(cancellationToken);

            _logger.LogInformation("Intento de login fallido para usuario inexistente '{Usuario}'", nombreNormalizado);
            return new ResultadoLogin(ResultadoLoginTipo.CredencialesInvalidas, "Usuario o contraseña incorrectos.");
        }

        // 4. Bloqueado temporalmente
        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > ahoraUtc)
        {
            var audBloqueado = FabricaAuditoria.Crear(
                _timeProvider,
                usuario.Id,
                AccionAuditoria.LoginFallido,
                EntidadesAuditoria.Usuario,
                usuario.NombreUsuario,
                motivo: "Usuario bloqueado");

            uow.Auditoria.Registrar(audBloqueado);
            await uow.GuardarCambiosAsync(cancellationToken);

            var minutosRestantes = Math.Max(1, (int)Math.Ceiling((usuario.BloqueadoHasta.Value - ahoraUtc).TotalMinutes));
            _logger.LogInformation("Intento de login para usuario bloqueado '{Usuario}'", usuario.NombreUsuario);
            return new ResultadoLogin(
                ResultadoLoginTipo.UsuarioBloqueado,
                $"Usuario bloqueado temporalmente por intentos fallidos. Intente de nuevo en {minutosRestantes} minuto(s).");
        }

        // 5. Bloqueo vencido: reiniciar
        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value <= ahoraUtc)
        {
            usuario.BloqueadoHasta = null;
            usuario.IntentosFallidos = 0;
        }

        // 6. Verificar contraseña
        var passwordValida = _hasher.Verificar(password, usuario.Hash, usuario.Salt);
        if (!passwordValida)
        {
            usuario.IntentosFallidos++;
            var acabaDeBloquear = usuario.IntentosFallidos >= PoliticaSeguridad.MaximoIntentosFallidos;
            if (acabaDeBloquear)
            {
                usuario.BloqueadoHasta = ahoraUtc + PoliticaSeguridad.DuracionBloqueo;
                usuario.IntentosFallidos = 0;
            }

            var motivo = acabaDeBloquear
                ? "Contraseña incorrecta; usuario bloqueado"
                : "Contraseña incorrecta";

            var audError = FabricaAuditoria.Crear(
                _timeProvider,
                usuario.Id,
                AccionAuditoria.LoginFallido,
                EntidadesAuditoria.Usuario,
                usuario.NombreUsuario,
                motivo: motivo);

            uow.Auditoria.Registrar(audError);
            await uow.GuardarCambiosAsync(cancellationToken);

            _logger.LogInformation("Intento de login fallido para '{Usuario}': {Motivo}", usuario.NombreUsuario, motivo);

            if (acabaDeBloquear)
            {
                var minutos = Math.Max(1, (int)Math.Ceiling(PoliticaSeguridad.DuracionBloqueo.TotalMinutes));
                return new ResultadoLogin(
                    ResultadoLoginTipo.UsuarioBloqueado,
                    $"Usuario bloqueado temporalmente por intentos fallidos. Intente de nuevo en {minutos} minuto(s).");
            }

            return new ResultadoLogin(ResultadoLoginTipo.CredencialesInvalidas, "Usuario o contraseña incorrectos.");
        }

        // 7. Contraseña correcta pero inactivo
        if (!usuario.Activo)
        {
            var audInactivo = FabricaAuditoria.Crear(
                _timeProvider,
                usuario.Id,
                AccionAuditoria.LoginFallido,
                EntidadesAuditoria.Usuario,
                usuario.NombreUsuario,
                motivo: "Usuario inactivo");

            uow.Auditoria.Registrar(audInactivo);
            await uow.GuardarCambiosAsync(cancellationToken);

            _logger.LogInformation("Intento de login fallido para usuario inactivo '{Usuario}'", usuario.NombreUsuario);
            return new ResultadoLogin(ResultadoLoginTipo.UsuarioInactivo, "El usuario está desactivado. Contacte al administrador.");
        }

        // 8. Éxito
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UltimoAcceso = ahoraUtc;

        var audExito = FabricaAuditoria.Crear(
            _timeProvider,
            usuario.Id,
            AccionAuditoria.Login,
            EntidadesAuditoria.Usuario,
            usuario.NombreUsuario,
            motivo: "Inicio de sesión exitoso");

        uow.Auditoria.Registrar(audExito);
        await uow.GuardarCambiosAsync(cancellationToken);

        _sesionActual.Iniciar(new UsuarioSesion(
            usuario.Id,
            usuario.NombreUsuario,
            usuario.NombreCompleto,
            usuario.Rol,
            usuario.DebeCambiarPassword));

        _logger.LogInformation("Inicio de sesión exitoso para usuario '{Usuario}'", usuario.NombreUsuario);

        return new ResultadoLogin(ResultadoLoginTipo.Exitoso, "Inicio de sesión exitoso.", usuario.DebeCambiarPassword);
    }

    public async Task CerrarSesionAsync(CancellationToken cancellationToken = default)
    {
        var sesion = _sesionActual.Usuario;
        if (sesion is null)
        {
            return;
        }

        try
        {
            await using var uow = await _uowFactory.CrearAsync(cancellationToken);
            var aud = FabricaAuditoria.Crear(
                _timeProvider,
                sesion.Id,
                AccionAuditoria.Logout,
                EntidadesAuditoria.Usuario,
                sesion.NombreUsuario,
                motivo: "Cierre de sesión");

            uow.Auditoria.Registrar(aud);
            await uow.GuardarCambiosAsync(cancellationToken);
        }
        finally
        {
            // La sesión se cierra siempre, aunque falle el registro de auditoría: un error de
            // persistencia no debe dejar al usuario con una sesión abierta en memoria. La excepción
            // se propaga (no se traga aquí) para que el llamador decida si el fallo de auditoría
            // debe impedir el cierre de la aplicación (ContextoAplicacion la atrapa y continúa).
            _sesionActual.Cerrar();
            _logger.LogInformation("Sesión cerrada para usuario '{Usuario}'", sesion.NombreUsuario);
        }
    }

    public async Task CambiarPasswordPropiaAsync(
        string actual,
        string nueva,
        string confirmacion,
        CancellationToken cancellationToken = default)
    {
        var sesion = _sesionActual.ExigirSesion();

        var errores = ValidadorUsuario.ValidarPasswordNueva(sesion.NombreUsuario, nueva, confirmacion, actual);
        if (errores.Count > 0)
        {
            throw new ValidacionException(errores);
        }

        await using var uow = await _uowFactory.CrearAsync(cancellationToken);
        var usuario = await uow.Usuarios.ObtenerPorIdAsync(sesion.Id, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            throw new NoAutorizadoException("El usuario no existe o se encuentra inactivo.");
        }

        if (!_hasher.Verificar(actual, usuario.Hash, usuario.Salt))
        {
            throw new ValidacionException(CamposUsuario.PasswordActual, "La contraseña actual es incorrecta.");
        }

        var antes = FabricaAuditoria.CrearSnapshot(usuario);

        var (nuevoHash, nuevoSalt) = _hasher.Hashear(nueva);
        usuario.Hash = nuevoHash;
        usuario.Salt = nuevoSalt;
        usuario.DebeCambiarPassword = false;

        var despues = FabricaAuditoria.CrearSnapshot(usuario);

        var aud = FabricaAuditoria.Crear(
            _timeProvider,
            usuario.Id,
            AccionAuditoria.CambioPassword,
            EntidadesAuditoria.Usuario,
            usuario.NombreUsuario,
            antes: antes,
            despues: despues,
            motivo: "Cambio de contraseña propio");

        uow.Auditoria.Registrar(aud);
        await uow.GuardarCambiosAsync(cancellationToken);

        _sesionActual.MarcarPasswordCambiada();
        _logger.LogInformation("Contraseña cambiada exitosamente para usuario '{Usuario}'", usuario.NombreUsuario);
    }
}

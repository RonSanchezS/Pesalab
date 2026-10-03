using System.Globalization;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Abstracciones;
using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Domain.Entities;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Seguridad;

/// <summary>
/// Recuperación local de la cuenta de administrador (D17). Corre SIN sesión: solo lo invoca Program con
/// --restablecer-admin y el proceso elevado como administrador de Windows. Nunca registra la contraseña.
/// </summary>
public sealed class ServicioRecuperacionAdministrador : IServicioRecuperacionAdministrador
{
    public const string NombreUsuarioAdministrador = "admin";
    public const string Motivo = "Recuperación local de la cuenta de administrador (--restablecer-admin)";

    private readonly IUnidadDeTrabajoFactory _uowFactory;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _tiempo;
    private readonly ILogger<ServicioRecuperacionAdministrador> _logger;

    public ServicioRecuperacionAdministrador(
        IUnidadDeTrabajoFactory uowFactory,
        IPasswordHasher hasher,
        TimeProvider tiempo,
        ILogger<ServicioRecuperacionAdministrador> logger)
    {
        _uowFactory = uowFactory ?? throw new ArgumentNullException(nameof(uowFactory));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _tiempo = tiempo ?? throw new ArgumentNullException(nameof(tiempo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResultadoRecuperacionAdministrador> RestablecerAsync(string passwordTemporal, CancellationToken cancellationToken = default)
    {
        if (passwordTemporal is null ||
            passwordTemporal.Length < PoliticaSeguridad.LongitudMinimaPassword ||
            passwordTemporal.Length > PoliticaSeguridad.LongitudMaximaPassword)
        {
            throw new ArgumentException("La contraseña temporal no cumple la longitud de la política de seguridad.", nameof(passwordTemporal));
        }

        var (hash, salt) = _hasher.Hashear(passwordTemporal);
        var ahora = _tiempo.GetUtcNow().UtcDateTime;

        await using var uow = await _uowFactory.CrearAsync(cancellationToken).ConfigureAwait(false);
        var usuario = await uow.Usuarios.ObtenerPorNombreUsuarioAsync(NombreUsuarioAdministrador, cancellationToken).ConfigureAwait(false);

        bool creado;
        if (usuario is not null)
        {
            var antes = FabricaAuditoria.CrearSnapshot(usuario);

            usuario.Hash = hash;
            usuario.Salt = salt;
            usuario.Rol = Rol.Administrador;
            usuario.Activo = true;
            usuario.DebeCambiarPassword = true;
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;

            var despues = FabricaAuditoria.CrearSnapshot(usuario);
            uow.Auditoria.Registrar(FabricaAuditoria.Crear(
                _tiempo,
                null,
                AccionAuditoria.RestablecerPassword,
                EntidadesAuditoria.Usuario,
                usuario.Id.ToString(CultureInfo.InvariantCulture),
                antes: antes,
                despues: despues,
                motivo: Motivo));

            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
            creado = false;
        }
        else
        {
            var nuevo = new Usuario
            {
                NombreUsuario = NombreUsuarioAdministrador,
                NombreCompleto = "Administrador del Sistema",
                Hash = hash,
                Salt = salt,
                Rol = Rol.Administrador,
                Activo = true,
                DebeCambiarPassword = true,
                CreadoEn = ahora
            };

            // La auditoría necesita el Id autogenerado: transacción explícita (mismo patrón que las altas de catálogos).
            await using var tx = await uow.IniciarTransaccionAsync(cancellationToken).ConfigureAwait(false);
            uow.Usuarios.Agregar(nuevo);
            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);

            uow.Auditoria.Registrar(FabricaAuditoria.Crear(
                _tiempo,
                null,
                AccionAuditoria.Crear,
                EntidadesAuditoria.Usuario,
                nuevo.Id.ToString(CultureInfo.InvariantCulture),
                despues: FabricaAuditoria.CrearSnapshot(nuevo),
                motivo: Motivo));

            await uow.GuardarCambiosAsync(cancellationToken).ConfigureAwait(false);
            await tx.ConfirmarAsync(cancellationToken).ConfigureAwait(false);
            creado = true;
        }

        _logger.LogInformation(
            "Cuenta de administrador {Usuario} restablecida localmente (creada: {Creada}).",
            NombreUsuarioAdministrador,
            creado);

        return new ResultadoRecuperacionAdministrador(NombreUsuarioAdministrador, creado);
    }
}

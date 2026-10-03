namespace PesajeFaeneo.Application.Seguridad;

public sealed record ResultadoRecuperacionAdministrador(string NombreUsuario, bool Creado);

public interface IServicioRecuperacionAdministrador
{
    /// <summary>
    /// Deja la cuenta "admin" activa, con rol Administrador, desbloqueada y con la contraseña temporal indicada
    /// (debe cambiarla al ingresar). Si no existe, la crea.
    /// </summary>
    Task<ResultadoRecuperacionAdministrador> RestablecerAsync(string passwordTemporal, CancellationToken cancellationToken = default);
}

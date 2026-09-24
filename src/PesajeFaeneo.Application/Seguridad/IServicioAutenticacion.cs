namespace PesajeFaeneo.Application.Seguridad;

public enum ResultadoLoginTipo
{
    Exitoso,
    CredencialesInvalidas,
    UsuarioInactivo,
    UsuarioBloqueado
}

public sealed record ResultadoLogin(ResultadoLoginTipo Tipo, string Mensaje, bool DebeCambiarPassword = false);

public interface IServicioAutenticacion
{
    Task<ResultadoLogin> IniciarSesionAsync(string? nombreUsuario, string? password, CancellationToken cancellationToken = default);
    Task CerrarSesionAsync(CancellationToken cancellationToken = default);
    Task CambiarPasswordPropiaAsync(string actual, string nueva, string confirmacion, CancellationToken cancellationToken = default);
}

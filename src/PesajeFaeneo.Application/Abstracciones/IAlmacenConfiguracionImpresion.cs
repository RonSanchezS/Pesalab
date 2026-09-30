using PesajeFaeneo.Application.Boletas.Dtos;

namespace PesajeFaeneo.Application.Abstracciones;

public interface IAlmacenConfiguracionImpresion
{
    /// <summary>
    /// Nombre de este equipo en producción.
    /// </summary>
    string NombreEquipo { get; }

    /// <summary>
    /// null si no hay archivo, está dañado, tiene otra versión, pertenece a otro equipo o sus valores
    /// no pasan ValidadorConfiguracionImpresion. NUNCA lanza por esas causas (registra Warning).
    /// </summary>
    Task<ConfiguracionImpresion?> LeerAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Escribe de forma atómica (archivo temporal + reemplazo). Lanza si no puede escribir.
    /// </summary>
    Task GuardarAsync(ConfiguracionImpresion configuracion, CancellationToken cancellationToken = default);
}

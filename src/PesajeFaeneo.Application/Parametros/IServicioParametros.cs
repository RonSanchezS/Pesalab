using PesajeFaeneo.Application.Parametros.Dtos;
using PesajeFaeneo.Domain.Enums;

namespace PesajeFaeneo.Application.Parametros;

public interface IServicioParametros
{
    Task<ParametrosDto> ObtenerAsync(CancellationToken cancellationToken = default);
    Task ActualizarAsync(ActualizarParametros dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nombre y logo de la empresa para el encabezado de la pantalla de inicio de sesión (F10, O7). Es la única
    /// operación de este servicio que NO exige sesión: se usa antes de autenticarse y solo expone datos que ya
    /// salen impresos en cada boleta (nombre y logo), nunca direcciones, numeración ni configuración.
    /// </summary>
    Task<IdentidadEmpresa> ObtenerIdentidadEmpresaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// F11 (D11): tipo de pesaje efectivo de la empresa (sin definir = Ambos). Solo exige sesión: lo necesitan el
    /// Operador (pesaje, historial) y el Supervisor (boletas, reportes), que no gestionan parámetros.
    /// </summary>
    Task<ModoDesposte> ObtenerModoDesposteAsync(CancellationToken cancellationToken = default);

    /// <summary>F11 (D4): modo guardado (null = sin definir) y boletas vigentes por tipo. Exige GestionarParametros.</summary>
    Task<ConfiguracionDesposte> ObtenerConfiguracionDesposteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// F11 (D5): cambia el tipo de pesaje (exige GestionarParametros) y lo audita como CambioParametros. No modifica
    /// las boletas ya grabadas. Si el modo no cambia, no guarda ni audita.
    /// </summary>
    Task CambiarModoDesposteAsync(ModoDesposte modo, CancellationToken cancellationToken = default);
}

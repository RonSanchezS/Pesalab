using PesajeFaeneo.Application.Abstracciones.Persistencia;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.Application.Catalogos;

/// <summary>
/// F10 (O6): los códigos de ganadero, cliente y fletero los asigna el sistema (mayor código existente + 1, incluidos
/// los inactivos: un código no se reutiliza). Si dos altas simultáneas toman el mismo código, el índice único
/// rechaza la segunda y se reintenta una vez con el siguiente.
/// </summary>
internal static class AsignadorCodigos
{
    private const int IntentosMaximos = 2;

    /// <summary>
    /// Asigna el código (en <paramref name="asignarYAgregar"/>, que también agrega la entidad al contexto) y guarda;
    /// ante un <see cref="ConflictoException"/> por código repetido vuelve a calcularlo y guarda de nuevo.
    /// Debe invocarse dentro de la transacción del alta, antes de registrar la auditoría (que necesita el Id).
    /// </summary>
    public static async Task GuardarNuevoAsync(
        IUnidadDeTrabajo uow,
        Func<CancellationToken, Task> asignarYAgregar,
        CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            await asignarYAgregar(cancellationToken);
            try
            {
                await uow.GuardarCambiosAsync(cancellationToken);
                return;
            }
            catch (ConflictoException) when (intento < IntentosMaximos)
            {
                // Otra alta se llevó el código: se recalcula con el máximo ya confirmado.
            }
        }
    }
}

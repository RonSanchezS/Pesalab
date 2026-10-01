using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;
using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public interface IRepositorioImportacion
{
    Task<ClavesExistentes> ObtenerClavesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta el plan en UNA transacción. confirmar = false → rollback (simulacro). Devuelve las filas insertadas.
    /// </summary>
    Task<int> EscribirAsync(PlanImportacion plan, int usuarioId, DateTime ahoraUtc, AuditoriaEntidad? auditoria, bool confirmar, CancellationToken cancellationToken = default);
}

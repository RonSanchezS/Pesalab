using AuditoriaEntidad = PesajeFaeneo.Domain.Entities.Auditoria;

namespace PesajeFaeneo.Application.Abstracciones.Persistencia;

public interface IAuditoriaRepositorio
{
    void Registrar(AuditoriaEntidad entrada);

    /// <summary>
    /// Cantidad de auditorías Imprimir o Reimprimir del pesaje (Entidad = "Pesaje", EntidadId = id invariante).
    /// </summary>
    Task<int> ContarImpresionesPesajeAsync(int pesajeId, CancellationToken cancellationToken = default);
}

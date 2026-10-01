using PesajeFaeneo.Application.Utilitarios.Logs.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Logs;

public interface IServicioLogs
{
    string CarpetaLogs { get; }
    Task<IReadOnlyList<ArchivoLog>> ListarArchivosAsync(CancellationToken cancellationToken = default);
    Task<ContenidoLog> LeerAsync(string nombreArchivo, CancellationToken cancellationToken = default);
}

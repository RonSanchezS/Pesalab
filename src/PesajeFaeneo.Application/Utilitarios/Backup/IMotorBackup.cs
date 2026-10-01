namespace PesajeFaeneo.Application.Utilitarios.Backup;

using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

public interface IMotorBackup
{
    string CarpetaBackups { get; }
    Task<ResultadoCreacionBackup> CrearAsync(SolicitudBackup solicitud, CancellationToken cancellationToken = default);
    Task<bool> ExisteAutomaticoDelDiaAsync(DateOnly fechaLocal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default);
    Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default);
    Task VerificarCarpetaEscribibleAsync(string carpeta, CancellationToken cancellationToken = default);
    Task<ValidacionRestauracion> ValidarParaRestaurarAsync(string rutaBackup, CancellationToken cancellationToken = default);
    Task<InfoBackup> ProgramarRestauracionAsync(string token, string solicitadoPor, CancellationToken cancellationToken = default);
    Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default);
}

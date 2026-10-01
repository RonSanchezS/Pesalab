namespace PesajeFaeneo.Application.Utilitarios.Backup;

using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

public interface IServicioBackup
{
    string CarpetaBackups { get; }
    ResultadoBackupAutomatico? UltimoAutomatico { get; }
    Task<IReadOnlyList<InfoBackup>> ListarAsync(CancellationToken cancellationToken = default);
    Task<ResultadoCreacionBackup> CrearManualAsync(CancellationToken cancellationToken = default);
    Task ExportarAsync(string rutaBackup, string rutaDestino, CancellationToken cancellationToken = default);
    Task<ValidacionRestauracion> ValidarAsync(string rutaBackup, CancellationToken cancellationToken = default);
    Task<InfoBackup> ProgramarRestauracionAsync(string token, string confirmacion, CancellationToken cancellationToken = default);
    Task DescartarValidacionAsync(string token, CancellationToken cancellationToken = default);
    Task<ConfiguracionBackup> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default);
    Task GuardarConfiguracionAsync(ConfiguracionBackup configuracion, CancellationToken cancellationToken = default);
}

namespace PesajeFaeneo.Application.Utilitarios.Backup;

using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

public interface IProgramadorBackupAutomatico
{
    ResultadoBackupAutomatico? UltimoResultado { get; }
    void Iniciar();
    Task DetenerAsync();
    Task<ResultadoBackupAutomatico?> EjecutarAlCerrarAsync(CancellationToken cancellationToken = default);
}

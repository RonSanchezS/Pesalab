namespace PesajeFaeneo.Application.Utilitarios.Backup;

using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

public interface IAlmacenConfiguracionBackup
{
    Task<ConfiguracionBackup> LeerAsync(CancellationToken cancellationToken = default);
    Task GuardarAsync(ConfiguracionBackup configuracion, CancellationToken cancellationToken = default);
}

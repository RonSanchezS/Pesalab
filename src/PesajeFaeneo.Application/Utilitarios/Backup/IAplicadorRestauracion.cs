namespace PesajeFaeneo.Application.Utilitarios.Backup;

using PesajeFaeneo.Application.Utilitarios.Backup.Dtos;

/// <summary>
/// Aplica una restauración programada. Lo usa Program ANTES de abrir la base (sin sesión, sin UI).
/// AplicarPendiente nunca lanza: todo error se informa como Fallida.
/// </summary>
public interface IAplicadorRestauracion
{
    ResultadoRestauracionArranque AplicarPendiente();
    void Revertir(ResultadoRestauracionArranque aplicada);
    Task RegistrarAuditoriaAsync(ResultadoRestauracionArranque aplicada, CancellationToken cancellationToken = default);
}

using PesajeFaeneo.Application.Auditoria.Dtos;

namespace PesajeFaeneo.Application.Auditoria;

public interface IServicioAuditoria
{
    Task<IReadOnlyList<UsuarioFiltro>> ListarUsuariosAsync(CancellationToken cancellationToken = default);
    Task<ResultadoAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken cancellationToken = default);
}

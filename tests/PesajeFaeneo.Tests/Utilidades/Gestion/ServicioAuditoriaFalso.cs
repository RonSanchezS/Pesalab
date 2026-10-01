using PesajeFaeneo.Application.Auditoria;
using PesajeFaeneo.Application.Auditoria.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Gestion;

public class ServicioAuditoriaFalso : IServicioAuditoria
{
    public List<UsuarioFiltro> Usuarios { get; set; } = new();
    public ResultadoAuditoria ResultadoConsultar { get; set; } = new(Array.Empty<RegistroAuditoria>(), false);
    public List<FiltroAuditoria> FiltrosRecibidos { get; } = new();
    public Func<Task>? PausaConsultarAsync { get; set; }
    public Exception? ExcepcionConsultar { get; set; }

    public Task<IReadOnlyList<UsuarioFiltro>> ListarUsuariosAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<UsuarioFiltro>>(Usuarios);
    }

    public async Task<ResultadoAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken cancellationToken = default)
    {
        FiltrosRecibidos.Add(filtro);
        if (PausaConsultarAsync != null)
        {
            await PausaConsultarAsync();
        }

        if (ExcepcionConsultar != null)
        {
            throw ExcepcionConsultar;
        }

        return ResultadoConsultar;
    }
}

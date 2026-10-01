using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public interface ILectorOrigenDatos
{
    Task<IReadOnlyList<string>> ListarTablasAsync(string rutaAccess, CancellationToken cancellationToken = default);
    Task<TablaOrigen> LeerAsync(SolicitudLectura solicitud, CancellationToken cancellationToken = default);
}

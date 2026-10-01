using PesajeFaeneo.Application.Utilitarios.Importacion.Dtos;

namespace PesajeFaeneo.Application.Utilitarios.Importacion;

public interface IServicioImportacion
{
    Task<IReadOnlyList<string>> ListarTablasAccessAsync(string ruta, CancellationToken cancellationToken = default);
    Task<VistaOrigen> LeerOrigenAsync(SolicitudLectura lectura, TipoImportacion tipo, CancellationToken cancellationToken = default);
    Task<ResultadoImportacion> SimularAsync(SolicitudImportacion solicitud, CancellationToken cancellationToken = default);
    Task<ResultadoImportacion> ImportarAsync(SolicitudImportacion solicitud, string huellaSimulacro, CancellationToken cancellationToken = default);
}

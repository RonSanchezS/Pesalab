using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Application.Reportes;

public interface IServicioReportes
{
    Task<OpcionesReportes> ObtenerOpcionesAsync(CancellationToken cancellationToken = default);
    Task<ReportePesajes> GenerarAsync(FiltroReporte filtro, CancellationToken cancellationToken = default);
}

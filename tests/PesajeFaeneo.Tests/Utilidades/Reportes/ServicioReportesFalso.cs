using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Reportes;

internal sealed class ServicioReportesFalso : IServicioReportes
{
    public OpcionesReportes? Opciones { get; set; }
    public ReportePesajes Resultado { get; set; } = DatosReporteDePrueba.Crear();
    public Exception? Excepcion { get; set; }
    public Func<Task>? PausaGenerarAsync { get; set; }
    public List<FiltroReporte> FiltrosRecibidos { get; } = new();

    public Task<OpcionesReportes> ObtenerOpcionesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Opciones ?? new OpcionesReportes([], []));
    }

    public async Task<ReportePesajes> GenerarAsync(FiltroReporte filtro, CancellationToken cancellationToken = default)
    {
        FiltrosRecibidos.Add(filtro);

        if (PausaGenerarAsync != null)
        {
            await PausaGenerarAsync();
        }

        if (Excepcion != null)
        {
            throw Excepcion;
        }

        return Resultado;
    }
}

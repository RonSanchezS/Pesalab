using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Application.Reportes;

public interface IGeneradorPdfReportes
{
    /// <summary>
    /// Escribe el PDF en "destino" (no lo cierra). Corre fuera del hilo que llama (Task.Run).
    /// </summary>
    Task GenerarPdfAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default);

    /// <summary>
    /// Un PNG por página del MISMO documento que GenerarPdfAsync (para imprimir y para la vista previa).
    /// </summary>
    Task<IReadOnlyList<byte[]>> GenerarPaginasAsync(ReportePesajes reporte, int dpi, CancellationToken cancellationToken = default);
}

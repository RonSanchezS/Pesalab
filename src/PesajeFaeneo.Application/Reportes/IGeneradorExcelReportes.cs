using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Application.Reportes;

public interface IGeneradorExcelReportes
{
    /// <summary>
    /// Escribe el .xlsx en "destino" (no lo cierra). Corre fuera del hilo que llama (Task.Run).
    /// </summary>
    Task GenerarExcelAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default);
}

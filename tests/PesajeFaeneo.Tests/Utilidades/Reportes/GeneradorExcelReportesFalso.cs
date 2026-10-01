using System.Text;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Reportes;

internal sealed class GeneradorExcelReportesFalso : IGeneradorExcelReportes
{
    public List<ReportePesajes> ReportesExcel { get; } = new();
    public Exception? Excepcion { get; set; }

    public async Task GenerarExcelAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default)
    {
        ReportesExcel.Add(reporte);

        if (Excepcion != null)
        {
            throw Excepcion;
        }

        byte[] datos = Encoding.ASCII.GetBytes("PK-falso");
        await destino.WriteAsync(datos, cancellationToken);
    }
}

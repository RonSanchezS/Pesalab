using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;

namespace PesajeFaeneo.Tests.Utilidades.Reportes;

internal sealed class GeneradorPdfReportesFalso : IGeneradorPdfReportes
{
    public List<ReportePesajes> ReportesPdf { get; } = new();
    public List<ReportePesajes> ReportesPaginas { get; } = new();
    public List<int> DpiRecibidos { get; } = new();
    public IReadOnlyList<byte[]> Paginas { get; set; } = [CrearPng(170, 220), CrearPng(170, 220)];
    public Exception? Excepcion { get; set; }
    public Func<Task>? PausaPdfAsync { get; set; }

    public async Task GenerarPdfAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default)
    {
        if (PausaPdfAsync != null)
        {
            await PausaPdfAsync();
        }

        ReportesPdf.Add(reporte);

        if (Excepcion != null)
        {
            throw Excepcion;
        }

        byte[] datos = Encoding.ASCII.GetBytes("%PDF-falso");
        await destino.WriteAsync(datos, cancellationToken);
    }

    public async Task<IReadOnlyList<byte[]>> GenerarPaginasAsync(ReportePesajes reporte, int dpi, CancellationToken cancellationToken = default)
    {
        if (PausaPdfAsync != null)
        {
            await PausaPdfAsync();
        }

        ReportesPaginas.Add(reporte);
        DpiRecibidos.Add(dpi);

        if (Excepcion != null)
        {
            throw Excepcion;
        }

        return Paginas;
    }

    private static byte[] CrearPng(int ancho, int alto)
    {
        using var bmp = new Bitmap(ancho, alto);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}

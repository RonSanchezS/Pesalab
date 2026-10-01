using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Reportes;
using PesajeFaeneo.Application.Reportes.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PesajeFaeneo.Infrastructure.Reportes;

public sealed class GeneradorPdfReportes : IGeneradorPdfReportes
{
    private readonly ILogger<GeneradorPdfReportes> _logger;

    public GeneradorPdfReportes(ILogger<GeneradorPdfReportes> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task GenerarPdfAsync(ReportePesajes reporte, Stream destino, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        ArgumentNullException.ThrowIfNull(destino);
        return Task.Run(() =>
        {
            var logo = CargadorLogoReporte.Cargar(reporte.RutaLogo, _logger);
            DisenoPdfReporte.Crear(reporte, logo).GeneratePdf(destino);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<byte[]>> GenerarPaginasAsync(ReportePesajes reporte, int dpi, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reporte);
        if (dpi is < 72 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "La resolución debe estar entre 72 y 600 ppp.");
        }

        return Task.Run<IReadOnlyList<byte[]>>(
            () => DisenoPdfReporte.Crear(reporte, CargadorLogoReporte.Cargar(reporte.RutaLogo, _logger))
                .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = dpi })
                .ToList(),
            cancellationToken);
    }
}

using System.Drawing.Printing;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public class ServicioImpresionBoletasFalso : IServicioImpresionBoletas
{
    public List<(int PesajeId, SolicitudImpresion Solicitud)> Solicitudes { get; } = new();
    public List<int> VistasPrevias { get; } = new();

    public ResultadoImpresion ResultadoPorDefecto { get; set; } =
        new(EstadoImpresion.Omitida, "Impresión omitida por defecto", 0, false);

    public Func<int, SolicitudImpresion, Task<ResultadoImpresion>>? FabricaResultado { get; set; }

    public TaskCompletionSource<ResultadoImpresion>? TcsImpresion { get; set; }

    public Task<ResultadoImpresion> ImprimirAsync(int pesajeId, SolicitudImpresion solicitud, CancellationToken cancellationToken = default)
    {
        Solicitudes.Add((pesajeId, solicitud));

        if (TcsImpresion is not null)
        {
            return TcsImpresion.Task;
        }

        if (FabricaResultado is not null)
        {
            return FabricaResultado(pesajeId, solicitud);
        }

        return Task.FromResult(ResultadoPorDefecto);
    }

    public PrintDocument DocumentoVistaPrevia { get; set; } = new();

    public Task<PrintDocument> CrearVistaPreviaAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        VistasPrevias.Add(pesajeId);
        return Task.FromResult(DocumentoVistaPrevia);
    }
}

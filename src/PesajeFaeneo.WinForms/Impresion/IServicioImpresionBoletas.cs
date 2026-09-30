using System.Drawing.Printing;

namespace PesajeFaeneo.WinForms.Impresion;

public enum SolicitudImpresion
{
    AutomaticaAlGrabar,
    Manual
}

public enum EstadoImpresion
{
    Impresa,
    Omitida,
    Fallida
}

public sealed record ResultadoImpresion(EstadoImpresion Estado, string Mensaje, long NumeroBoleta, bool EsReimpresion);

public interface IServicioImpresionBoletas
{
    Task<ResultadoImpresion> ImprimirAsync(int pesajeId, SolicitudImpresion solicitud, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documento listo para PrintPreviewControl (el llamador lo desecha). Lanza las ReglaNegocioException IMP_*.
    /// </summary>
    Task<PrintDocument> CrearVistaPreviaAsync(int pesajeId, CancellationToken cancellationToken = default);
}

using System.Drawing.Printing;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.WinForms.Impresion;

public sealed class ServicioImpresionBoletas : IServicioImpresionBoletas
{
    private readonly IServicioBoletas _boletas;
    private readonly IServicioConfiguracionImpresion _configuracion;
    private readonly IImpresorBoletas _impresor;
    private readonly ILogger<ServicioImpresionBoletas> _logger;

    public ServicioImpresionBoletas(
        IServicioBoletas boletas,
        IServicioConfiguracionImpresion configuracion,
        IImpresorBoletas impresor,
        ILogger<ServicioImpresionBoletas> logger)
    {
        _boletas = boletas ?? throw new ArgumentNullException(nameof(boletas));
        _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        _impresor = impresor ?? throw new ArgumentNullException(nameof(impresor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResultadoImpresion> ImprimirAsync(int pesajeId, SolicitudImpresion solicitud, CancellationToken cancellationToken = default)
    {
        var equipo = await _configuracion.ObtenerAsync(cancellationToken);

        if (solicitud == SolicitudImpresion.AutomaticaAlGrabar && (!equipo.Configurada || !equipo.Configuracion.ImprimirAlGrabar))
        {
            return new ResultadoImpresion(EstadoImpresion.Omitida, "La impresión automática está desactivada en este equipo.", 0, false);
        }

        var datos = await _boletas.ObtenerPorIdAsync(pesajeId, cancellationToken);

        try
        {
            _impresor.Imprimir(datos, equipo.Configuracion);
        }
        catch (ReglaNegocioException ex) when (ex.Codigo.StartsWith("IMP_", StringComparison.Ordinal))
        {
            _logger.LogWarning(ex, "Fallo al imprimir boleta N° {Numero}: {Mensaje}", datos.NumeroBoleta, ex.Message);
            return new ResultadoImpresion(EstadoImpresion.Fallida, ex.Message, datos.NumeroBoleta, datos.EsReimpresion);
        }

        var registro = new RegistroImpresion(
            datos.PesajeId,
            datos.NumeroBoleta,
            datos.EsReimpresion,
            equipo.Configuracion.Copias,
            equipo.Configuracion.NombreImpresora ?? ReglasImpresion.TextoImpresoraPredeterminada,
            equipo.Configuracion.Formato,
            equipo.NombreEquipo);

        try
        {
            await _boletas.RegistrarImpresionAsync(registro, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Boleta N° {Numero} impresa pero no se pudo registrar en auditoría.", datos.NumeroBoleta);
            return new ResultadoImpresion(
                EstadoImpresion.Impresa,
                $"Boleta N° {datos.NumeroBoleta} impresa, pero no se pudo registrar la impresión en la auditoría.",
                datos.NumeroBoleta,
                datos.EsReimpresion);
        }

        var mensaje = datos.EsReimpresion
            ? $"Reimpresión de la boleta N° {datos.NumeroBoleta} enviada a la impresora."
            : $"Boleta N° {datos.NumeroBoleta} enviada a la impresora.";

        return new ResultadoImpresion(EstadoImpresion.Impresa, mensaje, datos.NumeroBoleta, datos.EsReimpresion);
    }

    public async Task<PrintDocument> CrearVistaPreviaAsync(int pesajeId, CancellationToken cancellationToken = default)
    {
        var equipo = await _configuracion.ObtenerAsync(cancellationToken);
        var datos = await _boletas.ObtenerPorIdAsync(pesajeId, cancellationToken);
        return _impresor.CrearVistaPrevia(datos, equipo.Configuracion);
    }
}

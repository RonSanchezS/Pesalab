using System.ComponentModel;
using System.Drawing.Printing;
using Microsoft.Extensions.Logging;
using PesajeFaeneo.Application.Boletas;
using PesajeFaeneo.Application.Boletas.Diseno;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.Domain.Enums;
using PesajeFaeneo.Domain.Excepciones;

namespace PesajeFaeneo.WinForms.Impresion;

internal sealed class ImpresorBoletasGdi : IImpresorBoletas
{
    private readonly ILogger<ImpresorBoletasGdi> _logger;

    public ImpresorBoletasGdi(ILogger<ImpresorBoletasGdi> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private DocumentoBoleta ConstruirDocumento(
        Func<int, PaginaBoleta> crearPagina,
        int totalCopias,
        float altoContenidoMm,
        FormatoBoleta formato,
        string? rutaLogo,
        ConfiguracionImpresion config,
        bool paraVistaPrevia,
        string nombreDocumento)
    {
        var logo = CargadorLogo.Cargar(rutaLogo, _logger);
        var doc = new DocumentoBoleta(
            crearPagina,
            totalCopias,
            logo,
            (float)config.DesplazamientoXMm,
            (float)config.DesplazamientoYMm,
            paraVistaPrevia,
            nombreDocumento);

        try
        {
            if (config.NombreImpresora is not null)
            {
                doc.PrinterSettings.PrinterName = config.NombreImpresora;
            }

            if (!doc.PrinterSettings.IsValid)
            {
                if (config.NombreImpresora is null)
                {
                    throw new ReglaNegocioException("IMP_SIN_IMPRESORA", "No hay una impresora predeterminada en este equipo. Configúrela en Administración › Configuración de impresión.");
                }
                else
                {
                    throw new ReglaNegocioException("IMP_IMPRESORA_NO_VALIDA", $"La impresora '{config.NombreImpresora}' no está instalada en este equipo. Revise Administración › Configuración de impresión.");
                }
            }

            var papeles = doc.PrinterSettings.PaperSizes
                .Cast<PaperSize>()
                .Select(p => new PapelDisponible(p.PaperName, p.Width, p.Height))
                .ToList();

            var eleccion = SelectorPapel.Elegir(formato, config.NombrePapel, papeles, altoContenidoMm);

            if (config.NombrePapel is not null && !papeles.Any(p => string.Equals(p.Nombre, config.NombrePapel, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("El papel configurado '{Papel}' no existe en la impresora '{Impresora}'. Se usará '{Seleccionado}'.",
                    config.NombrePapel, doc.PrinterSettings.PrinterName, eleccion.Papel.Nombre);
            }

            if (eleccion.Papel.EsPersonalizado)
            {
                doc.DefaultPageSettings.PaperSize = new PaperSize(eleccion.Papel.Nombre, eleccion.Papel.AnchoCentPulg, eleccion.Papel.AltoCentPulg);
            }
            else
            {
                var sizeOriginal = doc.PrinterSettings.PaperSizes
                    .Cast<PaperSize>()
                    .FirstOrDefault(p => string.Equals(p.PaperName, eleccion.Papel.Nombre, StringComparison.OrdinalIgnoreCase));
                if (sizeOriginal is not null)
                {
                    doc.DefaultPageSettings.PaperSize = sizeOriginal;
                }
            }

            doc.DefaultPageSettings.Landscape = eleccion.Horizontal;
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.PrinterSettings.Copies = 1;

            return doc;
        }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception)
        {
            // La impresora dejó de ser válida tras IsValid o la cola de impresión no responde (R39):
            // es un fallo de impresión esperable, no un defecto; la fachada lo informa como "Fallida".
            doc.Dispose();
            throw ErrorImpresora(config.NombreImpresora ?? ReglasImpresion.TextoImpresoraPredeterminada, ex);
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    private static ReglaNegocioException ErrorImpresora(string impresora, Exception ex) =>
        new("IMP_ERROR_IMPRESORA", $"No se pudo imprimir en '{impresora}': {ex.Message}", ex);

    public void Imprimir(DatosBoleta datos, ConfiguracionImpresion configuracion)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(configuracion);

        var altoContenido = DisenoBoleta.Crear(datos, configuracion.Formato, 1, configuracion.Copias).AltoMm;
        using var doc = ConstruirDocumento(
            c => DisenoBoleta.Crear(datos, configuracion.Formato, c, configuracion.Copias),
            configuracion.Copias,
            altoContenido,
            configuracion.Formato,
            datos.Empresa.RutaLogo,
            configuracion,
            paraVistaPrevia: false,
            $"Boleta {datos.NumeroBoleta}");

        try
        {
            doc.Print();
        }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception)
        {
            throw ErrorImpresora(doc.PrinterSettings.PrinterName, ex);
        }

        _logger.LogInformation("Boleta {Numero} enviada a {Impresora}: {Copias} copia(s), papel {Papel}, horizontal {Horizontal}.",
            datos.NumeroBoleta, doc.PrinterSettings.PrinterName, configuracion.Copias, doc.DefaultPageSettings.PaperSize?.PaperName, doc.DefaultPageSettings.Landscape);
    }

    public PrintDocument CrearVistaPrevia(DatosBoleta datos, ConfiguracionImpresion configuracion)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(configuracion);

        var altoContenido = DisenoBoleta.Crear(datos, configuracion.Formato, 1, configuracion.Copias).AltoMm;
        return ConstruirDocumento(
            c => DisenoBoleta.Crear(datos, configuracion.Formato, c, configuracion.Copias),
            configuracion.Copias,
            altoContenido,
            configuracion.Formato,
            datos.Empresa.RutaLogo,
            configuracion,
            paraVistaPrevia: true,
            $"Boleta {datos.NumeroBoleta}");
    }

    public void ImprimirCalibracion(ConfiguracionImpresion configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var paginaCalibracion = DisenoCalibracion.Crear(configuracion.Formato);
        using var doc = ConstruirDocumento(
            _ => DisenoCalibracion.Crear(configuracion.Formato),
            1,
            paginaCalibracion.AltoMm,
            configuracion.Formato,
            rutaLogo: null,
            configuracion,
            paraVistaPrevia: false,
            "Calibración de boleta");

        try
        {
            doc.Print();
        }
        catch (Exception ex) when (ex is InvalidPrinterException or Win32Exception)
        {
            throw ErrorImpresora(doc.PrinterSettings.PrinterName, ex);
        }
    }

    public IReadOnlyList<string> ListarImpresoras()
    {
        try
        {
            return PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron listar las impresoras instaladas (servicio de cola de impresión detenido).");
            return Array.Empty<string>();
        }
    }

    public string? ObtenerImpresoraPredeterminada()
    {
        try
        {
            var settings = new PrinterSettings();
            return settings.IsValid ? settings.PrinterName : null;
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener la impresora predeterminada.");
            return null;
        }
    }

    public IReadOnlyList<string> ListarPapeles(string? nombreImpresora)
    {
        try
        {
            var settings = new PrinterSettings();
            if (!string.IsNullOrWhiteSpace(nombreImpresora))
            {
                settings.PrinterName = nombreImpresora;
            }

            if (!settings.IsValid)
            {
                return Array.Empty<string>();
            }

            return settings.PaperSizes.Cast<PaperSize>().Select(p => p.PaperName).ToList();
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron listar los papeles para la impresora '{Impresora}'.", nombreImpresora);
            return Array.Empty<string>();
        }
    }
}

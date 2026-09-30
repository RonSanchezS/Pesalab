using System.Drawing.Printing;
using PesajeFaeneo.Application.Boletas.Dtos;
using PesajeFaeneo.WinForms.Impresion;

namespace PesajeFaeneo.Tests.Utilidades.Boletas;

public class ImpresorBoletasFalso : IImpresorBoletas
{
    public List<string> ImpresorasInstaladas { get; set; } = new() { "EPSON LX-350", "Microsoft Print to PDF" };
    public string? ImpresoraPredeterminada { get; set; } = "EPSON LX-350";
    public Dictionary<string, List<string>> PapelesPorImpresora { get; set; } = new()
    {
        ["EPSON LX-350"] = new() { "Letter", "Statement" },
        ["Microsoft Print to PDF"] = new() { "Letter", "A4" }
    };

    public Exception? ExcepcionImprimir { get; set; }
    public Exception? ExcepcionCrearVistaPrevia { get; set; }
    public Exception? ExcepcionImprimirCalibracion { get; set; }

    public List<(DatosBoleta Datos, ConfiguracionImpresion Config)> Impresiones { get; } = new();
    public List<(DatosBoleta Datos, ConfiguracionImpresion Config)> VistasPrevias { get; } = new();
    public List<ConfiguracionImpresion> Calibraciones { get; } = new();

    public IReadOnlyList<string> ListarImpresoras()
    {
        return ImpresorasInstaladas;
    }

    public string? ObtenerImpresoraPredeterminada()
    {
        return ImpresoraPredeterminada;
    }

    public IReadOnlyList<string> ListarPapeles(string? nombreImpresora)
    {
        if (nombreImpresora is not null && PapelesPorImpresora.TryGetValue(nombreImpresora, out var papeles))
        {
            return papeles;
        }

        return Array.Empty<string>();
    }

    public void Imprimir(DatosBoleta datos, ConfiguracionImpresion configuracion)
    {
        if (ExcepcionImprimir is not null)
        {
            throw ExcepcionImprimir;
        }

        Impresiones.Add((datos, configuracion));
    }

    public PrintDocument CrearVistaPrevia(DatosBoleta datos, ConfiguracionImpresion configuracion)
    {
        if (ExcepcionCrearVistaPrevia is not null)
        {
            throw ExcepcionCrearVistaPrevia;
        }

        VistasPrevias.Add((datos, configuracion));
        return new PrintDocument();
    }

    public void ImprimirCalibracion(ConfiguracionImpresion configuracion)
    {
        if (ExcepcionImprimirCalibracion is not null)
        {
            throw ExcepcionImprimirCalibracion;
        }

        Calibraciones.Add(configuracion);
    }
}
